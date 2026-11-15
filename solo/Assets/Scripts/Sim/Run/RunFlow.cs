using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using Wizard.Solo.Data;

namespace Wizard.Solo.Sim.Run
{
    /// <summary>
    /// 판 상태 기계(06 요약서 4절, 00 문서 5.5절), 전리품, 야영, 원정 지갑, 판 중 드랍, 연습·튜토리얼 방.
    /// RunPhase·AttackLocked·RunState·w.Drops의 주인이다. 보스 구간의 시계는 세지 않고 w.Boss.State만 읽는다(공통 규칙 5).
    /// 야영에는 조립·회복·비기 선택만 있다(강화 없음 — 06 요약서 4.3절).
    /// </summary>
    public sealed partial class RunFlow : RunModule
    {
        /// <summary>IContentDb.SoloExtra에 넘기는 보충 수치 조각 이름(Resources/DataSolo/G10.json).</summary>
        public const string ExtraName = "G10";

        static readonly Vector2 NoPos = new Vector2(-1f, -1f);

        // 세계마다 따로 드는 보조 상태. 모듈 객체 하나를 여러 세계가 같이 써도 섞이지 않는다(정적 필드 아님).
        readonly ConditionalWeakTable<SimWorld, RunScratch> _scratch = new ConditionalWeakTable<SimWorld, RunScratch>();

        RunScratch Scratch(SimWorld w)
        {
            RunScratch s;
            if (_scratch.TryGetValue(w, out s)) return s;
            s = new RunScratch { Tuning = RunTuning.From(w.Content), Plan = TutorialPlan.From(w.Content) };
            _scratch.Add(w, s);
            return s;
        }

        // ------------------------------------------------------------ 판 시작

        public override void StartRun(SimWorld w, RunConfig cfg)
        {
            _scratch.Remove(w);
            var s = Scratch(w);

            var run = new RunState { Config = cfg };
            w.Run = run;
            w.Wave = new WaveState();
            // 복수 중 여부·알아낸 약점은 판 설정에서 시작한다(00 문서 5.5절, D-54).
            run.RevengeActive = cfg.RevengePending;
            if (cfg.KnownAffinities != null)
                for (int i = 0; i < cfg.KnownAffinities.Count; i++) run.AffinityKnown.Add(cfg.KnownAffinities[i]);

            // 슬롯 0 = cfg, 슬롯 1.. = cfg.Party(협동, 11-online.md). 나머지 코드는 Active인 플레이어를 전부 돈다.
            SetupMember(w, w.Players[0], cfg);
            int party = cfg.Party != null ? Mathf.Min(cfg.Party.Count, w.Players.Length - 1) : 0;
            for (int i = 0; i < party; i++)
            {
                var m = cfg.Party[i];
                if (m == null) continue;
                SetupMember(w, w.Players[i + 1], m);
            }

            w.Emit(SimEventType.RunStarted, a: cfg.StartWave);

            if (cfg.Mode == RunMode.Duel)
            {
                StartDuel(w);
                return;
            }

            if (cfg.Mode == RunMode.Practice || cfg.Mode == RunMode.Tutorial)
            {
                EnterPractice(w, s);
                return;
            }

            if (cfg.StartWave > 1)
            {
                // 건너뛴 구간: 점수만 받고(06 요약서 6.2절) 보상은 없다. 웨이브 1~6의 해금 차례는 출발 야영에서(04 요약서 11.3절).
                int skipped = 0;
                for (int wv = 1; wv < cfg.StartWave; wv++) skipped += w.Mods.Score.SkipScore(w.Content, wv);
                run.SkippedScore = skipped;
                run.Score = skipped;
                int lastTurn = Mathf.Min(cfg.StartWave - 1, w.Content.Camp.ArtUnlockLastWave);
                for (int wv = 1; wv <= lastTurn; wv++) run.PendingArtTurns.Enqueue(wv);
                EnterCamp(w, s, startCamp: true);
            }
            else
            {
                EnterPrelude(w, s, 1);
            }
        }

        /// <summary>한 사람을 세운다. 기본 공격 유형(Docs/12-attack-styles.md 1절): 출발 조립의 코어가 공통 시작 블록(마력탄)이면
        /// 고른 유형의 핵 블록으로 바꾼다. 튜토리얼 판은 손잡이가 켜져 있으면 A 유형. 판 설정 객체의 조립은 사본으로 갈아 끼운다.</summary>
        static void SetupMember(SimWorld w, PlayerState p, RunConfig cfg)
        {
            string style = AttackStyles.EffectiveStyle(w.Content, cfg);
            if (style != null)
            {
                cfg.AttackStyle = style;
                cfg.Loadout = AttackStyles.ApplyToLoadout(w.Content, cfg.Loadout, style);
            }
            p.Active = true;
            w.Mods.Player.SetupPlayer(w, p, cfg);
            EnsureLoadout(p, cfg);
        }

        /// <summary>조립의 주인은 Run이다. Player.SetupPlayer가 채우지 않았으면(빈 구현) 판 설정의 출발 조립을 그대로 옮긴다.</summary>
        static void EnsureLoadout(PlayerState p, RunConfig cfg)
        {
            if (cfg.Loadout == null) return;
            var lo = p.Loadout;
            bool empty = lo.Core.IsEmpty;
            for (int i = 0; empty && i < lo.Links.Length; i++) if (!lo.Links[i].IsEmpty) empty = false;
            if (!empty) return;
            int n = Mathf.Max(cfg.LinkSlotCount, cfg.Loadout.Links.Count);
            if (lo.Links.Length >= n && !string.IsNullOrEmpty(lo.Core.BlockId)) return;
            var links = new SlotItem[n];
            for (int i = 0; i < cfg.Loadout.Links.Count; i++) links[i] = new SlotItem { BlockId = cfg.Loadout.Links[i] };
            lo.Core = new SlotItem { BlockId = cfg.Loadout.Core };
            lo.Links = links;
        }

        // ------------------------------------------------------------ 상태 전이 공통

        void SetPhase(SimWorld w, RunPhase phase)
        {
            var run = w.Run;
            RunPhase prev = run.Phase;
            run.Phase = phase;
            run.PhaseStartTick = w.Tick;
            w.Emit(SimEventType.PhaseChanged, a: (int)phase, b: (int)prev);
        }

        void SetAttackLocked(SimWorld w, bool locked)
        {
            for (int i = 0; i < w.Players.Length; i++)
                if (w.Players[i].Active) w.Players[i].AttackLocked = locked;
        }

        void EndRun(SimWorld w, RunScratch s, RunEndReason reason)
        {
            var run = w.Run;
            if (run.Phase == RunPhase.RunEnded) return;
            TrackPhantomDent(w, s);
            SetAttackLocked(w, false);
            run.EndReason = reason;
            SetPhase(w, RunPhase.RunEnded);
            w.Emit(SimEventType.RunEnded, a: (int)reason);
        }

        static bool AnyInputLocked(SimWorld w)
        {
            for (int i = 0; i < w.Players.Length; i++)
                if (w.Players[i].Active && w.Players[i].InputLocked) return true;
            return false;
        }

        static bool AnyJPressed(SimWorld w)
        {
            for (int i = 0; i < w.Players.Length; i++)
                if (w.Players[i].Active && w.Players[i].JPressedThisTick) return true;
            return false;
        }

        // ------------------------------------------------------------ 웨이브 시작

        void EnterPrelude(SimWorld w, RunScratch s, int wave)
        {
            var run = w.Run;
            WavePlan plan = run.NextPlan != null && run.NextPlan.Wave == wave ? run.NextPlan : w.Mods.Waves.BuildPlan(w, wave);
            run.NextPlan = null;
            run.Wave = wave;
            run.ReachedWave = wave;
            run.CampSub = CampSubState.FireIdle;
            run.DepartHoldStartTick = -1;
            run.RevengeKillTick = -1;
            s.WaveGold = 0;
            s.RevengeClearing = false;
            s.PhantomHpMax = 0f;

            // 00 문서 5.4절 (아): BeginWave(좁은 들 예고 틱을 돌려준다) → Traits.OnWaveStart → 판정기 초기화
            s.PreludeTicks = Mathf.Max(0, w.Mods.Waves.BeginWave(w, plan));
            w.Mods.Traits.OnWaveStart(w);
            for (int i = 0; i < w.Players.Length; i++)
                if (w.Players[i].Active) w.Mods.Arts.OnCommandReset(w, w.Players[i], CommandResetReason.WaveStart);
            // 솔로 조정(G10 wave_start_crystal_until_wave, 기본 0 = 명세 그대로): 초반 웨이브는 결정 1개를 쥐고 시작해 첫 웨이브부터 K를 쓴다.
            // 결정이 없을 때만 1개(웨이브 끝 환전 전에 쓰지 않으면 골드가 된다 — 명세의 환전 규칙 그대로).
            if (wave <= s.Tuning.WaveStartCrystalUntilWave)
                for (int i = 0; i < w.Players.Length; i++)
                {
                    var p = w.Players[i];
                    if (p.Active && p.Crystals <= 0) w.Mods.Player.GainCrystal(w, p, CrystalSource.Combo);
                }
            SetPhase(w, RunPhase.WavePrelude);
        }

        static WavePlan CurrentPlan(SimWorld w) => w.Wave != null ? w.Wave.Plan : null;

        static bool IsBossWave(SimWorld w)
        {
            var plan = CurrentPlan(w);
            if (plan != null) return plan.BossId != null;
            return w.Wave != null && w.Wave.Stats.IsBoss;
        }

        static bool IsPhantomFirstWave(SimWorld w)
        {
            if (w.Boss != null) return w.Boss.FirstEncounter;
            var plan = CurrentPlan(w);
            return plan != null && plan.PhantomFirst;
        }

        /// <summary>복수 대상인 싸움인가: 정해진 웨이브(20)의 허상, 첫 만남이 아님.</summary>
        static bool IsRevengeFight(SimWorld w, RunScratch s)
        {
            if (w.Run.Wave != s.Tuning.RevengeWave) return false;
            string bossId = w.Boss != null && w.Boss.Def != null ? w.Boss.Def.Id : (CurrentPlan(w) != null ? CurrentPlan(w).BossId : null);
            if (bossId != Ids.Boss.MirrorPhantom) return false;
            return !IsPhantomFirstWave(w);
        }

        /// <summary>웨이브 20 허상에 낸 가장 큰 흠집(%) = floor((hpMax − hpMin) × 100 / hpMax). 05 요약서 10.4절.</summary>
        void TrackPhantomDent(SimWorld w, RunScratch s)
        {
            var run = w.Run;
            if (!run.PhantomAttempted || w.Boss == null || !IsRevengeFight(w, s)) return;
            if (s.PhantomHpMax <= 0f)
            {
                var body = w.FindEnemy(w.Boss.EnemyId);
                if (body != null && body.MaxHp > 0f) s.PhantomHpMax = body.MaxHp;
                else s.PhantomHpMax = w.Mods.Bosses.MaxHp(w.Content, Ids.Boss.MirrorPhantom, run.Wave, false);
            }
            if (s.PhantomHpMax <= 0f) return;
            float hpMin = Mathf.Clamp(w.Boss.HpMin, 0f, s.PhantomHpMax);
            int percent = Mathf.FloorToInt((s.PhantomHpMax - hpMin) * 100f / s.PhantomHpMax);
            if (percent > run.PhantomDentPercent) run.PhantomDentPercent = percent;
        }

        // ------------------------------------------------------------ 11단계

        public override void Stage11_Phase(SimWorld w)
        {
            var run = w.Run;
            if (run.Phase == RunPhase.NotStarted || run.Phase == RunPhase.RunEnded) return;
            var s = Scratch(w);
            if (IsDuel(w))
            {
                DuelRingAdvance(w);
                TickDuel(w, s);
                return;
            }
            TickRevive(w);

            switch (run.Phase)
            {
                case RunPhase.WavePrelude:
                    TickPrelude(w, s);
                    break;
                case RunPhase.BossIntro:
                    TrackPhantomDent(w, s);
                    // 등장 길이를 세지 않는다. Bosses가 Intro를 끝내면 따라간다(공통 규칙 5).
                    if (w.Boss == null || w.Boss.State != BossFightState.Intro)
                    {
                        SetAttackLocked(w, false);
                        SetPhase(w, RunPhase.WaveCombat);
                    }
                    break;
                case RunPhase.WaveCombat:
                    TickDrops(w, s);
                    TickCombat(w, s);
                    break;
                case RunPhase.PhantomFinale:
                    if (w.Boss != null && w.Boss.State == BossFightState.Collapse) SetPhase(w, RunPhase.PhantomDowned);
                    else if (w.Boss != null && w.Boss.State == BossFightState.CollapseDone) RisePhantomFirst(w, s);
                    break;
                case RunPhase.PhantomDowned:
                    if (w.Boss != null && w.Boss.State == BossFightState.CollapseDone) RisePhantomFirst(w, s);
                    break;
                case RunPhase.WaveClearing:
                    TickClearing(w, s);
                    break;
                case RunPhase.LootPick:
                    break;
                case RunPhase.Camp:
                case RunPhase.StartCamp:
                    TickCamp(w, s);
                    break;
                case RunPhase.Practice:
                    TickPractice(w, s);
                    break;
            }
        }

        void TickPrelude(SimWorld w, RunScratch s)
        {
            var run = w.Run;
            if (w.Tick - run.PhaseStartTick < s.PreludeTicks) return;
            var plan = CurrentPlan(w);
            if (plan != null && plan.BossId != null)
            {
                // 들어가는 틱에 보스를 세우고 발사·비기를 잠근다. 푸는 것도 이 모듈뿐이다.
                w.Mods.Bosses.SpawnBoss(w, plan);
                SetAttackLocked(w, true);
                if (IsRevengeFight(w, s)) run.PhantomAttempted = true;
                SetPhase(w, RunPhase.BossIntro);
            }
            else
            {
                SetPhase(w, RunPhase.WaveCombat);
            }
        }

        void TickCombat(SimWorld w, RunScratch s)
        {
            TrackPhantomDent(w, s);
            var boss = w.Boss;
            if (boss != null && boss.FirstEncounter)
            {
                // 첫 만남: 되비침 180틱·쓰러짐 240틱은 Bosses가 센다. 여기서는 상태만 본다.
                if (boss.State == BossFightState.Reflection) { SetPhase(w, RunPhase.PhantomFinale); return; }
                if (boss.State == BossFightState.Collapse) { SetPhase(w, RunPhase.PhantomDowned); return; }
                if (boss.State == BossFightState.CollapseDone) { RisePhantomFirst(w, s); return; }
                return;
            }

            bool cleared;
            if (IsBossWave(w) && boss != null) cleared = boss.State == BossFightState.Dead;
            else cleared = w.Mods.Waves.IsCleared(w);
            if (cleared) ClearWave(w, s, phantomFirst: false);
        }

        /// <summary>첫 만남의 일어남(00 문서 5.5절 표): Rise → Dismiss → 복수 시작 → 웨이브 10을 넘긴 것으로 처리.</summary>
        void RisePhantomFirst(SimWorld w, RunScratch s)
        {
            var run = w.Run;
            for (int i = 0; i < w.Players.Length; i++)
                if (w.Players[i].Active) w.Mods.Player.Rise(w, w.Players[i]);
            w.Mods.Bosses.Dismiss(w);
            run.PhantomFirstDone = true;
            run.RevengeActive = true;
            ClearWave(w, s, phantomFirst: true);
        }

        /// <summary>웨이브 클리어 처리. 순서는 06 요약서 4.2절 1~8.</summary>
        void ClearWave(SimWorld w, RunScratch s, bool phantomFirst)
        {
            ReviveAllOnWaveClear(w);
            var run = w.Run;
            var db = w.Content;
            var drops = db.Drops;
            int wave = run.Wave;
            var stats = w.Wave.Stats;
            bool isBoss = phantomFirst || IsBossWave(w);
            bool revenge = !phantomFirst && isBoss && IsRevengeFight(w, s);
            TrackPhantomDent(w, s);

            // 1. 걸린 틱(점수 계산이 읽는다. 점수보다 먼저 적는다)
            stats.ElapsedTicks = w.Wave.WaveTick;

            // 2. 남은 결정 환전: 빚이 있으면 그만큼 빼고 남은 것만(06 요약서 4.2절, TV-175). 빚은 그대로 둔다(10 요약서 6절).
            int crystalGold = 0;
            for (int i = 0; i < w.Players.Length; i++)
            {
                var p = w.Players[i];
                if (!p.Active) continue;
                int left = Mathf.Max(0, p.Crystals - p.Debt);
                crystalGold += left * db.Economy.CrystalGoldEach;
                p.Crystals = 0;
            }
            if (crystalGold > 0) GainGold(w, s, crystalGold, NoPos);

            // 3. 클리어 골드(보스 웨이브에도) + 보스 처치 골드. 첫 만남은 클리어 골드만.
            GainGold(w, s, drops.WaveClearGold, NoPos);
            if (isBoss && !phantomFirst) GainGold(w, s, drops.BossGoldPerS10 * (9 + wave), NoPos);

            // 5. 기회 목록(재료는 정산 때 뽑는다). 엘리트 기회는 처치 때 이미 적혔다.
            var mat = drops.MaterialTable;
            Vector2 center = new Vector2(w.ArenaWidth * 0.5f, w.ArenaHeight * 0.5f);
            if (mat != null && DayCycle.Slot(wave) == mat.FifthWaveSlot) AddChance(w, DropChanceSource.FifthWave, wave, center);
            if (isBoss && !phantomFirst) AddChance(w, DropChanceSource.BossKill, wave, center);
            if (run.Config != null && run.Config.CrowBundlePending && !run.CrowBundleEarned
                && mat != null && mat.CrowBundle != null && wave >= mat.CrowBundle.Wave)
            {
                run.CrowBundleEarned = true;
                AddChance(w, DropChanceSource.CrowBundle, wave, center);
            }
            if (phantomFirst) AddChance(w, DropChanceSource.PhantomFirst, wave, center);

            // 6. 웨이브 점수
            if (isBoss && !phantomFirst) stats.BossKilled = true;
            ScoreBreakdown breakdown;
            // 떼 출현이 늘린 만큼 빠른 처리 목표를 늘린다(솔로 조정, 꺼져 있으면 stats 그대로).
            int score = w.Mods.Score.WaveScore(db, FastClearStats(w, s, stats), out breakdown);
            run.Score += score;
            run.WaveResults.Add(new WaveResult { Wave = wave, Stats = stats, Score = breakdown, GoldEarned = s.WaveGold });
            w.Emit(SimEventType.ScoreChanged, a: run.Score);

            // 7. 직접 넘긴 웨이브
            run.ClearedDirect++;
            run.LastClearedWave = wave;
            if (wave >= 1 && wave <= db.Camp.ArtUnlockLastWave) run.PendingArtTurns.Enqueue(wave);
            if (revenge)
            {
                run.KilledPhantomAtWave20 = true;
            }

            // 8. 바닥의 체력 조각 자동 회수(가득이면 효과 없음)
            for (int i = 0; i < w.Drops.Count; i++)
            {
                var d = w.Drops[i];
                if (d.Picked || d.Kind != DropKind.HealthShard) continue;
                var p = FirstHurtPlayer(w);
                if (p != null) w.Mods.Player.Heal(w, p, d.Amount);
                d.Picked = true;
                w.Emit(SimEventType.DropPicked, slot: p != null ? p.Slot : -1, entityId: d.Id, a: (int)d.Kind);
            }

            // 웨이브 끝 정리(00 문서 5.4절 (아))
            for (int i = 0; i < w.Players.Length; i++)
                if (w.Players[i].Active) w.Mods.Player.ResetForWaveEnd(w, w.Players[i]);
            w.Mods.Threats.ClearAll(w, false);
            w.Mods.Arts.ClearZones(w);
            w.Mods.Spells.ClearProjectiles(w, -1);
            w.Mods.Waves.EndWave(w);
            SetAttackLocked(w, false);
            w.Emit(SimEventType.WaveCleared, a: wave, b: score);
            // 보스 웨이브면 끝에 항상 보스를 걷는다(첫 만남은 일어남에서 이미 걷었다).
            if (isBoss && !phantomFirst) w.Mods.Bosses.Dismiss(w);

            // 전리품이 읽을 사실
            s.ClearedWave = wave;
            s.ClearedIsBoss = isBoss;
            s.ClearedPhantomFirst = phantomFirst;
            s.ClearedElites = stats.ElitesKilled;

            // WaveClearing의 길이(D-58)
            var camp = db.Camp;
            s.RevengeClearing = false;
            if (revenge && run.RevengeActive)
            {
                run.ClearingTicks = camp.RevengeClearTicks;
                run.RevengeKillTick = w.Tick;
                s.RevengeClearing = true;
            }
            else if (isBoss && !phantomFirst) run.ClearingTicks = SimConst.Ticks(camp.BossClearSeconds);
            else run.ClearingTicks = SimConst.Ticks(camp.WaveClearSeconds);
            SetPhase(w, RunPhase.WaveClearing);
        }

        void TickClearing(SimWorld w, RunScratch s)
        {
            var run = w.Run;
            bool done = w.Tick - run.PhaseStartTick >= run.ClearingTicks;
            // 복수 처치 연출: 84틱이 지난 뒤의 J 누름으로 넘긴다(그 전의 J는 무시).
            if (!done && s.RevengeClearing && run.RevengeKillTick >= 0
                && w.Tick - run.RevengeKillTick >= w.Content.Camp.RevengeSkipAfterTicks && AnyJPressed(w))
                done = true;
            if (!done) return;
            s.RevengeClearing = false;
            // 남은 연출용 드랍을 걷는다.
            for (int i = 0; i < w.Drops.Count; i++) w.Drops[i].Picked = true;
            run.Loot = BuildLoot(w, s);
            SetPhase(w, RunPhase.LootPick);
            w.Emit(SimEventType.LootOffered, a: run.Loot.Cards.Count);
        }

        // ------------------------------------------------------------ 지갑·기회·드랍

        void GainGold(SimWorld w, RunScratch s, int gold, Vector2 from)
        {
            if (gold <= 0) return;
            s.WaveGold += gold;
            w.Run.Purse += gold;
            w.Emit(SimEventType.GoldGained, a: gold, pos: from);
            w.Emit(SimEventType.PurseChanged, a: w.Run.Purse);
        }

        public override void AddPurse(SimWorld w, int gold, Vector2 from)
        {
            if (w.Run.Phase == RunPhase.RunEnded || w.Run.Phase == RunPhase.NotStarted) return;
            GainGold(w, Scratch(w), gold, from);
        }

        /// <summary>기회 한 줄을 더한다. 엘리트는 웨이브마다 한 줄에 마리 수를 쌓는다(06 요약서 5.1절).</summary>
        void AddChance(SimWorld w, DropChanceSource source, int wave, Vector2 pos)
        {
            var list = w.Run.DropChances;
            bool merged = false;
            if (source == DropChanceSource.EliteKill)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i].Source != source || list[i].Wave != wave) continue;
                    var c = list[i];
                    c.Count++;
                    list[i] = c;
                    merged = true;
                    break;
                }
            }
            if (!merged) list.Add(new DropChance { Source = source, Wave = wave, Count = 1 });
            // 재료 꾸러미 그림은 기회가 생길 때 떨어진다(05 요약서 3.4절). 연출용 개체.
            SpawnDrop(w, DropKind.MaterialBundle, pos, 1);
        }

        DropEntity SpawnDrop(SimWorld w, DropKind kind, Vector2 pos, int amount)
        {
            var d = new DropEntity { Id = w.NewEntityId(), Kind = kind, Pos = pos, SpawnTick = w.Tick, Amount = amount };
            w.Drops.Add(d);
            w.Emit(SimEventType.DropSpawned, entityId: d.Id, a: (int)kind, pos: pos);
            return d;
        }

        static PlayerState FirstHurtPlayer(SimWorld w)
        {
            for (int i = 0; i < w.Players.Length; i++)
            {
                var p = w.Players[i];
                if (p.Active && !p.Downed && p.Hp < p.MaxHp) return p;
            }
            return null;
        }

        /// <summary>전투 중 줍기: 체력 조각은 닿으면 체력 +1칸(가득이면 바닥에 둔다), 골드 조각은 가까우면 빨려 온다(연출 — 지갑은 처치 때 올랐다).</summary>
        void TickDrops(SimWorld w, RunScratch s)
        {
            if (w.Drops.Count == 0) return;
            float shardR = s.Tuning.HealthShardPickupRadius, goldR = s.Tuning.GoldPullRadius;
            for (int i = 0; i < w.Drops.Count; i++)
            {
                var d = w.Drops[i];
                if (d.Picked) continue;
                if (d.Kind == DropKind.MaterialBundle) continue;
                float r = d.Kind == DropKind.HealthShard ? shardR : goldR;
                if (r <= 0f) continue;
                for (int k = 0; k < w.Players.Length; k++)
                {
                    var p = w.Players[k];
                    if (!p.Active || p.Downed) continue;
                    if ((p.Pos - d.Pos).sqrMagnitude > r * r) continue;
                    if (d.Kind == DropKind.HealthShard)
                    {
                        if (p.Hp >= p.MaxHp) continue;
                        w.Mods.Player.Heal(w, p, d.Amount);
                    }
                    d.Picked = true;
                    w.Emit(SimEventType.DropPicked, slot: p.Slot, entityId: d.Id, a: (int)d.Kind);
                    break;
                }
            }
        }

        // ------------------------------------------------------------ 훅

        public override void OnEnemyKilled(SimWorld w, EnemyState e, in HitInfo killingHit)
        {
            var run = w.Run;
            // 판이 끝난 뒤(같은 틱에 플레이어가 먼저 쓰러짐)에는 아무것도 바꾸지 않는다(D-45, 공통 규칙 8).
            if (run.Phase == RunPhase.RunEnded || run.Phase == RunPhase.NotStarted || run.Phase == RunPhase.Practice) return;
            if (e == null) return;
            var s = Scratch(w);
            var stats = w.Wave.Stats;

            if (e.IsBoss)
            {
                // 보스 처치 골드·기회는 클리어 처리에서 준다.
                stats.BossKilled = true;
                stats.KillCount++;
                run.TotalKills++;
                return;
            }
            // 졸개·갈라진·불러낸 개체: 골드·점수·드랍 0
            if (e.NoReward) return;

            var db = w.Content;
            var drops = db.Drops;
            int cost = e.Def != null ? e.Def.Cost : e.Cost;
            bool elite = e.IsElite;

            stats.KillCount++;
            run.TotalKills++;

            // 떼 출현 나누기(솔로 조정 split): 원래 한 마리 몫을 무리 전체가 나눈다. 아니면 1/1(명세 그대로).
            HordeShare share;
            int shareNum, shareDen;
            HordeShareOf(w, s, e, out share, out shareNum, out shareDen);

            // 골드는 처치 순간 지갑에(동전은 연출)
            int gold = cost * drops.GoldPerCost * (elite ? drops.EliteGoldMul : 1);
            if (share != null) gold = SplitPay(share.GoldAcc, e.DefId, gold, shareNum, shareDen);
            if (gold > 0)
            {
                GainGold(w, s, gold, e.Pos);
                SpawnDrop(w, DropKind.GoldCoin, e.Pos, gold);
            }

            // 점수 재료: cost × 10 × (엘리트 3) — 지체(7,200틱) 뒤에는 절반. 2배 정수로 든다.
            bool stalled = w.Wave.Stalled
                || (db.Waves.StallSeconds > 0f && w.Wave.WaveTick >= SimConst.Ticks(db.Waves.StallSeconds));
            int raw = cost * db.Scoring.KillPerCost * (elite ? db.Waves.EliteCostMul : 1);
            int rawX2 = raw * (stalled ? 1 : 2);
            if (share != null) rawX2 = SplitPay(share.ScoreAcc, e.DefId, rawX2, shareNum, shareDen);
            stats.KillScoreX2 += rawX2;

            if (elite)
            {
                stats.ElitesKilled++;
                AddChance(w, DropChanceSource.EliteKill, run.Wave, e.Pos);
            }

            // 체력 조각: 이번 웨이브에 이미 나왔거나 체력이 가득이면 추첨 자체를 안 한다(06 요약서 4.5절).
            if (!w.Wave.HealthShardDropped && FirstHurtPlayer(w) != null)
            {
                int bp = elite ? drops.HealthShardEliteBp : cost * drops.HealthShardBpPerCost;
                if (share != null) bp = SplitPay(share.ShardAcc, e.DefId, bp, shareNum, shareDen);
                if (w.Rng(RngStream.Drops).RollBp(bp))
                {
                    w.Wave.HealthShardDropped = true;
                    SpawnDrop(w, DropKind.HealthShard, e.Pos, drops.HealthShardHealCells);
                }
            }
        }

        public override void OnPlayerDowned(SimWorld w, PlayerState p)
        {
            var run = w.Run;
            if (run.Phase == RunPhase.RunEnded || run.Phase == RunPhase.NotStarted) return;
            if (IsDuel(w)) return; // 결투는 라운드 규칙(TickDuel)이 쓰러짐을 본다
            var s = Scratch(w);
            if (run.Phase == RunPhase.Practice)
            {
                // 연습 방에는 판 종료가 없다(적 탄 쏘기 옵션으로 체력이 바닥나도 바로 일어난다).
                w.Mods.Player.Rise(w, p);
                return;
            }
            if (w.Boss != null && w.Boss.FirstEncounter)
            {
                // 첫 만남: 판 종료가 아니라 쓰러짐. 상태 전이는 11단계에서 Boss.State를 보고 한다.
                w.Mods.Bosses.BeginCollapse(w);
                return;
            }
            // 여럿이면 전원이 쓰러졌을 때만 끝난다(부른 플레이어는 쓰러진 것으로 센다).
            for (int i = 0; i < w.Players.Length; i++)
            {
                var other = w.Players[i];
                if (other.Active && other != p && !other.Downed) return;
            }
            EndRun(w, s, RunEndReason.HpZero);
        }

        public override void OnPlayerHit(SimWorld w, PlayerState p)
        {
            var run = w.Run;
            if (run.Phase == RunPhase.RunEnded || run.Phase == RunPhase.NotStarted || run.Phase == RunPhase.Practice) return;
            w.Wave.Stats.NoHit = false;
        }

        public override void OnGraze(SimWorld w, PlayerState p)
        {
            var run = w.Run;
            if (run.Phase == RunPhase.RunEnded || run.Phase == RunPhase.NotStarted || run.Phase == RunPhase.Practice) return;
            // 보스 최종 격노 뒤의 스침은 점수에 세지 않는다.
            if (w.Boss != null && w.Boss.FinalRage) return;
            w.Wave.Stats.Grazes++;
        }

        public override void OnArtFired(SimWorld w, PlayerState p, string artId)
        {
            var run = w.Run;
            if (run == null || run.Phase == RunPhase.RunEnded) return;
            // 만개 발동 수(손맛 칭호의 재료, 00 문서 D-25).
            if (artId == Ids.Art.Bloom) run.BloomCasts++;
            // 튜토리얼 관문(비기 단계·커맨드 단계)의 진척. RunFlow.Practice.cs.
            if (run.Phase == RunPhase.Practice && run.Config != null && run.Config.Mode == RunMode.Tutorial)
                TutorialOnArtFired(w, Scratch(w), artId);
        }

        public override void OnCrystalsSpent(SimWorld w, PlayerState p, int count)
        {
            var run = w.Run;
            if (run == null || run.Phase != RunPhase.Practice || count <= 0) return;
            // 연습 수치판 "결정 1개당 피해"(08 요약서 8.5절)의 분모.
            Scratch(w).CrystalsSpent += count;
            if (run.Config != null && run.Config.Mode == RunMode.Tutorial) Scratch(w).TutSpent += count;
        }

        // ------------------------------------------------------------ 요약

        public override RunSummary BuildSummary(SimWorld w)
        {
            var run = w.Run;
            var cfg = run.Config;
            var s = Scratch(w);
            var sum = new RunSummary
            {
                ReachedWave = run.ReachedWave,
                ClearedDirect = run.ClearedDirect,
                LastClearedWave = run.LastClearedWave,
                EndReason = run.EndReason,
                Score = run.Score,
                SkippedScore = run.SkippedScore,
                Purse = run.Purse,
                PhantomFirstDone = run.PhantomFirstDone,
                RevengeActive = run.RevengeActive,
                KilledPhantomAtWave20 = run.KilledPhantomAtWave20,
                PhantomAttempted = run.PhantomAttempted,
                PhantomDentPercent = run.PhantomDentPercent,
                CrowBundleEarned = run.CrowBundleEarned,
                BloomCasts = run.BloomCasts,
                TotalKills = run.TotalKills
            };
            if (cfg != null)
            {
                sum.RunId = cfg.RunId;
                sum.CharId = cfg.CharId;
                sum.WandId = cfg.WandId;
                sum.Mode = cfg.Mode;
                sum.WandStage = cfg.WandStage;
                sum.StartWave = cfg.StartWave;
                sum.PhantomFirst = cfg.PhantomFirst;
                sum.RevengePending = cfg.RevengePending;
            }
            sum.WaveResults.AddRange(run.WaveResults);
            sum.DropChances.AddRange(run.DropChances);
            sum.NewArts.AddRange(run.NewArtsThisRun);
            foreach (string key in run.AffinityKnown) sum.AffinityKnown.Add(key);
            sum.AffinityKnown.Sort(string.CompareOrdinal);

            // 판 끝의 조립(코어 먼저, 빈 칸 null)과 야생 블록(보관함 + 지팡이에 끼운 것). 서고 블록은 넣지 않는다(D-53).
            for (int i = 0; i < run.Storage.Count; i++)
                if (!run.Storage[i].FromLibrary) sum.WildBlocks.Add(run.Storage[i]);
            bool first = true;
            for (int i = 0; i < w.Players.Length; i++)
            {
                var p = w.Players[i];
                if (!p.Active) continue;
                var lo = p.Loadout;
                if (first)
                {
                    sum.LoadoutIds.Add(lo.Core.IsEmpty ? null : lo.Core.BlockId);
                    for (int k = 0; k < lo.Links.Length; k++) sum.LoadoutIds.Add(lo.Links[k].IsEmpty ? null : lo.Links[k].BlockId);
                    first = false;
                }
                if (!lo.Core.IsEmpty && lo.Core.WildUid != 0) sum.WildBlocks.Add(WildOf(w, s, lo.Core));
                for (int k = 0; k < lo.Links.Length; k++)
                    if (!lo.Links[k].IsEmpty && lo.Links[k].WildUid != 0) sum.WildBlocks.Add(WildOf(w, s, lo.Links[k]));
            }
            return sum;
        }
    }
}

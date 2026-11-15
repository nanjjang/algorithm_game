using UnityEngine;

namespace Wizard.Solo.Sim.Run
{
    // 온라인 1대1 결투(Docs/11-online.md, 규칙 원문 docs/spec/10-duel.md 2·3절). 값은 DataSolo/Duel.json(없으면 아래 기본값 = 원문 [제안] 값).
    // 솔로 시뮬 위에 얹은 것: 적·웨이브 없이 두 사람만 선다. 공격은 결투 과녁으로 상대를 맞히고, 라운드·승패는 여기서 센다.
    // - 3판 2선승(rounds_to_win), 라운드 90초, 카운트다운 3초(움직이지 못한다), 시작 결정 1개, 체력 가득.
    // - 시간 종료: 체력 비율 → 준 피해 합 → 같으면 연장 30초(받는 피해 2배) → 그래도 같으면 무승부 라운드.
    // - 순간 피해 상한: 0.5초 동안 최대 체력의 30%. 결투 강화 배율 1 + 0.03 × 단계(무한 웨이브의 1.15^단계 대신).
    // - 라운드 수 상한 2 × 선승 + 1. 상한에서 승수가 같으면 경기 무승부.
    // 상대 피해: 사람마다 보이지 않는 과녁(EnemyState.DuelProxyOf)을 세워 적을 맞히는 모든 공격이 상대에게 닿게 한다.
    // 아직 없는 것(원문에는 있음): 스침 벌칙, 조준 보정, 결투용 비기 값. 11-online.md 5절에 적었다.
    public sealed partial class RunFlow
    {
        public const string DuelExtraName = "Duel";

        struct DuelTuning
        {
            public int RoundsToWin, RoundTicks, CountdownTicks, RoundEndTicks, OvertimeTicks, StartCrystals, BurstWindow, BurstPercent;
            public float HitRadius, EnhancePerStage, OvertimeMul;
            public bool ThornOn, PillarsOn;
            public int ThornStepTicks, ThornDamageTicks, ThornPercent, ThornMaxRing;
            public Vector2 LeftStart, RightStart;
        }

        static DuelTuning DuelTune(SimWorld w)
        {
            var t = new DuelTuning
            {
                RoundsToWin = 2, RoundTicks = SimConst.Ticks(90f), CountdownTicks = SimConst.Ticks(3f), RoundEndTicks = SimConst.Ticks(2.5f),
                OvertimeTicks = SimConst.Ticks(30f), StartCrystals = 1, BurstWindow = 30, BurstPercent = 30,
                HitRadius = 0.30f, EnhancePerStage = 0.03f, OvertimeMul = 2f,
                LeftStart = new Vector2(6f, 7f), RightStart = new Vector2(18f, 7f),
                ThornOn = true, PillarsOn = true, ThornStepTicks = 300, ThornDamageTicks = 30, ThornPercent = 4, ThornMaxRing = 6,
            };
            var r = w.Content != null ? w.Content.SoloExtra(DuelExtraName) : null;
            if (r == null) return t;
            if (r["rounds_to_win"] != null) t.RoundsToWin = Mathf.Max(1, (int)r["rounds_to_win"]);
            if (r["round_seconds"] != null) t.RoundTicks = SimConst.Ticks((float)r["round_seconds"]);
            if (r["countdown_seconds"] != null) t.CountdownTicks = SimConst.Ticks((float)r["countdown_seconds"]);
            if (r["round_end_seconds"] != null) t.RoundEndTicks = SimConst.Ticks((float)r["round_end_seconds"]);
            if (r["overtime_seconds"] != null) t.OvertimeTicks = SimConst.Ticks((float)r["overtime_seconds"]);
            if (r["round_start_crystals"] != null) t.StartCrystals = (int)r["round_start_crystals"];
            if (r["burst_cap_ticks"] != null) t.BurstWindow = Mathf.Clamp((int)r["burst_cap_ticks"], 1, 64);
            if (r["burst_cap_percent"] != null) t.BurstPercent = (int)r["burst_cap_percent"];
            if (r["hit_radius"] != null) t.HitRadius = (float)r["hit_radius"];
            if (r["enhance_per_stage"] != null) t.EnhancePerStage = (float)r["enhance_per_stage"];
            if (r["overtime_damage_mult"] != null) t.OvertimeMul = (float)r["overtime_damage_mult"];
            if (r["thorn"] != null) t.ThornOn = (bool)r["thorn"];
            if (r["pillars"] != null) t.PillarsOn = (bool)r["pillars"];
            if (r["thorn_step_seconds"] != null) t.ThornStepTicks = SimConst.Ticks((float)r["thorn_step_seconds"]);
            if (r["thorn_damage_seconds"] != null) t.ThornDamageTicks = SimConst.Ticks((float)r["thorn_damage_seconds"]);
            if (r["thorn_damage_percent"] != null) t.ThornPercent = (int)r["thorn_damage_percent"];
            if (r["thorn_max_ring"] != null) t.ThornMaxRing = (int)r["thorn_max_ring"];
            return t;
        }

        public static bool IsDuel(SimWorld w) => w.Run != null && w.Run.Config != null && w.Run.Config.Mode == RunMode.Duel;

        // ------------------------------------------------------------ 시작·라운드

        void StartDuel(SimWorld w)
        {
            w.Run.Duel = new DuelState();
            BeginRound(w, DuelTune(w), overtime: false);
        }

        void BeginRound(SimWorld w, DuelTuning t, bool overtime)
        {
            var d = w.Run.Duel;
            if (!overtime)
            {
                d.Round++;
                d.DamageDealt[0] = d.DamageDealt[1] = 0;
                // 판 안 상태를 비운다: 탄·지대·적 탄, 체력·열·콤보(SetupPlayer), 결정 = 시작 개수, 자리 = 양 끝에서 마주 본다.
                w.Projectiles.Clear();
                w.PlayerZones.Clear();
                w.Enemies.Clear(); // 과녁도 새로(다음 Stage4_DuelSync가 다시 세운다)
                w.Hazards.Clear();
                d.ThornRing = 0;
                if (t.PillarsOn && w.Obstacles.Count == 0) SpawnPillars(w);
                w.EnemyShots.Clear();
                w.Zones.Clear();
                for (int i = 0; i < 2 && i < w.Players.Length; i++)
                {
                    var p = w.Players[i];
                    if (!p.Active) continue;
                    var cfg = i == 0 ? w.Run.Config : (w.Run.Config.Party != null && w.Run.Config.Party.Count > 0 ? w.Run.Config.Party[0] : null);
                    if (cfg != null) w.Mods.Player.SetupPlayer(w, p, cfg);
                    p.Pos = p.PrevPos = i == 0 ? t.LeftStart : t.RightStart;
                    p.Facing = p.LockedFacing = p.LastPressedCardinal = i == 0 ? Dir8.E : Dir8.W;
                    p.Crystals = t.StartCrystals;
                    p.InvulnUntilTick = 0;
                }
                for (int k = 0; k < d.TakenRing0.Length; k++) { d.TakenRing0[k] = 0; d.TakenRing1[k] = 0; }
                d.Overtime = false;
                SetPhase(w, RunPhase.DuelCountdown);
            }
            else
            {
                d.Overtime = true;
                SetPhase(w, RunPhase.DuelFight);
            }
            d.RoundStartTick = w.Tick;
            w.Emit(SimEventType.DuelRoundStarted, a: d.Round, b: overtime ? 1 : 0);
        }

        /// <summary>11단계: 결투 판의 상태 기계(Stage11_Phase가 Mode == Duel이면 이것만 부른다).</summary>
        void TickDuel(SimWorld w, RunScratch s)
        {
            var run = w.Run;
            var d = run.Duel;
            if (d == null) return;
            var t = DuelTune(w);
            int elapsed = w.Tick - run.PhaseStartTick;
            switch (run.Phase)
            {
                case RunPhase.DuelCountdown:
                    if (elapsed >= t.CountdownTicks) { SetPhase(w, RunPhase.DuelFight); d.RoundStartTick = w.Tick; }
                    break;
                case RunPhase.DuelFight:
                    {
                        var a = w.Players[0];
                        var b = w.Players[1];
                        bool aDown = !a.Active || a.Downed, bDown = !b.Active || b.Downed;
                        if (aDown || bDown)
                        {
                            int winner = aDown && bDown ? (d.DamageDealt[0] == d.DamageDealt[1] ? -1 : (d.DamageDealt[0] > d.DamageDealt[1] ? 0 : 1)) : (aDown ? 1 : 0);
                            EndRound(w, winner, DuelRoundEndKind.Knockout);
                            break;
                        }
                        TickThorn(w, t, d);
                        int limit = d.Overtime ? t.OvertimeTicks : t.RoundTicks;
                        if (w.Tick - d.RoundStartTick < limit) break;
                        int byTime = CompareTimeout(w, d);
                        if (byTime >= 0) EndRound(w, byTime, d.Overtime ? DuelRoundEndKind.Overtime : DuelRoundEndKind.Timeout);
                        else if (!d.Overtime) BeginRound(w, t, overtime: true);
                        else EndRound(w, -1, DuelRoundEndKind.Overtime);
                        break;
                    }
                case RunPhase.DuelRoundEnd:
                    if (elapsed < t.RoundEndTicks) break;
                    if (d.MatchOver) { EndRun(w, s, RunEndReason.None); break; }
                    BeginRound(w, t, overtime: false);
                    break;
            }
        }

        /// <summary>시간 종료 판정: 체력 비율(정수 곱으로) → 준 피해 합. 같으면 −1.</summary>
        static int CompareTimeout(SimWorld w, DuelState d)
        {
            var a = w.Players[0];
            var b = w.Players[1];
            long lhs = (long)a.Hp * b.MaxHp, rhs = (long)b.Hp * a.MaxHp;
            if (lhs != rhs) return lhs > rhs ? 0 : 1;
            if (d.DamageDealt[0] != d.DamageDealt[1]) return d.DamageDealt[0] > d.DamageDealt[1] ? 0 : 1;
            return -1;
        }

        void EndRound(SimWorld w, int winner, DuelRoundEndKind kind)
        {
            var d = w.Run.Duel;
            var t = DuelTune(w);
            if (winner >= 0) d.Wins[winner]++;
            d.LastRoundWinner = winner;
            d.LastRoundEnd = kind;
            w.Emit(SimEventType.DuelRoundEnded, a: winner, b: (int)kind);
            int maxRounds = 2 * t.RoundsToWin + 1;
            if (d.Wins[0] >= t.RoundsToWin || d.Wins[1] >= t.RoundsToWin || d.Round >= maxRounds)
            {
                d.MatchOver = true;
                d.MatchWinner = d.Wins[0] == d.Wins[1] ? -1 : (d.Wins[0] > d.Wins[1] ? 0 : 1);
                w.Emit(SimEventType.DuelMatchEnded, a: d.MatchWinner, b: d.Wins[0], f: d.Wins[1]);
            }
            SetPhase(w, RunPhase.DuelRoundEnd);
        }

        /// <summary>기권·끊김: 그 사람이 경기에서 진다.</summary>
        public override void ForfeitDuel(SimWorld w, int loserSlot)
        {
            var d = w.Run != null ? w.Run.Duel : null;
            if (d == null || d.MatchOver || w.Run.Phase == RunPhase.RunEnded) return;
            int winner = loserSlot == 0 ? 1 : 0;
            d.Wins[winner] = Mathf.Max(d.Wins[winner], DuelTune(w).RoundsToWin);
            EndRound(w, winner, DuelRoundEndKind.Forfeit);
        }

        // ------------------------------------------------------------ 기둥·가시밭 (docs/spec/10-duel.md 3.1·3.4절)

        /// <summary>기둥 1×1칸 넷: 칸 (9,3)·(14,3)·(9,10)·(14,10). 탄을 막고 부서지지 않는다.</summary>
        static void SpawnPillars(SimWorld w)
        {
            Vector2[] cells = { new Vector2(9, 3), new Vector2(14, 3), new Vector2(9, 10), new Vector2(14, 10) };
            foreach (var c in cells)
                w.Obstacles.Add(new ObstacleEntity
                {
                    Id = w.NewEntityId(), Kind = "duel.pillar", Pos = c + new Vector2(0.5f, 0.5f), Radius = 0.5f,
                    Hp = 1e9f, UntilTick = int.MaxValue, BlocksPlayerShots = true, BlocksEnemyShots = true,
                });
        }

        /// <summary>
        /// 가시밭: 라운드 시간 − 30초(90초면 60초)부터 가장자리에서 5초에 1칸씩(최대 6칸) 안으로. 0.5초마다 그 안에 선 사람은 최대 체력의 4%.
        /// 순간 상한·연장 배율은 받지만 "준 피해"에는 넣지 않는다. 보이는 것은 가시 띠(HazardKind.ThornBorder, 피해 0 — 피해는 여기서만).
        /// </summary>
        void TickThorn(SimWorld w, DuelTuning t, DuelState d)
        {
            if (!t.ThornOn) return;
            int elapsed = (d.Overtime ? t.RoundTicks : 0) + (w.Tick - d.RoundStartTick);
            int start = Mathf.Max(0, t.RoundTicks - SimConst.Ticks(30f));
            if (elapsed < start) return;
            int ring = Mathf.Min(1 + (elapsed - start) / Mathf.Max(1, t.ThornStepTicks), t.ThornMaxRing);
            if (ring != d.ThornRing)
            {
                d.ThornRing = ring;
                w.Hazards.Clear();
                float aw = w.ArenaWidth, ah = w.ArenaHeight, k = ring;
                AddBand(w, new Vector2(0f, k * 0.5f), new Vector2(1f, 0f), aw, k);
                AddBand(w, new Vector2(0f, ah - k * 0.5f), new Vector2(1f, 0f), aw, k);
                AddBand(w, new Vector2(k * 0.5f, k), new Vector2(0f, 1f), ah - 2f * k, k);
                AddBand(w, new Vector2(aw - k * 0.5f, k), new Vector2(0f, 1f), ah - 2f * k, k);
            }
            if ((elapsed - start) % Mathf.Max(1, t.ThornDamageTicks) != 0) return;
            for (int i = 0; i < 2 && i < w.Players.Length; i++)
            {
                var p = w.Players[i];
                if (!p.Active || p.Downed) continue;
                int cx = Mathf.FloorToInt(p.Pos.x), cy = Mathf.FloorToInt(p.Pos.y);
                int edge = Mathf.Min(Mathf.Min(cx, (int)w.ArenaWidth - 1 - cx), Mathf.Min(cy, (int)w.ArenaHeight - 1 - cy));
                if (edge >= ring) continue;
                ApplyDuelDamage(w, p, Mathf.Ceil(p.MaxHp * t.ThornPercent / 100f), t, -1, p.Pos);
            }
        }

        static void AddBand(SimWorld w, Vector2 pos, Vector2 dir, float length, float width)
        {
            w.Hazards.Add(new HazardEntity
            {
                Id = w.NewEntityId(), Kind = HazardKind.ThornBorder, Shape = ZoneShape.Band, Pos = pos, Dir = dir,
                Width = width, Length = length, StartTick = w.Tick, UntilTick = int.MaxValue, BaseDamage = 0, SlowOnly = true, SlowPercent = 0f,
            });
        }

        // ------------------------------------------------------------ 결투 과녁(보이지 않는 적)

        /// <summary>
        /// 사람마다 보이지 않는 적(과녁)을 하나 세워 둔다(EnemyState.DuelProxyOf = 슬롯). 적을 맞히는 모든 길(탄, 폭발, 연쇄, 비기, 지대, 화상)이
        /// 그대로 상대에게 닿고, 피해는 SpellSystem.ApplyDamage가 DuelProxyDamage로 넘긴다. 자기 과녁은 맞히지 않는다.
        /// </summary>
        void EnsureDuelProxies(SimWorld w)
        {
            var def = FirstEnemyDef(w);
            for (int slot = 0; slot < 2 && slot < w.Players.Length; slot++)
            {
                if (FindProxy(w, slot) != null) continue;
                var p = w.Players[slot];
                w.Enemies.Add(new EnemyState
                {
                    Id = w.NewEntityId(),
                    DefId = def != null ? def.Id : null,
                    Def = def,
                    DuelProxyOf = slot,
                    NoReward = true,
                    Pos = p.Pos,
                    PrevPos = p.Pos,
                    BodyRadius = DuelTune(w).HitRadius,
                    Hp = 1e9f,
                    MaxHp = 1e9f,
                    Ai = EnemyAiState.Waiting,
                });
            }
        }

        static Wizard.Solo.Data.EnemyDef FirstEnemyDef(SimWorld w)
        {
            var list = w.Content != null ? w.Content.Enemies : null;
            return list != null && list.Count > 0 ? list[0] : null;
        }

        static EnemyState FindProxy(SimWorld w, int slot)
        {
            for (int i = 0; i < w.Enemies.Count; i++)
                if (w.Enemies[i].DuelProxyOf == slot && !w.Enemies[i].Dead) return w.Enemies[i];
            return null;
        }

        /// <summary>4단계 뒤: 과녁을 주인 자리로 옮긴다. 쓰러진 사람의 과녁은 땅속(맞지 않음).</summary>
        public override void Stage4_DuelSync(SimWorld w)
        {
            if (!IsDuel(w)) return;
            EnsureDuelProxies(w);
            for (int slot = 0; slot < 2 && slot < w.Players.Length; slot++)
            {
                var e = FindProxy(w, slot);
                if (e == null) continue;
                var p = w.Players[slot];
                e.PrevPos = e.Pos;
                e.Pos = p.Pos;
                e.Burrowed = !p.Active || p.Downed || w.Run.Phase != RunPhase.DuelFight;
                e.Hp = e.MaxHp;
            }
        }

        public override int DuelProxyDamage(SimWorld w, int victimSlot, int attackerSlot, float amount, Vector2 at)
        {
            if (!IsDuel(w) || w.Run.Phase != RunPhase.DuelFight) return 0;
            if (victimSlot < 0 || victimSlot >= 2 || attackerSlot < 0 || attackerSlot >= 2) return 0;
            var q = w.Players[victimSlot];
            if (!q.Active || q.Downed) return 0;
            var t = DuelTune(w);
            int dealt = ApplyDuelDamage(w, q, amount * DuelStageDamageScale(w, attackerSlot, t), t, attackerSlot, at);
            w.Run.Duel.DamageDealt[attackerSlot] += dealt;
            return dealt;
        }

        /// <summary>탄의 피해 배율에는 무한 웨이브 강화(1.15^단계)가 들어 있다 → 결투 강화(1 + 0.03 × 단계)로 바꾼다.</summary>
        static float DuelStageDamageScale(SimWorld w, int slot, DuelTuning t)
        {
            if (slot < 0 || slot >= w.Players.Length) return 1f;
            var p = w.Players[slot];
            int stage = Mathf.Max(0, p.Wand.EffectiveStage);
            float endless = p.Derived.EnhanceMul > 0f ? p.Derived.EnhanceMul : 1f;
            return (1f + t.EnhancePerStage * stage) / endless;
        }

        /// <summary>결투 피해 한 함수: 연장 배율 → 순간 상한(최근 창) → 체력. 0이 되면 쓰러짐. 돌려주는 값 = 실제로 깎인 체력.</summary>
        static int ApplyDuelDamage(SimWorld w, PlayerState q, float raw, DuelTuning t, int attackerSlot, Vector2 at)
        {
            var d = w.Run.Duel;
            float amt = raw * (d.Overtime ? t.OvertimeMul : 1f);
            int dmg = Mathf.Max(1, Mathf.RoundToInt(amt));
            var ring = q.Slot == 0 ? d.TakenRing0 : d.TakenRing1;
            int used = 0;
            for (int k = 0; k < t.BurstWindow; k++) used += ring[k];
            int cap = q.MaxHp * t.BurstPercent / 100;
            dmg = Mathf.Clamp(Mathf.Min(dmg, cap - used), 0, q.Hp);
            ring[w.Tick % t.BurstWindow] += dmg;
            if (dmg <= 0) return 0;
            q.Hp -= dmg;
            q.HitThisTick = true;
            w.Emit(SimEventType.PlayerHit, q.Slot, a: dmg, b: q.Hp, f: (int)PlayerDamageSource.Projectile, pos: at);
            if (q.Hp <= 0)
            {
                q.Hp = 0;
                q.Downed = true;
                q.Moving = false;
                w.Emit(SimEventType.PlayerDowned, q.Slot);
            }
            return dmg;
        }

        /// <summary>틱마다 순간 상한 창의 이번 칸을 비운다(창을 미끄러뜨린다).</summary>
        static void DuelRingAdvance(SimWorld w)
        {
            var d = w.Run.Duel;
            if (d == null) return;
            int n = DuelTune(w).BurstWindow;
            int k = (w.Tick + 1) % n;
            d.TakenRing0[k] = 0;
            d.TakenRing1[k] = 0;
        }

        static float SegmentDistSq(Vector2 a, Vector2 b, Vector2 p)
        {
            Vector2 ab = b - a;
            float len = ab.sqrMagnitude;
            float u = len > 1e-6f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / len) : 0f;
            return (a + ab * u - p).sqrMagnitude;
        }
    }
}

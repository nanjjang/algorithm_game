using UnityEngine;
using Wizard.Solo.Data;

namespace Wizard.Solo.Sim.Run
{
    // 연습·튜토리얼 방(RunPhase.Practice). 00 문서 5.5절, 08 요약서 6.4·8.5절, D-56·D-57·D-59·D-77.
    // 옵션은 플래그만 든다(읽어 적용하는 것은 Player와 Enemies의 허수아비 두뇌).
    // 수치판·튜토리얼 완료 판정은 이벤트가 아니라 상태 변화로 센다: 허수아비의 깎인 체력, PlayerState의 발사 틱·열·결정·자세·위치,
    // 반동 사격은 Run.OnArtFired(art.recoil)로 센다.
    public sealed partial class RunFlow
    {
        void EnterPractice(SimWorld w, RunScratch s)
        {
            var run = w.Run;
            var cfg = run.Config;
            var practice = w.Content.Practice;

            // 웨이브 없는 방: 기본값(웨이브 1, 동시 예고 상한 없음). 적 체력의 H(w)는 max(1, w.Wave.Wave)로 읽힌다.
            var ws = new WaveState { Wave = 1, StartTick = w.Tick };
            ws.Stats.Wave = 1;
            w.Wave = ws;
            run.Wave = 1;

            run.PracticeInfiniteCrystal = cfg.PracticeInfiniteCrystal;
            run.PracticeEnemyFire = cfg.PracticeEnemyFire;
            run.PracticeHeatFreeze = cfg.PracticeHeatFreeze;

            if (cfg.Mode == RunMode.Tutorial)
            {
                // 튜토리얼 판(DataSolo/Tutorial.json 단계 표). 결정 무한·적 탄 쏘기는 단계마다 ApplyTutorialRules가 정한다
                // (결정을 모아 쓰는 단계에서는 실전처럼 결정이 줄어야 한다). 보관함에 끼워 볼 블록 1개(블록 단계, D-59).
                run.TutorialStep = Mathf.Clamp(cfg.TutorialStep, 1, TutorialCount(s) + 1);
                if (practice != null && !string.IsNullOrEmpty(practice.TutorialBlockId))
                {
                    BlockDef def = w.Content.Block(practice.TutorialBlockId);
                    AddWild(w, s, 0, practice.TutorialBlockId, def != null ? def.Rarity : null, false, 0);
                }
            }

            int window = practice != null ? SimConst.Ticks(practice.RecentDamageWindowSeconds) : 0;
            s.Recent = new float[Mathf.Max(1, window)];
            s.ResetPracticeStats(w.Tick);
            s.ResetTutorialProgress(w.Tick);
            s.TutDoneStep = 0;
            s.NextReportTick = w.Tick + ReportInterval(w);

            SpawnDummy(w, s);
            SetPhase(w, RunPhase.Practice);
        }

        static int ReportInterval(SimWorld w)
        {
            var practice = w.Content.Practice;
            return Mathf.Max(1, practice != null ? SimConst.Ticks(practice.ReportIntervalSeconds) : 1);
        }

        /// <summary>허수아비를 세운다. 자리는 전장 가운데 + 오프셋. 받은 피해를 잴 수 있게 체력 통을 채워 둔다(틱 끝마다 다시 채운다).</summary>
        void SpawnDummy(SimWorld w, RunScratch s)
        {
            var t = s.Tuning;
            Vector2 pos = new Vector2(w.ArenaWidth * 0.5f + t.DummyOffsetX, w.ArenaHeight * 0.5f + t.DummyOffsetY);
            var spec = new EnemySpawnSpec { DefId = Ids.Enemy.Dummy, Pos = pos, NoReward = true, Instant = true, HpOverride = t.DummyHpPool };
            EnemyState e = w.Mods.Enemies.Spawn(w, spec);
            s.DummyId = e != null ? e.Id : 0;
            if (e == null) return;
            if (t.DummyHpPool > 0f)
            {
                e.MaxHp = t.DummyHpPool;
                e.Hp = t.DummyHpPool;
                e.HpFloor = t.DummyHpFloor;
            }
        }

        void CmdPracticeSetOption(SimWorld w, int option, bool on)
        {
            var run = w.Run;
            switch ((PracticeOption)option)
            {
                case PracticeOption.InfiniteCrystal: run.PracticeInfiniteCrystal = on; break;
                case PracticeOption.EnemyFire: run.PracticeEnemyFire = on; break;
                case PracticeOption.HeatFreeze: run.PracticeHeatFreeze = on; break;
            }
        }

        /// <summary>다시 하기: 허수아비·탄·지대를 치우고 열·콤보·결정과 수치판을 처음으로.</summary>
        void CmdPracticeReset(SimWorld w, RunScratch s)
        {
            w.Mods.Enemies.RemoveAll(w, false);
            w.Mods.Threats.ClearAll(w, false);
            w.Mods.Spells.ClearProjectiles(w, -1);
            w.Mods.Arts.ClearZones(w);
            for (int i = 0; i < w.Players.Length; i++)
            {
                var p = w.Players[i];
                if (!p.Active) continue;
                w.Mods.Player.ResetForWaveEnd(w, p);
                p.Crystals = 0;
                p.Debt = 0;
            }
            s.ResetPracticeStats(w.Tick);
            s.NextReportTick = w.Tick + ReportInterval(w);
            SpawnDummy(w, s);
        }

        void CmdTutorialAdvance(SimWorld w, RunScratch s, int step)
        {
            var run = w.Run;
            if (run.Config == null || run.Config.Mode != RunMode.Tutorial) return;
            // 완료 전에 넘기는 것(건너뛰기)도 받는다. 마지막 단계 + 1 = 끝.
            int next = Mathf.Clamp(step, 1, TutorialCount(s) + 1);
            if (next == run.TutorialStep) return;
            run.TutorialStep = next;
            s.ResetTutorialProgress(w.Tick);
            for (int i = 0; i < s.HasLast.Length; i++) s.HasLast[i] = false;
        }

        /// <summary>이번 틱의 J 발사가 임계 사격이었나("발사 뒤 열" 기준 — 02 요약서 6.3절. 넘쳐 잠긴 발사도 임계).</summary>
        static bool WasCritShot(SimWorld w, PlayerState p)
        {
            if (p.Locked && p.LockStartTick == w.Tick) return true;
            int heat = p.HeatHu;
            // 열 고정이면 열이 오르지 않으므로 이번 발사의 열을 더해 본다.
            if (w.Run.PracticeHeatFreeze && p.Spell != null) heat += p.Spell.HeatPerShotHu;
            return p.Derived.CritStartHu > 0 && heat >= p.Derived.CritStartHu;
        }

        void TickPractice(SimWorld w, RunScratch s)
        {
            var run = w.Run;

            // 허수아비가 이번 틱에 받은 피해 = 가득에서 깎인 양. 재고 나서 가득으로 되돌린다.
            float dealt = 0f;
            EnemyState dummy = s.DummyId != 0 ? w.FindEnemy(s.DummyId) : null;
            if (dummy != null && !dummy.Dead)
            {
                dealt = Mathf.Max(0f, dummy.MaxHp - dummy.Hp);
                dummy.Hp = dummy.MaxHp;
            }
            if (dealt > 0f)
            {
                if (s.FirstDamageTick < 0) s.FirstDamageTick = w.Tick;
                s.TotalDamage += dealt;
            }
            if (s.Recent.Length > 0)
            {
                s.Recent[s.RecentIndex] = dealt;
                s.RecentIndex = (s.RecentIndex + 1) % s.Recent.Length;
            }

            bool tutorial = run.Config != null && run.Config.Mode == RunMode.Tutorial;
            for (int i = 0; i < w.Players.Length; i++)
            {
                var p = w.Players[i];
                if (!p.Active) continue;

                bool fired = p.LastFireTick == w.Tick && s.LastFireSeen[i] != w.Tick;
                bool crit = false;
                if (fired)
                {
                    s.LastFireSeen[i] = w.Tick;
                    crit = WasCritShot(w, p);
                    s.Shots++;
                    if (crit) s.CritShots++;
                }

                if (tutorial)
                {
                    float moved = s.HasLast[i] ? (p.Pos - s.LastPos[i]).magnitude : 0f;
                    s.TutMoved += moved;
                    if (p.InStance) s.TutStanceMoved += moved;
                    if (fired) s.TutShots++;
                    if (crit) s.TutCritShots++;
                    if (i == 0) TickTutorialGate(w, s, p, fired, crit, dealt > 0f);
                }

                s.LastPos[i] = p.Pos;
                s.HasLast[i] = true;
            }

            if (w.Tick >= s.NextReportTick)
            {
                s.NextReportTick = w.Tick + ReportInterval(w);
                EmitPracticeReport(w, s);
            }
        }

        // ------------------------------------------------------------ 튜토리얼 관문

        static int TutorialCount(RunScratch s) => s.Plan != null && s.Plan.Count > 0 ? s.Plan.Count : s.Tuning.TutorialLastStep;

        static string TutorialStepId(SimWorld w, RunScratch s) => s.Plan != null ? s.Plan.IdOf(w.Run.TutorialStep) : null;

        /// <summary>
        /// 단계별 연습 설정(튜토리얼 판에서만 — 실전 규칙은 그대로): 도입 대사 중에는 공격 잠금, 스침 단계에만 허수아비가 느린 탄을 쏜다,
        /// 결정 무한은 커맨드·블록 단계에만(결정을 모아 쓰는 단계에서는 실전처럼 줄어든다). 매 틱 다시 적는다.
        /// </summary>
        static void ApplyTutorialRules(SimWorld w, PlayerState p, string id)
        {
            var run = w.Run;
            p.AttackLocked = id == TutorialStepIds.Intro;
            run.PracticeEnemyFire = id == TutorialStepIds.Graze;
            run.PracticeInfiniteCrystal = id == TutorialStepIds.Command || id == TutorialStepIds.Block;
            run.PracticeHeatFreeze = false;
        }

        /// <summary>단계에 들어선 첫 틱의 준비.</summary>
        static void PrepareTutorialStep(SimWorld w, RunScratch s, PlayerState p, string id)
        {
            s.TutStepPrepared = true;
            switch (id)
            {
                case TutorialStepIds.Graze:
                    // 맞아도 다시 해 볼 수 있게 체력을 채워 둔다.
                    w.Mods.Player.Heal(w, p, p.MaxHpCells);
                    s.TutLastGrazeBuffTick = p.GrazeBuffUntilTick;
                    break;
                case TutorialStepIds.Combo:
                    // 앞 단계에서 모인 콤보·결정은 비우고 처음부터 모으게 한다.
                    w.Mods.Player.ResetCombo(w, p, ComboResetReason.Timeout);
                    p.Crystals = 0;
                    s.TutCrystalsAtStart = 0;
                    break;
                case TutorialStepIds.Art:
                    s.TutSpent = 0;
                    break;
                case TutorialStepIds.Command:
                    // 스승이 결정을 빌려 준다(이 단계만 결정 무한 — 실패해도 다시 해 볼 수 있다).
                    for (int k = 0; k < s.Plan.CommandGrantCrystals; k++) w.Mods.Player.GainCrystal(w, p, CrystalSource.Combo);
                    break;
            }
        }

        /// <summary>RunFlow.OnArtFired에서(튜토리얼 판일 때만).</summary>
        void TutorialOnArtFired(SimWorld w, RunScratch s, string artId)
        {
            if (artId == Ids.Art.Recoil) s.TutRecoil = true;
            string id = TutorialStepId(w, s);
            if (id == TutorialStepIds.Art && s.TutSpent > 0) s.TutArtPaid = true;
            if (id == TutorialStepIds.Command && artId != Ids.Art.Burst) s.TutCommandArt = true;
        }

        /// <summary>지금 단계의 관문을 센다(상태 변화로). 진척이 바뀌면 PracticeReport(Progress), 실수는 PracticeReport(Setback).</summary>
        void TickTutorialGate(SimWorld w, RunScratch s, PlayerState p, bool fired, bool crit, bool dummyHit)
        {
            var run = w.Run;
            int step = run.TutorialStep;
            string id = TutorialStepId(w, s);
            ApplyTutorialRules(w, p, id);
            if (id == null || s.TutDoneStep == step) return;
            if (!s.TutStepPrepared) PrepareTutorialStep(w, s, p, id);

            var plan = s.Plan;
            int progress = 0;
            bool done = false;
            switch (id)
            {
                case TutorialStepIds.Intro:
                    // 화면이 대사를 다 보이면 TutorialAdvance로 넘긴다(완료 판정 없음).
                    break;
                case TutorialStepIds.Move:
                    done = (p.Pos - plan.MoveTarget(w)).magnitude <= plan.MoveTargetRadius;
                    break;
                case TutorialStepIds.Fire:
                    if (dummyHit) s.TutHits++;
                    progress = s.TutHits;
                    done = s.TutHits >= plan.FireHits;
                    break;
                case TutorialStepIds.Overheat:
                    if (p.Locked) s.TutLockSeen = true;
                    progress = p.Locked ? 1 : 0;
                    done = s.TutLockSeen && !p.Locked;
                    break;
                case TutorialStepIds.Crit:
                    if (fired)
                    {
                        bool lockedNow = p.Locked && p.LockStartTick == w.Tick;
                        if (lockedNow)
                        {
                            s.TutCritStreak = 0;
                            EmitTutorialSetback(w, step, TutorialSetback.Overflow);
                        }
                        else if (crit) s.TutCritStreak++;
                    }
                    progress = s.TutCritStreak;
                    done = s.TutCritStreak >= plan.CritGoal(p);
                    break;
                case TutorialStepIds.Graze:
                    if (p.GrazeBuffUntilTick != s.TutLastGrazeBuffTick && p.GrazeBuffUntilTick > w.Tick)
                    {
                        s.TutLastGrazeBuffTick = p.GrazeBuffUntilTick;
                        s.TutGrazePendingUntil = w.Tick + SimConst.Ticks(plan.GrazeConfirmSeconds);
                    }
                    if (p.HitThisTick)
                    {
                        s.TutGrazePendingUntil = -1;
                        EmitTutorialSetback(w, step, TutorialSetback.Hit);
                    }
                    else if (s.TutGrazePendingUntil >= 0 && w.Tick >= s.TutGrazePendingUntil)
                    {
                        s.TutGrazePendingUntil = -1;
                        s.TutGrazes++;
                    }
                    progress = s.TutGrazes;
                    done = s.TutGrazes >= plan.GrazeGoal;
                    break;
                case TutorialStepIds.Combo:
                    done = p.Crystals > s.TutCrystalsAtStart;
                    progress = done ? plan.GoalOf(id, p) : Mathf.Min(p.Combo, plan.GoalOf(id, p));
                    break;
                case TutorialStepIds.Art:
                    done = s.TutArtPaid;
                    break;
                case TutorialStepIds.Stance:
                    done = s.TutStanceMoved >= plan.StanceCells;
                    break;
                case TutorialStepIds.Reverse:
                    if (fired && p.Stance == StanceState.Reverse) s.TutReverseShots++;
                    progress = s.TutReverseShots;
                    done = s.TutReverseShots >= plan.ReverseShots;
                    break;
                case TutorialStepIds.Command:
                    done = s.TutCommandArt;
                    break;
                case TutorialStepIds.Block:
                    done = s.TutInserted;
                    break;
            }

            if (progress != s.TutProgressSent)
            {
                s.TutProgressSent = progress;
                w.Emit(SimEventType.PracticeReport, p.Slot, a: step, b: progress, f: plan.GoalOf(id, p), id: TutorialReportKeys.Progress);
            }
            if (!done) return;
            s.TutDoneStep = step;
            w.Emit(SimEventType.TutorialStepDone, a: step);
        }

        static void EmitTutorialSetback(SimWorld w, int step, TutorialSetback why) =>
            w.Emit(SimEventType.PracticeReport, a: step, b: (int)why, id: TutorialReportKeys.Setback);

        void EmitPracticeReport(SimWorld w, RunScratch s)
        {
            float seconds = 0f;
            if (s.FirstDamageTick >= 0) seconds = (w.Tick - s.FirstDamageTick + 1) * SimConst.TickSeconds;
            seconds = Mathf.Max(seconds, s.Tuning.PracticeDpsMinSeconds);
            float dps = seconds > 0f ? s.TotalDamage / seconds : 0f;
            float recent = 0f;
            for (int i = 0; i < s.Recent.Length; i++) recent += s.Recent[i];
            float heatRatio = s.Shots > 0 ? (float)s.CritShots / s.Shots : 0f;
            float perCrystal = s.CrystalsSpent > 0 ? s.TotalDamage / s.CrystalsSpent : 0f;

            w.Emit(SimEventType.PracticeReport, f: dps, id: PracticeReportKeys.Dps);
            w.Emit(SimEventType.PracticeReport, f: recent, id: PracticeReportKeys.RecentDamage);
            w.Emit(SimEventType.PracticeReport, f: heatRatio, id: PracticeReportKeys.HeatRatio);
            w.Emit(SimEventType.PracticeReport, f: perCrystal, id: PracticeReportKeys.DamagePerCrystal);
        }
    }
}

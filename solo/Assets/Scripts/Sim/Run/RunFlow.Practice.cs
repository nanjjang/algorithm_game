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
                // D-59: 결정 무한(④ 반동 사격), 보관함에 끼워 볼 블록 1개(⑤).
                run.PracticeInfiniteCrystal = true;
                run.TutorialStep = Mathf.Clamp(cfg.TutorialStep, 1, s.Tuning.TutorialLastStep + 1);
                if (practice != null && !string.IsNullOrEmpty(practice.TutorialBlockId))
                {
                    BlockDef def = w.Content.Block(practice.TutorialBlockId);
                    AddWild(w, s, practice.TutorialBlockId, def != null ? def.Rarity : null, false, 0);
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
            int next = Mathf.Clamp(step, 1, s.Tuning.TutorialLastStep + 1);
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
                }

                s.LastPos[i] = p.Pos;
                s.HasLast[i] = true;
            }

            if (tutorial)
            {
                CheckTutorialStep(w, s);
            }

            if (w.Tick >= s.NextReportTick)
            {
                s.NextReportTick = w.Tick + ReportInterval(w);
                EmitPracticeReport(w, s);
            }
        }

        /// <summary>지금 단계의 완료 조건(08 요약서 6.4절)을 보고 TutorialStepDone을 단계마다 한 번 낸다.</summary>
        void CheckTutorialStep(SimWorld w, RunScratch s)
        {
            var run = w.Run;
            int step = run.TutorialStep;
            if (step < 1 || step > s.Tuning.TutorialLastStep || s.TutDoneStep == step) return;
            float cells = s.Tuning.TutorialMoveCells;
            bool done;
            switch (step)
            {
                case 1: done = s.TutMoved >= cells && s.TutShots >= 1; break;        // ① 1칸 이상 이동 + 발사 1회
                case 2: done = s.TutStanceMoved >= cells; break;                      // ② 자세를 누른 채 1칸 이동
                case 3: done = s.TutCritShots >= 1; break;                            // ③ 임계 사격 1회
                case 4: done = s.TutRecoil; break;                                    // ④ 반동 사격 1회
                default: done = s.TutInserted; break;                                 // ⑤ 빈 칸에 블록 1개 끼움
            }
            if (!done) return;
            s.TutDoneStep = step;
            w.Emit(SimEventType.TutorialStepDone, a: step);
        }

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

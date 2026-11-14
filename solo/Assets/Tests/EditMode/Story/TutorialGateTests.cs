using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.Sim;
using Wizard.Solo.Sim.Run;
using Wizard.Solo.Tests.Integration;
using Wizard.Solo.Tests.RunModuleTests;

namespace Wizard.Solo.Tests.Story
{
    /// <summary>
    /// 튜토리얼 관문(시뮬레이션 쪽 완료 판정, Sim/Run/RunFlow.Practice.cs + DataSolo/Tutorial.json).
    /// 기대값의 출처: 08 6.4절 단계(이동·발사·임계·반동·블록) + 이 작업의 재설계(해 보며 배우는 관문, 꾹 누르기로는 통과 못 하는 임계 관문,
    /// 맞지 않은 스침만 세는 스침 관문, 실제로 결정을 쓰는 비기 관문). 횟수는 Tutorial.json(솔로 임시) 값을 읽는다.
    /// 튜토리얼 판에서만 바뀌는 연습 설정(결정 무한·적 탄 쏘기·공격 잠금)이 실전 판에 새지 않는지도 본다.
    /// </summary>
    public sealed class TutorialGateTests
    {
        static TutorialPlan Plan => TutorialPlan.From(TestContent.Db);
        static int StepOf(string id) => Plan.StepOf(id);

        static Rig Tutorial(string id) => Rig.Create(c => { c.Mode = RunMode.Tutorial; c.TutorialStep = StepOf(id); });

        static int Done(Rig r, int step)
        {
            int n = 0;
            foreach (var ev in r.W.Events) if (ev.Type == SimEventType.TutorialStepDone && ev.A == step) n++;
            return n;
        }

        static int Reports(SimWorld w, string id, int b = int.MinValue)
        {
            int n = 0;
            foreach (var ev in w.Events)
                if (ev.Type == SimEventType.PracticeReport && ev.Id == id && (b == int.MinValue || ev.B == b)) n++;
            return n;
        }

        static EnemyState Dummy(SimWorld w)
        {
            foreach (var e in w.Enemies) if (e.DefId == Ids.Enemy.Dummy && !e.Dead) return e;
            return null;
        }

        [Test]
        public void Tutorial__RoomSetup_BlockInStorage_IntroLocksAttack_RealCrystalRule()
        {
            var r = Tutorial(TutorialStepIds.Intro);
            r.Step();
            Assert.AreEqual(RunPhase.Practice, r.Run.Phase);
            Assert.AreEqual(1, r.Run.TutorialStep);
            Assert.AreEqual(1, r.Run.Storage.Count, "블록 단계에서 끼울 블록 하나");
            Assert.AreEqual(Ids.Block.SplitTwo, r.Run.Storage[0].BlockId);
            Assert.IsTrue(r.P.AttackLocked, "스승의 말 중에는 쏘지 않는다");
            Assert.IsFalse(r.W.AttackAllowed(r.P));
            Assert.IsFalse(r.Run.PracticeInfiniteCrystal, "결정을 모아 쓰는 단계 전에는 실전처럼 결정이 줄어든다");
            Assert.IsFalse(r.Run.PracticeEnemyFire);
            r.Step(600);
            Assert.AreEqual(0, Done(r, 1), "도입은 화면이 넘긴다(완료 판정 없음)");
            r.Cmd(RunCommandKind.TutorialAdvance, 2);
            r.Step(2);
            Assert.IsFalse(r.P.AttackLocked, "도입이 끝나면 공격 잠금을 푼다");
        }

        [Test]
        public void Tutorial__MoveGate_ReachTargetRing()
        {
            var r = Tutorial(TutorialStepIds.Move);
            int st = StepOf(TutorialStepIds.Move);
            r.Step();
            var target = Plan.MoveTarget(r.W);
            Assert.IsTrue(r.W.InArena(target, 0.5f), "목표 고리는 전장 안");
            r.P.Pos = target + new Vector2(Plan.MoveTargetRadius + 0.3f, 0f);
            r.Step();
            Assert.AreEqual(0, Done(r, st));
            r.P.Pos = target + new Vector2(Plan.MoveTargetRadius * 0.5f, 0f);
            r.Step();
            Assert.AreEqual(1, Done(r, st));
            r.Step(5);
            Assert.AreEqual(1, Done(r, st), "한 번만");
        }

        [Test]
        public void Tutorial__FireGate_CountsTicksTheDummyWasHit_WithProgressReports()
        {
            var r = Tutorial(TutorialStepIds.Fire);
            int st = StepOf(TutorialStepIds.Fire);
            r.Step();
            var d = Dummy(r.W);
            for (int i = 0; i < Plan.FireHits - 1; i++) { d.Hp = d.MaxHp - 10f; r.Step(); }
            Assert.AreEqual(0, Done(r, st));
            Assert.GreaterOrEqual(Reports(r.W, TutorialReportKeys.Progress), Plan.FireHits - 1, "진척 n/m을 알린다");
            d.Hp = d.MaxHp - 10f;
            r.Step();
            Assert.AreEqual(1, Done(r, st));
        }

        [Test]
        public void Tutorial__OverheatGate_DoneAfterLockThenUnlock()
        {
            var r = Tutorial(TutorialStepIds.Overheat);
            int st = StepOf(TutorialStepIds.Overheat);
            r.Step();
            r.P.Locked = true;
            r.Step(30);
            Assert.AreEqual(0, Done(r, st), "잠기는 것만으로는 아니다(잠금 설명을 보여 준다)");
            r.P.Locked = false;
            r.Step();
            Assert.AreEqual(1, Done(r, st), "잠금을 겪고 풀리면 완료");
        }

        [Test]
        public void Tutorial__GrazeGate_OnlyGrazesThatAreNotFollowedByAHitCount()
        {
            var r = Tutorial(TutorialStepIds.Graze);
            int st = StepOf(TutorialStepIds.Graze);
            r.Step();
            Assert.IsTrue(r.Run.PracticeEnemyFire, "스침 단계에만 허수아비가 탄을 쏜다");
            int confirm = SimConst.Ticks(Plan.GrazeConfirmSeconds);
            // 스친 뒤 맞으면 세지 않는다
            r.P.GrazeBuffUntilTick = r.W.Tick + 90;
            r.Step(2);
            r.P.HitThisTick = true;
            r.Step();
            r.P.HitThisTick = false;
            r.Step(confirm + 2);
            Assert.AreEqual(1, Reports(r.W, TutorialReportKeys.Setback, (int)TutorialSetback.Hit));
            for (int g = 0; g < Plan.GrazeGoal; g++)
            {
                Assert.AreEqual(0, Done(r, st));
                r.P.GrazeBuffUntilTick = r.W.Tick + 90 + g;
                r.Step(confirm + 1);
            }
            Assert.AreEqual(1, Done(r, st), "맞지 않은 스침 n번");
            r.Cmd(RunCommandKind.TutorialAdvance, st + 1);
            r.Step(2);
            Assert.IsFalse(r.Run.PracticeEnemyFire, "다음 단계에서는 쏘지 않는다");
        }

        [Test]
        public void Tutorial__ComboGate_StartsFromZeroAndNeedsARealCrystal()
        {
            var r = Tutorial(TutorialStepIds.Combo);
            int st = StepOf(TutorialStepIds.Combo);
            r.P.Crystals = 2;
            r.Step();
            Assert.AreEqual(0, r.P.Crystals, "앞에서 모인 결정은 비우고 시작한다");
            r.Step(10);
            Assert.AreEqual(0, Done(r, st));
            r.P.Crystals = 1;
            r.Step();
            Assert.AreEqual(1, Done(r, st));
        }

        [Test]
        public void Tutorial__ArtGate_NeedsCrystalsActuallySpent()
        {
            var r = Tutorial(TutorialStepIds.Art);
            int st = StepOf(TutorialStepIds.Art);
            r.Step();
            Assert.IsFalse(r.Run.PracticeInfiniteCrystal, "비기 단계는 결정이 실제로 줄어든다");
            r.W.Mods.Run.OnArtFired(r.W, r.P, Ids.Art.Burst);
            r.Step();
            Assert.AreEqual(0, Done(r, st), "결정을 치르지 않은 발동은 세지 않는다");
            r.W.Mods.Run.OnCrystalsSpent(r.W, r.P, 1);
            r.W.Mods.Run.OnArtFired(r.W, r.P, Ids.Art.Burst);
            r.Step();
            Assert.AreEqual(1, Done(r, st));
        }

        [Test]
        public void Tutorial__StanceAndReverseGates()
        {
            var r = Tutorial(TutorialStepIds.Stance);
            int st = StepOf(TutorialStepIds.Stance);
            r.Step();
            for (int i = 0; i < 30; i++) { r.P.Pos += new Vector2(0.1f, 0f); r.Step(); }
            Assert.AreEqual(0, Done(r, st), "자세 없이 걸은 것은 세지 않는다");
            r.P.Stance = StanceState.Stance;
            for (int i = 0; i < Mathf.CeilToInt(Plan.StanceCells / 0.1f) + 1; i++) { r.P.Pos += new Vector2(0.1f, 0f); r.Step(); }
            Assert.AreEqual(1, Done(r, st));

            var v = Tutorial(TutorialStepIds.Reverse);
            int rs = StepOf(TutorialStepIds.Reverse);
            v.Step();
            v.P.LastFireTick = v.W.Tick; v.Step();
            Assert.AreEqual(0, Done(v, rs), "보통 자세로 쏜 것은 세지 않는다");
            v.P.Stance = StanceState.Reverse;
            for (int i = 0; i < Plan.ReverseShots; i++) { v.P.LastFireTick = v.W.Tick; v.Step(2); }
            Assert.AreEqual(1, Done(v, rs));
        }

        [Test]
        public void Tutorial__CommandGate_AnyCommandArtButNotBurst_LendsCrystals()
        {
            var r = Tutorial(TutorialStepIds.Command);
            int st = StepOf(TutorialStepIds.Command);
            r.Step();
            Assert.IsTrue(r.Run.PracticeInfiniteCrystal, "커맨드 단계는 스승이 결정을 빌려 준다");
            r.W.Mods.Run.OnArtFired(r.W, r.P, Ids.Art.Burst);
            r.Step();
            Assert.AreEqual(0, Done(r, st), "자세 밖 K(마력 방출)는 커맨드가 아니다");
            r.W.Mods.Run.OnArtFired(r.W, r.P, Plan.CommandArtId);
            r.Step();
            Assert.AreEqual(1, Done(r, st));
        }

        [Test]
        public void Tutorial__BlockGate_InsertIntoEmptySlot()
        {
            var r = Tutorial(TutorialStepIds.Block);
            int st = StepOf(TutorialStepIds.Block);
            r.Step();
            r.Cmd(RunCommandKind.LoadoutMove, 1, 4);
            r.Step();
            Assert.AreEqual(0, Done(r, st), "칸끼리 옮기는 것은 끼움이 아니다");
            r.Cmd(RunCommandKind.LoadoutMove, 100, 2);
            r.Step();
            Assert.AreEqual(1, Done(r, st));
        }

        [Test]
        public void Tutorial__AdvanceClampsAndEndsAfterLastStep()
        {
            var r = Tutorial(TutorialStepIds.Intro);
            r.Step();
            r.Cmd(RunCommandKind.TutorialAdvance, Plan.Count + 1);
            r.Step();
            Assert.AreEqual(Plan.Count + 1, r.Run.TutorialStep, "마지막 + 1 = 끝");
            Assert.IsFalse(r.P.AttackLocked);
            r.Cmd(RunCommandKind.TutorialAdvance, 99);
            r.Step();
            Assert.AreEqual(Plan.Count + 1, r.Run.TutorialStep);
            r.P.Pos = Plan.MoveTarget(r.W);
            r.Step(3);
            Assert.AreEqual(0, r.Count(SimEventType.TutorialStepDone), "끝난 뒤에는 내지 않는다");

            var practice = Rig.Create(c => c.Mode = RunMode.Practice);
            practice.Cmd(RunCommandKind.TutorialAdvance, 3);
            practice.Step();
            Assert.AreEqual(0, practice.Run.TutorialStep, "연습 모드에서는 받지 않는다");
            Assert.IsFalse(practice.P.AttackLocked);
        }

        // ------------------------------------------------------------ 진짜 모듈로: 꾹 누르기로는 임계 관문을 통과할 수 없다

        static SimWorld RealTutorial(IntegrationKit kit, string stepId)
        {
            var wand = kit.Profile.Wands.Find(x => x.Character == 3);
            var cfg = kit.Ctx.Wands.BuildPracticeConfig(3, wand.Id, RunMode.Tutorial, null, 99UL).Value;
            cfg.TutorialStep = StepOf(stepId);
            var w = new SimWorld(kit.Db, SimModuleSet.CreateDefault(), 99UL);
            w.StartRun(cfg);
            return w;
        }

        static void Run(SimWorld w, int ticks, System.Func<SimWorld, bool> fireWanted, List<SimEvent> log, ref bool down)
        {
            for (int i = 0; i < ticks; i++)
            {
                bool want = fireWanted(w);
                if (want != down) { w.EnqueueInput(0, new InputEvent(LogicalKey.Fire, want, SimConst.TickStartU(w.Tick))); down = want; }
                w.Step();
                log.AddRange(w.Events);
                w.Events.Clear();
            }
        }

        [Test]
        public void Tutorial__CritGate_HoldingFireNeverPasses_FeatheringPasses()
        {
            using (var kit = new IntegrationKit())
            {
                var w = RealTutorial(kit, TutorialStepIds.Crit);
                int st = StepOf(TutorialStepIds.Crit);
                var log = new List<SimEvent>();
                bool down = false;
                Run(w, 1800, x => true, log, ref down);   // 30초 꾹
                int done = 0, overflow = 0, locks = 0;
                foreach (var e in log)
                {
                    if (e.Type == SimEventType.TutorialStepDone && e.A == st) done++;
                    if (e.Type == SimEventType.PracticeReport && e.Id == TutorialReportKeys.Setback && e.B == (int)TutorialSetback.Overflow) overflow++;
                    if (e.Type == SimEventType.HeatLockEntered) locks++;
                }
                Assert.Greater(locks, 1, "꾹 누르면 넘쳐 잠긴다");
                Assert.AreEqual(locks, overflow, "넘칠 때마다 진척을 0으로 돌리고 알린다");
                Assert.AreEqual(0, done, "꾹 눌러서는 통과하지 못한다");

                // 끊어 쏘기: 다음 발이 넘치면 쏘지 않고 식기를 기다린다.
                var p = w.P0;
                Run(w, 240, x => false, log, ref down);
                log.Clear();
                Run(w, 3600, x => !x.P0.Locked && x.P0.HeatHu + x.P0.Spell.HeatPerShotHu <= Sim.Player.PlayerTuning.HeatMaxHu, log, ref down);
                bool passed = false;
                foreach (var e in log) if (e.Type == SimEventType.TutorialStepDone && e.A == st) passed = true;
                Assert.IsTrue(passed, "흰 테에서 끊어 쏘면 통과한다");
                Assert.Greater(Plan.CritGoal(p), TutorialPlan.CritShotsPerFill(p), "목표가 한 번 차는 동안의 임계 수보다 많다");
            }
        }

        [Test]
        public void Tutorial__RealRunIsUntouched_NoAttackLockNoFreeCrystals()
        {
            using (var kit = new IntegrationKit())
            {
                var wand = kit.Profile.Wands.Find(x => x.Character == 3);
                var cfg = kit.Ctx.Wands.BuildPracticeConfig(3, wand.Id, RunMode.Practice, null, 5UL).Value;
                var w = new SimWorld(kit.Db, SimModuleSet.CreateDefault(), 5UL);
                w.StartRun(cfg);
                for (int i = 0; i < 120; i++) w.Step();
                Assert.IsFalse(w.P0.AttackLocked, "연습 방(튜토리얼 아님)에는 관문 설정이 없다");
                Assert.AreEqual(cfg.PracticeInfiniteCrystal, w.Run.PracticeInfiniteCrystal, "연습 옵션은 설정 그대로");
                Assert.AreEqual(cfg.PracticeEnemyFire, w.Run.PracticeEnemyFire);
            }
        }
    }
}

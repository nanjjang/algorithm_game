using NUnit.Framework;
using Wizard.Solo.Screens.Story;
using Wizard.Solo.Sim;
using Wizard.Solo.Sim.Run;
using Wizard.Solo.UI;

namespace Wizard.Solo.Tests.Story
{
    /// <summary>
    /// 튜토리얼(허수아비 방 5단계). 기대값의 출처: 08 요약서 6.4절(5단계와 완료 조건 U-53, 풍선 열쇠 str.tutorial.step.1~5, 본문 미정 N-05),
    /// 00 문서 4.1절(Esc = 건너뛰기 확인, 일시정지 화면을 쓰지 않음, 전투 입력의 문), 5.5절(TutorialAdvance A = 넘어갈 단계, 6 = 끝),
    /// 8.1절(Tutorial의 RunConfig = 진짜 판, 고치지 않고 Battle에 넘긴다), D-59, MetaContracts의 BuildPracticeConfig 약속.
    /// </summary>
    public sealed class TutorialFlowTests
    {
        static TutorialPlan Plan => TutorialPlan.From(TestContent.Db);
        static int Delay => SimConst.Ticks(Plan.AdvanceDelaySeconds);
        static int Count => Plan.Count;
        static int StepOf(string id) => Plan.StepOf(id);
        const int Hint = 300, Setback = 240;

        static TutorialFlowModel Model() => new TutorialFlowModel(Plan, Delay, Hint, Setback);

        // ------------------------------------------------------------ 순수 모델: 단계 진행표

        [Test]
        public void Tutorial__PlanHasAllGatesInTeachingOrder()
        {
            var plan = Plan;
            CollectionAssert.AreEqual(new[]
            {
                TutorialStepIds.Intro, TutorialStepIds.Move, TutorialStepIds.Fire, TutorialStepIds.Overheat, TutorialStepIds.Crit,
                TutorialStepIds.Graze, TutorialStepIds.Combo, TutorialStepIds.Art, TutorialStepIds.Stance, TutorialStepIds.Reverse,
                TutorialStepIds.Command, TutorialStepIds.Block
            }, plan.Steps);
            Assert.Greater(plan.FireHits, 0);
            Assert.Greater(plan.GrazeGoal, 0);
            Assert.Greater(plan.CritExtraShots, 0, "꾹 눌러 통과하지 못하게 덧 횟수가 있어야 한다");
            Assert.Greater(plan.IntroLines, 0);
            Assert.Greater(plan.HintAfterSeconds, 0f);
        }

        [Test]
        public void S08_U53__StepTableAdvancesThenEnds()
        {
            var m = Model();
            int tick = 100;
            for (int step = 1; step <= Count; step++)
            {
                bool last;
                Assert.IsTrue(m.OnStepDone(step, step, tick, out last), "step " + step);
                Assert.AreEqual(step == Count, last);
                Assert.AreEqual(TutorialActionKind.None, m.Poll(tick + Delay - 1).Kind, "잠깐 뒤에 넘긴다");
                var a = m.Poll(tick + Delay);
                Assert.AreEqual(step + 1, a.Next);
                Assert.AreEqual(step == Count ? TutorialActionKind.AdvanceAndFinish : TutorialActionKind.Advance, a.Kind);
                Assert.AreEqual(TutorialActionKind.None, m.Poll(tick + Delay + 1).Kind, "한 번만");
                tick += 500;
            }
            Assert.IsTrue(m.Finished);
        }

        [Test]
        public void S08_U53__DoneOfAnotherStepIsIgnored()
        {
            var m = Model();
            bool last;
            Assert.IsFalse(m.OnStepDone(3, 2, 10, out last), "지금 단계(2)가 아닌 완료");
            Assert.IsFalse(m.OnStepDone(0, 1, 10, out last));
            Assert.IsFalse(m.OnStepDone(Count + 1, 1, 10, out last));
            Assert.IsTrue(m.OnStepDone(2, 2, 10, out last));
            Assert.IsFalse(m.OnStepDone(2, 2, 11, out last), "같은 단계 두 번");
        }

        [Test]
        public void Tutorial__BalloonKeysPerGate_LoadoutButtonOnlyOnBlockStep()
        {
            var names = TestContent.Names;
            var m = Model();
            for (int s = 1; s <= Count; s++)
            {
                string id = Plan.IdOf(s);
                var b = m.Balloon(s, 0, names, false);
                Assert.IsTrue(b.Visible);
                Assert.AreEqual(Count, b.Total);
                Assert.AreEqual("str.tutorial.gate." + id + ".title", b.TitleKey);
                Assert.IsTrue(names.Has(b.TitleKey), b.TitleKey);
                Assert.IsNotNull(b.BodyKey, "본문(1~2문장)이 모든 단계에 있다: " + id);
                Assert.AreEqual(id == TutorialStepIds.Intro, b.IsIntro);
                Assert.AreEqual(id == TutorialStepIds.Block, b.ShowOpenLoadout, id);
                if (id != TutorialStepIds.Intro)
                {
                    Assert.IsNotNull(b.GoalKey, "화면 안 목표가 있다: " + id);
                    Assert.IsTrue(names.Has(TutorialFlowModel.HintKeyOf(id)), "막혔을 때 힌트: " + id);
                    Assert.IsTrue(names.Has(TutorialFlowModel.DoneKeyOf(id)), "칭찬: " + id);
                }
            }
            Assert.IsFalse(m.Balloon(Count + 1, 0, names, false).Visible, "마지막 + 1 = 끝");
            Assert.IsFalse(m.Balloon(0, 0, names, false).Visible);
            Assert.IsTrue(m.CanOpenLoadout(StepOf(TutorialStepIds.Block)));
            Assert.IsFalse(m.CanOpenLoadout(StepOf(TutorialStepIds.Command)));
            for (int i = 1; i <= Plan.IntroLines; i++) Assert.IsTrue(names.Has(TutorialFlowModel.IntroLineKey(i)), "도입 대사 " + i);
        }

        [Test]
        public void S08_N05__BalloonBodyHiddenWithoutValue()
        {
            var names = new StoryFakeNames();
            var m = Model();
            int fire = StepOf(TutorialStepIds.Fire);
            Assert.IsNull(m.Balloon(fire, 0, names, false).BodyKey, "열쇠가 없으면 본문 줄을 숨긴다");
            names.Set("str.tutorial.gate.fire.body", string.Empty);
            Assert.IsNull(m.Balloon(fire, 0, names, false).BodyKey, "값이 비어도 숨긴다");
            names.Set("str.tutorial.gate.fire.body", "x");
            Assert.AreEqual("str.tutorial.gate.fire.body", m.Balloon(fire, 0, names, false).BodyKey);
        }

        [Test]
        public void Tutorial__OverheatBodySwitchesWhileLocked()
        {
            var m = Model();
            int st = StepOf(TutorialStepIds.Overheat);
            Assert.AreEqual("str.tutorial.gate.overheat.body", m.Balloon(st, 0, TestContent.Names, false).BodyKey);
            Assert.AreEqual("str.tutorial.gate.overheat.body_locked", m.Balloon(st, 1, TestContent.Names, true).BodyKey, "잠긴 동안 잠금 설명");
        }

        [Test]
        public void Tutorial__StuckHintAfterNoProgress_ProgressResetsTheClock()
        {
            var names = TestContent.Names;
            var m = Model();
            int st = StepOf(TutorialStepIds.Fire);
            Assert.IsNull(m.Balloon(st, 1000, names, false).HintKey, "처음에는 힌트 없음");
            Assert.IsNull(m.Balloon(st, 1000 + Hint - 1, names, false).HintKey);
            Assert.AreEqual(TutorialFlowModel.HintKeyOf(TutorialStepIds.Fire), m.Balloon(st, 1000 + Hint, names, false).HintKey, "진척 없이 오래 → 힌트");
            m.OnProgress(st, 1, Plan.FireHits, 1000 + Hint + 10);
            Assert.IsNull(m.Balloon(st, 1000 + Hint + 11, names, false).HintKey, "진척이 오르면 힌트를 거둔다");
            Assert.AreEqual(1, m.Balloon(st, 1000 + Hint + 11, names, false).Progress);
            Assert.AreEqual(Plan.FireHits, m.Balloon(st, 1000 + Hint + 11, names, false).Goal);
        }

        [Test]
        public void Tutorial__SetbackHintShowsImmediatelyThenExpires()
        {
            var names = TestContent.Names;
            var m = Model();
            int st = StepOf(TutorialStepIds.Crit);
            m.Balloon(st, 0, names, false);
            m.OnSetback(st, TutorialSetback.Overflow, 50);
            Assert.AreEqual("str.tutorial.setback.overflow", m.Balloon(st, 51, names, false).HintKey);
            Assert.IsTrue(names.Has("str.tutorial.setback.overflow"));
            Assert.IsTrue(names.Has("str.tutorial.setback.hit"));
            Assert.IsTrue(names.Has("str.tutorial.setback.fizzle"));
            Assert.AreNotEqual("str.tutorial.setback.overflow", m.Balloon(st, 50 + Setback, names, false).HintKey, "잠시 뒤 거둔다");
            m.OnSetback(st - 1, TutorialSetback.Hit, 60);
            Assert.AreNotEqual("str.tutorial.setback.hit", m.Balloon(st, 61, names, false).HintKey, "지난 단계의 실수는 무시");
        }

        [Test]
        public void Tutorial__IntroLinesThenAdvance()
        {
            var m = Model();
            int st = StepOf(TutorialStepIds.Intro);
            for (int i = 1; i < Plan.IntroLines; i++)
            {
                Assert.AreEqual(i, m.IntroLine);
                Assert.AreEqual(TutorialActionKind.None, m.NextIntroLine(st).Kind);
            }
            var a = m.NextIntroLine(st);
            Assert.AreEqual(TutorialActionKind.Advance, a.Kind);
            Assert.AreEqual(st + 1, a.Next);
            Assert.AreEqual(TutorialActionKind.None, m.NextIntroLine(st + 1).Kind, "다른 단계에서는 줄 넘김이 없다");
        }

        [Test]
        public void S08_6_4__TitleKeysExistInNameTable()
        {
            Assert.IsTrue(TestContent.Names.Has(TutorialController.SkipTitleKey));
            Assert.IsTrue(TestContent.Names.Has(TutorialController.SkipBodyKey));
            Assert.IsTrue(TestContent.Names.Has(TutorialScreen.OpenLoadoutKey));
            Assert.IsTrue(TestContent.Names.Has(TutorialScreen.SkipActionKey));
            Assert.IsTrue(TestContent.Names.Has(TutorialScreen.StepCounterKey));
            Assert.IsTrue(TestContent.Names.Has(TutorialScreen.NextLineActionKey));
            Assert.IsTrue(TestContent.Names.Has(TutorialScreen.IntroCounterKey));
            Assert.IsTrue(TestContent.Names.Has(TutorialLayer.TargetLabelKey));
        }

        [Test]
        public void S08_6_4__DoneMarkWhileWaitingToAdvance()
        {
            var m = Model();
            int st = StepOf(TutorialStepIds.Block);
            bool last;
            m.OnStepDone(st, st, 0, out last);
            var b = m.Balloon(st, 0, TestContent.Names, false);
            Assert.IsTrue(b.Done);
            Assert.AreEqual(TutorialFlowModel.DoneKeyOf(TutorialStepIds.Block), b.PraiseKey);
            Assert.IsFalse(b.ShowOpenLoadout, "끝난 뒤에는 조립 열기를 다시 띄우지 않는다");
            Assert.IsNull(b.HintKey, "완료 뒤에는 힌트 없음");
        }

        // ------------------------------------------------------------ 계약 벡터(가짜 IWandService·IGameSession·IScreenManager)

        [Test]
        public void S00_G25_C01__TutorialConfigFromBuildPracticeConfigNotBuildRunConfig()
        {
            var rig = new TutorialRig();
            var ctl = new TutorialController(rig.Ctx);
            Assert.IsTrue(ctl.Enter(rig.Args()));
            Assert.AreEqual(1, rig.Wands.PracticeCalls);
            Assert.AreEqual(0, rig.Wands.BuildRunConfigCalls, "BuildRunConfig를 부르지 않는다(출발 횟수가 오르지 않는다)");
            Assert.AreEqual(3, rig.Wands.LastCharacter, "char.3 → 3");
            Assert.AreEqual(4242L, rig.Wands.LastWandId);
            Assert.AreEqual(RunMode.Tutorial, rig.Wands.LastMode);
            Assert.IsNull(rig.Wands.LastOverride, "overrideLoadout = null(그 지팡이의 출발 조립)");
            Assert.AreEqual(1, rig.Session.Started.Count);
            Assert.AreSame(rig.Wands.Returned, rig.Session.Started[0]);
            Assert.AreNotSame(rig.RealCfg, rig.Session.Started[0], "튜토리얼 설정과 진짜 판 설정은 다른 객체");
            Assert.AreEqual(RunMode.Endless, rig.RealCfg.Mode);
        }

        [Test]
        public void S00_G25_C02__InputScreenIsTutorialThenRestored()
        {
            var rig = new TutorialRig();
            rig.Session.InputScreen = ScreenId.ExpeditionPrep;
            rig.Session.InputHistory.Clear();
            var ctl = new TutorialController(rig.Ctx);
            ctl.Enter(rig.Args());
            Assert.AreEqual(ScreenId.Tutorial, rig.Session.InputScreen);
            ctl.RequestSkip();
            rig.Screens.ConfirmResult(true);
            Assert.AreEqual(ScreenId.ExpeditionPrep, rig.Session.InputScreen, "끝나면 앞의 값으로 되돌린다");
            CollectionAssert.AreEqual(new[] { ScreenId.Tutorial, ScreenId.ExpeditionPrep }, rig.Session.InputHistory);
            ctl.Exit();
            Assert.AreEqual(ScreenId.ExpeditionPrep, rig.Session.InputScreen, "OnExit에서 두 번 되돌리지 않는다");
        }

        [Test]
        public void S00_G25_C03__StepDoneEnqueuesTutorialAdvanceNextStep()
        {
            var rig = new TutorialRig();
            var ctl = new TutorialController(rig.Ctx, Tuning());
            ctl.Enter(rig.Args());
            var w = rig.Session.World;
            for (int n = 2; n <= 4; n++)
            {
                w.Run.TutorialStep = n;
                w.Tick = n * 1000;
                rig.Session.Raise(new SimEvent { Type = SimEventType.TutorialStepDone, A = n, Tick = w.Tick, Slot = -1 });
                ctl.Tick();
                Assert.AreEqual(0, w.Commands.Count, "곧바로 넘기지 않는다(잠깐 뒤)");
                w.Tick += Delay;
                ctl.Tick();
                Assert.AreEqual(1, w.Commands.Count);
                Assert.AreEqual(RunCommandKind.TutorialAdvance, w.Commands[0].Kind);
                Assert.AreEqual(n + 1, w.Commands[0].A, "A = 단계 + 1");
                w.Commands.Clear();
            }
            Assert.IsTrue(ctl.Running);
        }

        [Test]
        public void S00_G25_C04__Step5OpensCampInTutorialMode()
        {
            var rig = new TutorialRig();
            var ctl = new TutorialController(rig.Ctx, Tuning());
            ctl.Enter(rig.Args());
            var w = rig.Session.World;
            w.Run.TutorialStep = StepOf(TutorialStepIds.Block) - 1;
            Assert.IsFalse(ctl.OpenLoadout(), "블록 단계 전에는 열지 않는다");
            w.Run.TutorialStep = StepOf(TutorialStepIds.Block);
            Assert.IsTrue(ctl.OpenLoadout());
            Assert.AreEqual(1, rig.Screens.Pushed.Count);
            Assert.AreEqual(ScreenId.Camp, rig.Screens.Pushed[0].Key);
            var a = rig.Screens.Pushed[0].Value;
            Assert.AreEqual(ScreenId.Tutorial, a.Entry);
            Assert.AreEqual(CampMode.Tutorial, a.Get(ScreenArgKeys.CampMode, CampMode.Camp));
        }

        [Test]
        public void S00_G25_C05__Step5DoneClosesCampAndHandsRealConfigToBattle()
        {
            var rig = new TutorialRig();
            var ctl = new TutorialController(rig.Ctx, Tuning());
            ctl.Enter(rig.Args());
            var w = rig.Session.World;
            w.Run.TutorialStep = Count;
            ctl.OpenLoadout();
            Assert.AreEqual(ScreenId.Camp, rig.Screens.Top);
            w.Tick = 900;
            rig.Session.Raise(new SimEvent { Type = SimEventType.TutorialStepDone, A = Count, Tick = 900, Slot = -1 });
            Assert.AreEqual(ScreenId.Tutorial, rig.Screens.Top, "Camp를 PopTo(Tutorial)로 닫는다");
            CollectionAssert.Contains(rig.Screens.PopToCalls, ScreenId.Tutorial);
            Assert.IsTrue(ctl.Running, "잠깐 뒤에 끝낸다");

            w.Tick = 900 + Delay;
            ctl.Tick();
            Assert.AreEqual(Count + 1, w.Commands.Count > 0 ? w.Commands[w.Commands.Count - 1].A : -1, "TutorialAdvance(A = 마지막 + 1 = 끝)");
            Assert.IsFalse(ctl.Running);

            Assert.IsTrue(rig.Profiles.Data.Flags.TutorialDummyDone, "flags.tutorial_dummy_done");
            Assert.AreEqual(1, rig.Profiles.Saves);
            Assert.AreEqual(1, rig.Session.EndRunCalls);
            Assert.AreEqual(ScreenId.Loading, rig.Screens.ReplacedWith);
            var args = rig.Screens.ReplacedArgs;
            Assert.AreEqual(ScreenId.Battle, args.Get(ScreenArgKeys.NextScreen, ScreenId.None));
            var next = args.Get<ScreenArgs>(ScreenArgKeys.NextArgs);
            Assert.IsNotNull(next);
            var handed = next.Get<RunConfig>(ScreenArgKeys.RunConfig);
            Assert.AreSame(rig.RealCfg, handed, "받은 realCfg와 같은 객체");
            Assert.AreEqual(RunMode.Endless, handed.Mode);

            // 끝의 순서: 표시 저장 → EndRun → InputScreen 되돌림 → Loading.
            int save = rig.Log.IndexOf("Save tutorial_dummy_done=1");
            int end = rig.Log.IndexOf("EndRun");
            int restore = rig.Log.Lines.LastIndexOf("InputScreen=" + ScreenId.None);
            int replace = rig.Log.IndexOf("Replace " + ScreenId.Loading);
            Assert.IsTrue(save >= 0 && save < end && end < restore && restore < replace, string.Join(" | ", rig.Log.Lines));
        }

        [Test]
        public void S00_G25_C06__EscOpensSkipConfirmNotPause()
        {
            var rig = new TutorialRig();
            var ctl = new TutorialController(rig.Ctx, Tuning());
            ctl.Enter(rig.Args());
            ctl.RequestSkip();
            Assert.AreEqual(1, rig.Screens.ConfirmCalls);
            Assert.AreEqual(TutorialController.SkipTitleKey, rig.Screens.ConfirmTitle);
            Assert.AreEqual(TutorialController.SkipBodyKey, rig.Screens.ConfirmBody);
            Assert.AreEqual(0, rig.Session.PausedSets, "일시정지를 쓰지 않는다");
            Assert.IsFalse(rig.Session.PauseRequestedSubscribed, "PauseRequested는 듣지 않는다");
            CollectionAssert.DoesNotContain(rig.Screens.Stack, ScreenId.Pause);

            rig.Screens.ConfirmResult(false);
            Assert.IsTrue(ctl.Running, "아니오 = 계속");
            Assert.AreEqual(0, rig.Session.EndRunCalls);

            ctl.RequestSkip();
            rig.Screens.ConfirmResult(true);
            Assert.IsFalse(ctl.Running);
            Assert.IsTrue(rig.Profiles.Data.Flags.TutorialDummyDone, "건너뛰어도 완료 표시를 저장한다");
            Assert.AreEqual(ScreenId.Loading, rig.Screens.ReplacedWith);
            var next = rig.Screens.ReplacedArgs.Get<ScreenArgs>(ScreenArgKeys.NextArgs);
            Assert.AreSame(rig.RealCfg, next.Get<RunConfig>(ScreenArgKeys.RunConfig));
            Assert.AreEqual(0, rig.Session.SimEventSubscribers, "구독을 걷는다");
        }

        [Test]
        public void S00_G25_C07__NoWandServiceGoesStraightToRealRun()
        {
            var rig = new TutorialRig(withWands: false);
            var ctl = new TutorialController(rig.Ctx, Tuning());
            Assert.IsFalse(ctl.Enter(rig.Args()));
            Assert.AreEqual(0, rig.Session.Started.Count, "튜토리얼 방을 열지 않는다");
            Assert.AreEqual(ScreenId.None, rig.Session.InputScreen);
            Assert.AreEqual(ScreenId.Loading, rig.Screens.ReplacedWith);
            var next = rig.Screens.ReplacedArgs.Get<ScreenArgs>(ScreenArgKeys.NextArgs);
            Assert.AreEqual(ScreenId.Battle, rig.Screens.ReplacedArgs.Get(ScreenArgKeys.NextScreen, ScreenId.None));
            Assert.AreSame(rig.RealCfg, next.Get<RunConfig>(ScreenArgKeys.RunConfig));
        }

        [Test]
        public void S00_G25_C07b__PracticeConfigFailureGoesStraightToRealRun()
        {
            var rig = new TutorialRig();
            rig.Wands.Fail = true;
            var ctl = new TutorialController(rig.Ctx, Tuning());
            Assert.IsFalse(ctl.Enter(rig.Args()));
            Assert.AreEqual(0, rig.Session.Started.Count);
            Assert.AreEqual(0, rig.Wands.BuildRunConfigCalls);
            Assert.AreEqual(ScreenId.Loading, rig.Screens.ReplacedWith);
        }

        [Test]
        public void S00_G25_C08__EventsOfAnotherWorldAreIgnored()
        {
            var rig = new TutorialRig();
            var ctl = new TutorialController(rig.Ctx, Tuning());
            ctl.Enter(rig.Args());
            var w = rig.Session.World;
            var other = new SimWorld(TestContent.Db, SimModuleSet.Stubs());
            other.Run.TutorialStep = 2;
            w.Run.TutorialStep = 2;
            rig.Session.Raise(other, new SimEvent { Type = SimEventType.TutorialStepDone, A = 2, Tick = 0 });
            w.Tick = Delay * 2;
            ctl.Tick();
            Assert.AreEqual(0, w.Commands.Count);
        }

        [Test]
        public void Tutorial__IntroJAdvancesLinesThenStepWithoutStepDone()
        {
            var rig = new TutorialRig();
            var ctl = new TutorialController(rig.Ctx, Tuning());
            ctl.Enter(rig.Args());
            var w = rig.Session.World;
            w.Run.TutorialStep = StepOf(TutorialStepIds.Intro);
            for (int i = 1; i < Plan.IntroLines; i++) Assert.IsTrue(ctl.NextIntroLine());
            Assert.AreEqual(0, w.Commands.Count, "대사 중에는 넘기지 않는다");
            Assert.IsTrue(ctl.NextIntroLine());
            Assert.AreEqual(1, w.Commands.Count);
            Assert.AreEqual(RunCommandKind.TutorialAdvance, w.Commands[0].Kind);
            Assert.AreEqual(StepOf(TutorialStepIds.Intro) + 1, w.Commands[0].A);
            w.Run.TutorialStep = StepOf(TutorialStepIds.Move);
            Assert.IsFalse(ctl.NextIntroLine(), "다른 단계의 J는 줄 넘김이 아니다");
        }

        [Test]
        public void Tutorial__ProgressAndSetbackReportsReachBalloon()
        {
            var rig = new TutorialRig();
            var ctl = new TutorialController(rig.Ctx, Tuning());
            ctl.Enter(rig.Args());
            var w = rig.Session.World;
            int st = StepOf(TutorialStepIds.Graze);
            w.Run.TutorialStep = st;
            w.Tick = 10;
            ctl.Balloon();
            rig.Session.Raise(new SimEvent { Type = SimEventType.PracticeReport, Id = TutorialReportKeys.Progress, A = st, B = 2, F = 3, Tick = 10 });
            var b = ctl.Balloon();
            Assert.AreEqual(2, b.Progress);
            Assert.AreEqual(3, b.Goal);
            int setbacks = 0;
            ctl.SetbackRaised += _ => setbacks++;
            rig.Session.Raise(new SimEvent { Type = SimEventType.PracticeReport, Id = TutorialReportKeys.Setback, A = st, B = (int)TutorialSetback.Hit, Tick = 11 });
            Assert.AreEqual(1, setbacks);
            Assert.AreEqual("str.tutorial.setback.hit", ctl.Balloon().HintKey);
            rig.Session.Raise(new SimEvent { Type = SimEventType.ArtFizzled, A = (int)ArtFizzleReason.NoCrystal, Tick = 12 });
            Assert.AreEqual("str.tutorial.setback.fizzle", ctl.Balloon().HintKey, "결정 부족 불발도 바로 알려 준다");
        }

        static StoryTuning Tuning()
        {
            var t = StoryTuning.Defaults();
            t.TutorialAdvanceDelayTicks = Delay;
            return t;
        }
    }
}

using NUnit.Framework;
using Wizard.Solo.Screens.Story;
using Wizard.Solo.Sim;
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
        const int Delay = 60;

        // ------------------------------------------------------------ 순수 모델: 단계 진행표

        [Test]
        public void S08_U53__StepTableAdvancesOneToFiveThenEnds()
        {
            var m = new TutorialFlowModel(5, Delay);
            int tick = 100;
            for (int step = 1; step <= 5; step++)
            {
                bool last;
                Assert.IsTrue(m.OnStepDone(step, step, tick, out last), "step " + step);
                Assert.AreEqual(step == 5, last);
                Assert.AreEqual(TutorialActionKind.None, m.Poll(tick + Delay - 1).Kind, "잠깐 뒤에 넘긴다");
                var a = m.Poll(tick + Delay);
                Assert.AreEqual(step + 1, a.Next);
                Assert.AreEqual(step == 5 ? TutorialActionKind.AdvanceAndFinish : TutorialActionKind.Advance, a.Kind);
                Assert.AreEqual(TutorialActionKind.None, m.Poll(tick + Delay + 1).Kind, "한 번만");
                tick += 500;
            }
            Assert.IsTrue(m.Finished);
        }

        [Test]
        public void S08_U53__DoneOfAnotherStepIsIgnored()
        {
            var m = new TutorialFlowModel(5, Delay);
            bool last;
            Assert.IsFalse(m.OnStepDone(3, 2, 10, out last), "지금 단계(2)가 아닌 완료");
            Assert.IsFalse(m.OnStepDone(0, 1, 10, out last));
            Assert.IsFalse(m.OnStepDone(6, 1, 10, out last));
            Assert.IsTrue(m.OnStepDone(2, 2, 10, out last));
            Assert.IsFalse(m.OnStepDone(2, 2, 11, out last), "같은 단계 두 번");
        }

        [Test]
        public void S08_6_4__BalloonKeysAndLoadoutButtonOnStep5()
        {
            var names = new StoryFakeNames();
            var m = new TutorialFlowModel(5, Delay);
            for (int s = 1; s <= 5; s++)
            {
                var b = m.Balloon(s, names);
                Assert.IsTrue(b.Visible);
                Assert.AreEqual("str.tutorial.step." + s, b.TitleKey);
                Assert.AreEqual(s == 5, b.ShowOpenLoadout);
            }
            Assert.IsFalse(m.Balloon(6, names).Visible, "6 = 끝");
            Assert.IsFalse(m.Balloon(0, names).Visible);
            Assert.IsTrue(m.CanOpenLoadout(5));
            Assert.IsFalse(m.CanOpenLoadout(4));
        }

        [Test]
        public void S08_N05__BalloonBodyHiddenWithoutValue()
        {
            var names = new StoryFakeNames();
            var m = new TutorialFlowModel(5, Delay);
            Assert.IsNull(m.Balloon(1, names).BodyKey, "열쇠가 없으면 본문 줄을 숨긴다");
            names.Set("str.tutorial.step.1.body", string.Empty);
            Assert.IsNull(m.Balloon(1, names).BodyKey, "값이 비어도 숨긴다");
            names.Set("str.tutorial.step.1.body", "x");
            Assert.AreEqual("str.tutorial.step.1.body", m.Balloon(1, names).BodyKey);
        }

        [Test]
        public void S08_6_4__TitleKeysExistInNameTable()
        {
            for (int s = 1; s <= 5; s++) Assert.IsTrue(TestContent.Names.Has("str.tutorial.step." + s), "step " + s);
            Assert.IsTrue(TestContent.Names.Has(TutorialController.SkipTitleKey));
            Assert.IsTrue(TestContent.Names.Has(TutorialController.SkipBodyKey));
            Assert.IsTrue(TestContent.Names.Has(TutorialScreen.OpenLoadoutKey));
            Assert.IsTrue(TestContent.Names.Has(TutorialScreen.SkipActionKey));
        }

        [Test]
        public void S08_6_4__DoneMarkWhileWaitingToAdvance()
        {
            var names = new StoryFakeNames();
            var m = new TutorialFlowModel(5, Delay);
            bool last;
            m.OnStepDone(5, 5, 0, out last);
            var b = m.Balloon(5, names);
            Assert.IsTrue(b.Done);
            Assert.IsFalse(b.ShowOpenLoadout, "끝난 뒤에는 조립 열기를 다시 띄우지 않는다");
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
            for (int n = 1; n <= 4; n++)
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
            w.Run.TutorialStep = 4;
            Assert.IsFalse(ctl.OpenLoadout(), "⑤ 전에는 열지 않는다");
            w.Run.TutorialStep = 5;
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
            w.Run.TutorialStep = 5;
            ctl.OpenLoadout();
            Assert.AreEqual(ScreenId.Camp, rig.Screens.Top);
            w.Tick = 900;
            rig.Session.Raise(new SimEvent { Type = SimEventType.TutorialStepDone, A = 5, Tick = 900, Slot = -1 });
            Assert.AreEqual(ScreenId.Tutorial, rig.Screens.Top, "Camp를 PopTo(Tutorial)로 닫는다");
            CollectionAssert.Contains(rig.Screens.PopToCalls, ScreenId.Tutorial);
            Assert.IsTrue(ctl.Running, "잠깐 뒤에 끝낸다");

            w.Tick = 900 + Delay;
            ctl.Tick();
            Assert.AreEqual(6, w.Commands.Count > 0 ? w.Commands[w.Commands.Count - 1].A : -1, "TutorialAdvance(A = 6 = 끝)");
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
            other.Run.TutorialStep = 1;
            rig.Session.Raise(other, new SimEvent { Type = SimEventType.TutorialStepDone, A = 1, Tick = 0 });
            w.Tick = Delay * 2;
            ctl.Tick();
            Assert.AreEqual(0, w.Commands.Count);
        }

        static StoryTuning Tuning()
        {
            var t = StoryTuning.Defaults();
            t.TutorialAdvanceDelayTicks = Delay;
            return t;
        }
    }
}

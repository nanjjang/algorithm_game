using System.IO;
using NUnit.Framework;
using Wizard.Solo.App;
using Wizard.Solo.Meta;
using Wizard.Solo.Save;
using Wizard.Solo.Screens.Start;
using Wizard.Solo.UI;

namespace Wizard.Solo.Tests.ScreensStart
{
    /// <summary>시작 흐름(08 요약서 2·3절, 00 문서 4절 10번·8.1절·D-68): 첫 실행, 타이틀, 프로필 고르기, 오프라인 안내.</summary>
    public class StartFlowTests
    {
        string _dir;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "solo-g18-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            try { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); } catch (IOException) { }
        }

        // TV-001·TV-003 + 3.1절: 설정 없음 → FirstRunPrompt → 고르면 바로 저장 → Title. 다음 부팅은 설정이 있어 Title.
        [Test]
        public void S13_TV_001_003__FirstRunFlowSavesThenTitle()
        {
            string file = Path.Combine(_dir, "settings.json");
            var store = new SettingsStore(file);
            store.Load();
            Assert.IsFalse(store.ExistedAtLoad);
            Assert.AreEqual(ScreenId.FirstRunPrompt, Bootstrap.FirstScreen(new BootResult { FirstRun = !store.ExistedAtLoad }));

            var nav = new FirstRunPromptModel().Choose(store, 2, ScreenArgs.From(ScreenId.None));
            Assert.AreEqual(StartNavKind.Replace, nav.Kind);
            Assert.AreEqual(ScreenId.Title, nav.Target);
            Assert.IsTrue(File.Exists(file));

            var again = new SettingsStore(file);
            again.Load();
            Assert.IsTrue(again.ExistedAtLoad);
            Assert.AreEqual(OptionValues.Minimal, SettingsOptions.Str(again.Current, OptionKeys.MotionPreset));
            Assert.AreEqual(ScreenId.Title, Bootstrap.FirstScreen(new BootResult { FirstRun = !again.ExistedAtLoad }));
        }

        // TV-002: Reduced 확정 → settings.motion_preset == reduced 저장(즉시, 한 번), 화면 = Title.
        [TestCase(0, "full")]
        [TestCase(1, "reduced")]
        [TestCase(2, "minimal")]
        public void S13_TV_002__ChoiceSavesMotionPreset(int level, string expected)
        {
            var store = new CountingSettingsStore();
            var model = new FirstRunPromptModel();
            var nav = model.Choose(store, level, null);
            Assert.AreEqual(expected, SettingsOptions.Str(store.Current, OptionKeys.MotionPreset));
            Assert.AreEqual(1, store.Saves);
            Assert.AreEqual(ScreenId.Title, nav.Target);
            // 확인 단계 없음 + 두 번 눌러도 한 번만.
            Assert.AreEqual(StartNavKind.None, model.Choose(store, 0, null).Kind);
            Assert.AreEqual(1, store.Saves);
            Assert.AreEqual(expected, SettingsOptions.Str(store.Current, OptionKeys.MotionPreset));
        }

        // 3.1절: 커서 기본 = 전체 [임시], Esc = 저장하지 않는다(다음 실행에 다시 묻는다).
        [Test]
        public void S13_3_1__DefaultCursorFullAndEscDoesNotSave()
        {
            Assert.AreEqual(0, FirstRunPromptModel.DefaultCursor);
            Assert.AreEqual("str.first_run_prompt.option.full", FirstRunPromptModel.OptionKeys[FirstRunPromptModel.DefaultCursor]);
            var store = new CountingSettingsStore();
            var nav = new FirstRunPromptModel().Skip(ScreenArgs.From(ScreenId.None));
            Assert.AreEqual(ScreenId.Title, nav.Target);
            Assert.AreEqual(0, store.Saves);
            Assert.IsFalse(store.Current.Options.ContainsKey(OptionKeys.MotionPreset));
        }

        // TV-028: 오프라인 첫 진입은 MainMenu 전에 OfflineGuide 한 번. 두 번째 진입에는 안 뜬다.
        [Test]
        public void S13_TV_028__OfflineGuideOnlyOnce()
        {
            var p = StartTestData.Profile();
            var profiles = new CountingProfileStore(p);
            var first = new TitleModel(new StartTuning()).Start(p, ScreenArgs.From(ScreenId.None));
            Assert.AreEqual(StartNavKind.Push, first.Kind);
            Assert.AreEqual(ScreenId.OfflineGuide, first.Target);

            var done = OfflineGuideModel.Confirm(profiles, first.Args);
            Assert.IsTrue(p.Flags.OfflineGuideSeen);
            Assert.AreEqual(1, profiles.Saves);
            Assert.AreEqual(StartNavKind.PopTo, done.Kind);
            Assert.AreEqual(ScreenId.MainMenu, done.Target);

            var second = new TitleModel(new StartTuning()).Start(p, ScreenArgs.From(ScreenId.None));
            Assert.AreEqual(StartNavKind.PopTo, second.Kind);
            Assert.AreEqual(ScreenId.MainMenu, second.Target);
        }

        // 3.4절: 메인 메뉴 묶음 상자에서 다시 보면 [확인] = 닫기(메인 메뉴로). 이미 본 플래그는 다시 저장하지 않는다.
        [Test]
        public void S13_3_4__RevisitFromMainMenuCloses()
        {
            var p = StartTestData.Profile();
            p.Flags.OfflineGuideSeen = true;
            var profiles = new CountingProfileStore(p);
            var nav = OfflineGuideModel.Confirm(profiles, ScreenArgs.From(ScreenId.MainMenu));
            Assert.AreEqual(StartNavKind.Close, nav.Kind);
            Assert.AreEqual(0, profiles.Saves);
        }

        // 3.4절: 표 — 되는 것 7, 안 되는 것 6, 세 줄(이름 표에 전부 있다).
        [Test]
        public void S13_3_4__GuideTableKeys()
        {
            Assert.AreEqual(3, OfflineGuideModel.LineKeys.Length);
            Assert.AreEqual(7, OfflineGuideModel.CanKeys.Length);
            Assert.AreEqual(6, OfflineGuideModel.CannotKeys.Length);
            foreach (var k in OfflineGuideModel.LineKeys) Assert.IsTrue(TestContent.Names.Has(k), k);
            foreach (var k in OfflineGuideModel.CanKeys) Assert.IsTrue(TestContent.Names.Has(k), k);
            foreach (var k in OfflineGuideModel.CannotKeys) Assert.IsTrue(TestContent.Names.Has(k), k);
            Assert.AreEqual("서버로 옮길 수 없다", TestContent.Names.Get(OfflineGuideModel.LineKeys[2]));
        }

        // 계약 벡터(00 8.1절·D-68): PendingSettled가 FirstRunPrompt → Title → OfflineGuide를 거쳐 MainMenu에 닿고, 알림은 한 번만.
        [Test]
        public void S00_D_68__PendingSettledReachesMainMenuOnce()
        {
            var pending = new SettlementResult { FromCheckpoint = true, GoldGained = 120 };
            var boot = Bootstrap.FirstScreenArgs(new BootResult { FirstRun = true, ProfileRestored = true, PendingSettled = pending });

            var toTitle = new FirstRunPromptModel().Choose(new CountingSettingsStore(), 0, boot);
            Assert.AreEqual(ScreenId.Title, toTitle.Target);
            Assert.AreSame(pending, StartFlow.Pending(toTitle.Args));
            Assert.IsTrue(StartFlow.Restored(toTitle.Args));

            var title = new TitleModel(new StartTuning());
            Assert.IsTrue(title.OnEnter(toTitle.Args), "SaveRestored 창을 띄운다");
            Assert.IsFalse(StartFlow.Restored(toTitle.Args), "한 번 쓰고 지운다");
            Assert.AreSame(pending, StartFlow.Pending(toTitle.Args), "PendingSettled는 남긴다");

            var p = StartTestData.Profile();
            var toGuide = title.Start(p, toTitle.Args);
            Assert.AreEqual(ScreenId.OfflineGuide, toGuide.Target);
            Assert.AreSame(pending, StartFlow.Pending(toGuide.Args));
            Assert.IsFalse(toGuide.Args.Values.ContainsKey(ScreenArgKeys.ProfileRestored));

            var toMenu = OfflineGuideModel.Confirm(new CountingProfileStore(p), toGuide.Args);
            Assert.AreEqual(ScreenId.MainMenu, toMenu.Target);
            Assert.IsInstanceOf<SettlementResult>(toMenu.Args.Values[ScreenArgKeys.PendingSettled]);
            Assert.AreSame(pending, StartFlow.Pending(toMenu.Args));

            Assert.AreSame(pending, StartFlow.TakePending(toMenu.Args));
            Assert.IsNull(StartFlow.TakePending(toMenu.Args), "알림은 한 번만");
        }

        // 계약 벡터: 설정이 있는 기기(FirstRunPrompt 없음)·안내를 이미 본 프로필이면 Title → MainMenu로 바로 넘어가도 PendingSettled가 간다.
        [Test]
        public void S00_D_68__PendingSettledTitleDirectToMainMenu()
        {
            var pending = new SettlementResult();
            var boot = Bootstrap.FirstScreenArgs(new BootResult { PendingSettled = pending });
            var title = new TitleModel(new StartTuning());
            Assert.IsFalse(title.OnEnter(boot));
            var p = StartTestData.Profile();
            p.Flags.OfflineGuideSeen = true;
            var nav = title.Start(p, boot);
            Assert.AreEqual(StartNavKind.PopTo, nav.Kind);
            Assert.AreEqual(ScreenId.MainMenu, nav.Target);
            Assert.AreSame(pending, StartFlow.Pending(nav.Args));
            Assert.AreEqual(StartNavKind.None, title.Start(p, boot).Kind, "J 두 번은 한 번만 넘어간다");
        }

        // 3.3절 + 계약 벡터: 프로필 고르기 — 오프라인 카드만 동작(PendingSettled를 넘긴다), 서버 카드는 꺼짐 + error.not_supported_offline.
        [Test]
        public void S13_3_3__ProfileSelectOfflineOnly()
        {
            var pending = new SettlementResult();
            var titleArgs = ScreenArgs.From(ScreenId.None).With(ScreenArgKeys.PendingSettled, pending);
            var toSelect = new TitleModel(new StartTuning()).ChangeProfile(titleArgs);
            Assert.AreEqual(StartNavKind.Push, toSelect.Kind);
            Assert.AreEqual(ScreenId.ProfileSelect, toSelect.Target);

            var p = StartTestData.Profile();
            Assert.IsTrue(ProfileSelectModel.CardEnabled(ProfileKind.Offline));
            Assert.IsFalse(ProfileSelectModel.CardEnabled(ProfileKind.Server));
            Assert.AreEqual("error.not_supported_offline", ProfileSelectModel.CardDisabledReason(ProfileKind.Server));
            Assert.IsNull(ProfileSelectModel.CardDisabledReason(ProfileKind.Offline));
            Assert.AreEqual(StartNavKind.None, ProfileSelectModel.Pick(ProfileKind.Server, p, toSelect.Args).Kind);

            var offline = ProfileSelectModel.Pick(ProfileKind.Offline, p, toSelect.Args);
            Assert.AreEqual(ScreenId.OfflineGuide, offline.Target);
            Assert.AreSame(pending, StartFlow.Pending(offline.Args));
            p.Flags.OfflineGuideSeen = true;
            Assert.AreEqual(ScreenId.MainMenu, ProfileSelectModel.Pick(ProfileKind.Offline, p, toSelect.Args).Target);
        }

        // 3.2절: 지금 프로필 표지는 항상 보인다(솔로 = str.title.profile.offline), 판 번호 "판 {version}".
        [Test]
        public void S13_3_2__TitleProfileBadgeAndVersion()
        {
            Assert.AreEqual("str.title.profile.offline", TitleModel.ProfileKey(ProfileKind.Offline));
            Assert.AreEqual("오프라인 프로필", TestContent.Names.Get(TitleModel.ProfileKey(ProfileKind.Offline)));
            Assert.AreEqual("판 0.1.0", TitleModel.BuildVersionText(TestContent.Names, "0.1.0"));
        }

        // 3.2절: 시작 안내는 2초(120 t) 주기로 밝기만 바뀐다(깜박이지 않는다 — 값이 이어진다).
        [Test]
        public void S13_3_2__StartHintPulseTwoSeconds()
        {
            var tuning = new StartTuning();
            Assert.AreEqual(2f, tuning.TitlePulseSeconds);
            Assert.AreEqual(1f, TitleModel.PulseBrightness(0f, 2f, 0.5f), 1e-5);
            Assert.AreEqual(0.5f, TitleModel.PulseBrightness(1f, 2f, 0.5f), 1e-5);
            Assert.AreEqual(1f, TitleModel.PulseBrightness(2f, 2f, 0.5f), 1e-5);
            var m = new TitleModel(tuning);
            m.OnEnter(null);
            int wraps = 0;
            float prev = 1f;
            const float Dt = 1f / 60f;
            for (int i = 0; i < 250; i++)
            {
                if (m.Advance(Dt, out float b)) wraps++;
                Assert.Less(System.Math.Abs(b - prev), 0.05f, "한 틱에 밝기가 튀지 않는다");
                prev = b;
            }
            Assert.AreEqual(2, wraps);
        }
    }
}

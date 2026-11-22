using System.Collections.Generic;
using NUnit.Framework;
using Wizard.Solo.Meta;
using Wizard.Solo.Screens.Start;
using Wizard.Solo.UI;

namespace Wizard.Solo.Tests.ScreensStart
{
    /// <summary>메인 메뉴 모델(08 요약서 4절, 13.2절, 19절 벡터; 00 문서 8.1절 인자 표, D-51).</summary>
    public class MainMenuModelTests
    {
        static List<MainMenuEntry> All(MainMenuModel m) => new List<MainMenuEntry>(m.All);

        // TV-005: 메인 메뉴에는 프로필 전환 수단이 없다(타이틀에서만).
        [Test]
        public void S13_TV_005__MainMenuHasNoProfileSwitch()
        {
            var m = MainMenuModel.Build(StartTestData.Profile(), TestContent.Db, false);
            foreach (var e in All(m))
            {
                Assert.AreNotEqual(ScreenId.ProfileSelect, e.Target, e.Id);
                Assert.AreNotEqual(ScreenId.Title, e.Target, e.Id);
                Assert.AreNotEqual(ScreenId.ProfileSelect, m.Open(e).Target, e.Id);
                Assert.AreNotEqual(ScreenId.Title, m.Open(e).Target, e.Id);
                Assert.IsFalse(e.NameKey.Contains("change_profile"), e.Id);
            }
        }

        // TV-133: 판(원정) 중에는 강화 화면을 열 수 없다(메뉴에 없음). 메뉴 목록을 검사한다.
        [Test]
        public void S13_TV_133__NoEnhanceEntryDuringRun()
        {
            var during = MainMenuModel.Build(StartTestData.Profile(), TestContent.Db, true);
            foreach (var e in All(during))
            {
                Assert.AreNotEqual(MainMenuAction.Enhance, e.Action, e.Id);
                var nav = during.Open(e);
                Assert.AreNotEqual(ScreenId.WandPicker, nav.Target, e.Id);
                Assert.AreNotEqual(ScreenId.Enhance, nav.Target, e.Id);
            }
            // 판 밖: 강화로 가는 길은 "강화" 줄 하나뿐이고 지팡이 고르기를 거친다(08 4.2절 1번).
            var idle = MainMenuModel.Build(StartTestData.Profile(), TestContent.Db, false);
            int toPicker = 0;
            foreach (var e in All(idle))
            {
                var nav = idle.Open(e);
                Assert.AreNotEqual(ScreenId.Enhance, nav.Target, e.Id);
                if (nav.Target == ScreenId.WandPicker) { toPicker++; Assert.AreEqual(MainMenuAction.Enhance, e.Action); }
            }
            Assert.AreEqual(1, toPicker);
        }

        // TV-237: 넘긴 최고 웨이브 14 → 한 줄 꼴 "복수 · 최고 웨이브 14 / 19".
        [Test]
        public void S13_TV_237__RevengeTrackerShortLine()
        {
            var p = StartTestData.Profile(3, 14, 7);
            p.Flags.PhantomMet = true;
            var m = MainMenuModel.Build(p, TestContent.Db, false);
            Assert.AreEqual("복수 · 최고 웨이브 14 / 19", m.TrackerLine(TestContent.Names));
            // 값은 RevengeTracker.Build의 것 그대로(화면이 따로 계산하지 않는다).
            var v = RevengeTracker.Build(p, TestContent.Db);
            Assert.AreEqual(v.Visible, m.Tracker.Visible);
            Assert.AreEqual(v.Progress, m.Tracker.Progress);
            Assert.AreEqual(v.ProgressMax, m.Tracker.ProgressMax);
        }

        // 13.2절: TrackerProgressNumerator = min(19, bestPassed).
        [Test]
        public void S13_13_2__TrackerProgressCappedAt19()
        {
            var p = StartTestData.Profile(25);
            p.Flags.PhantomMet = true;
            var m = MainMenuModel.Build(p, TestContent.Db, false);
            Assert.AreEqual("복수 · 최고 웨이브 19 / 19", m.TrackerLine(TestContent.Names));
        }

        // 계약 벡터: 만나기 전 프로필이면 추적기 줄 없음(첫 만남을 미리 알리지 않는다, 13.2절 NotMet).
        [Test]
        public void S13_13_2__NoTrackerLineBeforeMeeting()
        {
            var p = StartTestData.Profile(14);
            var m = MainMenuModel.Build(p, TestContent.Db, false);
            Assert.IsFalse(m.Tracker.Visible);
            Assert.IsNull(m.TrackerLine(TestContent.Names));
        }

        // 13.2절 Completed: 그 판 정산에서 한 번 바뀐 뒤 사라진다 — 메인 메뉴에는 없다.
        [Test]
        public void S13_13_2__NoTrackerLineAfterRevengeDone()
        {
            var p = StartTestData.Profile(22);
            p.Flags.PhantomMet = true;
            p.Flags.RevengeDoneAt = "2026-11-01";
            Assert.IsNull(MainMenuModel.Build(p, TestContent.Db, false).TrackerLine(TestContent.Names));
        }

        // 4.1절: 원정 카드 기록 줄 = "최고 웨이브 {n}", n = 전 캐릭터 최고.
        [Test]
        public void S13_4_1__BestWaveIsMaxOverCharacters()
        {
            var m = MainMenuModel.Build(StartTestData.Profile(3, 14, 7, 0, 9), TestContent.Db, false);
            Assert.AreEqual(14, m.BestWave);
            Assert.AreEqual("최고 웨이브 14", m.BestWaveLine(TestContent.Names));
            Assert.AreEqual("최고 웨이브 0", MainMenuModel.Build(StartTestData.Profile(), TestContent.Db, false).BestWaveLine(TestContent.Names));
        }

        // 4.2절 메뉴 목록: 솔로 7줄 순서, 서버 전용은 목록에 없다.
        [Test]
        public void S13_4_2__MenuListOrder()
        {
            var m = MainMenuModel.Build(StartTestData.Profile(), TestContent.Db, false);
            var ids = new List<string>();
            foreach (var e in m.Items) ids.Add(e.Id);
            CollectionAssert.AreEqual(new[] { "coop", "enhance", "assembly_library", "practice", "records", "wardrobe", "shop", "settings" }, ids); // 협동(11-online.md)이 맨 앞
            Assert.AreEqual("str.main_menu.item.exit", m.Exit.NameKey);
            Assert.AreEqual("str.main_menu.mode.expedition", m.Expedition.NameKey);
        }

        // TV-026: 결투·광장 카드와 우편함·친구·순위표·시즌·장터 줄이 묶음 상자 안. 흐리게 그리지 않고(enabled 그대로) "서버에 접속하면 열립니다" 한 줄.
        [Test]
        public void S13_TV_026__ServerOnlyGroup()
        {
            CollectionAssert.AreEquivalent(new[]
            {
                "str.main_menu.mode.duel", "str.main_menu.mode.plaza", "str.main_menu.item.mailbox", "str.main_menu.item.friends",
                "str.main_menu.item.leaderboard", "str.main_menu.item.season", "str.main_menu.item.market"
            }, MainMenuModel.ServerOnlyKeys);
            var m = MainMenuModel.Build(StartTestData.Profile(), TestContent.Db, false);
            Assert.IsTrue(m.ServerOnly.Enabled);
            Assert.AreEqual("str.main_menu.server_only", m.ServerOnly.NameKey);
            Assert.AreEqual("서버에 접속하면 열립니다", TestContent.Names.Get(m.ServerOnly.NameKey));
            foreach (var e in m.Items)
            {
                Assert.IsTrue(e.Enabled, e.Id);
                CollectionAssert.DoesNotContain(MainMenuModel.ServerOnlyKeys, e.NameKey);
            }
            // 묶음 상자 → OfflineGuide 다시 보기(Entry = MainMenu).
            var nav = m.Open(m.ServerOnly);
            Assert.AreEqual(StartNavKind.Push, nav.Kind);
            Assert.AreEqual(ScreenId.OfflineGuide, nav.Target);
            Assert.AreEqual(ScreenId.MainMenu, nav.Args.Entry);
            Assert.IsTrue(OfflineGuideModel.IsRevisit(nav.Args));
        }

        // D-51: 봇 상대 결투 연습 항목은 없다.
        [Test]
        public void S00_D_51__NoDuelPracticeEntry()
        {
            var m = MainMenuModel.Build(StartTestData.Profile(), TestContent.Db, false);
            foreach (var e in All(m))
            {
                Assert.IsFalse(e.Id.Contains("duel"), e.Id);
                if (e.Action != MainMenuAction.ServerOnly) Assert.IsFalse(e.NameKey.Contains("duel"), e.Id);
            }
            var practice = m.Open(m.Items.Find(e => e.Action == MainMenuAction.Practice));
            Assert.AreEqual(ScreenId.DummyPractice, practice.Target);
        }

        // 계약 벡터: 각 항목이 여는 화면과 인자(00 문서 8.1절 표의 열쇠·형).
        [Test]
        public void S00_8_1__EachEntryOpensScreenWithTypedArgs()
        {
            var p = StartTestData.Profile();
            p.LastCharacter = 3;
            var m = MainMenuModel.Build(p, TestContent.Db, false);
            StartNav Nav(MainMenuAction a) => m.Open(new List<MainMenuEntry>(m.All).Find(e => e.Action == a));

            var exp = Nav(MainMenuAction.Expedition);
            Assert.AreEqual(StartNavKind.Push, exp.Kind);
            Assert.AreEqual(ScreenId.ExpeditionPrep, exp.Target);
            Assert.AreEqual(ScreenId.MainMenu, exp.Args.Entry);

            var enh = Nav(MainMenuAction.Enhance);
            Assert.AreEqual(ScreenId.WandPicker, enh.Target);
            Assert.IsInstanceOf<int>(enh.Args.Values[ScreenArgKeys.Character]);
            Assert.AreEqual(3, enh.Args.Get(ScreenArgKeys.Character, 0));

            var lib = Nav(MainMenuAction.AssemblyLibrary);
            Assert.AreEqual(ScreenId.Camp, lib.Target);
            Assert.IsInstanceOf<CampMode>(lib.Args.Values[ScreenArgKeys.CampMode]);
            Assert.AreEqual(CampMode.Library, lib.Args.Get(ScreenArgKeys.CampMode, CampMode.Camp));

            var pr = Nav(MainMenuAction.Practice);
            Assert.AreEqual(ScreenId.DummyPractice, pr.Target);
            Assert.IsInstanceOf<PracticeEntry>(pr.Args.Values[ScreenArgKeys.PracticeEntry]);
            Assert.AreEqual(PracticeEntry.MainMenu, pr.Args.Get(ScreenArgKeys.PracticeEntry, PracticeEntry.Camp));

            Assert.AreEqual(ScreenId.Records, Nav(MainMenuAction.Records).Target);
            Assert.AreEqual(ScreenId.Wardrobe, Nav(MainMenuAction.Wardrobe).Target);
            Assert.AreEqual(ScreenId.Shop, Nav(MainMenuAction.Shop).Target);
            Assert.AreEqual(ScreenId.Settings, Nav(MainMenuAction.Settings).Target);
            Assert.AreEqual(ScreenId.OfflineGuide, Nav(MainMenuAction.ServerOnly).Target);
            Assert.AreEqual(StartNavKind.None, Nav(MainMenuAction.Exit).Kind);
            foreach (var a in new[] { MainMenuAction.Expedition, MainMenuAction.Records, MainMenuAction.Wardrobe, MainMenuAction.Shop, MainMenuAction.Settings })
                Assert.AreEqual(StartNavKind.Push, Nav(a).Kind, a.ToString());
        }

        // 4.1절 [임시] U-14: 06:00~16:59 낮, 17:00~19:59 해질녘, 20:00~05:59 밤.
        [TestCase(6, MenuTimeOfDay.Day)]
        [TestCase(16, MenuTimeOfDay.Day)]
        [TestCase(17, MenuTimeOfDay.Dusk)]
        [TestCase(19, MenuTimeOfDay.Dusk)]
        [TestCase(20, MenuTimeOfDay.Night)]
        [TestCase(0, MenuTimeOfDay.Night)]
        [TestCase(5, MenuTimeOfDay.Night)]
        public void S13_4_1__BackgroundTimeOfDay(int hour, MenuTimeOfDay expected)
        {
            Assert.AreEqual(expected, MainMenuModel.TimeOfDay(hour, new StartTuning()));
        }
    }
}

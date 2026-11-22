using System.Collections.Generic;
using NUnit.Framework;
using Wizard.Solo.Meta;
using Wizard.Solo.Screens.Camp;
using Wizard.Solo.Sim;
using Wizard.Solo.UI;

namespace Wizard.Solo.Tests.ScreensCamp
{
    /// <summary>비기 트라이(08 8.4절, 04 11절)와 허수아비 연습(08 8.5절, 04 6.1절)의 화면 규칙·세션 계약.</summary>
    public class ArtTrieAndPracticeTests
    {
        // ---------------------------------------------------------------- 트라이

        [Test]
        public void S04_U_R13__NewProfileUniqueNodeUnlockedWithStar()
        {
            // 새 프로필의 해금 = 마력 방출·반동 사격·고유 비기(TV-078). 실제 Arts 모듈의 DescribeTrie를 거친다.
            var rules = SimModuleSet.CreateDefault();
            var unlocked = new List<string> { Ids.Art.Burst, Ids.Art.Recoil, Ids.Art.Kiln };
            var m = ArtTrieModel.Build(TestContent.Db, rules.Arts, Ids.Char.C1, unlocked, null);
            var kiln = m.FindByArt(Ids.Art.Kiln);
            Assert.IsNotNull(kiln, "char.1 트라이에 불가마 마디");
            Assert.AreEqual(ArtNodeState.Unlocked, kiln.State);
            Assert.IsTrue(kiln.IsUnique, "★");
            Assert.AreEqual("str.art_trie.state.unlocked", kiln.StateKey);
            var fan = m.FindByArt(Ids.Art.Fan);
            Assert.AreEqual(ArtNodeState.Locked, fan.State, "공용 비기만 잠김");
            Assert.IsNull(m.FindByArt(Ids.Art.ShadowHand), "다른 캐릭터의 ★는 없다");
        }

        [Test]
        public void S08_TV_078__UniqueUnlockedOnFirstScreen_ViaProfileUnlocks()
        {
            var ctx = new GameContext { Content = TestContent.Db };
            var list = ArtTrieScreen.UnlockedFor(ctx, null, 4);
            CollectionAssert.Contains(list, Ids.Art.Burst);
            CollectionAssert.Contains(list, Ids.Art.Recoil);
            CollectionAssert.Contains(list, TestContent.Db.Character(Ids.Char.C4).UniqueArt);
        }

        [Test]
        public void S08_TV_079__OneCandidateShowsOneCard()
        {
            var arts = new CampFakeArts();
            var one = ArtTrieModel.Build(TestContent.Db, arts, Ids.Char.C1, new List<string>(), new List<string> { Ids.Art.Fan });
            Assert.AreEqual(1, one.PickCardCount);
            var two = ArtTrieModel.Build(TestContent.Db, arts, Ids.Char.C1, new List<string>(), new List<string> { Ids.Art.Fan, Ids.Art.Wings });
            Assert.AreEqual(2, two.PickCardCount);
            Assert.AreEqual("새 비기를 고른다", TestContent.Names.Get("str.art_trie.unlock_pick"));
        }

        [Test]
        public void S08_TV_080__DefaultGapHidden_018Shown()
        {
            Assert.IsNull(ArtTrieModel.GapText(new ArtTrieNodeView { GapSeconds = 0.3f, GapIsDefault = true }));
            string s = ArtTrieModel.GapText(new ArtTrieNodeView { GapSeconds = 0.18f, GapIsDefault = false });
            Assert.AreEqual("0.18", s);
            Assert.AreEqual("0.18초", TestContent.Names.Format("str.art_trie.interval", "sec", s));
            Assert.AreEqual("0.2", ArtTrieModel.GapText(new ArtTrieNodeView { GapSeconds = 0.2f }), "04 11.4절 \"0.2초\"");
            Assert.AreEqual("0.25", ArtTrieModel.GapText(new ArtTrieNodeView { GapSeconds = 0.25f }));
        }

        [Test]
        public void S08_8_4__StatesAndCurrentNodeFromRecentInputs()
        {
            var arts = new CampFakeArts();
            arts.Views.Add(new ArtTrieNodeView { NodeId = 0, ParentId = -1, Depth = 0, Path = new CmdDir[0], ArtId = Ids.Art.Burst, State = ArtNodeState.Unlocked, GapIsDefault = true });
            arts.Views.Add(new ArtTrieNodeView { NodeId = 1, ParentId = 0, Depth = 1, Path = new[] { CmdDir.Forward }, ArtId = Ids.Art.Fan, State = ArtNodeState.Candidate, GapIsDefault = true });
            arts.Views.Add(new ArtTrieNodeView { NodeId = 2, ParentId = 1, Depth = 2, Path = new[] { CmdDir.Forward, CmdDir.Forward }, ArtId = Ids.Art.Spear, JustArtId = Ids.Art.ThunderSpear, State = ArtNodeState.Locked, GapIsDefault = true });
            var m = ArtTrieModel.Build(TestContent.Db, arts, Ids.Char.C1, new List<string>(), new List<string> { Ids.Art.Fan });
            Assert.AreEqual("str.art_trie.state.candidate", m.Nodes[1].StateKey);
            Assert.AreEqual("str.art_trie.state.locked", m.Nodes[2].StateKey);
            Assert.AreEqual(ArtTrieModel.ColumnStep * 2, m.Nodes[2].X, "열 = 깊이(간격 176)");
            Assert.AreEqual("str.art_trie.dir.forward", m.Nodes[2].EdgeGlyphKey);
            var recent = new List<RecentInput> { new RecentInput { Dir = CmdDir.Back }, new RecentInput { Dir = CmdDir.Forward, Chained = false }, new RecentInput { Dir = CmdDir.Forward, Chained = true } };
            m.MarkCurrent(recent);
            Assert.AreEqual(2, m.CurrentNodeId, "이어진 F,F → 꿰뚫는 창 마디");
            Assert.AreEqual(ArtNodeState.Current, m.Nodes[2].State);
            var d = ArtTrieModel.Describe(TestContent.Db, m.Nodes[2]);
            CollectionAssert.AreEqual(new[] { "str.art_trie.dir.forward", "str.art_trie.dir.forward", "str.art_trie.dir.k" }, d.CommandGlyphKeys);
            Assert.AreEqual(Ids.Art.ThunderSpear, d.JustArtId);
            Assert.LessOrEqual(d.EffectLines.Count, 2, "효과 두 줄");
        }

        // ---------------------------------------------------------------- 연습: 문구·막대·게이지

        [Test]
        public void S04_6_1__LateSecondsRoundUpToHundredths()
        {
            Assert.AreEqual(0.01, CampFormat.LateSecondsFromUnits(153), 1e-9);
            Assert.AreEqual(0.02, CampFormat.LateSecondsFromUnits(307), 1e-9);
            Assert.AreEqual(0.07, CampFormat.LateSecondsFromUnits(1075), 1e-9);
            Assert.AreEqual(0.01, CampFormat.LateSecondsFromUnits(1), 1e-9);
        }

        [Test]
        public void S08_TV_081__LateMessagesTwoDecimals()
        {
            var e = new SimEvent { Type = SimEventType.ArtFizzled, A = (int)ArtFizzleReason.FullBloomLate, B = 3, F = 0.02f };
            var line = PracticeModel.FromFizzle(e);
            Assert.AreEqual("str.dummy_practice.fail.late_input", line.Key);
            Assert.AreEqual("3번째 입력이 0.02초 늦음", TestContent.Names.Format(line.Key, "n", line.Index, "sec", line.Seconds));

            // K가 0.03초 늦음: 초과 400u → ceil(400 × 100 / 15360) / 100 = 0.03.
            long lateU = PracticeModel.LateKUnits(1000 + 1075 + 400, 1000, 1075);
            Assert.AreEqual(400, lateU);
            var k = PracticeModel.LateK(lateU);
            Assert.AreEqual("K가 0.03초 늦음", TestContent.Names.Format(k.Key, "sec", k.Seconds));

            var nc = PracticeModel.FromFizzle(new SimEvent { Type = SimEventType.ArtFizzled, A = (int)ArtFizzleReason.NoCrystal });
            Assert.AreEqual("결정 부족", TestContent.Names.Get(nc.Key));
        }

        [Test]
        public void S00_G21_C11__GapBarRatioIsGapOverAllowed()
        {
            var recent = new List<RecentInput>
            {
                new RecentInput { Dir = CmdDir.Forward, GapU = 0, AllowedGapU = 0 },
                new RecentInput { Dir = CmdDir.Back, GapU = 2304, AllowedGapU = 4608, Chained = true },
                new RecentInput { Dir = CmdDir.Back, GapU = 3000, AllowedGapU = 2765 }
            };
            var bars = new List<GapBar>();
            PracticeModel.GapBars(recent, bars);
            Assert.AreEqual(2, bars.Count, "첫 입력은 막대 없음");
            Assert.AreEqual(0.5f, bars[0].MarkRatio, 1e-6f);
            Assert.IsFalse(bars[0].Over);
            Assert.AreEqual(3000f / 2765f, bars[1].MarkRatio, 1e-6f);
            Assert.IsTrue(bars[1].Over, "넘으면 막대 밖(호박색)");
        }

        [Test]
        public void S00_G21_C12__JustWindowBoundary1075u()
        {
            long window = SimConst.Units(0.07);
            Assert.AreEqual(1075, window);
            Assert.IsTrue(PracticeModel.InJustWindow(5000 + 1075, 5000, window), "1,075u는 창 안");
            Assert.IsFalse(PracticeModel.InJustWindow(5000 + 1076, 5000, window), "1,076u는 밖");
            Assert.AreEqual(0, PracticeModel.LateKUnits(5000 + 1075, 5000, window));
            Assert.AreEqual(1, PracticeModel.LateKUnits(5000 + 1076, 5000, window));
            Assert.AreEqual(0f, PracticeModel.JustGauge(5000, 5000, window));
            Assert.AreEqual(0.5f, PracticeModel.JustGauge(5000 + 537, 5000, 1074), 1e-3f);
            Assert.AreEqual(1f, PracticeModel.JustGauge(5000 + 1075, 5000, window));
            Assert.AreEqual(0f, PracticeModel.JustGauge(5000, -1, window), "입력이 없으면 0");
        }

        [Test]
        public void S00_G21_C13__OptionCommandsAAndB_NoDuelOption()
        {
            var on = PracticeModel.OptionCommand(PracticeOption.EnemyFire, true);
            Assert.AreEqual(RunCommandKind.PracticeSetOption, on.Kind);
            Assert.AreEqual((int)PracticeOption.EnemyFire, on.A);
            Assert.AreEqual(1, on.B);
            var off = PracticeModel.OptionCommand(PracticeOption.HeatFreeze, false);
            Assert.AreEqual((int)PracticeOption.HeatFreeze, off.A);
            Assert.AreEqual(0, off.B);
            Assert.AreEqual(3, PracticeModel.Options.Length, "옵션 셋");
            foreach (var o in PracticeModel.Options) StringAssert.DoesNotContain("duel", PracticeModel.OptionKey(o), "결투 입력 지연 체험 없음(D-51)");
            Assert.AreEqual(RunCommandKind.PracticeReset, PracticeModel.ResetCommand().Kind);
            var run = new RunState { PracticeEnemyFire = true };
            Assert.IsTrue(PracticeModel.OptionValue(run, PracticeOption.EnemyFire));
            Assert.IsFalse(PracticeModel.OptionValue(run, PracticeOption.InfiniteCrystal));
        }

        [Test]
        public void S08_8_5__TargetProgressAndReport()
        {
            var target = PracticeModel.TargetPath(TestContent.Db, Ids.Art.Spear);
            CollectionAssert.AreEqual(new[] { CmdDir.Forward, CmdDir.Forward }, target);
            var recent = new List<RecentInput> { new RecentInput { Dir = CmdDir.Forward } };
            Assert.AreEqual(1, PracticeModel.TargetProgress(target, recent));
            recent.Add(new RecentInput { Dir = CmdDir.Forward, Chained = true });
            Assert.AreEqual(2, PracticeModel.TargetProgress(target, recent));
            recent.Add(new RecentInput { Dir = CmdDir.Back, Chained = false });
            Assert.AreEqual(0, PracticeModel.TargetProgress(target, recent), "끊긴 뒤에는 처음부터");

            var st = new PracticeStats();
            st.Apply(new SimEvent { Type = SimEventType.PracticeReport, Id = PracticeReportKeys.Dps, F = 44.9f });
            st.Apply(new SimEvent { Type = SimEventType.PracticeReport, Id = PracticeReportKeys.HeatRatio, F = 0.5f });
            Assert.AreEqual(44.9f, st.Dps);
            Assert.AreEqual(0.5f, st.HeatRatio);
        }

        // ---------------------------------------------------------------- 연습: 세션 계약

        [Test]
        public void S08_TV_082__MainMenuEntryStartsAndEndsRun()
        {
            var s = new CampFakeSession { };
            s.InputScreen = ScreenId.None;
            s.Calls.Clear();
            var wands = new CampFakeWands();
            var ctx = CampTestKit.Ctx(s, wands);
            var ps = new PracticeSession(ctx);
            Assert.IsTrue(ps.Enter(PracticeEntry.MainMenu, 0, 5));
            Assert.AreEqual(2, ps.Character, "인자가 없으면 profile.last_character(D-56)");
            Assert.AreEqual(12, wands.LastWandId, "그 캐릭터의 든 지팡이");
            Assert.AreEqual(RunMode.Practice, wands.LastMode);
            Assert.IsNull(wands.LastOverride, "메인 메뉴는 출발 조립");
            Assert.AreEqual(0, wands.RunConfigCalls, "BuildRunConfig를 쓰지 않는다");
            CollectionAssert.AreEqual(new[] { "input " + ScreenId.DummyPractice, "start" }, s.Calls);
            ps.Exit();
            CollectionAssert.AreEqual(new[] { "input " + ScreenId.DummyPractice, "start", "end", "input " + ScreenId.None }, s.Calls);
        }

        [Test]
        public void S08_TV_082__CampEntryPushesSubRunWithCurrentLoadout()
        {
            var s = new CampFakeSession();
            s.Main = CampTestKit.World();
            s.Main.P0.Loadout.Links[1] = new SlotItem { BlockId = Ids.Block.Fireball, WildUid = 4 };
            s.InputScreen = ScreenId.Battle;
            s.Calls.Clear();
            var wands = new CampFakeWands();
            var ps = new PracticeSession(CampTestKit.Ctx(s, wands));
            Assert.IsTrue(ps.Enter(PracticeEntry.Camp, 3, 9));
            Assert.AreEqual(1, ps.Character, "야영은 인자가 아니라 지금 판의 캐릭터");
            Assert.AreEqual(11, wands.LastWandId);
            Assert.IsNotNull(wands.LastOverride, "지금 판의 조립");
            Assert.AreEqual(Ids.Block.MagicBolt, wands.LastOverride.Core);
            CollectionAssert.AreEqual(new string[] { null, Ids.Block.Fireball, null, null }, wands.LastOverride.Links, "야생 블록 포함");
            Assert.AreEqual(0, wands.RunConfigCalls);
            CollectionAssert.AreEqual(new[] { "input " + ScreenId.DummyPractice, "push" }, s.Calls);
            Assert.IsTrue(s.HasSubRun);
            ps.Exit();
            CollectionAssert.AreEqual(new[] { "input " + ScreenId.DummyPractice, "push", "pop", "input " + ScreenId.Battle }, s.Calls, "InputScreen 되돌리기");
        }

        [Test]
        public void S00_G21_C14__CoreEmptyDoesNotOpenRoom()
        {
            var s = new CampFakeSession();
            var wands = new CampFakeWands { PracticeError = ErrorCode.CoreEmpty };
            var ps = new PracticeSession(CampTestKit.Ctx(s, wands));
            Assert.IsFalse(ps.Enter(PracticeEntry.MainMenu, 1, 1));
            Assert.AreEqual("error.core_empty", ps.ErrorKey);
            Assert.IsFalse(s.Calls.Contains("start"), "방을 열지 않는다");
            ps.Exit();
            Assert.IsFalse(s.Calls.Contains("end"));
            Assert.AreEqual(ScreenId.None, s.InputScreen);
        }

        [Test]
        public void S00_G21_C15__QeSwitchCharacterEndsThenRebuilds()
        {
            var s = new CampFakeSession();
            var wands = new CampFakeWands();
            var ps = new PracticeSession(CampTestKit.Ctx(s, wands));
            ps.Enter(PracticeEntry.MainMenu, 5, 1);
            s.Calls.Clear();
            Assert.IsTrue(ps.SwitchCharacter(1, 2));
            Assert.AreEqual(1, ps.Character, "5 → 1로 돈다");
            Assert.AreEqual(11, wands.LastWandId);
            CollectionAssert.AreEqual(new[] { "end", "start" }, s.Calls);
            ps.SwitchCharacter(-1, 3);
            Assert.AreEqual(5, ps.Character);
        }
    }
}

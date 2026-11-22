using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.Screens.Camp;
using Wizard.Solo.Sim;
using Wizard.Solo.UI;

namespace Wizard.Solo.Tests.ScreensCamp
{
    /// <summary>
    /// 화면 조립 연기 시험(Unity EditMode에서만 — GameObject가 필요하다). 그림 창고 없이(ctx.Art = null) 네 화면을 만들고
    /// 모드마다 들어가 키를 넣어 본다. 조작은 명령으로만 나가는지, 연습 화면이 세션을 열고 닫는지 본다.
    /// </summary>
    public class CampScreenSmokeTests
    {
        GameObject _root;

        [SetUp] public void SetUp() { _root = new GameObject("SmokeRoot", typeof(RectTransform)); }
        [TearDown] public void TearDown() { if (_root != null) Object.DestroyImmediate(_root); }

        RectTransform NewRoot()
        {
            var go = new GameObject("Screen", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(_root.transform, false);
            rt.sizeDelta = new Vector2(UiScale.RefWidth, UiScale.RefHeight);
            return rt;
        }

        [Test]
        public void S00_G21_C16__CampScreenBuildsInAllFourModes_NoEnhanceButton()
        {
            var s = new CampFakeSession { Main = CampTestKit.World() };
            s.Main.Run.NextPlan = new WavePlan { Wave = 4, Tod = Ids.Tod.Day };
            var ctx = CampTestKit.Ctx(s, new CampFakeWands());
            var screen = new CampScreen();
            screen.Build(ctx, NewRoot());
            foreach (var mode in new[] { CampMode.Camp, CampMode.Library, CampMode.LoadoutEdit, CampMode.Tutorial })
            {
                var args = ScreenArgs.From(ScreenId.MainMenu).With(ScreenArgKeys.CampMode, mode).With(ScreenArgKeys.WandId, 11L);
                screen.OnEnter(args);
                screen.Tick(0.016f);
                screen.OnMenuKey(MenuKey.Right);
                screen.OnMenuKey(MenuKey.NextTab);
                screen.Tick(0.016f);
                foreach (var label in _root.GetComponentsInChildren<UiLabel>(true))
                    StringAssert.DoesNotContain("str.enhance", label.FullText ?? string.Empty, mode + ": 강화 글자");
                screen.OnExit();
            }
            // 야영 모드의 Esc = CampClose 명령(화면을 닫는 것은 Battle).
            screen.OnEnter(ScreenArgs.From(ScreenId.Battle).With(ScreenArgKeys.CampMode, CampMode.Camp));
            s.Main.Commands.Clear();
            Assert.IsTrue(screen.OnBack());
            Assert.AreEqual(RunCommandKind.CampClose, s.Main.Commands[0].Kind);
        }

        [Test]
        public void S00_G21_C17__LootPickScreenSendsCommandsOnly()
        {
            var s = new CampFakeSession { Main = CampTestKit.World() };
            s.Main.Run.Phase = RunPhase.LootPick;
            s.Main.Run.Purse = 100;
            s.Main.Run.Loot = new LootOffer { Wave = 2, PickCount = 2 };
            for (int i = 0; i < 4; i++) s.Main.Run.Loot.Cards.Add(new LootCard { BlockId = Ids.Block.Frost, Rarity = Ids.Rarity.Common });
            var ctx = CampTestKit.Ctx(s, new CampFakeWands());
            var screen = new LootPickScreen();
            screen.Build(ctx, NewRoot());
            screen.OnEnter(ScreenArgs.From(ScreenId.Battle));
            for (int i = 0; i < 30; i++) screen.Tick(SimConst.TickSeconds);
            screen.OnMenuKey(MenuKey.Confirm);
            Assert.AreEqual(1, s.Main.Commands.Count);
            Assert.AreEqual(RunCommandKind.LootPick, s.Main.Commands[0].Kind);
            Assert.IsFalse(s.Main.Run.Loot.Cards[0].Picked, "상태는 시뮬레이션이 바꾼다");
            Assert.IsTrue(screen.OnBack(), "Esc로 건너뛰지 않는다");
        }

        [Test]
        public void S00_G21_C18__DummyPracticeScreenOpensAndClosesSession()
        {
            var s = new CampFakeSession();
            var wands = new CampFakeWands();
            var ctx = CampTestKit.Ctx(s, wands);
            var screen = new DummyPracticeScreen();
            screen.Build(ctx, NewRoot());
            screen.OnEnter(ScreenArgs.From(ScreenId.MainMenu).With(ScreenArgKeys.PracticeEntry, PracticeEntry.MainMenu).With(ScreenArgKeys.Character, 3));
            Assert.AreEqual(ScreenId.DummyPractice, s.InputScreen);
            Assert.AreEqual(3, wands.LastCharacter);
            screen.Tick(0.016f);
            Assert.IsTrue(screen.OnMenuKey(MenuKey.Confirm), "J는 전투 키 — 메뉴로 쓰지 않고 삼킨다");
            screen.OnExit();
            CollectionAssert.Contains(s.Calls, "end");
            Assert.AreEqual(0, wands.RunConfigCalls);
        }

        [Test]
        public void S00_G21_C19__ArtTrieScreenBuildsOutOfRun()
        {
            var ctx = CampTestKit.Ctx(new CampFakeSession(), new CampFakeWands());
            ctx.Rules = SimModuleSet.CreateDefault();
            var screen = new ArtTrieScreen();
            screen.Build(ctx, NewRoot());
            screen.OnEnter(ScreenArgs.From(ScreenId.MainMenu).With(ScreenArgKeys.Character, 1));
            screen.Tick(0.016f);
            screen.OnMenuKey(MenuKey.Down);
            screen.OnExit();
        }
    }
}

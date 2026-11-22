using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.Meta;
using Wizard.Solo.Screens.Meta;
using Wizard.Solo.Screens.Settings;
using Wizard.Solo.UI;

namespace Wizard.Solo.Tests.ScreensMeta
{
    /// <summary>
    /// 화면을 실제로 조립해(캔버스 없이 GameObject만, 이름 표는 열쇠를 그대로 돌려준다) 그려지는 글자를 본다:
    /// 유료 요소·꾸미기 탭·결투 전적·[다시 보기]가 트리에 없다, 설정 줄 수가 표와 같다, 계정의 저장 위치 글자, 등록.
    /// </summary>
    public class ScreenTreeTests
    {
        GameObject _root;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("G24TestRoot", typeof(RectTransform));
            ((RectTransform)_root.transform).sizeDelta = new Vector2(1920f, 1080f);
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
        }

        RectTransform Root => (RectTransform)_root.transform;

        static GameContext Ctx(FakeShop shop = null, FakeProfileStore profiles = null, CountingSettingsStore settings = null) =>
            new GameContext
            {
                Names = new KeyNames(), Shop = shop ?? new FakeShop(), Profiles = profiles ?? new FakeProfileStore { Current = MetaProfiles.Five() },
                Settings = settings ?? new CountingSettingsStore(), Screens = new RecordingScreens(), Content = TestContent.Db
            };

        [Test]
        public void S07_TV_PAY_03__ShopTreeHasNoPaidElementsOrCosmeticTab()
        {
            var shop = new FakeShop();
            shop.State.Gold = 12345;
            var s = new ShopScreen();
            s.Build(Ctx(shop), Root);
            s.OnEnter(ScreenArgs.From(ScreenId.MainMenu));
            var texts = TreeText.All(_root);
            Assert.IsTrue(TreeText.AnyContains(texts, "str.shop.tab.charm"));
            Assert.IsTrue(TreeText.AnyContains(texts, "str.shop.tab.wand"));
            Assert.IsTrue(TreeText.AnyContains(texts, "str.shop.tab.other"));
            foreach (var bad in new[] { "starlight", "paid", "str.shop.kind.season", "change_nickname", "cosmetic", "payment" })
                Assert.IsFalse(TreeText.AnyContains(texts, bad), bad);
            var tabs = _root.GetComponentsInChildren<UiTabs>(true);
            Assert.AreEqual(1, tabs.Length);
            Assert.AreEqual(3, tabs[0].Buttons.Count, "부적 / 지팡이 / 그 밖(꾸미기·유료 탭 없음)");
            Assert.IsTrue(TreeText.AnyContains(texts, "str.format.gold_amount|n=12345"), "골드 잔액만(유료 잔액 없음)");
        }

        [Test]
        public void S08_TV_232__ShopWandTabCardsFollowGold()
        {
            var shop = new FakeShop();
            shop.State.Gold = 499;
            var s = new ShopScreen();
            s.Build(Ctx(shop), Root);
            s.OnEnter(null);
            s.OnMenuKey(MenuKey.NextTab);
            var cards = new List<UiButton>();
            foreach (var b in _root.GetComponentsInChildren<UiButton>(true)) if (b.name.StartsWith("Card.item.wand_box")) cards.Add(b);
            Assert.AreEqual(5, cards.Count);
            foreach (var c in cards) Assert.IsFalse(c.Enabled);
        }

        [Test]
        public void S08_N_10_N_11__RecordsTreeHasNoDuelSeasonOrReplay()
        {
            var profiles = new FakeProfileStore { Current = MetaProfiles.Five() };
            profiles.Current.RunHistory.Add(MetaProfiles.Run("r1", 1, 7));
            profiles.Current.RunHistory.Add(MetaProfiles.Run("r2", 2, 23));
            var s = new RecordsScreen();
            s.Build(Ctx(null, profiles), Root);
            s.OnEnter(null);
            var texts = TreeText.All(_root);
            Assert.IsFalse(TreeText.AnyContains(texts, "str.records.duel_record"));
            Assert.IsFalse(TreeText.AnyContains(texts, "str.records.season_result"));
            Assert.IsFalse(TreeText.AnyContains(texts, "str.records.replay"));
            Assert.IsTrue(TreeText.AnyContains(texts, "str.records.titles"));
            var list = _root.GetComponentInChildren<UiScrollList>(true);
            Assert.AreEqual(2, list.Rows.Count);
            Assert.AreEqual("Run0", list.Rows[0].name);
            Assert.IsTrue(TreeText.AnyContains(texts, "str.format.day_wave|day=3|wave=23"), "최신(r2)이 위");
        }

        [Test]
        public void S08_TV_167__RecordsTreeHasNoRevengeBeforeFirstMeeting()
        {
            var s = new RecordsScreen();
            s.Build(Ctx(), Root);
            s.OnEnter(null);
            var texts = TreeText.All(_root);
            Assert.IsFalse(TreeText.AnyContains(texts, "revenge.tracker"));
        }

        [Test]
        public void S08_12_2__SettingsRowCountsMatchModelForEveryCategory()
        {
            var s = new SettingsScreen();
            var profiles = new FakeProfileStore { Current = MetaProfiles.Five(), Dir = "E:/saves/profiles/offline" };
            s.Build(Ctx(null, profiles), Root);
            s.OnEnter(null);
            var list = _root.GetComponentInChildren<UiScrollList>(true);
            for (int c = 0; c < SettingsModel.Categories.Length; c++)
            {
                if (c > 0) s.OnMenuKey(MenuKey.NextTab);
                Assert.AreEqual(SettingsModel.Rows(SettingsModel.Categories[c]).Count, list.Rows.Count, SettingsModel.Categories[c]);
            }
            // 마지막 분류 = 계정(오프라인): 저장 파일 위치 글자.
            var texts = TreeText.All(list.gameObject);
            Assert.IsTrue(TreeText.AnyContains(texts, "E:/saves/profiles/offline"));
            Assert.IsTrue(TreeText.AnyContains(texts, "str.settings.account.restore_save"));
        }

        [Test]
        public void S08_12_1__SettingsChangeThroughWidgetSavesOnce()
        {
            var settings = new CountingSettingsStore();
            var s = new SettingsScreen();
            s.Build(Ctx(null, null, settings), Root);
            s.OnEnter(null);
            // 첫 줄 = window_mode(고르기). 오른쪽 = 다음 값.
            Assert.IsTrue(s.OnMenuKey(MenuKey.Right));
            Assert.AreEqual(1, settings.Saves);
            Assert.AreEqual(1, settings.ChangedCount);
            Assert.AreEqual(OptionValues.Windowed, SettingsOptions.Str(settings.Current, OptionKeys.WindowMode));
        }

        [Test]
        public void S08_12_3__KeyRebindTreeShowsNineRowsAndFixedEsc()
        {
            var settings = new CountingSettingsStore();
            KeyLayouts.Apply(settings.Current, OptionValues.Default);
            var s = new KeyRebindScreen();
            s.Build(Ctx(null, null, settings), Root);
            s.OnEnter(null);
            var texts = TreeText.All(_root);
            foreach (var k in KeyRebindModel.DisplayOrder) Assert.IsTrue(TreeText.AnyContains(texts, KeyRebindModel.ActionNameKey(k)), k.ToString());
            Assert.IsTrue(TreeText.AnyContains(texts, "str.key_rebind.korean_note"));
            // 이름 표에 키 이름이 없으면 UiKeyLabels가 Key 이름을 그대로 쓴다.
            Assert.IsTrue(TreeText.AnyContains(texts, UnityEngine.InputSystem.Key.Escape.ToString()), "일시정지 보조 칸 = Esc 고정");
        }

        [Test]
        public void S00_2_1__MetaAndSettingsScreensAreRegistered()
        {
            var m = new RecordingScreens();
            ScreenRegistry.RegisterAll(m, new GameContext());
            foreach (var id in new[] { ScreenId.Records, ScreenId.TitlePicker, ScreenId.Wardrobe, ScreenId.Shop, ScreenId.ShopBuyConfirm,
                ScreenId.Settings, ScreenId.KeyRebind })
            {
                Assert.IsTrue(m.Factories.ContainsKey(id), id.ToString());
                Assert.AreEqual(id, m.Factories[id]().Id);
            }
        }
    }
}

using NUnit.Framework;
using Wizard.Solo.Meta;
using Wizard.Solo.Screens.Meta;
using Wizard.Solo.UI;

namespace Wizard.Solo.Tests.ScreensMeta
{
    /// <summary>골드 상점 화면 모델(08 11.3절, 07 8절·12.10·12.14절). 상점 서비스는 가짜(ctx.Shop 자리).</summary>
    public class ShopModelTests
    {
        static ShopCard WandCard(ShopState st, int ch)
        {
            foreach (var c in ShopModel.Cards(ShopTab.Wand, st, MetaProfiles.Five())) if (c.Character == ch) return c;
            return null;
        }

        [Test]
        public void S08_TV_232__WandCardGold499OffAnd500On()
        {
            var shop = new FakeShop();
            shop.State.Gold = 499;
            var c = WandCard(shop.GetState(), 3);
            Assert.IsFalse(c.Enabled);
            Assert.AreEqual("error.not_enough_gold", c.DisabledReason);
            shop.State.Gold = 500;
            Assert.IsTrue(WandCard(shop.GetState(), 3).Enabled);
            Assert.AreEqual(5, ShopModel.Cards(ShopTab.Wand, shop.GetState(), MetaProfiles.Five()).Count, "캐릭터마다 한 장");
        }

        [Test]
        public void S08_TV_232__WandCardIgnoresHowManyWandsYouHave()
        {
            var shop = new FakeShop();
            shop.State.Gold = 500;
            var p = MetaProfiles.Five();
            for (int i = 0; i < 1000; i++) p.Wands.Add(new WandData { Id = 100 + i, Character = 3 });
            ShopCard card = null;
            foreach (var c in ShopModel.Cards(ShopTab.Wand, shop.GetState(), p)) if (c.Character == 3) card = c;
            Assert.IsTrue(card.Enabled);
            Assert.AreEqual(1001, card.WandCount);
            Assert.AreEqual("지금 1001자루", TestContent.Names.Format(MetaKeys.ShopWandCount, "n", card.WandCount));
        }

        [Test]
        public void S08_TV_233__CharmSoldOutAfterThreeThisWeek()
        {
            var shop = new FakeShop();
            shop.State.Gold = 100000;
            shop.State.CharmBoughtThisWeek = 3;
            var cards = ShopModel.Cards(ShopTab.Charm, shop.GetState(), MetaProfiles.Five());
            Assert.AreEqual(1, cards.Count);
            var c = cards[0];
            Assert.IsFalse(c.Enabled);
            Assert.IsTrue(c.SoldOut);
            Assert.AreEqual("str.shop.sold_out", c.DisabledReason);
            Assert.AreEqual("이번 주 3 / 3", TestContent.Names.Format(MetaKeys.ShopWeeklyLimit, "n", c.Bought, "max", c.Limit));
        }

        [Test]
        public void S08_11_3__WeeklyLimitShownFromShopState()
        {
            var shop = new FakeShop();
            shop.State.Gold = 10000;
            shop.State.CharmBoughtThisWeek = 1;
            var c = ShopModel.Cards(ShopTab.Charm, shop.GetState(), null)[0];
            Assert.IsTrue(c.Enabled);
            Assert.IsTrue(c.ShowWeeklyLimit);
            Assert.AreEqual(1, c.Bought);
            Assert.AreEqual(3, c.Limit);
            Assert.AreEqual(10000, c.Price);
            Assert.AreEqual("이번 주 1 / 3", TestContent.Names.Format(MetaKeys.ShopWeeklyLimit, "n", c.Bought, "max", c.Limit));
            shop.State.Gold = 9999;
            Assert.AreEqual("error.not_enough_gold", ShopModel.Cards(ShopTab.Charm, shop.GetState(), null)[0].DisabledReason);
        }

        [Test]
        public void S08_11_3__LibraryExpansionCard()
        {
            var shop = new FakeShop();
            shop.State.Gold = 2000;
            var c = ShopModel.Cards(ShopTab.Other, shop.GetState(), null)[0];
            Assert.AreEqual(Wizard.Solo.Ids.Item.LibraryExpansion, c.Sku);
            Assert.AreEqual(2000, c.Price);
            Assert.AreEqual(50, c.LibraryStep);
            Assert.IsTrue(c.Enabled);
        }

        [Test]
        public void S08_TV_198__OnlyGoldTabsNoPaidNoCosmeticTab()
        {
            CollectionAssert.AreEqual(new[] { "str.shop.tab.charm", "str.shop.tab.wand", "str.shop.tab.other" }, ShopModel.TabKeys());
        }

        [Test]
        public void S00_8_1__ShopBuyConfirmArgTypes()
        {
            var shop = new FakeShop();
            shop.State.Gold = 500;
            var wand = WandCard(shop.GetState(), 4);
            var a = ShopModel.BuyArgs(wand);
            Assert.IsInstanceOf<string>(a.Values[ScreenArgKeys.Sku]);
            Assert.IsInstanceOf<int>(a.Values[ScreenArgKeys.Qty]);
            Assert.IsInstanceOf<int>(a.Values[ScreenArgKeys.Character]);
            Assert.AreEqual("item.wand_box", a.Get<string>(ScreenArgKeys.Sku));
            Assert.AreEqual(1, a.Get(ScreenArgKeys.Qty, 0));
            Assert.AreEqual(4, a.Get(ScreenArgKeys.Character, 0));
            var charm = ShopModel.BuyArgs(ShopModel.Cards(ShopTab.Charm, shop.GetState(), null)[0]);
            Assert.IsFalse(charm.Values.ContainsKey(ScreenArgKeys.Character), "Character는 지팡이 상자만");
        }

        [Test]
        public void S07_TV_SH_01__BuyConfirmQuantityUpToWeeklyLimitAndBuy()
        {
            var shop = new FakeShop();
            shop.State.Gold = 30000;
            var args = ShopModel.BuyArgs(ShopModel.Cards(ShopTab.Charm, shop.GetState(), null)[0]);
            var m = ShopBuyConfirmModel.From(args, shop.GetState());
            Assert.AreEqual(1, m.Qty);
            Assert.AreEqual(3, m.Max);
            Assert.IsTrue(m.Step(1));
            Assert.IsTrue(m.Step(1));
            Assert.IsFalse(m.Step(1), "주 3개를 넘길 수 없다");
            Assert.AreEqual(30000, m.Total);
            Assert.IsTrue(m.Buy(shop));
            CollectionAssert.AreEqual(new[] { "charm.safeguardx3@0" }, shop.Calls);
            Assert.AreEqual(0, shop.State.Gold);
            Assert.AreEqual(ErrorCode.None, m.LastError);
        }

        [Test]
        public void S07_TV_SH_06__BuyFailureShowsErrorKey()
        {
            var shop = new FakeShop();
            shop.State.Gold = 499;
            var args = ShopModel.BuyArgs(WandCard(shop.GetState(), 3));
            var m = ShopBuyConfirmModel.From(args, shop.GetState());
            Assert.AreEqual(3, m.Character);
            Assert.IsFalse(m.Buy(shop));
            Assert.AreEqual(ErrorCode.NotEnoughGold, m.LastError);
            Assert.AreEqual("error.not_enough_gold", m.ErrorKey);
            Assert.AreEqual("골드가 모자란다.", TestContent.Names.Get(m.ErrorKey));
            CollectionAssert.AreEqual(new[] { "item.wand_boxx1@3" }, shop.Calls);
        }

        [Test]
        public void S07_11__ErrorCodeToNameKey()
        {
            Assert.AreEqual("error.weekly_limit", MetaKeys.Error(ErrorCode.WeeklyLimit));
            Assert.AreEqual("error.not_enough_gold", MetaKeys.Error(ErrorCode.NotEnoughGold));
            Assert.AreEqual("error.invalid_argument", MetaKeys.Error(ErrorCode.InvalidArgument));
            Assert.IsNull(MetaKeys.Error(ErrorCode.None));
            Assert.IsTrue(TestContent.Names.Has(MetaKeys.Error(ErrorCode.WeeklyLimit)));
        }
    }
}

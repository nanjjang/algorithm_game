using System;
using System.Reflection;
using NUnit.Framework;
using Wizard.Solo.Meta;
using Wizard.Solo.Meta.Shop;

namespace Wizard.Solo.Tests.Inventory
{
    /// <summary>골드 상점. 기대값은 07 요약서 8절·12.10절(TV-SH-*)·12.14절, 08 요약서 11.3절(TV-232·233), 00 문서 D-33·D-65.</summary>
    public class ShopServiceTests
    {
        InvFixture f;

        [SetUp]
        public void SetUp() { f = new InvFixture(); }

        [Test]
        public void S07_8__StatePricesFromEconomy()
        {
            var s = f.Shop.GetState();
            Assert.AreEqual(10000, s.CharmPrice);
            Assert.AreEqual(3, s.CharmWeeklyLimit);
            Assert.AreEqual(0, s.CharmBoughtThisWeek);
            Assert.AreEqual(500, s.WandPrice);
            Assert.AreEqual(2000, s.LibraryStepPrice);
            Assert.AreEqual(50, s.LibraryStepBlocks);
            Assert.AreEqual(100, s.LibraryCap);
            Assert.AreEqual(0, s.Gold);
        }

        [Test]
        public void S07_TV_SH_01__ThreeCharmsFor30000()
        {
            f.P.Currencies.Gold = 30000;
            int charm = f.P.Item(Ids.Item.Charm).Qty;
            int saves = f.Store.SaveCount;
            var r = f.Shop.Buy(Ids.Item.Charm, 3, 0);
            Assert.IsTrue(r.Ok);
            Assert.AreEqual(0, f.P.Currencies.Gold);
            Assert.AreEqual(charm + 3, f.P.Item(Ids.Item.Charm).Qty);
            Assert.AreEqual(3, r.Value.CharmBoughtThisWeek);
            Assert.AreEqual(0, r.Value.Gold);
            Assert.AreEqual(saves + 1, f.Store.SaveCount);
        }

        [Test]
        public void S07_TV_SH_02__FourthCharmWeeklyLimit()
        {
            f.P.Currencies.Gold = 40000;
            Assert.IsTrue(f.Shop.Buy(Ids.Item.Charm, 3, 0).Ok);
            var r = f.Shop.Buy(Ids.Item.Charm, 1, 0);
            Assert.AreEqual(ErrorCode.WeeklyLimit, r.Error);
            Assert.AreEqual(10000, f.P.Currencies.Gold);
            Assert.AreEqual(3, f.P.Item(Ids.Item.Charm).Qty);
        }

        [Test]
        public void S07_TV_SH_02__LimitCheckedBeforeGold()
        {
            f.P.Currencies.Gold = 30000;
            Assert.IsTrue(f.Shop.Buy(Ids.Item.Charm, 3, 0).Ok);   // 골드 0
            Assert.AreEqual(ErrorCode.WeeklyLimit, f.Shop.Buy(Ids.Item.Charm, 1, 0).Error);
            f.P.Currencies.Gold = 5000;   // 다음 주가 되어도 골드가 모자라면 NotEnoughGold
            f.Clock.Value = f.Clock.Value.AddDays(7);
            Assert.AreEqual(ErrorCode.NotEnoughGold, f.Shop.Buy(Ids.Item.Charm, 1, 0).Error);
            Assert.AreEqual(ErrorCode.WeeklyLimit, f.Shop.Buy(Ids.Item.Charm, 4, 0).Error);   // 한 번에 4개는 한도 초과
        }

        [Test]
        public void S07_TV_SH_03__NextMondayResets()
        {
            f.P.Currencies.Gold = 40000;
            Assert.IsTrue(f.Shop.Buy(Ids.Item.Charm, 3, 0).Ok);
            f.Clock.Value = new DateTime(2026, 11, 16, 0, 0, 1, DateTimeKind.Local);   // 다음 주 월요일 00:00 지남
            var r = f.Shop.Buy(Ids.Item.Charm, 1, 0);
            Assert.IsTrue(r.Ok);
            Assert.AreEqual(1, r.Value.CharmBoughtThisWeek);
            Assert.AreEqual("2026-11-16", f.P.ShopWeek.WeekStart);
        }

        [Test]
        public void S07_D33__SundayLastMinuteStillLimitedMondayMidnightResets()
        {
            f.Clock.Value = new DateTime(2026, 11, 15, 23, 59, 0, DateTimeKind.Local);   // 일요일 23:59
            f.P.Currencies.Gold = 40000;
            Assert.IsTrue(f.Shop.Buy(Ids.Item.Charm, 3, 0).Ok);
            Assert.AreEqual(ErrorCode.WeeklyLimit, f.Shop.Buy(Ids.Item.Charm, 1, 0).Error);
            Assert.AreEqual("2026-11-09", f.P.ShopWeek.WeekStart);

            f.Clock.Value = new DateTime(2026, 11, 16, 0, 0, 0, DateTimeKind.Local);     // 월요일 00:00
            Assert.AreEqual(0, f.Shop.GetState().CharmBoughtThisWeek);
            Assert.IsTrue(f.Shop.Buy(Ids.Item.Charm, 1, 0).Ok);
        }

        [Test]
        public void S07_D33__WeekStartIsMondayOfSameWeek()
        {
            Assert.AreEqual(new DateTime(2026, 11, 9), ShopService.WeekStart(new DateTime(2026, 11, 9, 0, 0, 0), 1));
            Assert.AreEqual(new DateTime(2026, 11, 9), ShopService.WeekStart(new DateTime(2026, 11, 15, 23, 59, 59), 1));
            Assert.AreEqual(new DateTime(2026, 11, 16), ShopService.WeekStart(new DateTime(2026, 11, 16, 0, 0, 0), 1));
        }

        [Test]
        public void S07_TV_SH_04__DeviceClockDecidesWeek()
        {
            // 오프라인: 기기 시계(ctx.Clock)만 본다 — 같은 주 안에서는 시각이 달라도 같은 한도.
            f.P.Currencies.Gold = 40000;
            Assert.IsTrue(f.Shop.Buy(Ids.Item.Charm, 2, 0).Ok);
            f.Clock.Value = new DateTime(2026, 11, 13, 8, 0, 0, DateTimeKind.Local);   // 같은 주 금요일
            Assert.IsTrue(f.Shop.Buy(Ids.Item.Charm, 1, 0).Ok);
            Assert.AreEqual(ErrorCode.WeeklyLimit, f.Shop.Buy(Ids.Item.Charm, 1, 0).Error);
        }

        [Test]
        public void S07_TV_SH_05__WandBoxForChar3()
        {
            f.P.Currencies.Gold = 500;
            int before = f.Wands.List(3, WandSort.StageDesc).Count;
            int n = f.P.Wands.Count;
            var r = f.Shop.Buy(Ids.Item.WandBox, 1, 3);
            Assert.IsTrue(r.Ok);
            Assert.AreEqual(0, f.P.Currencies.Gold);
            Assert.AreEqual(n + 1, f.P.Wands.Count);
            Assert.AreEqual(before + 1, f.Wands.List(3, WandSort.StageDesc).Count);
            var w = f.P.Wands[f.P.Wands.Count - 1];
            Assert.AreEqual(3, w.Character);
            Assert.AreEqual(0, w.Stage);
            Assert.AreEqual(0, w.FloorPeak);
            Assert.AreEqual(0, w.RecordPeak);
            Assert.AreEqual(0, w.Aura);
            Assert.AreEqual(0, w.SlotsExtra);
            Assert.AreEqual(0, w.Mutations.Count);
            Assert.IsNull(w.Loadout.Core);                 // 빈 조립(코어도 빔)
            Assert.AreEqual(5, w.Loadout.Links.Count);     // char.3 기본 5칸, 전부 빔
            Assert.IsTrue(w.Loadout.Links.TrueForAll(x => x == null));
            Assert.AreEqual("Shop", w.Origin);
            Assert.AreEqual(1, w.OwnerCount);
            Assert.IsNull(w.SkinId);
            Assert.IsFalse(string.IsNullOrEmpty(w.AcquiredAt));
            Assert.AreNotEqual(w.Id, f.Starter(3).Id);
            Assert.AreEqual(f.Starter(3).Id, f.P.Character(3).EquippedWandId);   // 든 지팡이는 그대로
            Assert.AreEqual(0, f.P.Item(Ids.Item.StoneLow).Qty);                  // 강화석은 들지 않는다
        }

        [Test]
        public void S07_TV_SH_06__WandBox499NotEnoughGold()
        {
            f.P.Currencies.Gold = 499;
            int n = f.P.Wands.Count;
            int saves = f.Store.SaveCount;
            Assert.AreEqual(ErrorCode.NotEnoughGold, f.Shop.Buy(Ids.Item.WandBox, 1, 1).Error);
            Assert.AreEqual(499, f.P.Currencies.Gold);
            Assert.AreEqual(n, f.P.Wands.Count);
            Assert.AreEqual(saves, f.Store.SaveCount);
        }

        [Test]
        public void S07_TV_SH_07__ThousandWandsStillBuy()
        {
            while (f.P.Wands.Count < 1000) f.AddWand(1, 0);
            f.P.Currencies.Gold = 500;
            Assert.IsTrue(f.Shop.Buy(Ids.Item.WandBox, 1, 1).Ok);
            Assert.AreEqual(1001, f.P.Wands.Count);
        }

        [Test]
        public void S07_TV_SH_08__LibraryExpansionTo150()
        {
            f.P.Currencies.Gold = 2000;
            Assert.AreEqual(100, f.Wands.LibraryCap());
            var r = f.Shop.Buy(Ids.Item.LibraryExpansion, 1, 0);
            Assert.IsTrue(r.Ok);
            Assert.AreEqual(150, r.Value.LibraryCap);
            Assert.AreEqual(150, f.Wands.LibraryCap());
            Assert.AreEqual(0, f.P.Currencies.Gold);
            Assert.AreEqual(1, f.P.LibraryExpansions);
            Assert.AreEqual(ErrorCode.NotEnoughGold, f.Shop.Buy(Ids.Item.LibraryExpansion, 1, 0).Error);
        }

        [Test]
        public void S07_8__InvalidArguments()
        {
            f.P.Currencies.Gold = 100000;
            Assert.AreEqual(ErrorCode.InvalidArgument, f.Shop.Buy(Ids.Item.WandBox, 1, 0).Error);
            Assert.AreEqual(ErrorCode.InvalidArgument, f.Shop.Buy(Ids.Item.WandBox, 1, 6).Error);
            Assert.AreEqual(ErrorCode.InvalidArgument, f.Shop.Buy(Ids.Item.Charm, 0, 0).Error);
            Assert.AreEqual(ErrorCode.InvalidArgument, f.Shop.Buy(null, 1, 0).Error);
            Assert.AreEqual(100000, f.P.Currencies.Gold);
        }

        // ------------------------------------------------------------ TV-SH-10, 12.14 TV-PAY-03·13, D-65

        [Test]
        public void S07_TV_SH_10__NoPaidOrCosmeticProducts()
        {
            f.P.Currencies.Gold = 1000000;
            foreach (var sku in new[] { "starlight.pack", "currency.starlight", "cosmetic.any", "season_coin.pack", "item.nickname_change" })
                Assert.AreEqual(ErrorCode.InvalidArgument, f.Shop.Buy(sku, 1, 1).Error, sku);
            Assert.AreEqual(1000000, f.P.Currencies.Gold);

            // 상점 상태에는 골드 값만 있다(유료 잔액·유료 상품 필드 없음)
            foreach (var field in typeof(ShopState).GetFields())
                AssertNoPaymentWord(field.Name, "ShopState." + field.Name);
        }

        [Test]
        public void S07_TV_PAY_13__NoPaymentApiInShopOrWands()
        {
            foreach (var t in new[] { typeof(IShopService), typeof(ShopService), typeof(IWandService), typeof(Wizard.Solo.Meta.Inventory.WandService) })
                foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                    AssertNoPaymentWord(m.Name, t.Name + "." + m.Name);
        }

        static void AssertNoPaymentWord(string name, string where)
        {
            string n = name.ToLowerInvariant();
            foreach (var bad in new[] { "pay", "order", "receipt", "starlight", "paid", "purchase", "refundorder", "entitlement" })
                Assert.IsFalse(n.Contains(bad), where);
        }
    }
}

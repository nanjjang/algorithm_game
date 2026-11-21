using System.Collections.Generic;
using Wizard.Solo.Meta;
using Wizard.Solo.UI;

namespace Wizard.Solo.Screens.Meta
{
    /// <summary>골드 상점의 탭(오프라인). 꾸미기 탭은 그리지 않는다(D-65: 탭 열쇠·상품 데이터 미정). 유료 탭은 없다(TV-198, 07 12.14절).</summary>
    public enum ShopTab { Charm = 0, Wand = 1, Other = 2 }

    /// <summary>품목 카드 한 장(216×280).</summary>
    public sealed class ShopCard
    {
        public string Sku;
        /// <summary>지팡이 상자만: 1~5. 그 밖은 0.</summary>
        public int Character;
        public string NameKey;
        public long Price;
        public bool Enabled;
        /// <summary>꺼짐 이유 열쇠(켜져 있으면 null).</summary>
        public string DisabledReason;
        /// <summary>부적만: 남은 한도 줄을 그린다(이번 주 {n} / {max}).</summary>
        public bool ShowWeeklyLimit;
        public int Bought, Limit;
        public bool SoldOut;
        /// <summary>지팡이 상자만: 그 캐릭터의 지팡이 수(지금 {n}자루).</summary>
        public int WandCount;
        /// <summary>서고 확장만: 한 번에 늘어나는 블록 수.</summary>
        public int LibraryStep;
    }

    /// <summary>
    /// 골드 상점 규칙(07 8절, 08 11.3절, 엔진 없음). 값·한도는 IShopService.GetState()(ShopState)에서만 읽는다.
    /// 부적: 이번 주 한도를 다 사면 꺼짐 + str.shop.sold_out(TV-233), 골드 모자라면 꺼짐. 새 지팡이: 보관 한도 없음 → 골드만 보고 켠다(TV-232).
    /// </summary>
    public static class ShopModel
    {
        public static readonly ShopTab[] Tabs = { ShopTab.Charm, ShopTab.Wand, ShopTab.Other };

        public static string TabKey(ShopTab tab)
        {
            switch (tab)
            {
                case ShopTab.Wand: return MetaKeys.ShopTabWand;
                case ShopTab.Other: return MetaKeys.ShopTabOther;
                default: return MetaKeys.ShopTabCharm;
            }
        }

        /// <summary>탭 이름 열쇠 목록(그리는 순서).</summary>
        public static List<string> TabKeys()
        {
            var list = new List<string>(Tabs.Length);
            for (int i = 0; i < Tabs.Length; i++) list.Add(TabKey(Tabs[i]));
            return list;
        }

        public static int WandCount(ProfileData p, int character)
        {
            if (p == null || p.Wands == null) return 0;
            int n = 0;
            for (int i = 0; i < p.Wands.Count; i++) if (p.Wands[i] != null && p.Wands[i].Character == character) n++;
            return n;
        }

        public static List<ShopCard> Cards(ShopTab tab, ShopState st, ProfileData p)
        {
            var list = new List<ShopCard>();
            if (st == null) return list;
            switch (tab)
            {
                case ShopTab.Charm:
                {
                    var c = new ShopCard
                    {
                        Sku = Ids.Item.Charm, NameKey = Ids.Item.Charm, Price = st.CharmPrice, ShowWeeklyLimit = true,
                        Bought = st.CharmBoughtThisWeek, Limit = st.CharmWeeklyLimit
                    };
                    c.SoldOut = st.CharmWeeklyLimit > 0 && st.CharmBoughtThisWeek >= st.CharmWeeklyLimit;
                    if (c.SoldOut) c.DisabledReason = MetaKeys.ShopSoldOut;
                    else if (st.Gold < st.CharmPrice) c.DisabledReason = MetaKeys.Error(ErrorCode.NotEnoughGold);
                    c.Enabled = c.DisabledReason == null;
                    list.Add(c);
                    break;
                }
                case ShopTab.Wand:
                    for (int ch = 1; ch <= Ids.Char.Count; ch++)
                    {
                        var c = new ShopCard
                        {
                            Sku = Ids.Item.WandBox, Character = ch, NameKey = Ids.Item.WandBox, Price = st.WandPrice,
                            WandCount = WandCount(p, ch)
                        };
                        if (st.Gold < st.WandPrice) c.DisabledReason = MetaKeys.Error(ErrorCode.NotEnoughGold);
                        c.Enabled = c.DisabledReason == null;
                        list.Add(c);
                    }
                    break;
                case ShopTab.Other:
                {
                    var c = new ShopCard
                    {
                        Sku = Ids.Item.LibraryExpansion, NameKey = Ids.Item.LibraryExpansion, Price = st.LibraryStepPrice,
                        LibraryStep = st.LibraryStepBlocks
                    };
                    if (st.Gold < st.LibraryStepPrice) c.DisabledReason = MetaKeys.Error(ErrorCode.NotEnoughGold);
                    c.Enabled = c.DisabledReason == null;
                    list.Add(c);
                    break;
                }
            }
            return list;
        }

        /// <summary>구매 확인 창의 인자(00 8.1절: Sku string, Qty int, Character int — 지팡이 상자만).</summary>
        public static ScreenArgs BuyArgs(ShopCard card)
        {
            var a = ScreenArgs.From(ScreenId.Shop).With(ScreenArgKeys.Sku, card.Sku).With(ScreenArgKeys.Qty, 1);
            if (card.Sku == Ids.Item.WandBox) a.With(ScreenArgKeys.Character, card.Character);
            return a;
        }

        public static long PriceOf(string sku, ShopState st)
        {
            if (st == null) return 0;
            if (sku == Ids.Item.Charm) return st.CharmPrice;
            if (sku == Ids.Item.WandBox) return st.WandPrice;
            if (sku == Ids.Item.LibraryExpansion) return st.LibraryStepPrice;
            return 0;
        }

        /// <summary>한 번에 살 수 있는 가장 많은 수(최소 1): 골드로 살 수 있는 수, 부적은 이번 주 남은 한도까지.</summary>
        public static int MaxQty(string sku, ShopState st)
        {
            long price = PriceOf(sku, st);
            long byGold = price > 0 && st != null ? st.Gold / price : 1;
            long max = byGold;
            if (sku == Ids.Item.Charm && st != null && st.CharmWeeklyLimit > 0)
            {
                long left = st.CharmWeeklyLimit - st.CharmBoughtThisWeek;
                if (left < max) max = left;
            }
            if (max < 1) max = 1;
            if (max > int.MaxValue) max = int.MaxValue;
            return (int)max;
        }
    }

    /// <summary>구매 확인 창(ShopBuyConfirm 560×360)의 상태: 수량 조절과 구매.</summary>
    public sealed class ShopBuyConfirmModel
    {
        public string Sku;
        public int Character;
        public int Qty = 1;
        public ShopState State;
        public int Max = 1;
        /// <summary>마지막 구매 시도의 오류(None = 성공 또는 아직 안 함).</summary>
        public ErrorCode LastError;

        public static ShopBuyConfirmModel From(ScreenArgs args, ShopState st)
        {
            var m = new ShopBuyConfirmModel
            {
                Sku = args != null ? args.Get<string>(ScreenArgKeys.Sku) : null,
                Character = args != null ? args.Get(ScreenArgKeys.Character, 0) : 0,
                State = st
            };
            m.Max = ShopModel.MaxQty(m.Sku, st);
            int q = args != null ? args.Get(ScreenArgKeys.Qty, 1) : 1;
            m.Qty = q < 1 ? 1 : q > m.Max ? m.Max : q;
            return m;
        }

        public long UnitPrice => ShopModel.PriceOf(Sku, State);
        public long Total => UnitPrice * Qty;
        public bool CanDecrease => Qty > 1;
        public bool CanIncrease => Qty < Max;

        /// <summary>수량을 1 바꾼다(1~Max). 바뀌었으면 true.</summary>
        public bool Step(int dir)
        {
            int q = Qty + (dir < 0 ? -1 : 1);
            if (q < 1 || q > Max) return false;
            Qty = q;
            return true;
        }

        /// <summary>산다. 성공하면 상점의 새 상태를 State에 넣는다.</summary>
        public bool Buy(IShopService shop)
        {
            if (shop == null || string.IsNullOrEmpty(Sku)) { LastError = ErrorCode.InvalidArgument; return false; }
            var r = shop.Buy(Sku, Qty, Sku == Ids.Item.WandBox ? Character : 0);
            LastError = r.Error;
            if (r.Ok && r.Value != null) State = r.Value;
            return r.Ok;
        }

        public string ErrorKey => MetaKeys.Error(LastError);
    }
}

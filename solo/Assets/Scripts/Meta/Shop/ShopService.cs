using System;
using System.Globalization;
using Wizard.Solo.Data;
using Wizard.Solo.Meta.Inventory;

namespace Wizard.Solo.Meta.Shop
{
    /// <summary>
    /// 골드 상점(07 요약서 8절, 08 요약서 11.3절). 품목은 셋뿐이다: 하락 방지 부적(charm.safeguard, 주 한도),
    /// +0 새 지팡이(item.wand_box, 캐릭터 인자), 서고 확장(item.library_expansion).
    /// 유료 재화·결제·유료 탭은 없다(07 12.14절). 꾸미기 상품도 없다(00 문서 D-65 — 상품 표가 정해지면 sku를 더한다).
    /// 주의 경계는 기기 현지 시간(ctx.Clock.Now) shop_week_start_day_of_week(월요일) 00:00([임시] D-33).
    /// 프로필을 바꾸면 스스로 저장한다.
    /// </summary>
    public sealed class ShopService : IShopService
    {
        const int DaysPerWeek = 7;
        const string WeekFormat = "yyyy-MM-dd";
        const string IsoFormat = "yyyy-MM-dd'T'HH:mm:sszzz";

        readonly GameContext _ctx;

        public ShopService(GameContext ctx)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
        }

        ProfileData Profile => _ctx.Profiles != null ? _ctx.Profiles.Current : null;
        EconomyConfig Econ => _ctx.Content != null ? _ctx.Content.Economy : null;
        DateTime Now => _ctx.Clock != null ? _ctx.Clock.Now : DateTime.Now;

        /// <summary>그 시각이 속한 상점 주의 시작(현지 날짜 00:00). startDayOfWeek = System.DayOfWeek 번호(월요일 = 1).</summary>
        public static DateTime WeekStart(DateTime now, int startDayOfWeek)
        {
            DateTime day = now.Date;
            int since = (((int)day.DayOfWeek - startDayOfWeek) % DaysPerWeek + DaysPerWeek) % DaysPerWeek;
            return day.AddDays(-since);
        }

        public static string WeekStartIso(DateTime now, int startDayOfWeek) =>
            WeekStart(now, startDayOfWeek).ToString(WeekFormat, CultureInfo.InvariantCulture);

        /// <summary>주가 바뀌었으면 이번 주 부적 구매 수를 0으로 되돌린다. 바꿨으면 true.</summary>
        bool RollWeek(ProfileData p)
        {
            var econ = Econ;
            if (p == null || econ == null) return false;
            if (p.ShopWeek == null) p.ShopWeek = new ShopWeekData();
            string current = WeekStartIso(Now, econ.ShopWeekStartDayOfWeek);
            if (p.ShopWeek.WeekStart == current) return false;
            p.ShopWeek.WeekStart = current;
            p.ShopWeek.CharmBought = 0;
            return true;
        }

        public ShopState GetState()
        {
            var p = Profile;
            if (RollWeek(p)) Save();
            return BuildState(p);
        }

        ShopState BuildState(ProfileData p)
        {
            var econ = Econ;
            var s = new ShopState();
            if (econ != null)
            {
                s.CharmPrice = econ.ShopCharmGold;
                s.CharmWeeklyLimit = econ.ShopCharmWeeklyLimit;
                s.WandPrice = econ.ShopWandPlus0Gold;
                s.LibraryStepPrice = econ.ShopLibraryStepGold;
                s.LibraryStepBlocks = econ.ShopLibraryStepBlocks;
            }
            if (p != null)
            {
                s.CharmBoughtThisWeek = p.ShopWeek != null ? p.ShopWeek.CharmBought : 0;
                s.LibraryCap = InventoryRules.LibraryCap(econ, p);
                s.Gold = p.Currencies.Gold;
            }
            return s;
        }

        public Result<ShopState> Buy(string sku, int qty, int character)
        {
            var p = Profile;
            var econ = Econ;
            if (p == null || econ == null) return Result<ShopState>.Fail(ErrorCode.NotFound);
            if (qty < 1 || string.IsNullOrEmpty(sku)) return Result<ShopState>.Fail(ErrorCode.InvalidArgument);

            switch (sku)
            {
                case Ids.Item.Charm:
                {
                    bool rolled = RollWeek(p);
                    // 한도 먼저(TV-SH-02: 3개를 산 뒤에는 골드와 상관없이 WeeklyLimit).
                    if (p.ShopWeek.CharmBought + qty > econ.ShopCharmWeeklyLimit)
                    {
                        if (rolled) Save();
                        return Result<ShopState>.Fail(ErrorCode.WeeklyLimit);
                    }
                    long cost = (long)econ.ShopCharmGold * qty;
                    if (p.Currencies.Gold < cost)
                    {
                        if (rolled) Save();
                        return Result<ShopState>.Fail(ErrorCode.NotEnoughGold);
                    }
                    p.Currencies.Gold -= cost;
                    p.Item(Ids.Item.Charm).Qty += qty;
                    p.ShopWeek.CharmBought += qty;
                    break;
                }
                case Ids.Item.WandBox:
                {
                    // 캐릭터 5명 누구 것이든, 조건·개수 한도 없음(D1.22). 강화석은 들지 않는다.
                    if (!InventoryRules.ValidCharacter(character)) return Result<ShopState>.Fail(ErrorCode.InvalidArgument);
                    long cost = (long)econ.ShopWandPlus0Gold * qty;
                    if (p.Currencies.Gold < cost) return Result<ShopState>.Fail(ErrorCode.NotEnoughGold);
                    p.Currencies.Gold -= cost;
                    string now = Now.ToString(IsoFormat, CultureInfo.InvariantCulture);
                    for (int i = 0; i < qty; i++) p.Wands.Add(InventoryRules.NewShopWand(_ctx.Content, p, character, now));
                    break;
                }
                case Ids.Item.LibraryExpansion:
                {
                    long cost = (long)econ.ShopLibraryStepGold * qty;
                    if (p.Currencies.Gold < cost) return Result<ShopState>.Fail(ErrorCode.NotEnoughGold);
                    p.Currencies.Gold -= cost;
                    p.LibraryExpansions += qty;
                    break;
                }
                default:
                    return Result<ShopState>.Fail(ErrorCode.InvalidArgument);
            }
            Save();
            return Result<ShopState>.Success(BuildState(p));
        }

        void Save() { if (_ctx.Profiles != null) _ctx.Profiles.Save(); }
    }
}

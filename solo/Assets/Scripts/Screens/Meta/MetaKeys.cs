using Wizard.Solo.Meta;

namespace Wizard.Solo.Screens.Meta
{
    /// <summary>
    /// 기록·칭호·옷장·상점 화면이 쓰는 이름 표 열쇠(원본 names.ko.json + NamesSolo/G24.ko.json). 화면 글자는 이 열쇠로만 찾는다.
    /// </summary>
    public static class MetaKeys
    {
        // ---- 기록 (08 11.1절)
        public const string RecordsTitle = "str.records.title";
        public const string RecordsTabAll = "str.records.tab.all";
        /// <summary>WP4 조각(NamesSolo/Meta.ko.json): 도전 과제 탭과 그 판.</summary>
        public const string RecordsTabChallenges = "str.records.tab.challenges", RecordsChallengeAll = "str.records.challenge.all",
            RecordsChallengeDoneCount = "str.records.challenge.done_count";
        public const string RecordsBestScore = "str.records.best_score", RecordsBestWave = "str.records.best_wave",
            RecordsTotalWaves = "str.records.total_waves", RecordsTitles = "str.records.titles";
        public const string RecordsDate = "str.records.date", RecordsCharacter = "str.records.character",
            RecordsReachedWave = "str.records.reached_wave", RecordsStartWave = "str.records.start_wave",
            RecordsScore = "str.records.score", RecordsWandStage = "str.records.wand_stage", RecordsLoadout = "str.records.loadout";

        // ---- 칭호 고르기
        public const string TitlePickerTitle = "str.title_picker.title", TitlePickerEquip = "str.title_picker.equip",
            TitlePickerNone = "str.title_picker.none";

        // ---- 옷장 (08 11.2절)
        public const string WardrobeTitle = "str.wardrobe.title";
        public const string WardrobeTurn = "str.wardrobe.preview.turn", WardrobeWalk = "str.wardrobe.preview.walk",
            WardrobeSit = "str.wardrobe.preview.sit", WardrobeEmote = "str.wardrobe.preview.emote";
        public const string WardrobeStainPick = "str.wardrobe.wild_stain_pick", WardrobeWorn = "str.wardrobe.worn",
            WardrobeOriginal = "str.wardrobe.original", WardrobeAcquiredFrom = "str.wardrobe.acquired_from",
            WardrobeBound = "str.wardrobe.bound";
        public const string WardrobeSourceGoldShop = "str.wardrobe.source.gold_shop", WardrobeSourceRecord = "str.wardrobe.source.record",
            WardrobeSourceSeason = "str.wardrobe.source.season", WardrobeSourceTrade = "str.wardrobe.source.trade";

        // ---- 상점 (08 11.3절, 07 8절)
        public const string ShopTitle = "str.shop.title", ShopKindGold = "str.shop.kind.gold";
        public const string ShopTabCharm = "str.shop.tab.charm", ShopTabWand = "str.shop.tab.wand", ShopTabOther = "str.shop.tab.other";
        public const string ShopPrice = "str.shop.price", ShopWeeklyLimit = "str.shop.weekly_limit", ShopSoldOut = "str.shop.sold_out",
            ShopWandCount = "str.shop.wand_count", ShopBuy = "str.shop.buy";
        public const string ShopBuyConfirmTitle = "str.shop_buy_confirm.title", ShopBuyConfirmConfirm = "str.shop_buy_confirm.confirm";
        /// <summary>G24 조각: 수량 조절 단추 글자와 합계 줄.</summary>
        public const string ShopBuyConfirmMinus = "str.shop_buy_confirm.minus", ShopBuyConfirmPlus = "str.shop_buy_confirm.plus",
            ShopBuyConfirmTotal = "str.shop_buy_confirm.total";
        public const string NpcPeddler = "npc.peddler";

        // ---- 공통
        public const string CommonClose = "str.common.close", CommonEmpty = "str.common.empty", CommonBack = "str.common.back",
            CommonCancel = "str.common.cancel";
        public const string FormatDayWave = "str.format.day_wave", FormatWave = "str.format.wave", FormatWandStage = "str.format.wand_stage",
            FormatGoldAmount = "str.format.gold_amount", FormatMoreCount = "str.format.more_count", FormatLibraryCounter = "str.format.library_counter";

        // ---- 복수 추적기 (08 13.2절)
        public const string RevengeGoal = "revenge.tracker.goal", RevengeProgress = "revenge.tracker.progress",
            RevengeDent = "revenge.tracker.dent", RevengeDoneLine = "revenge.tracker.done_line";

        /// <summary>오류 코드 → 이름 표 "error.&lt;snake_case&gt;"(MetaContracts ErrorCode 주석의 규칙).</summary>
        public static string Error(ErrorCode code)
        {
            if (code == ErrorCode.None) return null;
            string name = code.ToString();
            var sb = new System.Text.StringBuilder("error.", name.Length + 12);
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (char.IsUpper(c))
                {
                    if (i > 0) sb.Append('_');
                    sb.Append(char.ToLowerInvariant(c));
                }
                else sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>캐릭터 1~5의 이름 열쇠("char.N").</summary>
        public static string Character(int number) => Ids.Char.Of(number);
    }
}

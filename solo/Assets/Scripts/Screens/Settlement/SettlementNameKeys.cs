namespace Wizard.Solo.Screens.Settlement
{
    /// <summary>
    /// 정산 화면의 이름 표 열쇠와 소리 사건 id(08 요약서 9절·13.2절, 01 요약서 10.6절).
    /// 원본 names.ko.json에 있는 열쇠는 그대로, 없는 열쇠는 NamesSolo/G22.ko.json에 둔다.
    /// </summary>
    public static class SettlementNameKeys
    {
        // ---------------------------------------------------------------- 원본 열쇠
        public const string Title = "str.settlement.title";
        public const string ScoreSection = "str.settlement.section.score";
        public const string PageRecord = "str.settlement.page.record";
        public const string PageReturnBag = "str.settlement.page.return_bag";
        public const string PageTakeaways = "str.settlement.page.takeaways";
        public const string CommonNext = "str.common.next";
        public const string CommonPrev = "str.common.prev";
        public const string CommonExit = "str.common.exit";
        public const string CommonNone = "str.common.none";

        public const string FormatDayWave = "str.format.day_wave";
        public const string FormatStartWave = "str.format.start_wave";
        public const string FormatGoldAmount = "str.format.gold_amount";
        public const string FormatWandStage = "str.format.wand_stage";

        public const string ScoreKills = "str.settlement.score.kills";
        public const string ScoreBoss = "str.settlement.score.boss";
        public const string ScoreClear = "str.settlement.score.clear";
        public const string ScoreFast = "str.settlement.score.fast";
        public const string ScoreNoHit = "str.settlement.score.no_hit";
        public const string ScoreGraze = "str.settlement.score.graze";
        public const string ScoreVariance = "str.settlement.score.variance";
        public const string ScoreSkipped = "str.settlement.score.skipped";
        public const string ScorePhantomFirst = "str.settlement.score.phantom_first";
        public const string ScoreTotal = "str.settlement.score.total";
        public const string BestRecord = "str.settlement.best_record";
        /// <summary>최고 기록 문구(F1 조각, 00 문서 D-26).</summary>
        public const string BestLine = "str.settlement.best_line";

        public const string BagSlots = "str.settlement.bag.slots";
        public const string BagHeroLimit = "str.settlement.bag.hero_limit";
        public const string BagFull = "str.settlement.bag.full";
        public const string BagBorrowedNote = "str.settlement.bag.borrowed_note";
        public const string BagLibraryFull = "str.settlement.bag.library_full";
        public const string BagLeftoverGold = "str.settlement.bag.leftover_gold";
        public const string BlockGradePrefix = "str.block.grade.";
        public const string BlockBorrowed = "str.block.borrowed";

        public const string TakeGold = "str.settlement.take.gold";
        public const string TakeMaterials = "str.settlement.take.materials";
        public const string TakeBundle = "str.settlement.take.bundle";
        public const string TakeNewArt = "str.settlement.take.new_art";
        public const string TakeRevengeReward = "str.settlement.take.revenge_reward";
        public const string TakeBound = "str.settlement.take.bound";
        public const string RevengeMasterLine = "revenge.master_line";
        public const string ItemMaterialBundle = "item.material_bundle";
        public const string ItemCrowBundle = "item.crow_bundle";

        public const string Again = "str.settlement.again";
        public const string ToEnhance = "str.settlement.to_enhance";
        public const string ToMenu = "str.settlement.to_menu";

        public const string TrackerGoal = "revenge.tracker.goal";
        public const string TrackerProgress = "revenge.tracker.progress";
        public const string TrackerDent = "revenge.tracker.dent";
        public const string TrackerDone = "revenge.tracker.done";

        // ---------------------------------------------------------------- 이 화면이 더한 열쇠(G22 조각)
        public const string BagAuto = "str.settlement.bag.auto";
        public const string BagTitle = "str.settlement.bag.bag_title";
        public const string BagHeroCount = "str.settlement.bag.hero_count";
        public const string BagLeftoverTitle = "str.settlement.bag.leftover_title";
        public const string BagLockedNote = "str.settlement.bag.locked_note";
        public const string BagNoSlots = "str.settlement.bag.no_slots";
        public const string BagWildTitle = "str.settlement.bag.wild_title";
        public const string BagEmpty = "str.settlement.bag.wild_empty";
        public const string EnhanceHint = "str.settlement.enhance_hint";
        public const string TakeBagged = "str.settlement.take.bagged";
        public const string TakeBundleOpen = "str.settlement.take.bundle_open";
        public const string TakeItemCount = "str.settlement.take.item_count";
        public const string TakeNewTitle = "str.settlement.take.new_title";
        public const string TakeNothing = "str.settlement.take.nothing";
        public const string TakeHeadwear = "str.settlement.take.headwear";
        public const string TakeTitle = "str.settlement.take.title_label";
        // WP4 조각(NamesSolo/Meta.ko.json): 새로 이룬 도전 과제 줄.
        public const string TakeChallenges = "str.settlement.take.challenges";
        public const string TakeChallengeLine = "str.settlement.take.challenge_line";

        // ---------------------------------------------------------------- 소리(event_bindings.json 열쇠, 08 9.3·13.2절)
        public const string SoundPage = "ui.settlement.page";
        public const string SoundNewBest = "ui.settlement.new_best";
        public const string SoundBagOpen = "ui.settlement.bag_open";
        public const string SoundBagPut = "ui.settlement.bag_put";
        public const string SoundBagFull = "ui.settlement.bag_full";
        public const string SoundBundleOpen = "ui.settlement.bundle_open";
        public const string SoundRevengeReward = "ui.settlement.revenge_reward";
        public const string SoundRevengeComplete = "ui.revenge.complete";
        public const string SoundTrackerRow = "ui.revenge.settlement_row";
        public const string SoundTrackerComplete = "ui.revenge.tracker_complete";
        public const string SoundDisabled = "ui.common.disabled_press";
    }
}

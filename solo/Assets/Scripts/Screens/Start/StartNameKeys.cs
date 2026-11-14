namespace Wizard.Solo.Screens.Start
{
    /// <summary>
    /// 시작 묶음 화면(08 요약서 3·4절)이 쓰는 이름 표 열쇠. 원본 names.ko.json에 있는 것과
    /// 이 작업의 조각(Resources/NamesSolo/G18.ko.json)에 더한 것(PendingSettled*, MainMenuExitConfirm).
    /// </summary>
    public static class StartNameKeys
    {
        // 3.1 FirstRunPrompt
        public const string FirstRunTitle = "str.first_run_prompt.title";
        public const string FirstRunFull = "str.first_run_prompt.option.full";
        public const string FirstRunFullDesc = "str.first_run_prompt.option.full_desc";
        public const string FirstRunReduced = "str.first_run_prompt.option.reduced";
        public const string FirstRunReducedDesc = "str.first_run_prompt.option.reduced_desc";
        public const string FirstRunMinimal = "str.first_run_prompt.option.minimal";
        public const string FirstRunMinimalDesc = "str.first_run_prompt.option.minimal_desc";

        // 3.2 Title
        public const string TitleTitle = "str.title.title";
        public const string TitleStartHint = "str.title.start_hint";
        public const string TitleProfileOffline = "str.title.profile.offline";
        public const string TitleProfileServer = "str.title.profile.server";
        public const string TitleChangeProfile = "str.title.change_profile";
        public const string TitleBuildVersion = "str.title.build_version";
        public const string TitleCredits = "str.title.credits_label";

        // 3.3 ProfileSelect
        public const string ProfileSelectTitle = "str.profile_select.title";
        public const string ProfileCardOffline = "str.profile_select.card.offline";
        public const string ProfileCardOfflineDesc = "str.profile_select.card.offline_desc";
        public const string ProfileCardServer = "str.profile_select.card.server";
        public const string ProfileCardServerDesc = "str.profile_select.card.server_desc";
        public const string ErrorNotSupportedOffline = "error.not_supported_offline";

        // 3.4 OfflineGuide
        public const string OfflineGuideTitle = "str.offline_guide.title";
        public const string OfflineGuideColumnCan = "str.offline_guide.column.can";
        public const string OfflineGuideColumnCannot = "str.offline_guide.column.cannot";

        // 4 MainMenu
        public const string MainMenuTitle = "str.main_menu.title";
        public const string MainMenuExpedition = "str.main_menu.mode.expedition";
        public const string MainMenuExpeditionSub = "str.main_menu.mode.expedition_sub";
        public const string MainMenuDuel = "str.main_menu.mode.duel";
        public const string MainMenuPlaza = "str.main_menu.mode.plaza";
        public const string MainMenuBestWave = "str.main_menu.best_wave";
        public const string MainMenuServerOnly = "str.main_menu.server_only";
        public const string MainMenuOfflineGuide = "str.main_menu.offline_guide";
        public const string MainMenuEnhance = "str.main_menu.item.enhance";
        public const string MainMenuAssemblyLibrary = "str.main_menu.item.assembly_library";
        public const string MainMenuPractice = "str.main_menu.item.practice";
        public const string MainMenuRecords = "str.main_menu.item.records";
        public const string MainMenuWardrobe = "str.main_menu.item.wardrobe";
        public const string MainMenuShop = "str.main_menu.item.shop";
        public const string MainMenuSettings = "str.main_menu.item.settings";
        public const string MainMenuMailbox = "str.main_menu.item.mailbox";
        public const string MainMenuFriends = "str.main_menu.item.friends";
        public const string MainMenuLeaderboard = "str.main_menu.item.leaderboard";
        public const string MainMenuSeason = "str.main_menu.item.season";
        public const string MainMenuMarket = "str.main_menu.item.market";
        public const string MainMenuExit = "str.main_menu.item.exit";
        /// <summary>G18 조각: [나가기]의 확인 창 본문(08 18.2절 N-02 "확인 창 한 번").</summary>
        public const string MainMenuExitConfirm = "str.main_menu.exit_confirm";
        public const string RevengeTrackerShort = "revenge.tracker.short";
        public const string CurrencyGold = "currency.gold";

        // 13.1 / D-68 자동 정산 알림 (G18 조각)
        public const string PendingTitle = "str.main_menu.pending_settled.title";
        public const string PendingMessage = "str.main_menu.pending_settled.message";
        public const string PendingMessageNoWave = "str.main_menu.pending_settled.message_no_wave";
        public const string PendingReceived = "str.main_menu.pending_settled.received";
        public const string PendingGold = "str.main_menu.pending_settled.gold";
        public const string PendingMaterial = "str.main_menu.pending_settled.material";
        public const string PendingBlock = "str.main_menu.pending_settled.block";
        public const string PendingCrowBundle = "str.main_menu.pending_settled.crow_bundle";
        public const string PendingTitleLine = "str.main_menu.pending_settled.title_line";
        public const string PendingRevengeReward = "str.main_menu.pending_settled.revenge_reward";
        public const string PendingArt = "str.main_menu.pending_settled.art";
        public const string PendingNothing = "str.main_menu.pending_settled.nothing";

        public const string CommonOk = "str.common.ok";
        public const string ConfirmTitle = "str.confirm_dialog.title";
    }

    /// <summary>시작 묶음 화면이 내는 ui.* 소리 사건(event_bindings.json 열쇠, 08 요약서 3·4절 이벤트 줄).</summary>
    public static class StartSounds
    {
        public const string FirstRunChoice = "ui.first_run.choice";
        public const string TitleStartPromptPulse = "ui.title.start_prompt_pulse";
        public const string TitleStart = "ui.title.start";
        public const string MainMenuModeCardFocus = "ui.main_menu.mode_card_focus";
        public const string MainMenuBgTimeChange = "ui.main_menu.bg_time_change";
        public const string OfflineGroupBoxOpen = "ui.offline.group_box_open";
    }
}

namespace Wizard.ViewLogic.Stack
{
    // spec 13 1.1절의 ScreenId 표(원본 ui 4절, ui 7). 번호는 더하기만 하고 바꾸거나 다시 쓰지 않는다(CLAUDE.md 5절).
    // None 은 "없음"(ReturnTarget 이 비었을 때, 빈 스택의 맨 위)이다. 표의 묶음 순서대로 1부터 매긴다.
    // spec 11 3.2절의 시설 화면 가운데 이 표에 없는 것(서고, 시세, 시즌 상점 등)은 그 화면의 작업이 뒤에 더한다.
    public enum ScreenId
    {
        None = 0,

        // 시작
        FirstRunPrompt = 1,
        Title = 2,
        ProfileSelect = 3,
        Login = 4,
        Signup = 5,
        EmailVerify = 6,
        PasswordForgot = 7,
        PasswordReset = 8,
        RecoveryMailOff = 9,
        NewDeviceNotice = 10,
        ConnectQueue = 11,
        ServerUnreachable = 12,
        OfflineGuide = 13,

        // 메뉴
        MainMenu = 14,
        ExpeditionPrep = 15,
        ChannelSelect = 16,
        Records = 17,
        TitlePicker = 18,
        Settings = 19,
        KeyRebind = 20,
        Wardrobe = 21,
        Shop = 22,

        // 판
        Loading = 23,
        BattleHud = 24,
        LootPick = 25,
        Camp = 26,
        ArtTrie = 27,
        DummyPractice = 28,
        Pause = 29,
        ControlsView = 30,
        Settlement = 31,

        // 결투
        DuelIntro = 32,
        DuelHud = 33,
        DuelRoundBreak = 34,
        SpectatorHud = 35,
        DuelResult = 36,
        DuelBoard = 37,
        DuelRoom = 38,
        RoomPassword = 39,
        MatchFound = 40,

        // 강화
        Enhance = 41,
        WandPicker = 42,
        WandReleaseConfirm = 43,
        MutationRemove = 44,
        SlotEngrave = 45,
        EnhanceTutorial = 46,

        // 광장
        PlazaOverlay = 47,
        EmoteWheel = 48,
        NearbyList = 49,
        PlazaMenu = 50,
        IncomingRequest = 51,
        Friends = 52,
        Inspect = 53,
        Report = 54,
        BlockConfirm = 55,
        Trade = 56,
        TradeFinal = 57,
        TradeLossWarning = 58,
        Market = 59,
        MarketBuyConfirm = 60,
        ShopBuyConfirm = 61,
        Mailbox = 62,
        MailCompose = 63,
        MasterChair = 64,
        SeasonInfo = 65,
        SuccessionScore = 66,
        SeasonReward = 67,
        League = 68,
        Leaderboard = 69,
        Monument = 70,

        // 공용
        ConfirmDialog = 71,
        Tooltip = 72,
        Disconnected = 73,
        RunSubmitPending = 74,
        SaveRestored = 75,
        OpsNotice = 76,
        SanctionNotice = 77,
        ExpeditionCleanupConfirm = 78,
        DebtList = 79,
    }
}

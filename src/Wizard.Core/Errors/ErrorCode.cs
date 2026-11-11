namespace Wizard.Core
{
    // CONTRACTS 0.2. 번호는 추가만 하고 바꾸지 않는다. JSON 에는 이름을 snake_case 로 싣는다.
    // 이 폴더(Errors/)는 코어 버전 해시에서 빠진다(T-18). Wizard.Sim 과 전투 쪽 Wizard.Content 는 참조하지 않는다.
    public enum ErrorCode : ushort
    {
        None = 0,
        // 1~99 통신·세션
        NetworkUnreachable = 1, NetworkTimeout = 2, Unauthorized = 3, TokenExpired = 4, Sanctioned = 5,
        Maintenance = 6, ServerFull = 7, ClientTooOld = 8, CoreVersionMismatch = 9, DataVersionMismatch = 10,
        RateLimited = 11, IdempotencyConflict = 12, VersionConflict = 13, BadRequest = 14, NotFound = 15,
        NotSupportedOffline = 16, KickedByNewLogin = 17,
        // 100~199 계정
        LoginFailed = 100, LoginThrottled = 101, LoginIdTaken = 102, NicknameTaken = 103, InvalidLoginId = 104,
        InvalidPassword = 105, InvalidNickname = 106, InvalidEmail = 107, EmailAccountLimit = 108,
        CodeInvalid = 109, CodeExpired = 110, MailUnavailable = 111, SignupClosed = 112, SignupLimit = 113,
        // 200~299 지팡이·강화·상점 (spec 08 13절)
        NotOwner = 200, MaxStage = 201, WandLocked = 202, MutationPending = 203, NoPendingMutation = 204,
        NotEnoughGold = 205, NotEnoughStone = 206, NotEnoughCharm = 207, CharmNotNeeded = 208,
        InvalidSacrifice = 209, LastProjectile = 210, NotEligible = 211, LastWand = 212,
        NameConfirmRequired = 213, TradeLocked = 214, PendingVerification = 215, HasDebt = 216,
        WeeklyLimit = 217, LibraryFull = 218, InvalidLoadout = 219, CoreEmpty = 220,
        // 300~399 판
        UnsettledRunFirst = 300, StartWaveNotAllowed = 301, RunNotFound = 302, RunAlreadySettled = 303,
        RunTokenInvalid = 304, RunRecordInvalid = 305, RunPrimaryCheckFailed = 306, WeeklyNotOpen = 307,
        // 400~499 거래·장터·우편·사회
        TradeNotOpen = 400, TradeRevisionStale = 401, ItemBound = 402, ItemNotTradable = 403,
        ListingGone = 404, PriceChanged = 405, GoldDailyCap = 406, Blocked = 407, TargetNotEligible = 408,
        MailAlreadyClaimed = 409, FriendLimit = 410, BlockLimit = 411,
        // 500~599 결투
        DuelNotEligible = 500, DuelQueueBanned = 501, DuelRttTooHigh = 502, DuelUdpRequired = 503,
        RoomFull = 504, RoomPasswordWrong = 505, IlCapExceeded = 506, ChallengeExpired = 507,
        // 600~699 결제
        PaymentsDisabled = 600, ProductUnavailable = 601, PaymentBlocked = 602, OrderExpired = 603,
        ReceiptInvalid = 604, CodeUsed = 605,
    }
}

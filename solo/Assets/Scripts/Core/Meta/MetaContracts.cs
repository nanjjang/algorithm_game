using System;
using System.Collections.Generic;
using Wizard.Solo.Sim;

// 판 밖 규칙(프로필을 바꾸는 것)의 계약. 화면은 이 인터페이스들만 부른다 — 나중에 서버 백엔드를 붙일 자리다.
// 규칙상의 "안 됨"은 예외가 아니라 ErrorCode로 돌려준다. 프로필을 바꾼 메서드는 스스로 저장까지 한다(07 요약서 9.3절 "쓰는 때").
namespace Wizard.Solo.Meta
{
    /// <summary>오류 코드(07 요약서 11절). 화면은 코드만 보고 이름 표 "error.&lt;코드 이름을 snake_case로&gt;" 문구를 고른다.</summary>
    public enum ErrorCode
    {
        None = 0,
        MaxStage = 201, WandLocked = 202, MutationPending = 203, NoPendingMutation = 204, NotEnoughGold = 205,
        NotEnoughStone = 206, NotEnoughCharm = 207, CharmNotNeeded = 208, InvalidSacrifice = 209, LastProjectile = 210,
        NotEligible = 211, LastWand = 212, NameConfirmRequired = 213, WeeklyLimit = 217, LibraryFull = 218,
        InvalidLoadout = 219, CoreEmpty = 220,
        NotFound = 290, InvalidArgument = 291
    }

    public struct Result<T>
    {
        public ErrorCode Error;
        public T Value;
        public bool Ok => Error == ErrorCode.None;
        public static Result<T> Success(T value) => new Result<T> { Value = value };
        public static Result<T> Fail(ErrorCode error) => new Result<T> { Error = error };
    }

    /// <summary>
    /// 프로필 저장소. 구현: Save/ProfileStore(F4). persistentDataPath/profiles/offline/profile.json,
    /// 임시 파일 → 이름 바꾸기, 3세대(.1 .2 .3), 마지막 줄 SHA-256. 깨졌으면 직전 세대로 되돌린다.
    /// </summary>
    public interface IProfileStore
    {
        ProfileData Current { get; }
        /// <summary>읽는다. 없으면 새 프로필을 만들어 저장한다. restored = 깨져서 이전 세대로 되돌렸다.</summary>
        void LoadOrCreate(out bool created, out bool restored);
        void Save();
        /// <summary>저장 폴더(테스트는 임시 폴더를 준다). 설정 화면의 "저장 파일 위치"가 그린다.</summary>
        string Directory { get; }
        /// <summary>직전 세대(.1)를 현재로 되돌리고 Current를 다시 읽는다(설정 &gt; 계정의 [저장 파일 되돌리기]). 되돌릴 세대가 없거나 깨졌으면 false(아무것도 바꾸지 않는다).</summary>
        bool RestorePrevious();
        event Action Saved;
    }

    /// <summary>기기 설정 저장소(프로필과 따로). 구현: Save/SettingsStore(F4).</summary>
    public interface ISettingsStore
    {
        SettingsData Current { get; }
        /// <summary>설정 파일이 있었나(없으면 기기 첫 실행 → FirstRunPrompt).</summary>
        bool ExistedAtLoad { get; }
        void Load();
        void Save();
        event Action Changed;
    }

    // ---------------------------------------------------------------- 강화 (07 요약서 4절)

    public enum EnhanceOutcome
    {
        GreatSuccess, Success, FailDrop, FailCharmBlocked, FailNoDrop, MutationOffered, MutationAccepted
    }

    /// <summary>확률·비용 분해(시도 전 화면 표시와 판정에 같은 값을 쓴다). 07 요약서 4.2~4.5절.</summary>
    public struct ProbBreakdown
    {
        public int BasePct;
        /// <summary>제물 보너스 전체(등급 합 + 같은 속성, 상한 뒤). 판정에 쓰는 값.</summary>
        public int SacrificePct;
        /// <summary>SacrificePct 중 같은 속성 보너스 몫(0 또는 same_element). 내역의 "제물 합" 줄 = SacrificePct − SameElementPct.</summary>
        public int SameElementPct;
        public int AuraPct;
        /// <summary>상한 적용 전 합.</summary>
        public int RawPct;
        /// <summary>최종 성공 확률(%).</summary>
        public int SuccessPct;
        public bool Capped;
        public int SuccessBp, GreatBp, MutationEndBp;
        public bool DropPossible;
        /// <summary>하락 바닥 단계.</summary>
        public int FloorStage;
        public long CostGold;
        /// <summary>mat.stone_low / mid / high.</summary>
        public string StoneId;
        public int StoneCount;
        /// <summary>지금 시도할 수 없는 까닭(None = 가능).</summary>
        public ErrorCode Blocked;
    }

    public struct FallbackResult
    {
        public EnhanceOutcome Outcome;
        public int StageAfter;
        public bool CharmConsumed;
        public int AuraAfter;
    }

    /// <summary>강화 한 번의 결과 꾸러미. 화면이 받는 유일한 입력. 07 요약서 4.12절.</summary>
    public sealed class EnhanceResult
    {
        public long AttemptId;
        public EnhanceOutcome Outcome;
        public int StageBefore, StageAfter;
        public bool RecordPeakUpdated;
        public bool SlotExpanded, SlotAlreadyFull;
        public bool CharmConsumed;
        public int AuraBefore, AuraAfter;
        public string MutationCandidate;
        public string MutationPushedOut;
        public bool HasFallback;
        public FallbackResult Fallback;
        public ProbBreakdown Breakdown;
        public int RollBp;
        public int MilestoneStage;
        public bool MilestoneFirstTime;
        public bool ScarAdded;
        /// <summary>오프라인은 항상 null(서버 전체 알림 없음).</summary>
        public string ServerNotice;
        public WandData Wand;
        public long GoldAfter;
        public int StoneLowAfter, StoneMidAfter, StoneHighAfter, CharmAfter;
    }

    public sealed class CarveResult
    {
        public bool Success;
        public int ChancePct;
        public int RollBp;
        public int SlotsExtraAfter;
        public int CarveBonusAfter;
        public long GoldAfter;
    }

    /// <summary>기댓값 표 한 줄(08 요약서 10.9절, 07 요약서 6절).</summary>
    public struct ExpectedRow
    {
        public int TargetStage;
        public double ExpectedAttempts;
        public double ExpectedGold;
        public double ExpectedStonesLow, ExpectedStonesMid, ExpectedStonesHigh;
    }

    /// <summary>강화 규칙과 적용. 구현 폴더 Meta/Enhance. 다섯 지팡이가 같은 확률·비용·실패 규칙을 쓴다.</summary>
    public interface IEnhanceService
    {
        /// <summary>시도 전 분해. sacrifices = 제물 블록 id(최대 5).</summary>
        ProbBreakdown Preview(long wandId, IReadOnlyList<string> sacrifices, bool useCharm);
        Result<EnhanceResult> Enhance(long wandId, IReadOnlyList<string> sacrifices, bool useCharm);
        /// <summary>대기 중인 변이 제시를 받거나 거절한다.</summary>
        Result<EnhanceResult> ResolveMutation(long wandId, long attemptId, bool accept);
        Result<WandData> RemoveMutation(long wandId, string mutationId);
        ProbBreakdown PreviewCarve(long wandId);
        Result<CarveResult> CarveSlot(long wandId);
        Result<WandData> SetScarVisibility(long wandId, bool show);
        /// <summary>강화 배율 1.15^단계(전투와 화면이 같은 값을 읽는다).</summary>
        float StageDamageMul(int stage);
        /// <summary>지금 지팡이 상태에서 목표 단계까지의 기댓값 표.</summary>
        List<ExpectedRow> ExpectedTable(long wandId, int upToStage);
        /// <summary>"보유 재화로 평균 N번"(07 요약서 6절, str.enhance.expect.with_owned). 지금 프로필의 골드·강화석으로 목표 단계까지.</summary>
        int AttemptsWithOwned(long wandId, int targetStage);
        /// <summary>그 지팡이가 지금 원정에 나가 있어 강화·변이·떠나보내기를 할 수 없는가.</summary>
        bool IsLocked(long wandId);
    }

    // ---------------------------------------------------------------- 지팡이·서고·상점 (07 요약서 3·7·8절)

    public enum WandSort { Recent, StageDesc, StageAsc }

    public sealed class WandListEntry
    {
        public WandData Wand;
        public bool Equipped;
        public bool Locked;
        public bool CoreEmpty;
        public long Appraisal;
    }

    /// <summary>지팡이 개체·조립·서고. 구현 폴더 Meta/Inventory.
    /// 메타 인터페이스는 캐릭터를 전부 int(1~5)로 받는다(시뮬레이션은 "char.N" 문자열. 변환은 Ids.Char.Of / NumberOf).</summary>
    public interface IWandService
    {
        /// <summary>보관 목록(원정 준비·강화 공용). character 0 = 전부.</summary>
        List<WandListEntry> List(int character, WandSort sort);
        Result<WandData> Equip(int character, long wandId);
        /// <summary>출발 조립을 바꾼다(서고 수량·끼움 수를 맞춘다). 코어는 투사체만.</summary>
        Result<WandData> SetLoadout(long wandId, LoadoutData loadout);
        /// <summary>떠나보내기(감정가의 일부 환급). 캐릭터의 마지막 한 자루는 못 보낸다. 환급 골드를 돌려준다.
        /// +7 이상은 typedName이 표시 이름과 같아야 한다(S8 9.1 ReleaseWands(ids, typedName), TV-RL-04). 아니면 NameConfirmRequired.</summary>
        Result<long> Release(IReadOnlyList<long> wandIds, string typedName);
        long Appraise(WandData wand);
        /// <summary>서고에서 아직 안 끼운 수량.</summary>
        int LibraryFree(string blockId);
        int LibraryTotal();
        int LibraryCap();
        /// <summary>원정 출발용 설정을 만든다(프로필 → RunConfig, Mode = Endless). 출발할 수 없으면 오류 코드. profile.runs_started를 올리고 저장한다.</summary>
        Result<RunConfig> BuildRunConfig(int character, long wandId, int startWave, ulong seed);
        /// <summary>
        /// 연습·튜토리얼 방의 설정을 만든다(mode = Practice 또는 Tutorial). **프로필을 바꾸지 않는다**(runs_started 그대로, 저장 없음).
        /// overrideLoadout이 null이면 그 지팡이의 출발 조립, 아니면 그 조립(야영에서 연 연습 = 지금 판의 조립, 야생 블록 id 포함 —
        /// 이 경우 서고 수량 검사를 하지 않는다). 코어가 비었거나 컴파일 오류면 CoreEmpty / InvalidLoadout.
        /// PhantomFirst·RevengePending·CrowBundlePending은 false, StartWave 1, RunId는 빈 글자. Tutorial이면 PracticeInfiniteCrystal = true.
        /// </summary>
        Result<RunConfig> BuildPracticeConfig(int character, long wandId, RunMode mode, LoadoutSpec overrideLoadout, ulong seed);
    }

    public sealed class ShopState
    {
        public int CharmPrice, CharmBoughtThisWeek, CharmWeeklyLimit;
        public int WandPrice;
        public int LibraryStepPrice, LibraryStepBlocks, LibraryCap;
        public long Gold;
    }

    /// <summary>골드 상점. 구현 폴더 Meta/Shop. 유료 재화·결제는 없다.</summary>
    public interface IShopService
    {
        ShopState GetState();
        /// <summary>sku = charm.safeguard / item.wand_box / item.library_expansion. character는 지팡이 상자에만.</summary>
        Result<ShopState> Buy(string sku, int qty, int character);
    }

    // ---------------------------------------------------------------- 정산 (01 요약서 10절, 06 요약서 5절)

    public struct BagLimits
    {
        public int Slots;
        public int HeroCap;
    }

    public sealed class MaterialGain
    {
        public string ItemId;
        public int Qty;
        public bool Guaranteed;
        public bool Bound;
        public DropChanceSource Source;
    }

    /// <summary>정산 화면이 그릴 것. Prepare가 만들고 Commit이 확정한다.</summary>
    public sealed class SettlementView
    {
        public RunSummary Summary;
        public ScoreBreakdown Score;
        public BagLimits Bag;
        /// <summary>가방에 담을 수 있는 후보(빌린 블록 제외).</summary>
        public List<WildBlock> BagCandidates = new List<WildBlock>();
        public int LibraryRoom;
        public bool NewBestScore, NewBestWave;
        public List<string> NewTitles = new List<string>();
    }

    public sealed class SettlementResult
    {
        public long GoldGained;
        public long GoldFromLeftovers;
        public List<WildBlock> Bagged = new List<WildBlock>();
        public List<MaterialGain> Materials = new List<MaterialGain>();
        public List<string> NewArts = new List<string>();
        public List<string> NewTitles = new List<string>();
        public bool RevengeReward;
        public bool PhantomMetNow;
        public bool CrowBundle;
        public bool NewBestScore, NewBestWave;
        public bool ShowEnhanceTutorial;
        /// <summary>자동 정산(중간 지점)으로 만들어진 결과다.</summary>
        public bool FromCheckpoint;
        /// <summary>기록 화면의 판 목록에 붙인 줄.</summary>
        public RunRecordData Record;
    }

    /// <summary>판 종료 → 프로필. 구현 폴더 Meta/Settlement. 재료는 판 씨앗이 아니라 기기 난수로 정산 때 뽑는다.</summary>
    public interface ISettlementService
    {
        BagLimits BagLimitsFor(int clearedDirect);
        /// <summary>그 캐릭터가 고를 수 있는 건너뛰는 수(0, 10, 20 …). 01 요약서 4.6절.</summary>
        List<int> StartSkipOptions(int bestWave);
        /// <summary>skip이 StartSkipOptions(bestWave)에 드는가(T085 "skip 15 거절").</summary>
        bool IsValidStartSkip(int bestWave, int skip);
        SettlementView Prepare(RunSummary summary);
        /// <summary>가방 선택(야생 블록 uid)을 받아 확정한다. 같은 판은 한 번만 정산된다.</summary>
        Result<SettlementResult> Commit(RunSummary summary, IReadOnlyList<int> bagUids);
        /// <summary>자동 가방(등급 높은 순, 영웅 한도 안에서).</summary>
        List<int> AutoPick(SettlementView view);
        /// <summary>중간 지점(웨이브 5의 배수 클리어)에 요약을 프로필에 적어 둔다.</summary>
        void SaveCheckpoint(RunSummary summary);
        /// <summary>지난 실행이 비정상 종료였으면 중간 지점까지 자동 정산한다. 정산했으면 결과를 돌려준다.</summary>
        SettlementResult SettlePendingCheckpoint();
        /// <summary>판 중 해금한 비기를 프로필에 바로 적는다(04 요약서 11.3절). character = 1~5.</summary>
        void RecordArtUnlock(int character, string artId);
    }

    /// <summary>
    /// 메타 서비스 등록 자리(SimModuleFactory와 같은 방식). 구현 작업이 자기 폴더의 `MetaFactory.&lt;이름&gt;.cs`에서
    /// partial 메서드 하나를 구현한다. Bootstrap이 GameContext를 만든 뒤 Fill을 부른다.
    /// </summary>
    public static partial class MetaFactory
    {
        static partial void CreateEnhance(GameContext ctx, ref IEnhanceService s);
        static partial void CreateWands(GameContext ctx, ref IWandService s);
        static partial void CreateShop(GameContext ctx, ref IShopService s);
        static partial void CreateSettlement(GameContext ctx, ref ISettlementService s);

        public static void Fill(GameContext ctx)
        {
            IEnhanceService e = null; CreateEnhance(ctx, ref e); ctx.Enhance = e;
            IWandService w = null; CreateWands(ctx, ref w); ctx.Wands = w;
            IShopService sh = null; CreateShop(ctx, ref sh); ctx.Shop = sh;
            ISettlementService st = null; CreateSettlement(ctx, ref st); ctx.Settlement = st;
        }
    }
}

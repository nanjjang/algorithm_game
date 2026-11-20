using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// data/ 에 없는 수치를 담는 솔로 보충 JSON(Assets/Resources/Data/solo/*.json)의 정의.
// 값은 요약서의 명세 값·[임시] 값 그대로다. 데이터 작업(F1)이 JSON을 만들고 모자란 필드를 더한다.
namespace Wizard.Solo.Data
{
    /// <summary>solo/enhance.json. 07 요약서 2절의 config 키.</summary>
    public sealed class EnhanceConfig
    {
        /// <summary>도전 +1..+13의 기본 확률(%). 첨자 = 현재 단계 0..12.</summary>
        public List<int> BaseProb = new List<int>();
        public int ProbCapPct;
        public int GreatRatioPct;
        public int MutationRatioPct;
        public int AuraStepPct;
        public int AuraMax;
        public int DropSafeMaxStage;
        public int FloorGap;
        public int CostCoeff;
        /// <summary>[4, 8]: 현재 단계 ≤4 하급, ≤8 중급, 그 밖 상급.</summary>
        public List<int> StoneTierBounds = new List<int>();
        public int StonesPerAttempt;
        public int SlotExtraMax;
        public int GreatSlotMinStage;
        /// <summary>변이 보유 상한 기본값(2)과 캐릭터별 예외("char.2" → 3). characters.json max_mutations와 같은 값.</summary>
        public int MutationCapDefault;
        public Dictionary<string, int> MutationCap = new Dictionary<string, int>();
        public int MaxStage;
        /// <summary>단계당 피해 배율 밑(1.15). 전투가 읽는다.</summary>
        public float StageDamageBase;
        /// <summary>07 요약서 4.10절 표(1.15^n, 첨자 = 단계 0..13). 화면 표기("×2.66 → ×3.06")용.</summary>
        public List<float> StageDamageMul = new List<float>();
        public List<int> Milestones = new List<int>();
        public SacrificeConfig Sacrifice = new SacrificeConfig();
        public CarveConfig Carve = new CarveConfig();
        public int MutationRemoveGold;
        [JsonExtensionData] public IDictionary<string, JToken> Extra;

        /// <summary>그 캐릭터의 변이 보유 상한.</summary>
        public int MutationCapFor(string charId)
        {
            int v;
            if (charId != null && MutationCap != null && MutationCap.TryGetValue(charId, out v)) return v;
            return MutationCapDefault;
        }
    }

    public sealed class SacrificeConfig
    {
        public int MaxBlocks;
        public int ScoreCommon;
        public int ScoreRare;
        public int ScoreHero;
        public int SameElement;
        public int Cap;
    }

    public sealed class CarveConfig
    {
        public int MinStage;
        public int BasePct;
        public int StepPct;
        public int GoldCoeff;
        public int HighStones;
    }

    /// <summary>solo/economy.json. 07 요약서 2절·7절·8절.</summary>
    public sealed class EconomyConfig
    {
        public int ShopCharmGold;
        public int ShopCharmWeeklyLimit;
        public int ShopWandPlus0Gold;
        public int ShopLibraryStepBlocks;
        public int ShopLibraryStepGold;
        /// <summary>상점 주의 첫날(System.DayOfWeek 번호, 월요일 = 1). 기기 현지 시간 00:00 경계 [임시 U-07].</summary>
        public int ShopWeekStartDayOfWeek;
        public int LibraryBaseCap;
        public float AppraisalWandFactor;
        public int AppraisalWandPlus0;
        public int ReleaseRefundPct;
        public int CrystalGoldEach;
        /// <summary>07 요약서 7.4절 표: 단계 0..13의 감정가와 떠나보내기 환급(골드).</summary>
        public List<int> AppraisalByStage = new List<int>();
        public List<int> ReleaseRefundByStage = new List<int>();
        [JsonExtensionData] public IDictionary<string, JToken> Extra;
    }

    /// <summary>solo/mutations.json. 02 요약서 9.1절(data/에 없던 변이 수치).</summary>
    public sealed class MutationsConfig
    {
        public float TwistedBarrelDamageMul;
        public int TwistedBarrelSpreadDegrees;
        public float UnstableCoreRadiusMul;
        public int UnstableCoreChancePercent;
        public float UnstableCoreLockSeconds;
        public float UnstableCoreWarnSeconds;
        public float HypersensitiveCritStart;
        public float HypersensitiveLockPlusSeconds;
        public float GluttonyHeatPerKill;
        public float GluttonyHeatPerSecondCap;
        /// <summary>탐식 상한을 세는 창(초, 60틱 합).</summary>
        public float GluttonyWindowSeconds;
        public float GluttonyCoolMul;
        public float MirrorImageHeatMul;
        public int GlassWandCritBonus;
        public int GlassWandMinStage;
        public int GlassWandDropsPerWave;
        [JsonExtensionData] public IDictionary<string, JToken> Extra;
    }

    /// <summary>solo/drops.json. 06 요약서 4.4·4.5·5.1~5.3절.</summary>
    public sealed class DropsConfig
    {
        public int CardsNormal;
        public int CardsElite;
        public int CardsBoss;
        public int CardsPhantomFirst;
        public int CardsPick;
        /// <summary>등급 안 추첨 순서(06 요약서 0.2절). 열쇠 = Ids.Rarity.*.</summary>
        public Dictionary<string, List<string>> RarityOrder = new Dictionary<string, List<string>>();
        /// <summary>보스 카드의 확정 칸이 영웅으로 바뀌는 웨이브(30).</summary>
        public int BossHeroGuaranteeWave;
        public List<CardRarityRowDef> RarityByDay = new List<CardRarityRowDef>();
        public int RerollGold;
        public int StorageCap;
        public int DiscardGoldCommon;
        public int DiscardGoldRare;
        public int DiscardGoldHero;
        public int DiscardGoldBorrowed;
        public int BorrowedBlockWave;
        /// <summary>빌린 블록의 등급(영웅)과 장 수.</summary>
        public string BorrowedBlockRarity;
        public int BorrowedBlockCount;
        public int GoldPerCost;
        public int EliteGoldMul;
        public int WaveClearGold;
        /// <summary>보스 처치 골드 = 이 값 × (9 + w).</summary>
        public int BossGoldPerS10;
        public int HealthShardBpPerCost;
        public int HealthShardEliteBp;
        public int HealthShardPerWave;
        /// <summary>체력 조각 하나가 채우는 칸.</summary>
        public int HealthShardHealCells;
        public int BagWavesPerSlot;
        public int BagSlotCap;
        public int BagWavesPerHero;
        /// <summary>재료 표 6.3-A~D 원문(JSON 그대로). 형이 있는 읽기는 <see cref="MaterialTable"/>.</summary>
        public JObject Materials;
        /// <summary>복수 보상(05 요약서 10.3절)의 귀속 재료. 칭호·머리 장식은 Ids.Title/Ids.Cosmetic.</summary>
        public List<ItemCountDef> RevengeReward = new List<ItemCountDef>();
        [JsonExtensionData] public IDictionary<string, JToken> Extra;

        /// <summary>Materials를 형으로 읽은 것. ContentDb가 읽을 때 채운다(없으면 빈 표).</summary>
        [JsonIgnore] public MaterialsDef MaterialTable = new MaterialsDef();

        /// <summary>그 날(1부터)의 카드 등급 확률 줄: DayFrom ≤ day인 마지막 줄. 없으면 null.</summary>
        public CardRarityRowDef RarityRowForDay(int day)
        {
            CardRarityRowDef best = null;
            if (RarityByDay == null) return null;
            for (int i = 0; i < RarityByDay.Count; i++)
                if (RarityByDay[i].DayFrom <= day) best = RarityByDay[i];
            return best;
        }

        /// <summary>버린 블록·가져가지 못한 야생 블록의 골드(06 요약서 4.4·5.3절).</summary>
        public int DiscardGold(string rarity, bool borrowed)
        {
            if (borrowed) return DiscardGoldBorrowed;
            if (rarity == Ids.Rarity.Hero) return DiscardGoldHero;
            if (rarity == Ids.Rarity.Rare) return DiscardGoldRare;
            return DiscardGoldCommon;
        }
    }

    public sealed class ItemCountDef
    {
        public string Item;
        public int Count;
    }

    /// <summary>재료 표 6.3-A~D(06 요약서 5.2절)의 형. 줄 순서가 추첨 순서다(하급, 중급, 상급, 상급 추가, 부적).</summary>
    public sealed class MaterialsDef
    {
        /// <summary>A. 까마귀 보따리: 프로필에서 처음 웨이브 Wave를 넘긴 판.</summary>
        public CrowBundleDef CrowBundle = new CrowBundleDef();
        /// <summary>B. 하루의 다섯 번째 웨이브(하루 속 순번 FifthWaveSlot).</summary>
        public int FifthWaveSlot;
        public List<MaterialTierDef> FifthWave = new List<MaterialTierDef>();
        /// <summary>C. 보스 처치.</summary>
        public List<MaterialTierDef> BossKill = new List<MaterialTierDef>();
        /// <summary>첫 만남(프로필 첫 웨이브 10): 확정만, 추첨 없음.</summary>
        public List<MaterialRowDef> PhantomFirst = new List<MaterialRowDef>();
        /// <summary>D. 엘리트 1마리(웨이브 EliteMinWave 이상).</summary>
        public int EliteMinWave;
        public List<MaterialTierDef> EliteKill = new List<MaterialTierDef>();
    }

    public sealed class CrowBundleDef
    {
        public int Wave;
        public List<MaterialRowDef> Rows = new List<MaterialRowDef>();
    }

    /// <summary>날 구간(DayFrom..DayTo, DayTo null = 끝없이) 하나의 줄들.</summary>
    public sealed class MaterialTierDef
    {
        public int DayFrom;
        public int? DayTo;
        public List<MaterialRowDef> Rows = new List<MaterialRowDef>();

        public bool Covers(int day) => day >= DayFrom && (!DayTo.HasValue || day <= DayTo.Value);

        /// <summary>그 날에 맞는 구간. 없으면 null(그 날엔 나오지 않음).</summary>
        public static MaterialTierDef ForDay(List<MaterialTierDef> tiers, int day)
        {
            if (tiers == null) return null;
            for (int i = 0; i < tiers.Count; i++)
                if (tiers[i].Covers(day)) return tiers[i];
            return null;
        }
    }

    /// <summary>재료 표 한 줄. Fixed &gt; 0이면 추첨 없이 그 수, 아니면 ChanceBp(d)로 독립 1회(맞으면 1개).</summary>
    public sealed class MaterialRowDef
    {
        /// <summary>mat.stone_low / mat.stone_mid / mat.stone_high / charm.safeguard.</summary>
        public string Item;
        public int Fixed;
        /// <summary>"상급 추가" 줄(보스 d ≥ 3).</summary>
        public bool IsExtra;
        public int BpBase;
        public int BpPerDay;
        public int BpDayOffset;
        public int BpCap;

        [JsonIgnore] public bool IsFixed => Fixed > 0;

        /// <summary>bp = min(BpCap, BpBase + BpPerDay × (d − BpDayOffset)). 0 아래면 0(추첨 생략). 확정 줄은 0.</summary>
        public int ChanceBp(int day)
        {
            if (IsFixed) return 0;
            int bp = BpBase + BpPerDay * (day - BpDayOffset);
            if (bp > BpCap) bp = BpCap;
            return bp < 0 ? 0 : bp;
        }
    }

    public sealed class CardRarityRowDef
    {
        /// <summary>이 날(1부터) 이상에 적용. 마지막 줄이 "4일째 이후".</summary>
        public int DayFrom;
        public int Common;
        public int Rare;
        public int Hero;
    }

    /// <summary>solo/camp.json. 06 요약서 4.1·4.3절, 04 요약서 11절.</summary>
    public sealed class CampConfig
    {
        public int HealBase;
        public int HealPerWave;
        public int HealRepeatMulNum;
        public int HealRepeatMulDen;
        public int DawnFreeHealCells;
        public float DepartHoldSeconds;
        public float WaveClearSeconds;
        public float TodTransitionSeconds;
        public float NarrowFieldPreludeSeconds;
        public int ArtUnlockLastWave;
        public int ArtCandidates;
        /// <summary>회복 1회가 채우는 칸.</summary>
        public int HealCells;
        public float DownedToSettlementSeconds;
        /// <summary>모닥불 자리 = 전장 가운데. 그 옆에서 J가 먹는 거리(칸). 솔로 임시(00 문서 D-52).</summary>
        public float FireInteractRadius;
        /// <summary>출발 표식 자리 = 모닥불 + 이 오프셋(칸, 시뮬레이션 좌표)과 J가 먹는 거리. 솔로 임시(D-52).</summary>
        public float DepartMarkerOffsetX;
        public float DepartMarkerOffsetY;
        public float DepartMarkerRadius;
        /// <summary>보스를 잡은 웨이브의 WaveClearing 길이(초). 사망 느린 연출 1.0초 + 드랍 1.0초. 솔로 임시(D-58).</summary>
        public float BossClearSeconds;
        /// <summary>복수 처치 웨이브의 WaveClearing 길이(틱, 05 요약서 10.3절 264)와 J로 넘길 수 있게 되는 틱(84).</summary>
        public int RevengeClearTicks;
        public int RevengeSkipAfterTicks;
        [JsonExtensionData] public IDictionary<string, JToken> Extra;
    }

    /// <summary>solo/const.json. 명세 본문에만 있고 data/에 없는 전투 상수. 02·03·05·10 요약서.</summary>
    public sealed class SoloConstDef
    {
        public float DepthDecayDefault;
        public float DepthDecayTrait;
        public float UnderlineMaxCut;
        public int TriggerMaxDepth;
        public int TreeShotCap;
        public int PlayerShotCap;
        public int EnemyShotCap;
        public int ArtShotCap;
        public float ComboIdleSeconds;
        public int ComboPerShotCap;
        public int ComboCritGain;
        public int GrazeComboGain;
        public float DiagonalHoldSeconds;
        public float ContactIntervalSeconds;
        public float SpawnNoticeSeconds;
        public float PlayerSlowOnFrostPercent;
        public int MaxInputEventsPerTick;
        public float OverflowRadius;
        public float OverflowDamageT;
        public float OverflowKnockback;
        public float LightningRodRadius;
        public int LightningRodTargets;
        public float LightningRodDamageT;
        public int ThinIceHits;
        public float ThinIceFreezeSeconds;
        public float ThinIceImmuneSeconds;
        public float ShadowContractPerCrystal;
        public float FreezeImmuneSeconds;
        public float StunDamageTakenMul;
        public float ArmorDamageMul;
        /// <summary>복수 추적기의 "금 간 거울" 문턱(%). 05 요약서 10.4절 50.</summary>
        public int RevengeCrackedPercent;
        [JsonExtensionData] public IDictionary<string, JToken> Extra;
    }

    /// <summary>solo/practice.json. 허수아비 연습·튜토리얼 방. 08 요약서 6.4·8.5절.</summary>
    public sealed class PracticeConfig
    {
        public EnemyDef Dummy;
        /// <summary>튜토리얼 방을 열 때 보관함에 넣어 주는 블록(5단계 "블록 끼우기"의 재료). 솔로 임시(00 문서 D-59).</summary>
        public string TutorialBlockId;
        /// <summary>허수아비 수치(PracticeReport)를 내는 주기(초)와 "최근 피해" 창(초, 08 요약서 8.5절 10초).</summary>
        public float ReportIntervalSeconds;
        public float RecentDamageWindowSeconds;
        [JsonExtensionData] public IDictionary<string, JToken> Extra;
    }
}

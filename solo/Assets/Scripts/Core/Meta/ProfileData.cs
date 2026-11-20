using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// 오프라인 프로필 저장 형식(07 요약서 9.3절 + 10 요약서 7절). JSON은 snake_case — 저장 구현(F4)이
// SnakeCaseNamingStrategy(사전 열쇠는 그대로)로 잇는다. id는 정본 id만 쓴다. 필드는 더하기만 한다.
namespace Wizard.Solo.Meta
{
    public sealed class ProfileData
    {
        public int SchemaVersion = 1;
        public string DataVersion;
        /// <summary>"Offline". 서버 프로필과 섞지 않는다(옮기는 기능 없음).</summary>
        public string ProfileKind = "Offline";
        public string CreatedAt;
        public List<CharacterRecord> Characters = new List<CharacterRecord>();
        public List<WandData> Wands = new List<WandData>();
        public CurrencyData Currencies = new CurrencyData();
        /// <summary>열쇠 = 정본 id(mat.stone_low, charm.safeguard, block.*).</summary>
        public Dictionary<string, ItemStack> Items = new Dictionary<string, ItemStack>();
        public int LibraryExpansions;
        public ShopWeekData ShopWeek = new ShopWeekData();
        public ProfileFlags Flags = new ProfileFlags();
        public RevengeData Revenge = new RevengeData();
        public TitlesData Titles = new TitlesData();
        public CosmeticsData Cosmetics = new CosmeticsData();
        public FeatsData Feats = new FeatsData();
        public WeeklyData Weekly = new WeeklyData();
        /// <summary>비정상 종료 대비 중간 지점(5웨이브마다). 정상 정산되면 null.</summary>
        public RunCheckpointData RunCheckpoint;
        public List<JObject> EnhanceLogTail = new List<JObject>();
        public long NextWandId = 1;
        public long NextAttemptId = 1;
        public int RunsStarted;
        /// <summary>마지막으로 정산한 판의 RunId(같은 판을 두 번 정산하지 않는다. 자동 정산 포함).</summary>
        public string LastSettledRunId;
        /// <summary>판 목록(기록 화면). 정산할 때 맨 뒤에 붙이고 상한을 넘으면 가장 오래된 것을 지운다(상한은 DataSolo/G11.json, 00 문서 D-55).</summary>
        public List<RunRecordData> RunHistory = new List<RunRecordData>();
        /// <summary>알아낸 약점·저항(Wizard.Solo.Sim.AffinityKey). 정산 때 판의 것을 합친다(00 문서 D-54).</summary>
        public List<string> KnownAffinities = new List<string>();
        /// <summary>원정 준비에서 마지막으로 고른 캐릭터(1~5). 메인 메뉴 "연습"·"조립과 서고"의 처음 캐릭터(00 문서 D-56).</summary>
        public int LastCharacter = 1;
        /// <summary>도전 과제(DataSolo/Challenges.json). 이룬 과제 id와 이룬 날. 옛 저장에는 없어 빈 값으로 읽힌다(더하기만).</summary>
        public ChallengesData Challenges = new ChallengesData();
        [JsonExtensionData] public IDictionary<string, JToken> Extra;

        public CharacterRecord Character(int number) => Characters.Find(c => c.Id == number);
        public WandData Wand(long wandId) => Wands.Find(w => w.Id == wandId);
        public ItemStack Item(string id)
        {
            if (!Items.TryGetValue(id, out var s)) { s = new ItemStack(); Items[id] = s; }
            return s;
        }
        [JsonIgnore] public RevengeState RevengeState =>
            !Flags.PhantomMet ? Wizard.Solo.RevengeState.BeforeMeeting
            : Flags.RevengeDoneAt == null ? Wizard.Solo.RevengeState.Pursuing : Wizard.Solo.RevengeState.Done;
    }

    public sealed class CharacterRecord
    {
        /// <summary>1~5.</summary>
        public int Id;
        public long EquippedWandId;
        /// <summary>해금한 비기 정본 id(시작 3종 포함해 적는다).</summary>
        public List<string> UnlockedArts = new List<string>();
        public long BestScore;
        /// <summary>직접 넘긴 최고 웨이브(시작 웨이브 한도 계산용).</summary>
        public int BestWave;
        /// <summary>누적 넘긴 웨이브(야생 물 단계 0/100/500/2000).</summary>
        public int TotalWaves;
        public int WildStainStageMax = 1;
        public int WildStainWorn = 1;
        /// <summary>누적 스침·처치·엘리트 처치(정산 때 판의 웨이브 결과를 더한다). 도전 과제가 읽는다. 옛 저장에는 없어 0.</summary>
        public int TotalGrazes;
        public int TotalKills;
        public int TotalElites;
        /// <summary>이 캐릭터로 웨이브 20 허상을 잡은 적이 있다(캐릭터별 도전 과제).</summary>
        public bool RevengeDone;
        /// <summary>한 판에서 가장 많이 스친 수, 한 판에서 맞지 않고 넘긴 웨이브 수의 최고, 맞지 않고 잡은 보스 웨이브 수(도전 과제).</summary>
        public int BestRunGrazes;
        public int BestRunNoHitWaves;
        public int BossNoHitClears;
        /// <summary>판 끝 요약의 개인 최고(표시 통계 — 정산 화면이 판 끝에 적는다): 한 방 피해, 스침 연속, 콤보.</summary>
        public int BestHit;
        public int BestGrazeChain;
        public int BestCombo;
        /// <summary>원정 준비에서 마지막으로 고른 기본 공격 유형(핵 블록 id, block.core.*). 옛 저장에는 없어 null = 그 캐릭터의 A 유형.</summary>
        public string AttackStyle;
    }

    public sealed class WandData
    {
        public long Id;
        /// <summary>1~5. 이 캐릭터만 쓴다.</summary>
        public int Character;
        public int Stage;
        public int FloorPeak;
        public int RecordPeak;
        public int DropCount;
        public int Aura;
        public int SlotsExtra;
        public int CarveBonus;
        /// <summary>받은 순서(앞이 가장 오래된 것).</summary>
        public List<MutationEntry> Mutations = new List<MutationEntry>();
        public MutationOffer PendingOffer;
        public LoadoutData Loadout = new LoadoutData();
        public string SkinId;
        public bool ShowScars;
        /// <summary>"Starter" / "Shop".</summary>
        public string Origin = "Starter";
        public int OwnerCount = 1;
        public string AcquiredAt;
        public int Version = 1;

        /// <summary>이 지팡이의 강화 기록(시도·결과 수, 도전 단계별 표기 확률 합과 성공 수). 강화 서비스가 시도마다 적는다. 옛 저장에는 없어 빈 기록.</summary>
        public WandEnhanceStats EnhanceStats = new WandEnhanceStats();

        public bool HasMutation(string mutationId) => Mutations.Exists(m => m.Id == mutationId);
    }

    /// <summary>
    /// 지팡이 한 자루의 강화 기록(강화 화면의 기록 칸). 확률·판정에는 쓰지 않는다 — 보여 주기만 한다.
    /// 성공 = 성공 + 대성공, 유지 = 하락 없음 + 부적이 막음 + (거절한) 변이가 하락 없이 끝남, 하락 = 단계가 내려감, 변이 = 받은 변이.
    /// </summary>
    public sealed class WandEnhanceStats
    {
        public int Attempts;
        public int Successes;
        public int GreatSuccesses;
        public int Keeps;
        public int Drops;
        public int Mutations;
        /// <summary>도전 단계(+1~+13)별. 순서 없음 — Target으로 찾는다.</summary>
        public List<StageEnhanceStats> Stages = new List<StageEnhanceStats>();

        public StageEnhanceStats Stage(int target)
        {
            if (Stages == null) Stages = new List<StageEnhanceStats>();
            for (int i = 0; i < Stages.Count; i++) if (Stages[i] != null && Stages[i].Target == target) return Stages[i];
            var s = new StageEnhanceStats { Target = target };
            Stages.Add(s);
            return s;
        }

        public StageEnhanceStats Find(int target)
        {
            if (Stages == null) return null;
            for (int i = 0; i < Stages.Count; i++) if (Stages[i] != null && Stages[i].Target == target) return Stages[i];
            return null;
        }
    }

    public sealed class StageEnhanceStats
    {
        /// <summary>도전 단계(이 시도가 성공하면 닿는 단계 = 시도 전 단계 + 1).</summary>
        public int Target;
        public int Attempts;
        /// <summary>성공 + 대성공.</summary>
        public int Successes;
        /// <summary>시도마다 화면에 보인 성공 확률(%)의 합. 평균 표기 확률 = 합 ÷ 시도.</summary>
        public long ShownPctSum;
    }

    /// <summary>도전 과제 진행(이룬 것만 적는다 — 진행 값은 캐릭터 기록에서 다시 계산한다).</summary>
    public sealed class ChallengesData
    {
        public List<string> Done = new List<string>();
        /// <summary>Done과 같은 순서의 이룬 시각(기기 현지, ISO 8601).</summary>
        public List<string> DoneAt = new List<string>();
        [JsonExtensionData] public IDictionary<string, JToken> Extra;

        public bool IsDone(string id) => Done != null && Done.Contains(id);
    }

    public sealed class MutationEntry
    {
        public string Id;
        public long Seq;
    }

    /// <summary>결정 대기 중인 변이 제시. 07 요약서 3.1절·4.6절.</summary>
    public sealed class MutationOffer
    {
        public long AttemptId;
        public string Candidate;
        public string WouldPushOut;
        /// <summary>거절하면 받는 결과: "FailDrop" / "FailCharmBlocked" / "FailNoDrop".</summary>
        public string FallbackOutcome;
        public int FallbackStageAfter;
        public bool FallbackCharmConsumed;
        public int FallbackAuraAfter;
        /// <summary>그 시도에서 뽑힌 값(만분율)과 확률. 변이 결정의 결과 꾸러미가 그대로 되돌려 준다. 옛 저장에는 없어 −1/0.</summary>
        public int RollBp = -1;
        public int SuccessPct;
        public int GreatBp;
        public int MutationEndBp;
    }

    /// <summary>출발 조립. Links 길이 = base_link_slots, Extra 길이 = slots_extra. 빈 칸은 null.</summary>
    public sealed class LoadoutData
    {
        public string Core;
        public List<string> Links = new List<string>();
        public List<string> Extra = new List<string>();

        /// <summary>연결 칸 전체(기본 + 확장)를 한 배열로.</summary>
        public List<string> AllLinks()
        {
            var all = new List<string>(Links.Count + Extra.Count);
            all.AddRange(Links);
            all.AddRange(Extra);
            return all;
        }
        public LoadoutData Clone() =>
            new LoadoutData { Core = Core, Links = new List<string>(Links), Extra = new List<string>(Extra) };
    }

    public sealed class CurrencyData
    {
        public long Gold;
    }

    public sealed class ItemStack
    {
        public int Qty;
        /// <summary>귀속분(복수 보상). 솔로에선 "강화에서 먼저 쓰임" 순서만 뜻이 있다. Qty에 포함된다.</summary>
        public int QtyBound;
        /// <summary>블록만: 지팡이에 끼워 둔 개수. Qty에 포함된다.</summary>
        public int QtySocketed;
    }

    public sealed class ShopWeekData
    {
        /// <summary>기기 현지 시간 월요일 00:00(ISO 날짜).</summary>
        public string WeekStart;
        public int CharmBought;
    }

    public sealed class ProfileFlags
    {
        public bool PhantomMet;
        public string RevengeDoneAt;
        public bool CrowBundleGiven;
        public bool EnhanceTutorialShown;
        public bool TutorialDummyDone;
        public bool OfflineGuideSeen;
        /// <summary>하급 강화석을 한 번이라도 받았다(정산의 강화 안내를 처음 한 번만 띄운다). 07 요약서 9절 보충.</summary>
        public bool LowStoneReceived;
        /// <summary>문맥 힌트(Screens/Hints)를 보인 횟수(힌트 id → 횟수). 처음 한 번 힌트는 1이면 다시 띄우지 않는다. 설정·도감의 [힌트 다시 보기]가 비운다.</summary>
        public Dictionary<string, int> HintsSeen = new Dictionary<string, int>();
        [JsonExtensionData] public IDictionary<string, JToken> Extra;
    }

    public sealed class RevengeData
    {
        public int PhantomTries;
        public int PhantomBestDent;
        public int RevengeRuns;
        public int RunsSinceFirstMeet;
    }

    public sealed class TitlesData
    {
        public List<string> Owned = new List<string>();
        public string Equipped;
    }

    public sealed class CosmeticsData
    {
        public List<string> Owned = new List<string>();
        public Dictionary<string, string> Equipped = new Dictionary<string, string>();
    }

    public sealed class FeatsData
    {
        public int BloomCount;
        [JsonExtensionData] public IDictionary<string, JToken> Extra;
    }

    public sealed class WeeklyData
    {
        public string IsoWeek;
        public int RunsThisWeek;
        public long BestScore;
    }

    /// <summary>기록 화면의 판 목록 한 줄(08 요약서 11.1절).</summary>
    public sealed class RunRecordData
    {
        public string RunId;
        /// <summary>정산한 시각(기기 현지, ISO 8601).</summary>
        public string Date;
        /// <summary>1~5.</summary>
        public int Character;
        public int ReachedWave;
        public int StartWave;
        public long Score;
        public int WandStage;
        /// <summary>판 끝의 조립 블록 id(코어 먼저, 빈 칸 null).</summary>
        public List<string> Loadout = new List<string>();
        /// <summary>"HpZero" / "Abandon" / "Checkpoint"(자동 정산).</summary>
        public string EndReason;
    }

    /// <summary>중간 지점 요약(자동 정산에 필요한 만큼). 내용은 정산 작업이 RunSummary를 직렬화해 넣는다.</summary>
    public sealed class RunCheckpointData
    {
        public string RunId;
        public int Wave;
        public JObject Summary;
    }

    /// <summary>
    /// 기기 단위 설정(프로필과 따로 저장). 이 파일이 없으면 기기 첫 실행이다. 08 요약서 12절.
    /// 옵션 표(12.2절)의 값은 전부 Options에 있다(열쇠 = OptionKeys.*, 값의 꼴·기본값·범위 = SettingsOptions). 직접 읽지 말고
    /// SettingsOptions.Int/Bool/Str/Set을 쓴다 — 없는 열쇠는 기본값이 나온다.
    /// </summary>
    public sealed class SettingsData
    {
        public int SchemaVersion = 1;
        /// <summary>논리 키 번호(0~8)별 기본·보조 물리 키(UnityEngine.InputSystem.Key의 정수값). 0 = 없음.
        /// 지금 쓰는 배치다(key_layout이 default·alternate면 그 표의 값, custom이면 내 배치). Pause의 Esc는 여기 넣지 않는다(메뉴 길의 고정 키).</summary>
        public List<int> KeyPrimary = new List<int>();
        public List<int> KeySecondary = new List<int>();
        /// <summary>옵션 표의 값. 열쇠 = OptionKeys.*.</summary>
        public Dictionary<string, JToken> Options = new Dictionary<string, JToken>();
    }
}

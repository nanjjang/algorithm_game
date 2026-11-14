using System.Collections.Generic;
using UnityEngine;

// 시뮬레이션은 메타(프로필) 형을 모른다. 프로필 → 판 설정 변환은 메타 쪽(IWandService)이 한다.
namespace Wizard.Solo.Sim
{
    /// <summary>판에 들고 나가는 조립(블록 id만). Links 길이 = 연결 칸 총수 L(기본 + 확장), 빈 칸은 null.</summary>
    public sealed class LoadoutSpec
    {
        public string Core;
        public List<string> Links = new List<string>();
        public LoadoutSpec Clone() => new LoadoutSpec { Core = Core, Links = new List<string>(Links) };
    }

    /// <summary>약점·저항 "알아냄" 기록의 열쇠(00 문서 D-54). 예: "enemy.slime_moss|weak".</summary>
    public static class AffinityKey
    {
        public static string Of(string speciesId, Affinity affinity) => speciesId + (affinity == Affinity.Weak ? "|weak" : "|resist");
    }

    /// <summary>판 하나의 출발 정보. 원정 준비 화면이 만들어 세션에 넘긴다. 01 요약서 4.5절.</summary>
    public sealed class RunConfig
    {
        public RunMode Mode = RunMode.Endless;
        public ulong Seed;
        public string RunId;
        public string CharId;
        public long WandId;
        public int WandStage;
        public int LinkSlotCount;
        public List<string> Mutations = new List<string>();
        /// <summary>출발 조립(서고 블록). 연습·튜토리얼이면 연습할 조립(야영에서 연 연습은 지금 판의 조립 — 야생 블록 id 포함).</summary>
        public LoadoutSpec Loadout;
        /// <summary>프로필에 남아 있는 해금 비기(시작 3종 포함).</summary>
        public List<string> UnlockedArts = new List<string>();
        /// <summary>첫 웨이브(1, 11, 21 …). 1보다 크면 출발 야영에서 시작한다.</summary>
        public int StartWave = 1;
        /// <summary>웨이브 10을 허상 첫 만남으로 치른다(= 출발 때 프로필의 phantom_met == false).</summary>
        public bool PhantomFirst;
        /// <summary>복수 중(첫 만남 뒤 ~ 웨이브 20 허상 첫 처치 전)으로 출발했다.</summary>
        public bool RevengePending;
        /// <summary>프로필이 처음 웨이브 3을 넘기면 까마귀 보따리 기회를 적는다.</summary>
        public bool CrowBundlePending;
        /// <summary>프로필이 이미 알아낸 약점·저항(AffinityKey). 판 시작 때 RunState.AffinityKnown으로 복사한다.</summary>
        public List<string> KnownAffinities = new List<string>();
        // 연습 방 옵션의 처음 값(08 요약서 8.5절). 판 중에는 RunState.Practice*가 정본이다(PracticeSetOption 명령).
        public bool PracticeInfiniteCrystal;
        public bool PracticeEnemyFire;
        public bool PracticeHeatFreeze;
        /// <summary>Mode == Tutorial일 때 시작 단계(1~5, 보통 1). 지금 단계는 RunState.TutorialStep.</summary>
        public int TutorialStep = 1;
    }

    /// <summary>판 중 얻은 야생 블록. 06 요약서 4.4절.</summary>
    public sealed class WildBlock
    {
        public int Uid;
        public string BlockId;
        public string Rarity;
        public bool Borrowed;
        public int AcquiredWave;
        /// <summary>출발 조립에서 뺀 서고 블록이다(RunState.LibraryBench에만 있다). 보관함 12칸·귀환 가방·버림 골드·판 끝 요약에 들지 않는다.</summary>
        public bool FromLibrary;
    }

    public sealed class LootCard
    {
        public string BlockId;
        public string Rarity;
        /// <summary>확정 등급 칸(다시 뽑아도 등급이 유지된다).</summary>
        public bool Guaranteed;
        public bool Picked;
    }

    /// <summary>전리품 고르기 한 번의 상태.</summary>
    public sealed class LootOffer
    {
        public int Wave;
        public readonly List<LootCard> Cards = new List<LootCard>();
        public int PickCount;
        public int Rerolls;
        /// <summary>그냥 받는 빌린 블록(1일째 웨이브 6). 없으면 null.</summary>
        public string BorrowedBlockId;
        /// <summary>보관함이 넘쳐 버릴 블록을 골라야 하는 수.</summary>
        public int OverflowToDiscard;
    }

    /// <summary>비기 해금 차례 하나. 04 요약서 11.3절.</summary>
    public sealed class ArtOffer
    {
        /// <summary>차례 번호 = 넘긴 웨이브 번호.</summary>
        public int TurnWave;
        public readonly List<string> Candidates = new List<string>();
    }

    public enum RunCommandKind
    {
        /// <summary>전리품(06 요약서 4절). LootPick·LootUnpick: A = 카드 번호. LootConfirm: 고른 카드와 빌린 블록을 Storage에 넣는다 —
        /// 12칸을 넘으면 Loot.OverflowToDiscard = 넘친 수로 두고 LootPick 단계에 머문다. LootDiscard: A = 보관함 자리(100 + i 또는 i),
        /// 넘친 수가 0이 되면 Camp로 간다.</summary>
        LootPick, LootUnpick, LootReroll, LootDiscard, LootConfirm,
        /// <summary>CampOpen: 야영 화면을 연다(마우스·테스트·화면이 다시 열 때). 모닥불 옆 J는 명령이 아니라 Run이 직접 읽는다.</summary>
        CampOpen, CampClose, CampHeal, CampDepart,
        /// <summary>조립: A = 원본 자리, B = 대상 자리(아래 SlotRef 규칙). Slot = 플레이어. CampHeal도 Slot = 플레이어.</summary>
        LoadoutMove, LoadoutRemoveAll,
        /// <summary>비기 해금 고르기: Slot = 플레이어, Id = 비기 id.</summary>
        ArtPick,
        GiveUp,
        /// <summary>PracticeSetOption: A = PracticeOption, B = 1 켬 / 0 끔.</summary>
        PracticeReset, PracticeSetOption,
        /// <summary>화면이 다음 튜토리얼 단계로 넘긴다(완료 뒤 넘김과 건너뛰기 둘 다). A = 넘어갈 단계(6 = 끝).</summary>
        TutorialAdvance
    }

    /// <summary>
    /// 화면 조작으로 생기는 판 상태 전이. 화면은 SimWorld.Enqueue(cmd)만 하고 0단계에서 RunFlow가 적용한다.
    /// 자리(SlotRef) 규칙: 0 = 코어, 1..L = 연결 칸, 100 + i = 보관함(RunState.Storage) i번째,
    /// 300 + i = 서고 블록 대기 칸(RunState.LibraryBench) i번째, -1 = 없음(빼기의 대상: 야생 블록은 보관함으로, 서고 블록은 대기 칸으로 간다).
    /// 조립 명령(LoadoutMove, LoadoutRemoveAll)은 Camp·StartCamp와 Practice(연습·튜토리얼 방) 단계에서 받는다.
    /// 서고(Library·LoadoutEdit) 모드의 화면은 명령을 쓰지 않고 IWandService.SetLoadout을 부른다.
    /// </summary>
    public struct RunCommand
    {
        public RunCommandKind Kind;
        public int Slot;
        public int A, B;
        public string Id;
        public RunCommand(RunCommandKind kind, int a = 0, int b = 0, string id = null, int slot = 0)
        { Kind = kind; A = a; B = b; Id = id; Slot = slot; }
    }

    /// <summary>웨이브 구성 한 줄.</summary>
    public struct WaveEntry
    {
        public string Species;
        public bool Elite;
        public bool OutsideBudget;
        /// <summary>묶음 번호(0부터).</summary>
        public int Bundle;
    }

    /// <summary>다음 웨이브의 구성. 야영에 들어갈 때 미리 만든다(06 요약서 3.2절).</summary>
    public sealed class WavePlan
    {
        public int Wave;
        public string Tod;
        public string Frame;
        public readonly List<string> Whims = new List<string>();
        public readonly List<WaveEntry> Entries = new List<WaveEntry>();
        public int BundleCount;
        /// <summary>보스 웨이브면 보스 id.</summary>
        public string BossId;
        public bool PhantomFirst;
        /// <summary>야영 미리보기에 띄울 종(중복 없이).</summary>
        public readonly List<string> PreviewSpecies = new List<string>();
        public int Budget;
        public bool HasWhim(string whimId) => Whims.Contains(whimId);
    }

    /// <summary>한 웨이브의 점수 재료. 01 요약서 6.2절.
    /// 채우는 곳: Wave·IsBoss·PhantomFirst·Whims = Waves.BeginWave(구성에서) / KillScoreX2·KillCount·ElitesKilled·BossKilled·NoHit·Grazes = Run(훅) /
    /// ElapsedTicks = Run이 클리어 처리에서 w.Wave.WaveTick을 적은 뒤 Score.WaveScore를 부른다.</summary>
    public sealed class WaveStats
    {
        public int Wave;
        public bool IsBoss;
        public bool PhantomFirst;
        /// <summary>처치 원점수 합 × 2(절반 처치를 정수로 담기 위해 2배로 든다): Σ cost × 10 × (지체 뒤 ? 1 : 2).</summary>
        public int KillScoreX2;
        public bool BossKilled;
        public int ElapsedTicks;
        public bool NoHit = true;
        public int Grazes;
        public int ElitesKilled;
        public int KillCount;
        public readonly List<string> Whims = new List<string>();
    }

    public struct ScoreBreakdown
    {
        public int Kills, Boss, Clear, Fast, NoHit, Graze, Variance, PhantomFirst, Skipped, Total;
    }

    public sealed class WaveResult
    {
        public int Wave;
        public WaveStats Stats;
        public ScoreBreakdown Score;
        public int GoldEarned;
    }

    public struct DropChance
    {
        public DropChanceSource Source;
        public int Wave;
        public int Count;
    }

    /// <summary>진행 중인 웨이브의 런타임 상태. 주인: Waves 모듈(읽기는 누구나).</summary>
    public sealed class WaveState
    {
        /// <summary>Waves가 소유하는 웨이브별 보조 상태.</summary>
        public object Scratch;
        public int Wave;
        public WavePlan Plan;
        /// <summary>점수용 시계. WaveCombat 동안만 흐른다.</summary>
        public int WaveTick;
        public int StartTick;
        public int NextBundle;
        public int LastBundleTick;
        public readonly List<WaveEntry> SpawnQueue = new List<WaveEntry>();
        public bool Stalled;
        public bool AllReleased;
        /// <summary>동시 예고 상한. 0 이하 = 상한 없음(BeginWave를 거치지 않은 연습 방·테스트).</summary>
        public int TelegraphCap;
        // 변덕이 바꾸는 값(없으면 기본)
        public float EnemyHpMul = 1f;
        public int BundleIntervalTicks;
        public float EnemyShotCountMul = 1f;
        public float EnemyShotSpeedMul = 1f;
        public bool LastBreath;
        public bool HealthShardDropped;
        public WaveStats Stats = new WaveStats();
        /// <summary>진행 바: 남은 적 체력 합 / 처음 합(0..1).</summary>
        public float RemainingRatio = 1f;
    }

    /// <summary>판 전체 상태. 주인: Run 모듈(RunFlow). 01·06 요약서.</summary>
    public sealed class RunState
    {
        public RunConfig Config;
        public RunPhase Phase = RunPhase.NotStarted;
        public int PhaseStartTick;
        /// <summary>웨이브 번호(누적). WaveClearing·LootPick 동안은 방금 넘긴 웨이브, 야영(Camp·StartCamp) 동안은 다음에 싸울 웨이브.</summary>
        public int Wave;
        /// <summary>마지막으로 시작한 웨이브(도달 웨이브).</summary>
        public int ReachedWave;
        /// <summary>직접 넘긴 웨이브 수(건너뛴 것 제외).</summary>
        public int ClearedDirect;
        /// <summary>직접 넘긴 마지막 웨이브 번호(없으면 0).</summary>
        public int LastClearedWave;
        /// <summary>원정 지갑.</summary>
        public int Purse;
        /// <summary>판 점수 합계(건너뛴 점수 SkippedScore 포함).</summary>
        public int Score;
        public int SkippedScore;
        public readonly List<WaveResult> WaveResults = new List<WaveResult>();
        public readonly List<DropChance> DropChances = new List<DropChance>();
        /// <summary>보관함(12칸). 야생 블록만.</summary>
        public readonly List<WildBlock> Storage = new List<WildBlock>();
        /// <summary>출발 조립에서 뺀 서고 블록의 대기 칸(상한 없음, FromLibrary = true). 판이 끝나면 버려지고 출발 조립은 프로필에 그대로다.</summary>
        public readonly List<WildBlock> LibraryBench = new List<WildBlock>();
        public int NextWildUid = 1;
        public LootOffer Loot;
        public WavePlan NextPlan;
        /// <summary>야영 하위 상태. Camp와 StartCamp 둘 다 쓴다.</summary>
        public CampSubState CampSub;
        /// <summary>모닥불·출발 표식의 자리(판정 좌표)와 J가 먹는 거리. Run이 야영에 들어갈 때 camp.json 값으로 정하고 표시는 이것만 읽는다.</summary>
        public Vector2 CampFirePos;
        public Vector2 DepartMarkerPos;
        public float CampInteractRadius;
        public float DepartMarkerRadius;
        public int HealsThisCamp;
        public bool DawnCamp;
        public int DepartHoldStartTick = -1;
        /// <summary>남은 비기 해금 차례(웨이브 번호). 야영을 떠나기 전 다 골라야 한다.</summary>
        public readonly Queue<int> PendingArtTurns = new Queue<int>();
        public ArtOffer CurrentArtOffer;
        public readonly List<string> NewArtsThisRun = new List<string>();
        public RunEndReason EndReason;
        public bool PhantomFirstDone;
        /// <summary>복수 중인 판인가(판 안의 실효 값) = Config.RevengePending || PhantomFirstDone. Run이 판 시작과 첫 만남 일어남 때 정한다.
        /// 복수 꼬리표·RevengeKill(Bosses), HUD의 허상까지 n웨이브, 야영·정산의 추적기는 이 값을 읽는다.</summary>
        public bool RevengeActive;
        /// <summary>이번 WaveClearing의 길이(틱). 보통 camp.json wave_clear_seconds, 보스 처치는 boss_clear_seconds, 복수 처치는 revenge_clear_ticks.</summary>
        public int ClearingTicks;
        /// <summary>복수 처치가 난 틱(-1 = 없음). 그 뒤 revenge_skip_after_ticks가 지나면 J 누름(JPressedThisTick)으로 WaveClearing을 끝낸다.</summary>
        public int RevengeKillTick = -1;
        /// <summary>이 판이 알고 있는 약점·저항(AffinityKey). Spells의 피해 함수가 Weak/Resist 첫 적중 때 더한다.</summary>
        public readonly HashSet<string> AffinityKnown = new HashSet<string>();
        /// <summary>튜토리얼의 지금 단계(1~5, 6 = 끝). Mode == Tutorial일 때만.</summary>
        public int TutorialStep;
        // 연습 방 옵션의 지금 값(주인 Run. 읽는 곳: InfiniteCrystal·HeatFreeze = Player, EnemyFire = Enemies의 허수아비 두뇌).
        public bool PracticeInfiniteCrystal;
        public bool PracticeEnemyFire;
        public bool PracticeHeatFreeze;
        public bool KilledPhantomAtWave20;
        /// <summary>웨이브 20 허상 싸움을 시작했다(도전 횟수 +1) / 깎은 최대 %.</summary>
        public bool PhantomAttempted;
        public int PhantomDentPercent;
        public bool CrowBundleEarned;
        public int BloomCasts;
        public int TotalKills;
        public bool GaveUp;
    }

    /// <summary>판이 끝났을 때 정산에 넘기는 요약(시뮬레이션 → 메타). 중간 지점 저장에도 같은 꼴을 쓴다.</summary>
    public sealed class RunSummary
    {
        public string RunId;
        public string CharId;
        public long WandId;
        public RunMode Mode;
        /// <summary>출발 때 지팡이 단계, 판 끝의 조립 블록 id(코어 먼저, 빈 칸 null) — 기록 화면의 판 목록용.</summary>
        public int WandStage;
        public List<string> LoadoutIds = new List<string>();
        public int StartWave;
        public int ReachedWave;
        public int ClearedDirect;
        public int LastClearedWave;
        public RunEndReason EndReason;
        public int Score;
        public int SkippedScore;
        public int Purse;
        public List<WaveResult> WaveResults = new List<WaveResult>();
        public List<DropChance> DropChances = new List<DropChance>();
        /// <summary>판 끝에 들고 있는 야생 블록(보관함 + 지팡이에 끼운 것). 빌린 블록 포함. 서고 블록(WildUid 0, LibraryBench)은 넣지 않는다.</summary>
        public List<WildBlock> WildBlocks = new List<WildBlock>();
        public List<string> NewArts = new List<string>();
        public bool PhantomFirst;
        public bool PhantomFirstDone;
        /// <summary>출발 때 복수 중이었나(RunConfig 값 그대로).</summary>
        public bool RevengePending;
        /// <summary>판 끝의 RunState.RevengeActive(이 판에서 첫 만남을 치른 경우 포함). 복수 보상·추적기 판단은 이 값으로 한다.</summary>
        public bool RevengeActive;
        /// <summary>판 끝의 RunState.AffinityKnown(정산이 프로필에 합친다).</summary>
        public List<string> AffinityKnown = new List<string>();
        public bool KilledPhantomAtWave20;
        public bool PhantomAttempted;
        public int PhantomDentPercent;
        public bool CrowBundleEarned;
        public int BloomCasts;
        public int TotalKills;
    }
}

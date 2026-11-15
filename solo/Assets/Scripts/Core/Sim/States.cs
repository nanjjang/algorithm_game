using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Data;

// 시뮬레이션 상태. 전부 평범한 C# 객체이고 SimWorld가 목록으로 가진다.
// 좌표는 원문 좌표(칸, 왼쪽 위 (0,0), y 아래가 +). 시간은 틱(int), 입력 시각만 u(long). 열은 int HU.
// "*UntilTick"은 "t < until 이면 아직 유효"로 읽는다. 표시 계층은 이 상태를 읽기만 한다.
namespace Wizard.Solo.Sim
{
    /// <summary>9개 논리 키의 눌림과 마지막 누름 시각(u). 02 요약서 3.2절.</summary>
    public sealed class KeyState
    {
        public readonly bool[] Down = new bool[SimConst.LogicalKeyCount];
        public readonly long[] LastPressU = new long[SimConst.LogicalKeyCount];
        public bool IsDown(LogicalKey k) => Down[(int)k];
    }

    /// <summary>판에 들고 나간 지팡이의 전투 쪽 값(판 중 단계는 안 변한다. 유리 지팡이 임시 하락만 예외).</summary>
    public sealed class WandRuntime
    {
        public long WandId;
        public int Stage;
        /// <summary>유리 지팡이의 판 안 임시 하락 수(0~). 실효 단계 = Stage − TempStageDrop.</summary>
        public int TempStageDrop;
        public bool GlassDroppedThisWave;
        public int LinkSlotCount;
        public readonly List<string> Mutations = new List<string>();
        public int EffectiveStage => Stage - TempStageDrop;
        public bool Has(string mutationId) => Mutations.Contains(mutationId);
    }

    /// <summary>조립 칸 하나. WildUid 0 = 서고 블록, 그 밖 = 판 중 얻은 야생 블록의 uid.</summary>
    public struct SlotItem
    {
        public string BlockId;
        public int WildUid;
        public bool Borrowed;
        /// <summary>판 한정 블록 성장(★). 야생 블록이면 WildBlock.Stars와 같은 값을 든다. 컴파일 문맥 SlotStars로 넘어간다.</summary>
        public int Stars;
        public bool IsEmpty => string.IsNullOrEmpty(BlockId);
    }

    /// <summary>판 중 조립(코어 1 + 연결 L). 판이 끝나면 버려지고 출발 조립은 그대로 남는다.</summary>
    public sealed class RunLoadout
    {
        public SlotItem Core;
        public SlotItem[] Links = new SlotItem[0];
        public string[] LinkIds()
        {
            var ids = new string[Links.Length];
            for (int i = 0; i < Links.Length; i++) ids[i] = Links[i].BlockId;
            return ids;
        }
        /// <summary>칸마다 별 수(0 = 코어, 1..L = 연결 칸). 컴파일 문맥 SlotStars용.</summary>
        public int[] SlotStars()
        {
            int n = Links != null ? Links.Length : 0;
            var stars = new int[n + 1];
            stars[0] = Core.Stars;
            for (int i = 0; i < n; i++) stars[i + 1] = Links[i].Stars;
            return stars;
        }
    }

    /// <summary>캐릭터·지팡이 특성·변이에서 나온 값. 판 시작과 변이·임시 하락이 바뀔 때 Traits 모듈이 다시 채운다. 다른 모듈은 읽기만.</summary>
    public struct PlayerDerived
    {
        public int CritStartHu;
        public int LockTicks;
        public int CoolHuPerTick;
        public int CoolDelayTicks;
        public float DepthDecay;
        /// <summary>강화 배율 1.15^실효 단계.</summary>
        public float EnhanceMul;
        /// <summary>변이 배율(뒤틀린 총열 1.6, 없으면 1).</summary>
        public float MutationDamageMul;
        /// <summary>임계 배율 덧셈(유리 지팡이 +1).</summary>
        public int CritBonus;
        public bool Underline, KeepCrystal, HasCredit;
        public bool MirrorImage, TwistedBarrel, UnstableCore, Gluttony, GlassWand, Hypersensitive;
        public int MaxDebt;
        public int GrazeBuffTicks;
        public int InvulnTicks;
    }

    public sealed class PlayerState
    {
        public int Slot;
        public bool Active;
        public string CharId;
        public CharacterDef Def;
        public WandRuntime Wand = new WandRuntime();
        public PlayerDerived Derived;

        // 이동·시야·자세 (02 요약서 4절)
        /// <summary>판정 중심(발밑 기준점 위 0.4칸).</summary>
        public Vector2 Pos;
        public Vector2 PrevPos;
        public Dir8 Facing = Dir8.S;
        public StanceState Stance;
        public Dir8 LockedFacing = Dir8.S;
        public Dir8 LastPressedCardinal = Dir8.S;
        public bool Moving;
        public Dir8 MoveDir;
        public readonly KeyState Keys = new KeyState();
        public bool DiagPendingActive;
        public Dir8 DiagPendingDir;
        public long DiagPendingExpireU;
        /// <summary>플레이어 이동 배율(서리밭 등 적이 건 감속. 1 = 없음). 매 틱 Threats가 다시 정한다.</summary>
        public float EnvSpeedMul = 1f;
        /// <summary>입력을 무시한다(되비침·쓰러짐 연출). 켜고 끄는 것은 Bosses 모듈뿐이다.</summary>
        public bool InputLocked;
        /// <summary>발사·비기를 무시한다(보스 등장 1.2초). 켜고 끄는 것은 Run 모듈뿐이다.</summary>
        public bool AttackLocked;

        // 체력·피격 (02 요약서 5절)
        public int Hp;
        public int MaxHp;
        public int InvulnUntilTick;
        /// <summary>잔상 걸음 무적 중 스침 1회 허용 창.</summary>
        public int ArtGrazeWindowUntilTick;
        public bool ArtGrazeWindowUsed;
        /// <summary>이번 틱에 피격이 확정됐다. Player.Stage10_ResolveHits가 켜고 다음 틱 Stage1_Timers가 끈다
        /// (값이 뜻 있는 구간: 그 틱의 10단계 뒤 ~ 11단계, 그리고 표시). 9단계의 스침 판정은 이것이 아니라 HasPendingHit을 본다.</summary>
        public bool HitThisTick;
        /// <summary>체력 0으로 쓰러졌다. 첫 만남의 쓰러짐에서도 켜고 Player.Rise가 끈다. 켜진 동안 SimWorld.NearestPlayer는 이 플레이어를 건너뛴다.</summary>
        public bool Downed;
        /// <summary>이번 틱 9단계까지 올라온 피격 후보가 있다(QueueHit이 true를 돌려줬다). 10단계가 확정하고 끈다.</summary>
        public bool HasPendingHit;
        public PlayerHitRequest PendingHit;

        // 과열 (02 요약서 6절)
        public int HeatHu;
        public bool Locked;
        public int UnlockTick;
        public int LockStartTick;
        public int FireReadyTick;
        public int LastFireTick = int.MinValue / 2;
        public int LastFireIntervalTicks;
        public bool HeatExemptArmed;
        public bool JPressedThisTick;
        public int NextShotId = 1;

        // 스침 (02 요약서 7절)
        public int GrazeBuffUntilTick;
        public readonly HashSet<int> GrazedCasts = new HashSet<int>();

        // 콤보·결정 (10 요약서 1·2절)
        public int Combo;
        public int ComboIdleTicks;
        public readonly Dictionary<int, int> ShotComboGain = new Dictionary<int, int>();
        public int Crystals;
        public int Debt;

        // 조립·비기
        public RunLoadout Loadout = new RunLoadout();
        public CompiledSpell Spell;
        /// <summary>이 판에서 쓸 수 있는 비기(시작 3종 + 프로필 해금 + 판 중 해금).</summary>
        public readonly HashSet<string> UnlockedArts = new HashSet<string>();
        /// <summary>불가마 열 흡수 남은 양(HU). 지대가 없으면 0.</summary>
        public int KilnAbsorbLeftHu;
        public bool InKilnZone;
        /// <summary>모듈 전용 상태(커맨드 판정기, 살얼음 횟수 등). 주인 모듈만 형을 안다.</summary>
        public object PlayerScratch;
        public object ArtScratch;
        public object TraitScratch;

        // 판 통계(HUD·정산)
        public int Kills;
        /// <summary>협동: 쓰러진 동안 동료가 곁에 서 있은 틱 수(부활 진행, Run이 센다 — RunFlow.Revive.cs).</summary>
        public int ReviveTicks;

        public bool Invulnerable(int tick) => tick < InvulnUntilTick;
        public bool InStance => Stance != StanceState.None;
        public int HpCells => (Hp + SimConst.HpPerCell - 1) / SimConst.HpPerCell;
        public int MaxHpCells => MaxHp / SimConst.HpPerCell;
        public FacingArrowMode ArrowMode =>
            Stance == StanceState.Stance ? FacingArrowMode.Stance : Stance == StanceState.Reverse ? FacingArrowMode.Reverse : FacingArrowMode.Normal;
    }

    /// <summary>플레이어 피격 후보. 같은 틱에 여럿이면 AttackId가 가장 작은 하나만 들어간다(02 요약서 5.2절).</summary>
    public struct PlayerHitRequest
    {
        /// <summary>공격 id = 위협 개체의 EntityId(SimWorld.NewEntityId 셈틀 하나): 적 탄·예고 구역·남는 구역은 자기 Id,
        /// 접촉·돌진은 그 적의 Id, 각본 피해는 0. 같은 틱에 여럿이면 가장 작은 것 하나만 확정한다(CastId를 넣지 않는다).</summary>
        public int AttackId;
        public int CastId;
        public int SourceEnemyId;
        /// <summary>3칸 상한까지 적용한 칸 수(05 요약서 3.1절). 빚 +1칸은 플레이어 쪽이 더한다.</summary>
        public int DamageCells;
        public PlayerDamageSource Source;
        public Vector2 FromPos;
    }

    public struct SlowEntry
    {
        /// <summary>0~1(0.3 = −30%).</summary>
        public float Strength;
        public int UntilTick;
    }

    /// <summary>적·엘리트·졸개·보스 몸통. 보스는 IsBoss이고 BossState가 따로 붙는다.</summary>
    public sealed class EnemyState
    {
        public int Id;
        /// <summary>enemy.* 또는 boss.* (엘리트도 바탕 종 id).</summary>
        public string DefId;
        public EnemyDef Def;
        public EliteDef Elite;
        public bool IsElite => Elite != null;
        public bool IsBoss;
        public BossDef BossDef;
        public EnemyTags Tags;
        public string Weak, Resist;
        public int Cost;
        /// <summary>졸개·갈라진·불러낸 개체: 골드·점수·드랍 0.</summary>
        public bool NoReward;
        /// <summary>이 개체를 불러낸 적의 Id(0 = 웨이브 구성으로 나온 것). 소환 상한(새싹 8, 엘리트 summon)은 이 값으로 센다.</summary>
        public int SummonerId;
        public int SpawnWave;

        public Vector2 Pos;
        public Vector2 PrevPos;
        /// <summary>바라보는 방향(단위 벡터, 시뮬레이션 좌표). 그림은 x 부호로 좌우만 뒤집는다.</summary>
        public Vector2 Facing = new Vector2(0, 1);
        public float BodyRadius;
        public float MoveSpeed;
        public float Hp;
        public float MaxHp;
        /// <summary>이 값 밑으로 내려가지 않는다(허상 첫 만남 50%). 0 = 없음.</summary>
        public float HpFloor;

        public EnemyAiState Ai = EnemyAiState.Spawning;
        public int AiStateStartTick;
        public int CurrentAttack = -1;
        public int ContactReadyTick;
        /// <summary>돌진 중(몸이 위협). 스침·병풍 충돌 판정이 읽는다.</summary>
        public bool DashActive;
        public int DashCastId;
        /// <summary>돌진 공격 정의의 기본 피해 칸(성장 전). 충돌한 틱에 Threats.DamageCells(이 값, Dash, 이 적)로 최종 칸을 구한다.</summary>
        public int DashBaseDamage;
        public Vector2 DashDir;
        /// <summary>땅속(꼬리표와 별개의 지금 상태). 피해·효과·접촉 없음.</summary>
        public bool Burrowed;
        /// <summary>빈틈·기절 중 받는 피해 배율(1 = 보통).</summary>
        public float DamageTakenMul = 1f;
        /// <summary>정면 철갑이 켜져 있다(선돌지기). 반각은 Def 쪽.</summary>
        public bool ArmorActive;

        // 상태이상 (03 요약서 10절, 02 요약서 8.3절)
        public readonly SlowEntry[] Slows = new SlowEntry[(int)SlowSlot.Count];
        public int CorrosionUntilTick;
        public int FrozenUntilTick;
        public int FreezeImmuneUntilTick;
        public int StaggerUntilTick;
        public int StunUntilTick;
        public int ThinIceCount;
        // 기본 공격 유형의 누적 상태(주인 Spells — 피해 적용과 같은 모듈. Docs/12-attack-styles.md 2절)
        /// <summary>화상 겹 수(0 = 없음), 끝 틱, 다음 화상 틱, 한 겹이 한 번에 주는 피해(속성·잠식·적 쪽 조정 전), 건 플레이어.</summary>
        public int BurnStacks;
        public int BurnUntilTick;
        public int BurnNextTick;
        public float BurnPerStack;
        public int BurnOwnerSlot = -1;
        /// <summary>화상 한 번 사이의 틱(건 유형의 burn_interval_seconds).</summary>
        public int BurnIntervalTicks;
        /// <summary>냉기 누적(눈보라) 겹 수와 끝 틱. 정해진 겹에 이르면 빙결하고 0으로.</summary>
        public int ChillStacks;
        public int ChillUntilTick;
        /// <summary>북두꺼비 버프를 받고 있다.</summary>
        public bool DrumBuffed;

        public bool PendingDeath;
        public HitInfo KillingHit;
        public bool Dead;
        /// <summary>종별 AI 상태(주인: Enemies 또는 Bosses 모듈).</summary>
        public object Brain;

        // 표시용(시뮬레이션이 정한다)
        public string AnimClip = "idle";
        public int AnimStartTick;
        public bool WeaponTint;

        /// <summary>이 틱의 남은 단계에서 "있는" 적인가. PendingDeath가 선 적은 10단계의 사망 처리 전까지 없는 것으로 본다
        /// (AI·이동·접촉·돌진·예고 요청 없음, 탄·직접 피해·대상 고르기에서 제외. Modules.cs 머리의 공통 규칙 2).</summary>
        public bool Alive => !Dead && !PendingDeath;
        /// <summary>플레이어 쪽 피해·효과·대상 고르기의 후보인가(살아 있고 땅속이 아님). 등장 예고(Spawning) 중의 취급은 05 요약서 4절을 따른다.</summary>
        public bool Targetable => !Dead && !PendingDeath && !Burrowed;

        public bool Has(EnemyTags t) => (Tags & t) != 0;
        public bool CanAct(int tick) => tick >= FrozenUntilTick && tick >= StaggerUntilTick && tick >= StunUntilTick;
        /// <summary>실효 감속(가장 큰 값 하나).</summary>
        public float SlowStrength(int tick)
        {
            float s = 0f;
            for (int i = 0; i < Slows.Length; i++)
                if (tick < Slows[i].UntilTick && Slows[i].Strength > s) s = Slows[i].Strength;
            return s;
        }
        public bool Corroded(int tick) => tick < CorrosionUntilTick;
    }

    /// <summary>보스 싸움 상태(보스 웨이브에만 있다). 05 요약서 8.3절.</summary>
    /// <remarks>State·StateStartTick의 주인은 Bosses 하나뿐이다. Run은 읽기만 하고 같은 구간을 따로 세지 않는다(Modules.cs 공통 규칙 5).</remarks>
    public sealed class BossState
    {
        public int EnemyId;
        public BossDef Def;
        public int Wave;
        public int Cycle;
        public bool FirstEncounter;
        public BossFightState State = BossFightState.Intro;
        public int StateStartTick;
        /// <summary>단계 색인(Def.Phases의 첨자).</summary>
        public int Phase;
        public int RageStacks;
        public bool FinalRage;
        /// <summary>등장이 끝난 뒤 흐른 틱(격노 시계).</summary>
        public int FightTicks;
        public string CurrentPattern;
        public string LastPattern;
        public int ActionHaltUntilTick;
        /// <summary>이번 싸움에서 본 가장 낮은 체력(복수 추적기 흠집).</summary>
        public float HpMin;
        /// <summary>밤삼킴의 마디 위치(머리 다음부터, 마지막이 꼬리). 없으면 빈 목록.</summary>
        public readonly List<Vector2> Segments = new List<Vector2>();
        /// <summary>Segments 원 하나의 반지름(플레이어 탄·폭발이 마디를 맞히는 판정). 0 = 마디 판정 없음. 주인: Bosses.</summary>
        public float SegmentRadius;
        public object Brain;
    }

    /// <summary>플레이어 탄 하나. 03 요약서 7절.</summary>
    public sealed class ProjectileEntity
    {
        /// <summary>발사 순간 고정한 스코프 외 피해 배율과 임계 보너스. 자손 탄이 그대로 물려받는다.</summary>
        public float BaseMul;
        public int CritBonus;
        public int Id;
        public int OwnerSlot;
        public int ShotId;
        public ShotSource Source;
        public BlockDef Block;
        public CompiledSpell Tree;
        public int ScopeDepth;
        public int MaxDepth;
        public ProjectileState State;
        public int SpawnTick;
        public Vector2 Pos;
        public Vector2 PrevPos;
        /// <summary>단위 방향(시뮬레이션 좌표).</summary>
        public Vector2 Dir;
        public float Speed;
        public float Radius;
        public float Traveled;
        public float ReturnTraveled;
        public int BouncesLeft;
        public int PierceLeft;
        public bool TriggerFired;
        public int IgnoreEnemyId = -1;
        public int HomingTargetId = -1;
        public readonly List<int> HitSet = new List<int>();
        public ScopeModifiers Mods;
        public bool IsCrit;
        /// <summary>강화 × (1 + 변형 보너스) × 변이 × 임계 × 비기 × 감쇠 × 캐릭터 × 보전까지 곱한 값(기본 피해·속성·대상 상태 제외).</summary>
        public float DamageMul;
        public ShotHitEffect HitEffect;
        // ---- 기본 공격 유형(핵 블록 Block.Style != null)의 상태. 주인 Spells. Docs/12-attack-styles.md 2절.
        /// <summary>단계(포탄: 0 날기 · 1 불바닥 / 룬: 0 대기 / 그 밖 0).</summary>
        public int StylePhase;
        /// <summary>떨어질 자리·지뢰 자리·낙뢰 자리.</summary>
        public Vector2 TargetPos;
        /// <summary>출발점(포탄 포물선, 부채꼴 원점, 고드름 줄의 시작).</summary>
        public Vector2 Origin;
        /// <summary>부채꼴·고드름 줄의 방향(단위).</summary>
        public Vector2 AimDir;
        /// <summary>다음 사건 틱(착탄, 화상 틱, 번개 지짐, 다음 고드름).</summary>
        public int TimerTick;
        /// <summary>끝나는 틱(불바닥, 지뢰 자동 폭발).</summary>
        public int EndTick;
        /// <summary>지뢰가 무장되는 틱.</summary>
        public int ArmTick;
        /// <summary>부채꼴 휩쓴 틱 수, 다음 고드름 번호.</summary>
        public int StepIndex;
        /// <summary>고드름 개수(튕김이 늘린다).</summary>
        public int StepCount;
        /// <summary>이번 발동(폭발 하나·부채꼴 하나·탄의 한 다리)에서 이미 맞힌 적 수. 둘째부터 extra_target_mul.</summary>
        public int StyleHits;
        /// <summary>남은 적 사이 튕김(튀는 불씨).</summary>
        public int RicochetsLeft;
        /// <summary>책장: 아직 덜 꺾은 각(도, 부호 = 꺾는 쪽).</summary>
        public float CurveLeftDegrees;
        /// <summary>범위 배율(크기 변형 ×2^n, 상한까지): 폭발·부채꼴·지짐·지뢰 반경에 곱한다.</summary>
        public float AreaScale = 1f;
        /// <summary>사거리 덤(칸, 책 던지기의 튕김).</summary>
        public float RangeBonus;
        /// <summary>눈알 추적탄의 세대(0 = 처음 탄).</summary>
        public int Generation;
        /// <summary>쫓는 적 id(포탄 추적·추적탄). -1 = 없음.</summary>
        public int TargetEnemyId = -1;
        /// <summary>이 탄이 콤보 덤을 이미 줬다(발사 1회에 한 번).</summary>
        public bool ComboBonusGiven;
        /// <summary>끝 처리(얼음 창 파편)를 이미 했거나 하지 않는다(웨이브 끝 지우기).</summary>
        public bool StyleEnded;
        public bool Dead;
    }

    /// <summary>적 탄 하나. 05 요약서 8.5절 proj.</summary>
    public sealed class EnemyShotEntity
    {
        public int Id;
        public int CastId;
        public int OwnerEnemyId;
        public Vector2 Pos;
        public Vector2 PrevPos;
        public Vector2 Vel;
        public float Radius;
        /// <summary>남은 사거리(칸). 음수 = 벽까지.</summary>
        public float RangeLeft = -1f;
        /// <summary>기본 피해 칸(성장·최종 격노 전). 맞힌 틱에 Threats.DamageCells(이 값, Projectile, 쏜 적)로 최종 칸을 구한다.</summary>
        public int BaseDamage = 1;
        public bool Grazeable = true;
        /// <summary>포물선(설치 벽을 넘는다. 떨어지는 틱에만 판정).</summary>
        public bool Lob;
        public int LandTick;
        public int BouncesLeft;
        /// <summary>얼음 병풍이 막았다(스침 아님).</summary>
        public bool Blocked;
        /// <summary>그림 열쇠(effects manifest의 enemy 묶음). null이면 기본 탄.</summary>
        public string SpriteKey;
        public int SpawnTick;
        public bool Dead;
    }

    /// <summary>예고 구역(떠 있는 동안 1개로 센다). 05 요약서 1.2절.</summary>
    public sealed class ZoneEntity
    {
        public int Id;
        public int CastId;
        public int OwnerEnemyId;
        public ZoneShape Shape;
        /// <summary>Circle·Ring 중심 / Band·AimLine 시작점 / Fan 꼭짓점.</summary>
        public Vector2 Pos;
        public Vector2 Dir = new Vector2(1, 0);
        public float Radius;
        public float InnerRadius;
        public float Width;
        public float Length;
        public float FanDegrees;
        public int StartTick;
        public int DetonateTick;
        /// <summary>방향이 고정되는 틱(at_start면 StartTick).</summary>
        public int LockTick;
        public bool DirLocked;
        /// <summary>기본 피해 칸(ZoneSpec.BaseDamage). 판정 틱에 Threats.DamageCells(이 값, DamageSource, 주인)로 최종 칸을 구한다.</summary>
        public int BaseDamage;
        public PlayerDamageSource DamageSource = PlayerDamageSource.Telegraph;
        public bool Grazeable = true;
        /// <summary>동시 예고 상한에 세는가(보스 패턴의 둘째 원부터는 false).</summary>
        public bool CountsToCap = true;
        /// <summary>피해 없는 예고(별똥, 등장 흔들림처럼 위험색을 쓰지 않는 것).</summary>
        public bool Harmless;
        public HazardDef LeavesHazard;
        public bool Cancelled;
        public bool Detonated;
        public float Fill(int tick) => DetonateTick <= StartTick ? 1f : Mathf.Clamp01((tick - StartTick) / (float)(DetonateTick - StartTick));
    }

    /// <summary>남는 위험 구역·방해 구역.</summary>
    public sealed class HazardEntity
    {
        public int Id;
        public HazardKind Kind;
        public int OwnerEnemyId;
        public ZoneShape Shape = ZoneShape.Circle;
        public Vector2 Pos;
        public Vector2 Dir = new Vector2(1, 0);
        public float Radius;
        public float Width;
        public float Length;
        public int StartTick;
        /// <summary>int.MaxValue = 웨이브 끝까지.</summary>
        public int UntilTick;
        /// <summary>기본 피해 칸. 닿은 틱에 Threats.DamageCells(이 값, Lingering, 주인)로 최종 칸을 구한다.</summary>
        public int BaseDamage = 1;
        public int ContactCooldownTicks;
        public int ContactReadyTick;
        /// <summary>피해 없이 느리게만 한다(서리밭). SlowPercent를 쓴다.</summary>
        public bool SlowOnly;
        public float SlowPercent;
        public bool Dead;
    }

    /// <summary>적·보스가 만든 장애물(바위, 얼음 기둥).</summary>
    public sealed class ObstacleEntity
    {
        public int Id;
        public string Kind;
        public Vector2 Pos;
        public float Radius;
        /// <summary>0 = 플레이어 탄으로 안 깨진다.</summary>
        public float Hp;
        public int UntilTick;
        public bool BlocksPlayerShots;
        public bool BlocksEnemyShots;
        public bool Dead;
    }

    /// <summary>플레이어가 깐 지대(불가마, 사본 마법진, 얼음 병풍). 주인: Arts 모듈.</summary>
    public sealed class PlayerZoneEntity
    {
        public int Id;
        public PlayerZoneKind Kind;
        public int OwnerSlot;
        public Vector2 Pos;
        /// <summary>깐 순간의 시야(병풍의 법선).</summary>
        public Dir8 Facing;
        public float Radius;
        public float Width;
        public float Thickness;
        public int StartTick;
        public int UntilTick;
        public int NextPulseTick;
        public int ChargesLeft;
        public int ReadyTick;
        /// <summary>깐 순간 고정한 T·캐릭터 배율(시전 순간 값).</summary>
        public float SnapshotT;
        public float SnapshotCharMul;
        public bool Dead;
    }

    public sealed class DropEntity
    {
        public int Id;
        public DropKind Kind;
        public Vector2 Pos;
        public int SpawnTick;
        public int Amount;
        public bool Picked;
    }

    public sealed class StarfallState
    {
        public StarfallPhase Phase;
        public Vector2 Pos;
        /// <summary>떨어질 때(웨이브 시계 WaveTick 기준 — 예고 구간을 웨이브 시간에 넣지 않으므로). PhaseStartTick·NextRetryTick은 절대 틱.</summary>
        public int ScheduledTick;
        public int PhaseStartTick;
        public int NextRetryTick;
    }

    public sealed class WeatherState
    {
        /// <summary>지금 켜진 사건 id(event.shower, event.fog).</summary>
        public readonly List<string> Active = new List<string>();
        public int ShowerUntilTick;
        public int ShowersToday;
        /// <summary>날이 바뀔 때 바뀌는 풀밭·소품 배치 번호(0~5).</summary>
        public int PropVariant;
        public int DiscardedItemVariant = -1;
    }
}

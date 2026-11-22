using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Data;

// 시뮬레이션 모듈 계약.
// 모듈마다 "기본 클래스"(가상 메서드가 전부 아무 일도 안 하는 본문)를 여기 두고, 구현 작업이 자기 폴더에서 상속해 채운다.
// 기본 클래스 그대로가 곧 빈 구현이라, 다른 모듈이 아직 없어도 자기 모듈만 끼운 SimWorld로 EditMode 테스트를 돌릴 수 있다.
// 모듈끼리는 여기 적힌 메서드로만 부른다(w.Mods.X.Method). 구현 클래스를 직접 참조하지 않는다.
// 틱 안의 순서는 SimWorld.Step 하나뿐이다. StageN_* 메서드는 SimWorld.Step만 부른다.
//
// ---- 모듈 공통 규칙(모든 구현이 지킨다. 00-architecture.md 5.6절과 같은 내용) ----
// 1. 주인만 쓴다: 상태는 주인 모듈만 바꾼다. 남의 상태는 주인 모듈의 메서드로 바꾼다.
// 2. 죽어 가는 적: EnemyState.PendingDeath가 선 적은 그 틱의 남은 단계에서 "없는 것"으로 본다 — AI·이동·접촉·돌진·예고 요청 없음,
//    내 탄·직접 피해·대상 고르기(피뢰침, 연쇄, 유도)에서 제외, 관통 횟수를 쓰지 않는다. KillingHit는 처음 것을 유지한다.
//    걸러낼 때는 EnemyState.Alive(AI·이동 쪽) 또는 EnemyState.Targetable(플레이어 쪽 피해·대상)을 쓴다. 사망 처리는 10단계 Enemies.Stage10_Deaths 한 곳.
// 3. 대상이 없을 때: SimWorld.NearestPlayer가 null이면(전원 쓰러짐) 적·보스 AI는 그 틱에 가만히 있는다(이동·예고 요청·탄 없음). 예외를 내지 않는다.
// 4. 예고 표(ticket)의 틱: 요청은 7단계, 허가되면 같은 틱 8단계에 구역이 생긴다(시작 틱 s). 판정 틱 = s + N(9단계에서 Threats가 판정).
//    주인이 스스로 하는 타격(돌진 시작, 탄 쏘기, 다음 상태로 넘어가기)은 Threats.TryGetTiming의 detonateTick과 **같은 틱의 7단계**
//    (w.Tick >= detonateTick)에 시작한다 — 돌진이면 그 틱 9단계 Enemies.Stage9_Move부터 몸이 움직인다. Status == Detonated를 기다리면 1틱 늦는다.
//    대기 줄에 있는 동안(Status == Waiting) 주인은 EnemyAiState.Waiting이고 WindUp 시계는 허가된 틱(startTick)부터 센다.
//    표는 판정·취소 뒤에도 주인이 CancelTicket 하거나 600틱이 지날 때까지 Detonated/Cancelled로 남는다.
// 5. 시계의 주인은 하나: BossState.State·StateStartTick은 Bosses만 세고 Run은 읽기만 한다(등장 72틱, 되비침 180틱, 쓰러짐 240틱을 Run이 따로 세지 않는다).
//    PlayerState.InputLocked는 Bosses만, AttackLocked는 Run만 켜고 끈다. RunPhase는 Run만 바꾼다.
// 6. 받는 피해 칸: 개체(ZoneEntity, EnemyShotEntity, HazardEntity, EnemyState.DashBaseDamage)는 기본 칸(BaseDamage)만 든다.
//    최종 칸은 Player.QueueHit을 부르는 순간 Threats.DamageCells(base, source, owner)로 구한다(도중에 최종 격노가 켜져도 맞는 값).
// 7. 공격 허용: 발사·비기 가능 여부는 SimWorld.AttackAllowed(p) 하나로 본다. Player.Stage2_Input은 InputLocked만 보고 방향·K를 그대로
//    Arts에 넘긴다(판정기는 계속 듣는다). Arts.OnSkillPress·Stage5_Arts(보관된 K 포함)와 Player.Stage6_Fire가 AttackAllowed를 본다 —
//    허용되지 않으면 K는 불발 없이 버리고(결정을 쓰지 않는다) 보관된 K도 버린다.
// 8. 판이 끝난 뒤: Run.OnPlayerDowned는 그 자리에서 RunPhase를 RunEnded로 바꾼다(첫 만남 제외). 같은 틱에 이어지는 훅에서
//    Run.OnEnemyKilled는 지갑·점수·기회·통계를 바꾸지 않고, Bosses는 BossKilled·RevengeKill을 내지 않는다(00 문서 D-45).
// 9. 이벤트: 플레이어와 무관한 이벤트는 Slot = -1(SimWorld.Emit의 기본값). 규칙 연결에 이벤트를 쓰지 않는다.
namespace Wizard.Solo.Sim
{
    // ---------------------------------------------------------------- 요청·결과 구조체

    public enum ZoneAnchor { Fixed, Owner, TargetPlayer }

    /// <summary>예고 구역 하나의 명세. 05 요약서 1.2절·8.5절.</summary>
    public struct ZoneSpec
    {
        public ZoneShape Shape;
        public ZoneAnchor Anchor;
        /// <summary>Anchor == Fixed면 절대 위치, 아니면 기준점에서의 오프셋.</summary>
        public Vector2 Pos;
        /// <summary>방향(단위 벡터). AimAtPlayer면 시작(또는 고정) 때 플레이어 쪽으로 다시 정한다.</summary>
        public Vector2 Dir;
        public bool AimAtPlayer;
        /// <summary>AimAtPlayer로 잡은 방향에 더할 각(도). 부채꼴·여러 띠용.</summary>
        public float AimOffsetDegrees;
        public float Radius, InnerRadius, Width;
        /// <summary>0 이하 = 벽까지.</summary>
        public float Length;
        public float FanDegrees;
        public int TelegraphTicks;
        /// <summary>0 = 시작 순간 고정(at_start). n &gt; 0 = 마지막 n틱에 고정(last_n_ticks).</summary>
        public int LockLastTicks;
        /// <summary>종별 기본 피해 칸(성장·상한 전). Threats가 05 요약서 3.1절 식으로 최종 칸을 정한다.
        /// 0 이하 = 판정 없는 예고(보스 돌진의 예고 띠 — 몸이 직접 때린다). 스침 후보도 아니다.</summary>
        public int BaseDamage;
        public PlayerDamageSource DamageSource;
        public bool Grazeable;
        public bool Harmless;
        public HazardDef LeavesHazard;
        public int TargetSlot;
    }

    /// <summary>예고 요청(여러 구역은 전부 아니면 전무). 상한·R5에 걸리면 대기 줄에서 기다린다.</summary>
    public sealed class TelegraphRequest
    {
        public int OwnerEnemyId;
        public int CastId;
        public readonly List<ZoneSpec> Zones = new List<ZoneSpec>();
        /// <summary>보스 패턴처럼 여러 구역을 상한 1개로 센다.</summary>
        public bool CountAsOne;
        /// <summary>상한·R5를 보지 않고 바로 시작한다(좁은 들, 각본 연출).</summary>
        public bool Bypass;
        /// <summary>&gt; 0이면 상한에 세지 않고 R5만 본다: 길이 막히면 이 틱만큼 미뤄 다시 검사한다(늦게 청한 것부터 밀린다).
        /// 마지막 숨의 원(06 요약서 1.4절 death_circle.stagger_seconds = 12틱)이 쓴다.</summary>
        public int StaggerIfBlockedTicks;
    }

    public enum TicketStatus { Unknown, Waiting, Active, Detonated, Cancelled }

    public struct EnemyShotSpec
    {
        public int OwnerEnemyId;
        public int CastId;
        public Vector2 Pos;
        public Vector2 Dir;
        public float Speed;
        public float Radius;
        /// <summary>음수 = 벽까지.</summary>
        public float Range;
        public int BaseDamage;
        public bool Grazeable;
        public bool Lob;
        public Vector2 LobTarget;
        public int LobFlightTicks;
        public int Bounces;
        public string SpriteKey;
    }

    public struct HazardSpec
    {
        public HazardKind Kind;
        public int OwnerEnemyId;
        public ZoneShape Shape;
        public Vector2 Pos, Dir;
        public float Radius, Width, Length;
        /// <summary>0 이하 = 웨이브 끝까지.</summary>
        public int DurationTicks;
        /// <summary>기본 피해 칸(성장 전).</summary>
        public int BaseDamage;
        public int ContactCooldownTicks;
        public bool SlowOnly;
        public float SlowPercent;
        /// <summary>같은 종류(Kind)의 전장 전체 동시 개수 상한(0 = 없음, 주인을 가리지 않는다 — 05 요약서 6.10절 "전장 동시 3개").
        /// 넘치면 가장 오래된 것을 지운다.</summary>
        public int MaxCount;
    }

    public struct ObstacleSpec
    {
        public string Kind;
        public Vector2 Pos;
        public float Radius;
        public float Hp;
        public int DurationTicks;
        public bool BlocksPlayerShots, BlocksEnemyShots;
    }

    /// <summary>스침 후보. SurfaceDistance = 플레이어 판정 중심에서 위협 겉면까지 거리(탄: d − r, 구역: 경계까지).
    /// 플레이어 쪽이 hit_radius &lt; SurfaceDistance ≤ graze_radius 인지 본다.</summary>
    public struct GrazeCandidate
    {
        public int CastId;
        public GrazeKind Kind;
        public float SurfaceDistance;
        public Vector2 Pos;
        public int ThreatId;
    }

    public struct EnemySpawnSpec
    {
        public string DefId;
        public bool Elite;
        public Vector2 Pos;
        public bool NoReward;
        /// <summary>불러낸 적의 Id(0 = 없음). EnemyState.SummonerId로 옮겨진다.</summary>
        public int SummonerId;
        /// <summary>등장 예고(덤불 흔들림 0.5초) 없이 바로 나온다(갈라진 슬라임, 불러낸 박쥐).</summary>
        public bool Instant;
        /// <summary>0 = 정의 값 × H(w) × 배율로 계산.</summary>
        public float HpOverride;
    }

    /// <summary>야영·연습 화면에 띄우는 조립 수치. 03 요약서 13.3절.</summary>
    public struct SpellStats
    {
        public float HeatPerShot;
        /// <summary>임계 사격 가능 횟수. -1 = 무한(열 0), 0 = 불가(경고).</summary>
        public int CritShots;
        public float DummyDps;
        public int ProjectilesPerShot;
        public float StandardT;
    }

    /// <summary>"지금 K" 미리보기. 04 요약서 6절.</summary>
    public struct ArtPreview
    {
        /// <summary>K 두 번이면 나갈 파이널 어빌리티(없으면 null)와 그때 쓰는 결정 수.</summary>
        public string FinalArtId;
        public int FinalCost;
        public string ArtId;
        public int Cost;
        public int DebtUsed;
        public bool Fizzle;
        public ArtFizzleReason Reason;
        public int LateIndex;
        public float LateSeconds;
        /// <summary>고른 노드에 해금된 섬 기술이 있으면 윗줄에 따로 보여 준다.</summary>
        public bool HasJustLine;
        public string JustArtId;
        public int JustCost;
        public bool Buffered;
        /// <summary>재사용 대기 진행(0 = 막 씀, 1 = 준비됨).</summary>
        public float CooldownRatio;
        /// <summary>마지막 방향 입력 시각(u). 없으면 -1. 연습 화면의 저스트 게이지(이 시각부터 JustWindowU 동안 참)용.</summary>
        public long LastDirTimeU;
        /// <summary>섬 기술 창(u). tries.json의 값(0.07초 = 1,075u).</summary>
        public long JustWindowU;
    }

    public struct RecentInput
    {
        public CmdDir Dir;
        /// <summary>앞 입력과 이어졌는가(간격 통과).</summary>
        public bool Chained;
        /// <summary>입력 시각(u).</summary>
        public long TimeU;
        /// <summary>앞 입력과의 간격(u). 첫 입력이면 0.</summary>
        public long GapU;
        /// <summary>그 간선에 허용된 간격(u). 첫 입력이면 0. 연습 화면의 입력 간격 막대용.</summary>
        public long AllowedGapU;
    }

    public enum ArtNodeState { Unlocked, Locked, Candidate, Current }

    /// <summary>비기 트라이 화면용 노드 설명. 04 요약서 2절.</summary>
    public sealed class ArtTrieNodeView
    {
        public int NodeId;
        public int ParentId;
        public int Depth;
        public CmdDir[] Path;
        public string ArtId;
        public string JustArtId;
        /// <summary>들어오는 간선 간격(초). 기본(0.3)과 같으면 화면에 적지 않는다.</summary>
        public float GapSeconds;
        public bool GapIsDefault;
        /// <summary>K 기술(ArtId) 기준 상태. 기술 없는 통과 노드는 Unlocked.</summary>
        public ArtNodeState State;
        /// <summary>섬 기술(JustArtId) 상태. 섬 기술이 없으면 Unlocked(뇌광창이 FF 마디에 따로 잠긴다).</summary>
        public ArtNodeState JustState;
    }

    // ---------------------------------------------------------------- 모듈 기본 클래스

    /// <summary>플레이어: 입력, 이동, 자세, 시야, 체력, 피격, 과열, 스침, 콤보, 결정. 구현 폴더 Sim/Player. 02·10 요약서.</summary>
    public class PlayerModule
    {
        /// <summary>판 시작: Def·체력·위치·지팡이·조립을 채우고 Traits.RecomputeDerived, 주문 컴파일, Arts.SetupPlayer를 부른다.</summary>
        public virtual void SetupPlayer(SimWorld w, PlayerState p, RunConfig cfg) { }
        /// <summary>1단계: 과열 잠금 풀림(t ≥ UnlockTick → 열 0).</summary>
        public virtual void Stage1_Timers(SimWorld w) { }
        /// <summary>2단계: 이 틱의 입력 이벤트를 정렬해 적용(키 상태 → 자세 → 시야 → 판정기에 방향·K 전달).</summary>
        public virtual void Stage2_Input(SimWorld w) { }
        /// <summary>3단계: 대각 시야 유지 창 만료.</summary>
        public virtual void Stage3_ResolveFacing(SimWorld w) { }
        /// <summary>4단계: 이동과 벽·장애물.</summary>
        public virtual void Stage4_Move(SimWorld w) { }
        /// <summary>6단계: J 발사(과열 판정 → 주문 실행 → 열 반영). 02 요약서 6.3절 TryFire.</summary>
        public virtual void Stage6_Fire(SimWorld w) { }
        /// <summary>9단계 끝: 피격 판정 뒤 스침 판정(Threats.CollectGraze). 줍기는 주인 모듈이 11단계에서 한다(별똥 = Waves, 체력 조각 = Run).
        /// p.HasPendingHit이면(이번 틱에 피격 후보가 올라왔으면) 그 플레이어의 이번 틱 스침을 전부 버린다. HitThisTick은 여기서 보지 않는다.
        /// Arts가 5단계에 직접 준 스침(벼락 질주)은 무적 중이라 같은 틱의 QueueHit이 false가 되므로 피격과 겹치지 않는다.</summary>
        public virtual void Stage9_GrazeAndPickups(SimWorld w) { }
        /// <summary>10단계: 쌓인 피격 후보 중 AttackId가 가장 작은 하나를 확정. 체력 0이면 Run.OnPlayerDowned.</summary>
        public virtual void Stage10_ResolveHits(SimWorld w) { }
        /// <summary>11단계: 냉각, 콤보 끊김 타이머, 무적·가속 끝 이벤트.</summary>
        public virtual void Stage11_TickEnd(SimWorld w) { }

        /// <summary>피격 후보를 올린다(9단계에서 Threats·Enemies·Bosses가 부른다).
        /// 돌려주는 값: true = 후보로 올라갔다 / false = 버렸다(무적, 쓰러짐, !w.PlayerDamageActive).
        /// 부른 쪽의 쿨다운(접촉 1.5초, 남는 구역별 1.5초)은 true일 때만 소비한다 — 같은 틱의 다른 후보(AttackId가 더 작은 것)에 밀려
        /// 실제 피해가 안 들어가도 소비한다. hit.DamageCells는 부르는 순간 Threats.DamageCells로 구한 값.</summary>
        public virtual bool QueueHit(SimWorld w, PlayerState p, in PlayerHitRequest hit) { return false; }
        /// <summary>각본 피해로 바로 쓰러뜨린다(첫 만남 되비침 +114틱). 무적·PlayerDamageActive·대기 중인 후보를 무시하고
        /// 체력 0, p.Downed = true, 콤보 0, PlayerHit·PlayerDowned 이벤트를 낸다. **Run.OnPlayerDowned는 부르지 않는다**
        /// (부른 쪽 — Bosses — 이 이미 자기 상태를 쓰러짐으로 옮겼다). 일으키는 것은 Rise.</summary>
        public virtual void ForceDown(SimWorld w, PlayerState p, PlayerDamageSource source) { }
        /// <summary>무적을 늘린다(InvulnUntil = max). grazeWindow면 그동안 스침 1회만 허용(잔상 걸음).</summary>
        public virtual void GrantInvuln(SimWorld w, PlayerState p, int ticks, bool grazeWindow) { }
        /// <summary>스침 보상을 준다(02 요약서 7.2절 Award). 벼락 질주의 길 위 탄처럼 밖에서 인정시킬 때도 쓴다.</summary>
        public virtual void AwardGraze(SimWorld w, PlayerState p, int castId, GrazeKind kind, Vector2 threatPos) { }
        /// <summary>플레이어 쪽 피해가 적을 맞혔다(콤보 +1/+2, 발사당 +3 상한). 비기·사본·패시브·지대는 안에서 걸러낸다.</summary>
        public virtual void OnPlayerHitEnemy(SimWorld w, in HitInfo hit) { }
        public virtual void AddCombo(SimWorld w, PlayerState p, int amount) { }
        public virtual void ResetCombo(SimWorld w, PlayerState p, ComboResetReason reason) { }
        /// <summary>결정 1개 얻기(빚부터 갚는다). 얻었으면 true. 상한이면 false(별똥은 남겨 둔다).</summary>
        public virtual bool GainCrystal(SimWorld w, PlayerState p, CrystalSource source) { return false; }
        /// <summary>결정 값을 치를 수 있나. allowCredit = 그 비기가 외상을 허용하는가(만개는 false).</summary>
        public virtual bool CanAfford(SimWorld w, PlayerState p, int cost, bool allowCredit) { return false; }
        /// <summary>결정을 쓴다(CanAfford가 참일 때만). 돌려주는 값 = 새로 진 빚.</summary>
        public virtual int SpendCrystals(SimWorld w, PlayerState p, int cost, bool allowCredit) { return 0; }
        /// <summary>결정을 전부 쓴다(만개). 돌려주는 값 = 쓴 개수.</summary>
        public virtual int SpendAllCrystals(SimWorld w, PlayerState p) { return 0; }
        /// <summary>과열 잠금에 넣는다(UnlockTick = t + intervalTicks + lockTicks, 이미 잠겼으면 더 긴 쪽). Traits.OnHeatLockEntered를 부른다.</summary>
        public virtual void EnterLock(SimWorld w, PlayerState p, int intervalTicks, int lockTicks, bool fromUnstableCore) { }
        public virtual void BreakLock(SimWorld w, PlayerState p, HeatUnlockReason reason, int heatAfterHu) { }
        /// <summary>방열 폭발용: 열(0~100, 잠금 중이면 100)을 돌려주고 열 0·잠금 해제.</summary>
        public virtual float ConsumeHeatForVent(SimWorld w, PlayerState p) { return 0f; }
        /// <summary>열을 깎는다(탐식). 잠금 중이면 아무 일도 없다.</summary>
        public virtual void ReduceHeat(SimWorld w, PlayerState p, int hu) { }
        /// <summary>비기의 순간 이동. 벽 앞에서 멈춘다. stopAtEnemies면 적 몸 앞에서도(잔상 걸음). 도착 위치를 돌려준다.</summary>
        public virtual Vector2 MoveByArt(SimWorld w, PlayerState p, Vector2 delta, bool stopAtEnemies, bool stopAtObstacles) { return p.Pos; }
        public virtual void Heal(SimWorld w, PlayerState p, int cells) { }
        /// <summary>웨이브 끝 초기화: 콤보 0, 열 0·잠금 해제, 면제·가속·불가마 흡수 해제([임시] 10 요약서 2.2절). 결정 환전은 Run이 한다.</summary>
        public virtual void ResetForWaveEnd(SimWorld w, PlayerState p) { }
        /// <summary>첫 만남 일어남: 체력 가득, 열 0, 잠금 해제, 콤보 0, 빚 0, 무적 0, Downed = false. PlayerRose 이벤트.</summary>
        public virtual void Rise(SimWorld w, PlayerState p) { }
        /// <summary>조립이 바뀌었다: 다시 컴파일하고 LoadoutChanged를 낸다.</summary>
        public virtual void RecompileSpell(SimWorld w, PlayerState p) { }
        /// <summary>열 고리 상태(02 요약서 6.5절). 수치는 w.Content에서 읽는다.</summary>
        public virtual HeatRingState RingState(SimWorld w, PlayerState p) { return HeatRingState.Cold; }
    }

    /// <summary>주문: 컴파일러, 실행, 플레이어 탄, 피해 공식. 구현 폴더 Sim/Spells. 03 요약서.</summary>
    public class SpellModule
    {
        /// <summary>슬롯 배열 → 실행 트리(순수 함수). core = 코어 블록 id(null = 빔), links = 연결 칸(빈 칸 null).</summary>
        public virtual CompiledSpell Compile(IContentDb db, string core, IReadOnlyList<string> links, in SpellCompileContext ctx) { return new CompiledSpell(); }
        /// <summary>트리 1회 실행(J, 비기, 사본 마법진 공용). 탄을 만들어 w.Projectiles에 붙인다.</summary>
        public virtual ExecuteResult Execute(SimWorld w, in ExecuteRequest req) { return default; }
        /// <summary>9단계: 플레이어 탄 이동·벽·적 충돌·사거리(탄 id 오름차순). 적중은 피해 공식을 거쳐 적에게 적용한다.</summary>
        public virtual void Stage9_Projectiles(SimWorld w) { }
        /// <summary>표준 T = StandardTBase × 강화 배율 × 변이 배율(캐릭터 배율 제외).</summary>
        public virtual float StandardT(SimWorld w, PlayerState p) { return 0f; }
        /// <summary>블록을 거치지 않는 피해를 적에게 준다(속성·잠식·적 쪽 조정을 곱하고 훅을 부른다).</summary>
        public virtual DamageResult DealDirect(SimWorld w, in DirectDamage dmg) { return default; }
        public virtual SpellStats ComputeStats(IContentDb db, CompiledSpell spell, CharacterDef ch, in PlayerDerived derived) { return default; }
        /// <summary>디버그 글자 출력(10 요약서 9절 형식, 정본 id).</summary>
        public virtual string DebugDump(CompiledSpell spell) { return string.Empty; }
        /// <summary>플레이어 탄을 지운다(웨이브 끝, 연출). ownerSlot &lt; 0 = 전부.</summary>
        public virtual void ClearProjectiles(SimWorld w, int ownerSlot) { }
    }

    /// <summary>커맨드 판정기와 비기 15종, 해금 차례. 구현 폴더 Sim/Arts. 04 요약서.</summary>
    public class ArtModule
    {
        /// <summary>판 시작: 캐릭터별 트라이를 만들고 판정기 상태를 p.ArtScratch에 둔다.</summary>
        public virtual void SetupPlayer(SimWorld w, PlayerState p) { }
        /// <summary>자세 진입·해제·반전·일시정지·웨이브 시작 → 판정기 초기화.</summary>
        public virtual void OnCommandReset(SimWorld w, PlayerState p, CommandResetReason reason) { }
        /// <summary>자세 중 방향 누름(누르는 순간만). physical = 누른 물리 방향(잔상 걸음 이동 방향용).</summary>
        public virtual void OnDirectionPress(SimWorld w, PlayerState p, CmdDir dir, Dir8 physical, long timeU) { }
        /// <summary>K 누름. 판정·결제는 누른 시각으로, 발동은 5단계에서.</summary>
        public virtual void OnSkillPress(SimWorld w, PlayerState p, long timeU) { }
        /// <summary>5단계: 보관된 K 발동, 준비 시간이 끝난 비기 실행, 만개 반복, 지대(불가마·사본·병풍) 갱신.</summary>
        public virtual void Stage5_Arts(SimWorld w) { }
        /// <summary>J 발사가 나갔다(사본 마법진 따라 쏘기).</summary>
        public virtual void OnPlayerFired(SimWorld w, PlayerState p, int shotId) { }
        /// <summary>불가마 열 흡수: 임계 발사의 열 heatHu 중 먹은 양(HU)을 돌려준다. 지대 밖·남은 양 0이면 0.</summary>
        public virtual int AbsorbKilnHeat(SimWorld w, PlayerState p, int heatHu) { return 0; }
        public virtual ArtPreview Preview(SimWorld w, PlayerState p, long nowU) { return default; }
        /// <summary>최근 방향 입력(오래된 것부터, 최대 5개).</summary>
        public virtual void GetRecentInputs(PlayerState p, List<RecentInput> into) { }
        /// <summary>선분 a→b가 얼음 병풍에 막히나(적 탄·적 이동용). 막히면 그 지대 id.</summary>
        public virtual bool SegmentHitsScreen(SimWorld w, Vector2 a, Vector2 b, out int zoneId) { zoneId = -1; return false; }
        /// <summary>적이 병풍에 닿았다. dash = 돌진 중. 병풍이 깨졌으면 true(경직은 이 안에서 Enemies.ApplyCc로 건다).</summary>
        public virtual bool OnEnemyTouchedScreen(SimWorld w, int zoneId, EnemyState e, bool dash) { return false; }
        /// <summary>해금 차례의 후보를 만든다(없으면 Candidates가 빈 채로). 04 요약서 11.3절.</summary>
        public virtual ArtOffer BuildOffer(SimWorld w, PlayerState p, int turnWave) { return new ArtOffer { TurnWave = turnWave }; }
        /// <summary>해금을 적용한다(이 판 즉시). 프로필 저장은 화면·정산 쪽이 한다.</summary>
        public virtual bool Unlock(SimWorld w, PlayerState p, string artId) { return false; }
        /// <summary>비기 트라이 화면용 설명(판 밖에서도 쓴다). candidates = 지금 고를 수 있는 후보.</summary>
        public virtual List<ArtTrieNodeView> DescribeTrie(IContentDb db, string charId, ICollection<string> unlocked, ICollection<string> candidates) { return new List<ArtTrieNodeView>(); }
        /// <summary>플레이어 지대를 전부 걷는다(웨이브 끝).</summary>
        public virtual void ClearZones(SimWorld w) { }
    }

    /// <summary>캐릭터 패시브 5종, 지팡이 특성 5종, 변이 6종. 구현 폴더 Sim/Traits. 02 요약서 8·9절.</summary>
    public class TraitModule
    {
        /// <summary>
        /// 캐릭터·실효 단계·변이에서 PlayerDerived를 만든다(순수 함수, 판 밖에서도 쓴다: 서고 화면의 수치판, 출발 검사).
        /// PlayerDerived를 만드는 곳은 이 함수 하나다 — G01·G13·G21은 ctx.Rules.Traits.ComputeDerived로만 얻는다.
        /// 구현이 없을 때의 기본값도 여기서 준다.
        /// </summary>
        public virtual PlayerDerived ComputeDerived(IContentDb db, CharacterDef ch, int effectiveStage, IReadOnlyList<string> mutations)
        {
            var d = new PlayerDerived { DepthDecay = 1f, EnhanceMul = 1f, MutationDamageMul = 1f };
            if (ch != null)
            {
                d.CritStartHu = SimConst.HeatToHu(ch.CritStart);
                d.LockTicks = SimConst.Ticks(ch.LockSeconds);
                d.CoolHuPerTick = SimConst.HeatToHu(ch.CoolPerSecond) / SimConst.TicksPerSecond;
                d.CoolDelayTicks = SimConst.Ticks(ch.CoolDelaySeconds);
                d.GrazeBuffTicks = SimConst.Ticks(ch.GrazeBuffSeconds);
                d.InvulnTicks = SimConst.Ticks(ch.InvulnSeconds);
            }
            return d;
        }
        /// <summary>주문 컴파일 문맥(밑줄, 깊이 감쇠, 연결 칸 수)을 만든다. 컴파일 문맥을 만드는 곳은 이 함수 하나다
        /// (Player.RecompileSpell, IWandService의 출발 검사, 야영·서고 화면의 미리보기가 같은 값을 쓴다).</summary>
        public virtual SpellCompileContext CompileContext(in PlayerDerived derived, int linkSlotCount)
        {
            return new SpellCompileContext { LinkSlotCount = linkSlotCount, Underline = derived.Underline, DepthDecay = derived.DepthDecay };
        }
        /// <summary>p.Derived를 다시 채운다(판 시작, 유리 지팡이 임시 하락 뒤) = ComputeDerived(w.Content, p.Def, p.Wand.EffectiveStage, p.Wand.Mutations).</summary>
        public virtual void RecomputeDerived(SimWorld w, PlayerState p)
        {
            p.Derived = ComputeDerived(w.Content, p.Def, p.Wand.EffectiveStage, p.Wand.Mutations);
        }
        /// <summary>과열 잠금에 들어간 순간(끓어넘침).</summary>
        public virtual void OnHeatLockEntered(SimWorld w, PlayerState p, bool fromUnstableCore) { }
        /// <summary>스침이 인정됐다(피뢰침 체질).</summary>
        public virtual void OnGrazeAwarded(SimWorld w, PlayerState p, int castId) { }
        /// <summary>플레이어 쪽 피해가 적을 맞혔다(살얼음 횟수). 한 번의 폭발·관통은 적마다 1번으로 넘어온다.</summary>
        public virtual void OnEnemyHit(SimWorld w, in HitInfo hit) { }
        /// <summary>적이 죽었다(탐식).</summary>
        public virtual void OnEnemyKilled(SimWorld w, EnemyState e, in HitInfo killingHit) { }
        /// <summary>플레이어가 맞았다(유리 지팡이 임시 하락).</summary>
        public virtual void OnPlayerHit(SimWorld w, PlayerState p) { }
        public virtual void OnWaveStart(SimWorld w) { }
        /// <summary>캐릭터 배율. crystalsHeld = 발사 순간 가진 결정(비기는 쓰고 남은 수). 그림자 계약이 아니면 1.</summary>
        public virtual float CharacterMul(SimWorld w, PlayerState p, int crystalsHeld) { return 1f; }
        /// <summary>불안정 핵: 이번 J 발사가 자폭인가. Player.Stage6_Fire가 **실제로 발사가 나갈 조건(J 눌림, 간격, 잠금 아님, AttackAllowed,
        /// 유효한 주문)을 다 통과한 뒤, 면제·열 계산 직전에 정확히 한 번** 부른다(누르고만 있는 틱에는 부르지 않는다).
        /// 난수는 여기서 굴리지 않아도 된다: 0.3초 전 예고(UnstableCoreWarn)를 위해 OnPlayerFired에서 다음 발사분을 미리 굴려 두고
        /// 이 메서드는 그 결과를 돌려준다(판의 첫 발사분은 SetupPlayer 뒤 RecomputeDerived에서). 발사 간격이 예고 시간보다 짧으면 예고는 직전 발사 때 낸다.</summary>
        public virtual bool RollUnstableCore(SimWorld w, PlayerState p) { return false; }
        /// <summary>J 발사가 실제로 나갔다(자폭 발사 포함). Player.Stage6_Fire가 Arts.OnPlayerFired 바로 뒤에 부른다.
        /// 불안정 핵의 다음 발사분 굴림·예고가 여기서 일어난다.</summary>
        public virtual void OnPlayerFired(SimWorld w, PlayerState p, int shotId, int intervalTicks) { }
        /// <summary>뒤틀린 총열: 루트 탄 하나의 방향 흔들림(도). 변이가 없으면 0.</summary>
        public virtual float RollSpreadDegrees(SimWorld w, PlayerState p) { return 0f; }
        /// <summary>폭발 반경 배율(불안정 핵 2, 없으면 1).</summary>
        public virtual float ExplosionRadiusMul(PlayerState p) { return 1f; }
        public virtual void Stage11_TickEnd(SimWorld w) { }
    }

    /// <summary>적의 위협: 예고 구역, 동시 예고 상한과 대기 줄, R5, 적 탄, 남는 구역, 장애물. 구현 폴더 Sim/Threats. 05 요약서 1~3절.</summary>
    public class ThreatModule
    {
        /// <summary>예고를 청한다. 돌려주는 값 = 표(ticket). 허가되면 구역이 w.Zones에 생긴다.</summary>
        public virtual int RequestTelegraph(SimWorld w, TelegraphRequest req) { return 0; }
        public virtual TicketStatus Status(SimWorld w, int ticket) { return TicketStatus.Unknown; }
        /// <summary>표의 시작 틱과 판정 틱(구역이 여럿이면 가장 늦은 판정 틱). 아직 대기 중이거나 모르는 표면 false.
        /// 주인의 자체 타격은 w.Tick &gt;= detonateTick인 틱의 7단계에 시작한다(공통 규칙 4).</summary>
        public virtual bool TryGetTiming(SimWorld w, int ticket, out int startTick, out int detonateTick) { startTick = 0; detonateTick = 0; return false; }
        /// <summary>표가 만든 구역(허가 전이면 빈 목록).</summary>
        public virtual void GetZones(SimWorld w, int ticket, List<ZoneEntity> into) { }
        public virtual void CancelTicket(SimWorld w, int ticket) { }
        /// <summary>그 적의 미판정 예고와 대기 중인 요청을 전부 취소(빙결·기절·사망).</summary>
        public virtual void CancelTelegraphsOf(SimWorld w, int enemyId) { }
        /// <summary>적 탄 하나를 만든다. 화면 상한(150)이면 null.</summary>
        public virtual EnemyShotEntity SpawnShot(SimWorld w, in EnemyShotSpec spec) { return null; }
        public virtual HazardEntity SpawnHazard(SimWorld w, in HazardSpec spec) { return null; }
        public virtual ObstacleEntity SpawnObstacle(SimWorld w, in ObstacleSpec spec) { return null; }
        /// <summary>8단계: 대기 줄 재검사(요청 틱, id 오름차순), 허가된 요청 → 구역. 상한 = w.Wave.TelegraphCap(0 이하 = 상한 없음).</summary>
        public virtual void Stage8_Queue(SimWorld w) { }
        /// <summary>9단계: 적 탄 이동·벽·병풍·플레이어 충돌(피격 후보).</summary>
        public virtual void Stage9_Shots(SimWorld w) { }
        /// <summary>9단계: 판정 틱이 된 구역 판정, 남는 구역 생성·접촉·만료, 플레이어 환경 감속(EnvSpeedMul) 갱신.</summary>
        public virtual void Stage9_Zones(SimWorld w) { }
        /// <summary>이번 틱의 스침 후보(탄, 돌진 중인 적의 몸, 이번 틱에 터진 구역)를 모은다. 막힌 탄은 넣지 않는다.</summary>
        public virtual void CollectGraze(SimWorld w, PlayerState p, List<GrazeCandidate> into) { }
        /// <summary>반경 안의 적 탄을 지운다(radius ≤ 0 = 전부). 지운 수를 돌려준다.</summary>
        public virtual int ClearShots(SimWorld w, Vector2 center, float radius) { return 0; }
        /// <summary>예고·남는 구역·적 탄·장애물을 전부 걷는다(웨이브 클리어, 되비침).</summary>
        public virtual void ClearAll(SimWorld w, bool keepObstacles) { }
        /// <summary>그 자리가 적·예고·남는 구역 경계에서 clearance 이상 떨어져 있나(별똥 자리 찾기).</summary>
        public virtual bool IsClear(SimWorld w, Vector2 pos, float clearance) { return true; }
        /// <summary>플레이어가 받는 최종 칸 수(성장, 최종 격노 +1, 3칸 상한). 05 요약서 3.1절. Dash는 Telegraph와 같이 센다.
        /// 최종 격노는 w.Boss != null &amp;&amp; w.Boss.State != Dead &amp;&amp; w.Boss.FinalRage일 때만 더한다. QueueHit을 부르는 순간에 부른다(공통 규칙 6).</summary>
        public virtual int DamageCells(SimWorld w, int baseDamage, PlayerDamageSource source, EnemyState owner) { return baseDamage; }
        /// <summary>R5: 제안한 구역을 더해도 안전 경로가 남는가.</summary>
        public virtual bool CheckEscape(SimWorld w, IReadOnlyList<ZoneSpec> proposed, int ownerEnemyId) { return true; }
        /// <summary>선분이 장애물에 막히나(플레이어 탄·적 탄·이동 공용).</summary>
        public virtual bool SegmentHitsObstacle(SimWorld w, Vector2 a, Vector2 b, bool forPlayerShot, out ObstacleEntity hit) { hit = null; return false; }
        /// <summary>플레이어 탄이 장애물을 맞혔다.</summary>
        public virtual void DamageObstacle(SimWorld w, ObstacleEntity o, float amount) { }
        /// <summary>남는 구역을 제거하고 수명 종료 이벤트와 위협 내부 상태를 함께 정리한다.</summary>
        public virtual void RemoveHazard(SimWorld w, HazardEntity h) { }
        /// <summary>살아 있는 남는 구역을 옮긴다.</summary>
        public virtual void MoveHazard(SimWorld w, HazardEntity h, Vector2 pos) { }
        /// <summary>장애물을 제거한다. broken은 파괴 연출 여부다.</summary>
        public virtual void DestroyObstacle(SimWorld w, ObstacleEntity o, bool broken) { }
    }

    /// <summary>일반 적 12종 + 졸개 + 엘리트 12종의 AI, 몸 이동, 접촉, 군중 제어, 사망. 구현 폴더 Sim/Enemies. 05 요약서 4~7절.</summary>
    public class EnemyModule
    {
        /// <summary>적 하나를 만든다(체력 = base × H(w) × 배율, w = max(1, w.Wave.Wave)). 보스 몸통은 Bosses가 이 메서드 대신 직접 만든다.</summary>
        public virtual EnemyState Spawn(SimWorld w, in EnemySpawnSpec spec) { return null; }
        /// <summary>7단계: id 오름차순으로 상태 전이·이동 의도·예고 요청.</summary>
        public virtual void Stage7_Ai(SimWorld w) { }
        /// <summary>9단계: 적 이동(감속·병풍·장애물), 돌진, 접촉 피해 후보.</summary>
        public virtual void Stage9_Move(SimWorld w) { }
        /// <summary>10단계: PendingDeath인 적을 id 오름차순으로 처리(사망 부수효과 → Run/Traits/Waves의 OnEnemyKilled → 제거).</summary>
        public virtual void Stage10_Deaths(SimWorld w) { }
        /// <summary>적 쪽 피해 조정(땅속 0, 철갑, 기절 ×1.5, 보스면 Bosses로 넘김). 상성·잠식은 이미 곱해져 온다.
        /// fromPos = 온 쪽(정면 철갑 판정: 탄이 날아온 구간의 시작점, 폭발 중심, 전이의 앞 대상), hitPos = 맞은 자리(몸 겉면. 밤삼킴의 부위 판정).</summary>
        public virtual float ModifyIncomingDamage(SimWorld w, EnemyState e, float amount, bool isCrit, bool ignoresArmor, Vector2 fromPos, Vector2 hitPos) { return amount; }
        /// <summary>군중 제어를 꼬리표 표대로 건다. amount = 칸(넉백·당김) 또는 틱(빙결·경직·기절) 또는 0~1(감속). 실제 걸린 양을 돌려준다.</summary>
        public virtual float ApplyCc(SimWorld w, EnemyState e, CcKind kind, float amount, CcSource source, Vector2 dir) { return 0f; }
        /// <summary>감속을 출처 칸에 건다(보스 절반은 이 안에서).</summary>
        public virtual void ApplySlow(SimWorld w, EnemyState e, SlowSlot slot, float strength, int ticks, CcSource source) { }
        public virtual void ApplyCorrosion(SimWorld w, EnemyState e, int ticks) { }
        /// <summary>돌진 예고 중인 꼬리표 없는 일반 적이면 돌진을 끊고 경직시킨다(반동 사격).</summary>
        public virtual bool TryCancelDash(SimWorld w, EnemyState e, int staggerTicks) { return false; }
        /// <summary>적을 피해 없이 치운다(보스 사망 때 졸개, 웨이브 끝). 보상·사망 부수효과·훅 없음.
        /// minionsOnly = true: IsBoss가 아닌 전부(보스 웨이브에는 졸개뿐이다) / false: 보스 몸통까지 전부.</summary>
        public virtual void RemoveAll(SimWorld w, bool minionsOnly) { }
    }

    /// <summary>보스 6종과 허상 첫 만남·복수. 구현 폴더 Sim/Bosses. 05 요약서 8~10절.</summary>
    public class BossModule
    {
        /// <summary>그 웨이브의 보스 id(첫 만남이면 허상).</summary>
        public virtual string SelectBoss(IContentDb db, int wave, bool phantomFirst) { return null; }
        public virtual float MaxHp(IContentDb db, string bossId, int wave, bool firstEncounter) { return 0f; }
        /// <summary>보스를 등장시킨다(w.Boss와 몸통 EnemyState를 만든다, State = Intro, StateStartTick = w.Tick). BossIntro에 들어가는 틱에 Run이 부른다.
        /// 복수 꼬리표 = w.Run.RevengeActive &amp;&amp; 웨이브 20 허상.</summary>
        public virtual EnemyState SpawnBoss(SimWorld w, WavePlan plan) { return null; }
        /// <summary>7단계. 싸움 상태 시계(Intro, Reflection, Collapse 포함)는 RunPhase·w.CombatActive와 무관하게 여기서 센다 — Run은 w.Boss.State만 읽는다:
        /// Intro가 끝나 Waiting이 되면 Run이 같은 틱 11단계에 WaveCombat으로, Reflection → PhantomFinale, Collapse → PhantomDowned,
        /// CollapseDone → Run이 Player.Rise + Dismiss. 패턴 고르기·실행은 w.CombatActive일 때만 한다.</summary>
        public virtual void Stage7_Ai(SimWorld w) { }
        /// <summary>10단계: 단계 전환, 체력 바닥, 첫 만남 종료 조건, 사망 연출 시작.</summary>
        public virtual void Stage10_AfterDamage(SimWorld w) { }
        /// <summary>보스 쪽 피해 조정(등장·되비침 중 0, 회복 배율, 두뇌의 부위 배율). Enemies.ModifyIncomingDamage가 넘긴다.</summary>
        public virtual float ModifyIncomingDamage(SimWorld w, EnemyState e, float amount, bool isCrit, Vector2 fromPos, Vector2 hitPos) { return amount; }
        /// <summary>뇌광창: 보스 동작 멈춤.</summary>
        public virtual void ActionHalt(SimWorld w, EnemyState e, int ticks) { }
        /// <summary>첫 만남에서 플레이어 체력이 0이 됐다 → 바로 쓰러짐으로(Run.OnPlayerDowned가 부른다). 되비침이 스스로 쓰러뜨릴 때는
        /// Bosses가 Player.ForceDown을 쓰고 직접 Collapse로 가므로 이 메서드가 다시 불리지 않는다.</summary>
        public virtual void BeginCollapse(SimWorld w) { }
        /// <summary>싸움이 끝나 보스를 걷는다: 남은 몸통을 치우고 **w.Boss = null**. Run이 보스 웨이브의 클리어 처리 끝과
        /// 첫 만남 일어남 뒤에 항상 부른다(다음 웨이브에 FinalRage·보스 바가 남지 않게).</summary>
        public virtual void Dismiss(SimWorld w) { w.Boss = null; }
    }

    /// <summary>웨이브 디렉터: 구성 생성, 묶음 방출, 변덕, 지체 방지, 별똥, 날씨. 구현 폴더 Sim/Waves. 06 요약서 1~3절, 01 요약서 7절.</summary>
    public class WaveModule
    {
        /// <summary>다음 웨이브 구성을 만든다(야영에 들어갈 때 Run이 부른다).</summary>
        public virtual WavePlan BuildPlan(SimWorld w, int wave) { return new WavePlan { Wave = wave, Tod = DayCycle.Tod(wave) }; }
        /// <summary>웨이브를 시작한다(w.Wave를 새로 채움, 변덕 적용, 날씨·별똥 추첨). 좁은 들 예고가 있으면 그 틱 수를 돌려준다.</summary>
        /// <remarks>Stats의 Wave·IsBoss·PhantomFirst·Whims를 구성에서 채우는 것도 BeginWave의 몫이다(점수 계산이 읽는다).</remarks>
        public virtual int BeginWave(SimWorld w, WavePlan plan)
        {
            var ws = new WaveState { Wave = plan.Wave, Plan = plan, StartTick = w.Tick };
            ws.Stats.Wave = plan.Wave;
            ws.Stats.IsBoss = plan.BossId != null;
            ws.Stats.PhantomFirst = plan.PhantomFirst;
            ws.Stats.Whims.AddRange(plan.Whims);
            w.Wave = ws;
            return 0;
        }
        /// <summary>7단계: 묶음 방출, 동시 상한 대기열, 등장 예고.</summary>
        public virtual void Stage7_Spawns(SimWorld w) { }
        /// <summary>11단계: 웨이브 시계, 지체 방지, 별똥(예고·착지·줍기 → Player.GainCrystal·만료), 날씨, 진행 바 비율.</summary>
        public virtual void Stage11_TickEnd(SimWorld w) { }
        /// <summary>일반 웨이브: 모든 묶음 방출 + 생존 0. 보스 웨이브는 Run이 w.Boss로 판단한다.</summary>
        public virtual bool IsCleared(SimWorld w) { return false; }
        /// <summary>적이 죽었다(마지막 숨의 원).</summary>
        public virtual void OnEnemyKilled(SimWorld w, EnemyState e) { }
        /// <summary>웨이브가 끝났다(별똥·날씨·좁은 들 정리).</summary>
        public virtual void EndWave(SimWorld w) { }
        /// <summary>곡선 함수(06 요약서 1.1절). 콘텐츠는 구현이 세계를 처음 받을 때(BuildPlan·BeginWave 등) w.Content에서 붙잡는다 —
        /// 붙기 전에는 H = 1, 예산 0, 상한 없음(연습 방의 허수아비는 웨이브 1이라 H(1) = 1로 같다).</summary>
        public virtual float HpMul(int wave) { return 1f; }
        public virtual int Budget(int wave) { return 0; }
        public virtual int TelegraphCap(int wave) { return int.MaxValue; }
        /// <summary>등장 자리(플레이어에게서 먼 두 변, 종별 예외). 좁은 들이면 가시 띠 바로 안쪽으로 당긴다.</summary>
        public virtual Vector2 PickSpawnPoint(SimWorld w, EnemyDef def) { return Vector2.zero; }
    }

    /// <summary>판 상태 기계, 전리품, 야영, 원정 지갑, 판 중 드랍, 연습·튜토리얼 방. 구현 폴더 Sim/Run. 01·06 요약서.</summary>
    public class RunModule
    {
        /// <summary>판을 시작한다: w.Run을 채우고 플레이어를 세우고 첫 상태(StartCamp 또는 WavePrelude)로.</summary>
        public virtual void StartRun(SimWorld w, RunConfig cfg) { }
        /// <summary>0단계: 쌓인 RunCommand를 적용한다.</summary>
        public virtual void Stage0_Commands(SimWorld w) { }
        /// <summary>11단계 끝: 체력 조각 줍기, 판 상태 전이(전투 → 클리어 → 전리품 → 야영 → 출발, 첫 만남, 판 종료).
        /// 야영(Camp·StartCamp)의 FireIdle에서는 여기서 직접 읽는다: 모닥불 반경 안 + p.JPressedThisTick → CampSub = CampScreen,
        /// 출발 표식 반경 안 + J 눌림 유지 → DepartHold(60틱) → 출발. 보스 웨이브의 전이는 w.Boss.State만 보고 한다(공통 규칙 5).</summary>
        public virtual void Stage11_Phase(SimWorld w) { }
        /// <summary>9단계(내 탄 이동 뒤): 결투에서 플레이어 탄이 상대 플레이어를 맞히는 판정(Mode == Duel일 때만 일한다).</summary>
        public virtual void Stage4_DuelSync(SimWorld w) { }
        /// <summary>결투 과녁이 맞았다: attackerSlot이 victimSlot에게 amount(무한 웨이브 배율이 든 값)만큼. 돌려주는 값 = 실제로 깎인 체력.</summary>
        public virtual int DuelProxyDamage(SimWorld w, int victimSlot, int attackerSlot, float amount, UnityEngine.Vector2 at) => 0;
        /// <summary>결투 기권·끊김: loserSlot이 경기에서 진다(결투가 아니면 아무 일도 없다).</summary>
        public virtual void ForfeitDuel(SimWorld w, int loserSlot) { }
        /// <summary>적이 죽었다: 골드(지갑), 점수 재료, 체력 조각, 재료 기회.</summary>
        public virtual void OnEnemyKilled(SimWorld w, EnemyState e, in HitInfo killingHit) { }
        /// <summary>플레이어 체력이 0이 됐다(Player.Stage10_ResolveHits가 부른다). 첫 만남이면 Bosses.BeginCollapse,
        /// 아니면 그 자리에서 Phase = RunEnded(HpZero)로 바꾼다(공통 규칙 8).</summary>
        public virtual void OnPlayerDowned(SimWorld w, PlayerState p) { }
        public virtual void OnPlayerHit(SimWorld w, PlayerState p) { }
        public virtual void OnGraze(SimWorld w, PlayerState p) { }
        /// <summary>비기가 발동했다(Arts가 발동을 확정한 틱 5단계 전에 부른다 — 효과는 준비 시간 뒤). 만개 발동 수(손맛 칭호),
        /// 튜토리얼 ④ 반동 사격 완료가 여기서 센다. 판이 끝난 뒤에는 K가 허용되지 않아 불리지 않는다.</summary>
        public virtual void OnArtFired(SimWorld w, PlayerState p, string artId) { }
        /// <summary>비기 값으로 결정을 썼다(Player.SpendCrystals·SpendAllCrystals). count = 치른 값(가진 결정 + 새 빚).
        /// 연습 방 "결정 무한"이 켜져 줄지 않아도 치른 값으로 부른다(수치판 "결정 1개당 피해").</summary>
        public virtual void OnCrystalsSpent(SimWorld w, PlayerState p, int count) { }
        public virtual void AddPurse(SimWorld w, int gold, Vector2 from) { }
        /// <summary>지금 야영의 체력 회복 값.</summary>
        public virtual int HealCost(SimWorld w) { return HealCost(w, 0); }
        /// <summary>그 슬롯 사람의 지금 야영 체력 회복 값(같은 야영의 두 번째부터 비싸지는 것은 사람마다 센다).</summary>
        public virtual int HealCost(SimWorld w, int slot) { return 0; }
        /// <summary>협동: 그 슬롯 사람이 판 도중에 나갔다(서버가 끊김 뒤 부른다). 보상 고르기·출발 기다림에서 빼고, 남은 사람이 다 끝냈으면 넘어간다.</summary>
        public virtual void OnPlayerLeft(SimWorld w, int slot) { }
        /// <summary>판 끝 요약(정산·중간 지점 저장용).</summary>
        public virtual RunSummary BuildSummary(SimWorld w) { return new RunSummary(); }
    }

    /// <summary>점수 규칙(순수 함수). 구현 폴더 Sim/Score. 01 요약서 6절.</summary>
    public class ScoreRules
    {
        public virtual int WaveScore(IContentDb db, WaveStats stats, out ScoreBreakdown breakdown) { breakdown = default; return 0; }
        public virtual int SkipScore(IContentDb db, int wave) { return 0; }
        /// <summary>
        /// 진행 중인 웨이브에서 지금까지 쌓인 몫(처치·보스 처치·스침, 웨이브 계수·변덕 배율 적용). 표시 전용 읽기 — 판 점수(넘긴 웨이브의 합,
        /// 01 요약서 6.2절)에는 더하지 않는다. 클리어·빠른 처리·무피격 몫은 클리어 때 정해지므로 들지 않는다. 상태를 바꾸지 않는 순수 함수.
        /// </summary>
        public virtual int WaveProgressScore(IContentDb db, WaveStats stats) { return 0; }
    }

    // ---------------------------------------------------------------- 모듈 묶음과 조립

    public sealed class SimModuleSet
    {
        public PlayerModule Player = new PlayerModule();
        public SpellModule Spells = new SpellModule();
        public ArtModule Arts = new ArtModule();
        public TraitModule Traits = new TraitModule();
        public ThreatModule Threats = new ThreatModule();
        public EnemyModule Enemies = new EnemyModule();
        public BossModule Bosses = new BossModule();
        public WaveModule Waves = new WaveModule();
        public RunModule Run = new RunModule();
        public ScoreRules Score = new ScoreRules();

        /// <summary>전부 빈 구현인 묶음(테스트가 자기 모듈만 갈아 끼운다).</summary>
        public static SimModuleSet Stubs() => new SimModuleSet();
        /// <summary>실제 게임에서 쓰는 시스템을 판마다 새로 만든다.</summary>
        public static SimModuleSet CreateDefault() => new SimModuleSet
        {
            Player = new Player.PlayerSystem(),
            Spells = new Spells.SpellSystem(),
            Arts = new Arts.ArtSystem(),
            Traits = new Traits.TraitSystem(),
            Threats = new Threats.ThreatSystem(),
            Enemies = new Enemies.EnemySystem(),
            Bosses = new Bosses.BossSystem(),
            Waves = new Waves.WaveDirector(),
            Run = new Run.RunFlow(),
            Score = new Score.ScoreCalculator(),
        };
    }
}

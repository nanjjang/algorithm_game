using UnityEngine;

namespace Wizard.Solo.Sim
{
    /// <summary>
    /// 표시 이벤트 종류. 시뮬레이션은 사건이 난 틱에 SimWorld.Emit으로 쌓고, 표시 계층(월드 뷰, HUD, 소리)이 틱 뒤에 읽는다.
    /// 이벤트는 표시 전용이다. 규칙 판단을 이벤트에 기대지 않는다(모듈끼리는 Modules.cs의 메서드로 부른다).
    /// 각 줄의 괄호는 SimEvent 필드 쓰임: S = Slot(플레이어), E = EntityId, A/B = 정수, F = 실수, P = Pos, Id = 문자열 id.
    /// 번호는 더하기만 한다.
    /// </summary>
    public enum SimEventType
    {
        None = 0,

        // --- 입력·자세 (02 요약서 11절)
        FacingChanged = 10,         // S, A = Dir8
        StanceEntered,              // S, A = StanceState
        StanceExited,               // S
        ReverseFlipped,             // S
        CommandReset,               // S, A = CommandResetReason
        CommandDirInput,            // S, A = CmdDir, B = 이어짐(1/0)

        // --- 과열
        ShotFired = 30,             // S, A = shotId, B = 임계(1/0), F = 발사 뒤 열(0~100)
        FireDryWhileLocked,         // S
        HeatRingChanged,            // S, A = HeatRingState
        HeatLockEntered,            // S, A = 잠금 전체 틱
        HeatUnlocked,               // S, A = HeatUnlockReason
        HeatExemptArmed,            // S
        HeatExemptConsumed,         // S
        KilnAbsorbed,               // S, A = 먹은 HU

        // --- 스침·피격
        GrazeAwarded = 50,          // S, A = castId, B = GrazeKind, P = 위협 위치
        GrazeBuffStarted,           // S, A = 틱
        GrazeBuffEnded,             // S
        LockBrokenByGraze,          // S
        PlayerHit,                  // S, A = 피해(점), B = 남은 체력(점), E = 때린 적 id, F = PlayerDamageSource(정수, 판 끝 요약의 사인), P = 온 쪽
        InvulnEnded,                // S
        PlayerDowned,               // S
        PlayerRose,                 // S (첫 만남 일어남)
        PlayerHealed,               // S, A = 회복 칸

        // --- 콤보·결정 (10 요약서 1·2절)
        ComboChanged = 70,          // S, A = 콤보, B = 오른 양
        ComboReset,                 // S, A = ComboResetReason, B = 끊기기 전 콤보
        CrystalGained,              // S, A = 몇 번째, B = CrystalSource
        CrystalsFull,               // S
        CrystalGainBlocked,         // S
        CrystalSpent,               // S, A = 쓴 개수
        CrystalLost,                // S, A = 깨진 개수
        CrystalProtected,           // S
        DebtAdded,                  // S, A = 지금 빚
        DebtRepaid,                 // S, A = 지금 빚

        // --- 비기 (04 요약서 13절)
        ArtFired = 90,              // S, Id = 비기 id, A = 부모 대체(1/0), B = 섬(1/0), F = 반복 수(만개)
        ArtFizzled,                 // S, Id = 고른 비기, A = ArtFizzleReason, B = 늦은 입력 번호, F = 늦은 초
        ArtBuffered,                // S
        ArtPulse,                   // S, Id = 비기 id, A = 몇 번째 파동/단계, P = 중심, F = 반경
        ArtLine,                    // S, Id = 비기 id, P = 시작, P2 = 끝, F = 폭 (창, 질주 길)
        PlayerZoneSpawned,          // S, E = zone id, A = PlayerZoneKind
        PlayerZoneExpired,          // S, E = zone id, A = PlayerZoneKind, B = 부서짐(1/0)
        PlayerMovedByArt,           // S, Id = 비기 id, P = 출발, P2 = 도착
        ArtUnlocked,                // S, Id = 비기 id
        ArtOffered,                 // S, A = 차례 웨이브

        // --- 패시브·변이
        BoiloverTriggered = 110,    // S, P
        LightningRodStrike,         // S, E = 적 id, P = 적 위치
        UnstableCoreWarn,           // S
        UnstableCoreExploded,       // S, P
        WandTempStageDropped,       // S, A = 내려간 뒤 실효 단계
        UnderlineSlotChanged,       // S, A = 슬롯 번호(-1 = 없음)
        ThinIceFroze,               // S, E = 적 id, A = 틱

        // --- 플레이어 탄·피해
        ProjectileSpawned = 130,    // S, E = 탄 id, Id = 블록 id, A = 슬롯 번호, B = 스코프 깊이, P = 생긴 자리
        ProjectileExpired,          // S = 쏜 슬롯, E = 탄 id, A = 이유(0 사거리, 1 벽, 2 적중, 3 돌아옴), P
        ProjectileBounced,          // S = 쏜 슬롯, E = 탄 id, P
        Explosion,                  // S, E = 탄 id, Id = 속성 id, P = 중심, F = 반경
        ChainLightningArc,          // S, E = 탄 id, P = 시작, P2 = 끝, A = 전이 번호
        EnemyDamaged,               // S = 때린 슬롯(-1 = 환경), E = 적 id, A = 피해(반올림), B = DamageFlags, Id = 속성 id, P = 적중 지점(몸 겉면)
        ShotCapHit,                 // S, A = 요청 수, B = 만든 수
        AffinityDiscovered,         // S, E = 적 id, A = Affinity(Weak/Resist), Id = 속성 id, P = 적 위치 (그 종의 약점·저항을 처음 알아냄)
        ProjectilePassed,           // S, E = 탄 id, A = 지나간 적 id, P = 지나간 자리 (그림자 구체가 적을 꿰뚫고 지나감)

        // --- 적
        EnemySpawnNotice = 150,     // Id = 종 id, P, A = 예고 틱
        EnemySpawned,               // E, Id = 종 id, A = 엘리트(1/0)
        EnemyKilled,                // E, Id = 종 id, A = 골드, B = 0 일반 / 1 엘리트 / 2 보스 / 3 보상 없음, P
        EnemyCc,                    // E, A = CcKind, B = 틱(또는 0), F = 거리
        EnemyStateChanged,          // E, A = EnemyAiState
        EnemyShotFired,             // E = 탄 id, A = 쏜 적 id, P
        EnemyShotBlocked,           // E = 탄 id, P
        EnemyShotsCleared,          // A = 개수, P = 중심, F = 반경(0 = 전부)
        EnemyBuff,                  // E, Id = 버프 이름, A = 켜짐(1/0)
        EnemyDashCancelled,         // S = 끊은 플레이어, E = 적 id, P = 적 위치 (반동 사격이 돌진 예고를 끊음, 04 요약서 ② "끊음")

        // --- 예고·구역 (05 요약서 11절)
        TelegraphStarted = 170,     // E = zone id
        TelegraphLocked,            // E
        TelegraphFlash,             // E, A = 번쩍 틱 수(판정 N−2틱에 한 번 낸다. 표시가 A틱 동안 그린다)
        TelegraphDetonated,         // E
        TelegraphCancelled,         // E
        HazardSpawned,              // E = hazard id, A = HazardKind
        HazardExpiring,             // E
        HazardExpired,              // E
        ObstacleSpawned,            // E = obstacle id, Id = 종류
        ObstacleDestroyed,          // E, A = 부서짐(1) / 만료(0)

        // --- 보스·첫 만남·복수
        BossIntroBegin = 190,       // E = 적 id, Id = 보스 id, A = 복수 꼬리표(1/0)
        BossIntroEnd,               // E
        BossPhaseChanged,           // E, A = 단계 색인
        BossRageStack,              // E, A = 스택
        BossFinalRage,              // E
        BossKilled,                 // E, Id = 보스 id, P
        BossMinionSpawned,          // E = 졸개 id
        BossPatternStarted,         // E, Id = 패턴 key, A·B = 패턴별(허상 따라 쓰기: A = 비기 번호(Ids.Art.All 순서, 1부터), B = 예고 틱 /
                                    //   되비침 만개 연출 Id = "reflection_bloom": A = 1~3 / 비늘 탄 Id = "scale_volley": B = 깜박임 틱)
        PhantomReflection,          // 되비침 시작
        PhantomCollapse,            // 쓰러짐 시작
        PhantomMasterLine,          // 대사 판(phantom_first.master_line)
        PhantomMirrorCrack,         // 허상 체력 바닥 도달
        RevengeKill,                // 복수 처치 연출 시작(E = 적 id, P). 연출 틱은 이 이벤트의 Tick 기준

        // --- 웨이브·판
        RunStarted = 220,           // A = 시작 웨이브
        PhaseChanged,               // A = 새 RunPhase, B = 이전 RunPhase
        WaveStarted,                // A = 웨이브, Id = 틀 id
        WaveCleared,                // A = 웨이브, B = 웨이브 점수
        WaveStalled,                // A = 웨이브
        DayChanged,                 // A = 새 날, B = 소품 배치 번호
        TodChanged,                 // Id = tod id
        WeatherStarted,             // Id = event id, A = 변형 번호
        WeatherEnded,               // Id = event id
        MasterAdvice,               // A = 문구 번호(1부터)
        StarfallTelegraph,          // P, A = 틱
        StarfallLanded,             // P
        StarfallPicked,             // S, P
        StarfallExpired,            // P
        DropSpawned,                // E = drop id, A = DropKind, P
        DropPicked,                 // S, E = drop id, A = DropKind
        GoldGained,                 // A = 금액, P = 나온 자리(없으면 (−1, −1))
        PurseChanged,               // A = 지갑
        ScoreChanged,               // A = 점수
        LootOffered,                // A = 카드 수
        LootChanged,
        CampEntered,                // A = 새벽 야영(1/0)
        CampChanged,                // 회복·조립·차례 등 야영 화면이 다시 그려야 할 때
        LoadoutChanged,             // S
        DepartHold,                 // A = 누른 틱(0 = 취소)
        RunEnded,                   // A = RunEndReason
        TutorialStepDone,           // A = 단계
        PracticeReport,             // Id = PracticeReportKeys.*, F = 값 (연습장 수치. Run이 practice.json report_interval_seconds마다 낸다)

        // --- 기본 공격 유형(핵 블록 block.core.*) — Docs/12-attack-styles.md 3절이 필드 표의 정본. Id = 블록 id(그림·소리 열쇠).
        StyleLobLaunched = 260,     // S, E = 탄 id, Id = 블록, P = 출발, P2 = 떨어질 자리, A = 날아가는 틱 수, B = 몇 번째 튐(0 = 처음)
        StyleBlast,                 // S, E = 탄 id, Id = 블록, P = 중심, F = 반경, A = 맞은 적 수, B = StyleBlastKind
        StyleGroundSpawned,         // S, E = 탄 id, Id = 블록, P = 자리, F = 반경, A = 남는 틱 수, B = 0 불바닥 / 1 룬 지뢰
        StyleGroundExpired,         // S, E = 탄 id, Id = 블록, P = 자리, B = 0 시간 끝 / 1 발동(룬 폭발)
        StyleConeStarted,           // S, E = 탄 id, Id = 블록, P = 원점, P2 = 가운데 방향(단위), F = 반경, A = 반각(도), B = 휩쓰기: n > 0 반지름이 n틱에 퍼짐 / −n 각도로 n틱(−반각 → +반각) / −(1000 + n) 각도로 n틱(+반각 → −반각)
        StyleStrikeWarned,          // S, E = 탄 id, Id = 블록, P = 떨어질 자리, F = 반경, A = 남은 예고 틱, B = 몇 번째(고드름 줄)
        StyleRicochet,              // S, E = 탄 id, Id = 블록, P = 튄 자리, P2 = 새 대상 위치, A = 새 대상 적 id
        StyleZap,                   // S, E = 탄 id, Id = 블록, P = 구체 위치, P2 = 맞은 적 위치, A = 맞은 적 id
        StyleSplit,                 // S, E = 부모 탄 id, Id = 블록, P = 갈라진 자리, A = 새 탄 수, B = 세대
        StyleShatter,               // S, E = 탄 id, Id = 블록, P = 깨진 자리, A = 파편 수
        StyleStatus,                // S, E = 적 id, A = 지금 겹 수(0 = 풀림), B = StyleStatusKind, P = 적 위치
        StyleMineArmed,             // S, E = 탄 id, Id = 블록, P = 자리, F = 발동 반경
        ArtStage = 280,             // S, Id = 비기 id, A = ArtStageKind, B = 순번(0부터), P = 중심, P2 = 방향 또는 끝점, F = 반경(고유 비기 강화 단계)
        PlayerRevived = 300,        // S = 일어난 사람, A = 살린 사람 슬롯(−1 = 웨이브 클리어로 일어남). 협동(Docs/11-online.md)
        DuelRoundStarted = 301,     // A = 라운드 번호(1부터), B = 1이면 연장
        DuelRoundEnded = 302,       // A = 이긴 슬롯(−1 = 무승부), B = DuelRoundEndKind
        DuelMatchEnded = 303        // A = 이긴 슬롯(−1 = 무승부), B = 슬롯 0 승수, F = 슬롯 1 승수
    }

    /// <summary>PracticeReport 이벤트의 Id(08 요약서 8.5절 허수아비 수치).</summary>
    public static class PracticeReportKeys
    {
        public const string Dps = "dps", RecentDamage = "recent_damage", HeatRatio = "heat_ratio", DamagePerCrystal = "damage_per_crystal";
    }

    public enum ArtFizzleReason { None = 0, NoCrystal = 1, FullBloomLate = 2 }

    /// <summary>EnemyDamaged의 B 비트.</summary>
    public static class DamageFlags
    {
        public const int Crit = 1, Weak = 2, Resist = 4, Corroded = 8, Art = 16, Passive = 32, Armored = 64;
    }

    public struct SimEvent
    {
        public SimEventType Type;
        public int Tick;
        public int Slot;
        public int EntityId;
        public int A, B;
        public float F;
        public Vector2 Pos;
        public Vector2 Pos2;
        public string Id;
    }
}

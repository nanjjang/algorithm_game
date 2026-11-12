using System;

namespace Wizard.Content
{
    // CONTRACTS 2.2. spec 07 2.1(단일 레코드), 3.1(하루와 시간대), 5절(예산·묶음·예고 상한·엘리트·종 상한·지체 방지),
    // 6절(체력·피해 성장), 7.2(고정 각본). 팩에서 읽은 뒤 바뀌지 않는다. 원본 data/sim/waves.json. 식은 spec 07 의 정수 식 그대로다.
    //
    // 구간 표(TodRange*, TelegraphCap*, EliteCount*, ModifierCount*)는 같은 길이의 배열 셋이다: 줄 i 는 웨이브 [From[i], To[i]] (닫힌 구간).
    // From 오름차순, 빈틈·겹침 없이 이어지고(To[i] + 1 == From[i + 1]), 마지막 줄만 To = int.MaxValue(∞)일 수 있다.
    // 첫 줄의 From 보다 앞의 웨이브는 0 이다(엘리트 수: 11 이하 0, 변덕 수: 20 이하 0). 보스 웨이브의 엘리트·변덕 0 은 표가 아니라 규칙이다(spec 07 5.4, 7.5).
    public sealed class WaveCurveDef
    {
        public const int Unbounded = int.MaxValue;          // 구간 표의 To 에서 "끝 없음"

        // ── 하루(spec 07 3.1, DECISIONS 2.3) ──
        public int DayLength;                               // 10. Day(w) = ceil(w ÷ 10), LocalWave(w) = (w − 1) % 10 + 1
        // 하루 속 순번(1..DayLength) → 시간대. 1..DayLength 를 빈틈 없이 덮고, tod.boss 는 정확히 순번 DayLength 하나다(IsBossWave = w % 10 == 0)
        public int[] TodRangeFrom = Array.Empty<int>();
        public int[] TodRangeTo = Array.Empty<int>();
        public TimeOfDay[] TodRangeTod = Array.Empty<TimeOfDay>();

        // ── 체력 배율 H(w) × 1,000,000 (spec 07 6.1) ──
        // w ≤ HpLinearUntil: 1,000,000 + HpLinearStepMicro × (w − 1). 그 뒤: HpExpBaseMicro 에서 웨이브마다 × HpExpRatePct ÷ 100 (매 단계 내림)
        public int HpLinearUntil;                           // 12
        public long HpLinearStepMicro;                      // 120,000 (= 0.12)
        public long HpExpBaseMicro;                         // 2,320,000 (= 2.32, w = 12 의 값)
        public int HpExpRatePct;                            // 106 (= ×1.06/웨이브)
        public int EliteHpMul;                              // 3. 엘리트 체력 ×3 (spec 07 5.4·6.2, monsters 3.1)
        public int BossHpRefWave;                           // 10. 보스 체력 = 기본 × HMicro(w) ÷ HMicro(10) (spec 07 6.2)

        // ── 스폰 예산 B(w) (spec 07 5.1). 보스 웨이브에는 예산이 없다 ──
        // w ≤ BudgetLinearUntil: BudgetLinearBase + BudgetLinearPerWave × w. 그 뒤: min(BudgetCap, BudgetLateBase + BudgetLatePerWave × (w − BudgetLinearUntil))
        public int BudgetLinearBase, BudgetLinearPerWave, BudgetLinearUntil;    // 10, 6, 12
        public int BudgetLateBase, BudgetLatePerWave, BudgetCap;                // 82, 4, 200

        // ── 묶음 (spec 07 5.2) ──
        // BundleCount(w) = w ≤ BundleGrowAfter ? BundleBase : min(BundleMax, BundleBase + (w − BundleGrowAfter) ÷ BundleGrowEvery)
        public int BundleBase, BundleGrowAfter, BundleGrowEvery, BundleMax;     // 4, 12, 8, 8
        public int BundleIntervalTicks;                     // 420 (7초). "몰려옴"의 240틱은 ModifierDef
        public int BundleEarlyReleaseAlive;                 // 3. 살아 있는 적이 3마리 이하이면 다음 묶음을 바로 낸다
        public int MaxAlive;                                // 40. 전장 동시 개체 상한

        // ── 동시 예고 상한 (spec 07 5.3): (1,4,2) (5,8,3) (9,12,4) (13,29,5) (30,∞,6) ──
        public int[] TelegraphCapFrom = Array.Empty<int>();
        public int[] TelegraphCapTo = Array.Empty<int>();
        public int[] TelegraphCap = Array.Empty<int>();

        // ── 엘리트 (spec 07 5.4): (12,19,1) (20,29,2) (30,39,3) (40,∞,4). 우두머리 행차의 +2 는 ModifierDef ──
        public int[] EliteCountFrom = Array.Empty<int>();
        public int[] EliteCountTo = Array.Empty<int>();
        public int[] EliteCount = Array.Empty<int>();
        public int EliteCostMul;                            // 3. 엘리트는 예산에서 비용 ×3

        // ── 종 상한 (spec 07 5.5): 한 웨이브 구성 전체의 마릿수 상한(엘리트도 바탕 종에 센다). 없는 종은 상한 없음 ──
        // SpeciesCapSpecies 는 EnemyTypeId 오름차순이고 겹치지 않는다. "긴 밤"의 도깨비불 6 은 ModifierDef
        public EnemyTypeId[] SpeciesCapSpecies = Array.Empty<EnemyTypeId>();
        public int[] SpeciesCap = Array.Empty<int>();

        // ── 지체 방지 (spec 07 5.6). 보스 웨이브에는 적용하지 않는다 ──
        public int StallTicks;                              // 7,200 (120초)
        public int StallKillScorePct;                       // 50. 그 뒤 처치 원점수 = 비용 × ScoringDef.KillPerCost × 50 ÷ 100 (= 비용 × 5)

        // ── 변덕 수 (spec 07 2.1 modifier_count, 9절): (21,40,1) (41,∞,2) ──
        public int[] ModifierCountFrom = Array.Empty<int>();
        public int[] ModifierCountTo = Array.Empty<int>();
        public int[] ModifierCount = Array.Empty<int>();

        // ── 피해 성장 (spec 07 6.3, 칸). 최종 격노 +1 은 보스(spec 06 2.12), 빚의 +1 은 패시브(world-characters 4.5)가 정한다 ──
        public int ContactDamage;                           // 1. 접촉·탄은 끝까지 1칸
        public int TelegraphDamagePlus;                     // 1. 예고 공격의 웨이브 보정
        public int EliteBossPlusFromWave;                   // 31. 이 웨이브부터 엘리트·보스의 예고 공격 +1
        public int AllPlusFromWave;                         // 61. 이 웨이브부터 일반 적의 예고 공격도 +1
        public int AttackDamageCap;                         // 3. 어떤 공격도 한 번에 3칸을 넘지 않는다(공격 쪽 상한, G11 확정)

        // ── 구성 (spec 07 7.2·7.4) ──
        public EnemyTypeId FillSpecies;                     // 끝까지 남은 예산을 채우는 종(enemy.slime_moss: 비용 1, 상한 없음, 항상 가용)
        // 고정 각본: ScriptWaves 오름차순(보스 웨이브 없음). ScriptPlans[i] 가 웨이브 ScriptWaves[i] 의 구성이다.
        // 각본이 있는 웨이브는 틀을 고르지 않는다(1~12, 그리고 새 종이 처음 나오는 13·15는 frame.mixed 강제, spec 07 7.3·18절 C-04)
        public int[] ScriptWaves = Array.Empty<int>();
        public FrameDef[] ScriptPlans = Array.Empty<FrameDef>();
    }
}

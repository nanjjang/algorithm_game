using Wizard.Core;

namespace Wizard.Content
{
    // CONTRACTS 2.2. 별똥 결정(world-characters 2.3 "날씨와 돌발 사건"의 event.star_crystal, GDD 7, effects 11.3).
    // 수치는 spec 01 10.5(원문 + [임시] 표)와 spec 01 2.3 시간 표. 팩에서 읽은 뒤 바뀌지 않는다. 원본 data/sim/starfall.json.
    // 상태: None → Telegraph(TelegraphTicks) → Landed(LandedTicks, 마지막 BlinkTicks 동안 깜빡임) → Picked | Expired(spec 01 10.5).
    // 데이터에 없는 규칙: 웨이브당 한 번만 뽑는다(웨이브당 1회 이하), 결투에는 없다(spec 10, DuelRuleSet), 결정 상한을 넘지 않고
    // 상한이 차 있으면 밟아도 남는다, 그믐은 빚부터 갚는다, 웨이브가 끝나면 남은 별똥은 사라진다(spec 01 10.5).
    public sealed class StarfallDef
    {
        // 나오는 시간대. 밤 = 하루 속 순번 8~9(spec 01 18절 C12, WaveCurveDef.TodRange*)
        public TimeOfDay OnlyTod;

        // 그 시간대의 웨이브마다 웨이브 시작 때 한 번 뽑는 확률(만분율, 50% → 5,000). rng.event 분기(spec 07 14.2)
        public int ChanceBp;

        // 떨어지는 때: 웨이브 시작 뒤 [DropFromTicks, DropToTicks] 틱에서 균등(10~40초 → 600~2,400). 웨이브가 그 전에 끝나면 나오지 않는다
        public int DropFromTicks;
        public int DropToTicks;

        public int TelegraphTicks;                          // 90 (1.5초) 예고. 위험색이 아니다(흰 금빛 고리)
        public int LandedTicks;                             // 480 (8초) 놓여 있는 시간
        public int BlinkTicks;                              // 120 (마지막 2초) 깜빡임. 착지 뒤 LandedTicks − BlinkTicks 틱부터(TV T-X19: 360)

        // 자리("빈 바닥"): 전장 가운데 AreaWidth × AreaHeight 칸(소품이 없는 곳, world-characters 2.1)의 GridStep 격자점 가운데
        // 적·예고 구역·남는 위험 구역 경계에서 HazardClearance 이상, 플레이어에게서 PlayerClearance 이상 떨어진 점을 균등으로.
        // 그런 점이 없으면 RetryTicks 뒤 다시 찾는다
        public Fix64 AreaWidth;                             // 20칸
        public Fix64 AreaHeight;                            // 10칸
        public Fix64 GridStep;                              // 0.5칸
        public Fix64 HazardClearance;                       // 2칸
        public Fix64 PlayerClearance;                       // 3칸
        public int RetryTicks;                              // 60 (1초)

        public Fix64 PickRadius;                            // 0.5칸. 플레이어 판정 중심과 별똥 중심 거리 ≤ PickRadius 이면 줍는다
        public int Crystals;                                // 1. 주우면 얻는 마력 결정 수
    }
}

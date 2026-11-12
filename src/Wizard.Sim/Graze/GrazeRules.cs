using Wizard.Core;

namespace Wizard.Sim.Graze
{
    // spec 01 8.3~8.4 의 상태 없는 규칙. 판정 기하와 가속 기한만 둔다.
    // 세계에 붙이는 것(언제, 누구에게, 몇 번)은 SimWorld.Graze.cs 의 일이다.
    // 열 보상의 계산(8.4, 12.2)은 열을 가진 Heat 모듈에만 있다(이음 훅 OnGrazeAwarded_Heat, T-50). 여기에 사본을 두지 않는다.
    internal static class GrazeRules
    {
        // 탄·돌진 몸(8.3): 감지 원(반지름 R)과 위협의 원이 겹치고(d ≤ R + r), 피격 원과는 겹치지 않는다(d > hitR + r).
        // 제곱으로 비교한다(제곱근 없이 같은 부등식. 거리는 전장 안이라 제곱이 넘치지 않는다)
        public static bool InBand(FixVec2 playerPos, FixVec2 threatPos, Fix64 threatRadius, Fix64 grazeRadius, Fix64 hitRadius)
        {
            Fix64 d2 = FixVec2.DistanceSq(playerPos, threatPos);
            Fix64 outer = grazeRadius + threatRadius;
            Fix64 inner = hitRadius + threatRadius;
            return d2 <= outer * outer && d2 > inner * inner;
        }

        // 터지는 구역(8.3): 피격 원과 겹치지 않고(!Contains), 판정 중심에서 경계까지 ≤ R.
        // 원문의 "경계 바깥 0.6 이내"를 감지 반지름 R 로 읽는다(초롱 0.75. 15절 C6 [해석])
        public static bool ZoneEdge(bool zoneContainsPlayer, Fix64 edgeDistance, Fix64 grazeRadius) =>
            !zoneContainsPlayer && edgeDistance <= grazeRadius;

        // 원 구역의 Contains·DistanceToBoundary. 경계까지 = |p − c| − 반지름. 피격 원이 구역에 닿으면(경계까지 ≤ hitR) 맞는다
        public static void CircleZone(FixVec2 center, Fix64 radius, FixVec2 playerPos, Fix64 playerHitRadius,
                                      out bool containsPlayer, out Fix64 edgeDistance)
        {
            Fix64 d = (playerPos - center).Length();
            edgeDistance = d - radius;
            containsPlayer = edgeDistance <= playerHitRadius;
        }

        // 가속 기한(8.4): 지금 + 캐릭터의 가속 틱. 중첩 없이 갱신한다. 음수 틱은 0으로 읽는다
        public static uint BuffUntil(uint tick, int buffTicks) =>
            buffTicks > 0 ? unchecked(tick + (uint)buffTicks) : tick;
    }
}

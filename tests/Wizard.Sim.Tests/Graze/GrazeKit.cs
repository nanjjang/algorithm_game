using Wizard.Core;
using Wizard.Sim.Graze;
using Wizard.Sim.Tests.Player;

namespace Wizard.Sim.Tests.Graze
{
    // Graze 시험의 도우미. 적 모듈이 아직 없어(M3-ENM-01) 시험은 9단계의 충돌 모듈 대신 Step 직전에
    // 스침 후보(OfferGrazeThreat)와 피격 후보(QueuePlayerHit)를 넣는다. 같은 Step 의 9단계가 판정한다.
    internal static class GrazeKit
    {
        public static readonly PlayerSlot P0 = new PlayerSlot(0);

        // 천분율 칸(0.70 → 700). 명세의 소수 값을 그대로 옮긴다
        public static Fix64 Milli(long permille) => Fix64.FromRatio(permille, 1000);

        // 플레이어 판정 중심에서 오른쪽으로 거리 d 인 점
        public static FixVec2 RightOf(FixVec2 center, Fix64 d) => new FixVec2(center.X + d, center.Y);

        // 첫 Step 이 플레이어를 세운다(시작 상태 훅이 없다). tick 까지 키 없이 진행
        public static PlayerRig ReadyAt(uint tick, PlayerRig rig)
        {
            rig.RunTo(tick);
            return rig;
        }

        // 탄 반지름 0.12(T-G12~T-G15)의 적 탄 하나를 플레이어 오른쪽 거리 dMilli 에 둔 후보
        public static GrazeThreat BulletAt(PlayerRig rig, int id, int castId, long dMilli, long radiusMilli = 120) =>
            GrazeThreat.Bullet(new EntityId(id), castId, RightOf(rig.P.Pos, Milli(dMilli)), Milli(radiusMilli));

        public static void Offer(PlayerRig rig, in GrazeThreat t) => rig.World.OfferGrazeThreat(P0, in t);
    }
}

using System.Collections.Generic;
using Wizard.Core;
using Wizard.Sim.Events;
using Wizard.Sim.Graze;
using Xunit;

namespace Wizard.Sim.Tests.Heat.Graze
{
    // 스침의 열 보상 시험(M1-SIM-10)의 도우미. 스침은 Graze 의 문으로만 일으킨다(작업 카드의 수락 조건):
    //   - 9단계의 스침: Step 직전에 OfferGrazeThreat 로 후보를 넣고 같은 Step 의 9단계가 판정한다(적 모듈이 아직 없다, GrazeKit 과 같다).
    //   - 5단계의 비기 스침: 비기 모듈이 아직 없어 Step 직전에 AwardSkillGraze 를 부른다. 5단계에서 부른 것과 같은 상태로 6단계에 들어간다.
    // 열은 세계가 Graze → RaiseGrazeAwarded → OnGrazeAwarded_Heat 로 반영한다. 이 시험은 Heat 의 메서드를 불러 스침의 열을 만들지 않는다.
    internal static class GrazeHeatKit
    {
        public static readonly PlayerSlot P0 = new PlayerSlot(0);

        public const int MagicBoltHeatHu = 480;     // 마력탄 열 4(spec 01 7.8 조건)
        public const int BaseCritStartHu = 10200;   // 기준값 임계 시작 85(7.1)

        public static Fix64 Milli(long permille) => Fix64.FromRatio(permille, 1000);

        // 기준값 감지 0.6, 피격 0.15(결투 0.30). 탄 반지름 0.12, 거리 0.70 은 무한 웨이브와 결투 모두에서 스침 띠 안이다(T-G12)
        public static GrazeThreat BulletBeside(HeatRig rig, int slot, int threatId, int castId)
        {
            FixVec2 p = rig.World.PlayerAt(slot).Pos;
            return GrazeThreat.Bullet(new EntityId(threatId), castId, new FixVec2(p.X + Milli(700), p.Y), Milli(120));
        }

        // 이번 틱(rig.Tick)의 9단계에 판정될 스침 후보 하나
        public static void OfferGraze(HeatRig rig, int threatId, int castId, int slot = 0)
        {
            GrazeThreat t = BulletBeside(rig, slot, threatId, castId);
            rig.World.OfferGrazeThreat(new PlayerSlot((byte)slot), in t);
        }

        // 틱 0 부터 J 를 누르고 있다가 n 발째를 쏜 다음 틱에 뗀다. 마력탄이면 n 발째는 틱 9(n − 1), 뗀 틱은 9(n − 1) + 1
        public static void FireShotsThenRelease(HeatRig rig, int n)
        {
            while (rig.Shots().Count < n)
            {
                rig.Step(true);
            }
            Assert.Equal(n, rig.Shots().Count);
            rig.Step(false);
        }

        // 잠길 때까지 J 를 누르고, 잠긴 다음 틱에 뗀다(기준값 마력탄: 26발째 틱 225, T-H01)
        public static void FireUntilLockedThenRelease(HeatRig rig)
        {
            while (!rig.H.Locked)
            {
                rig.Step(true);
            }
            rig.Step(false);
        }

        public static SimEvent LastShot(HeatRig rig)
        {
            List<SimEvent> shots = rig.Shots();
            Assert.NotEmpty(shots);
            return shots[shots.Count - 1];
        }

        public static List<SimEvent> At(HeatRig rig, uint tick) => rig.Log.Events.FindAll(e => e.Tick == tick);

        public static int CountAt(HeatRig rig, SimEventType type, uint tick) =>
            rig.Log.Events.FindAll(e => e.Type == type && e.Tick == tick).Count;

        public static int IndexOf(List<SimEvent> events, SimEventType type) => events.FindIndex(e => e.Type == type);
    }
}

using Wizard.Content;
using Wizard.Core;
using Wizard.Sim.Events;
using Xunit;

namespace Wizard.Sim.Tests.Heat
{
    // CONTRACTS 3.3 "뷰 훅"(T-54)의 Heat 쪽: PlayerView 의 열·임계 시작·링 상태·열 면제·잠금 칸(spec 01 17.1).
    // 기대값의 출처: spec 01 7.8 의 기준값 주기(마력탄 한 발 4 = 480HU, 9틱 간격, 임계 시작 85 = 10,200HU, 26발째 225틱에 잠금,
    // 잠금 = 간격 9 + 180틱), 8.4 의 스침 보상(잠금 해제, 열 = 임계 시작, 열 면제). 같은 값을 HeatPipelineTests·HeatVectorTests 가 상태로 본다.
    public sealed class HeatViewTests
    {
        private static ContentDb Db => HeatData.Baseline;

        private static readonly PlayerSlot P0 = new PlayerSlot(0);

        private static PlayerView View(HeatRig rig) => rig.World.View.Player(P0);

        [Fact]
        public void BeforeTheFirstStep_HeatCellsAreDefault()
        {
            SimWorld world = SimWorld.CreateEndless(Db, SimConfig.ForEndless(Db), new EndlessSetup { Seed = 1UL, StartWave = 1 });
            ulong before = world.ComputeStateHash();
            PlayerView v = world.View.Player(P0);
            Assert.Equal((0, 0, HeatRingState.Cold), (v.HeatHu, v.CritStartHu, v.HeatRing));
            Assert.False(world.HeatReady);                                    // 뷰를 읽어도 시작 상태를 세우지 않는다
            Assert.Equal(before, world.ComputeStateHash());
        }

        [Fact]
        public void HeatAndRing_FollowTheShots()
        {
            HeatRig rig = HeatRig.Endless(Db);
            rig.Step(true);                                                   // 틱 0: 첫 발
            PlayerView v = View(rig);
            Assert.Equal(480, v.HeatHu);
            Assert.Equal(10200, v.CritStartHu);
            Assert.Equal(HeatRingState.Rising, v.HeatRing);
            while (rig.Tick <= 189)
            {
                rig.Step(true);                                               // 22발째(틱 189)에 88 → 임계
            }
            v = View(rig);
            Assert.Equal(22 * 480, v.HeatHu);
            Assert.Equal(HeatRingState.Critical, v.HeatRing);
            Assert.Equal((0, 0), (v.LockRemainTicks, v.LockTotalTicks));
            Assert.False(v.ExemptArmed);
        }

        [Fact]
        public void Lock_ShowsRemainingAndTotalTicks()
        {
            HeatRig rig = HeatRig.Endless(Db);
            while (!rig.H.Locked)
            {
                rig.Step(true);
            }
            Assert.Equal(226U, rig.Tick);                                     // 26발째는 틱 225(spec 01 7.8)
            PlayerView v = View(rig);
            Assert.Equal(HeatRingState.Locked, v.HeatRing);
            Assert.Equal(189, v.LockTotalTicks);                              // 간격 9 + 잠금 180
            Assert.Equal(188, v.LockRemainTicks);                             // 풀리는 틱 414 − 다음 틱 226
            rig.Step(false);
            Assert.Equal(187, View(rig).LockRemainTicks);
            rig.RunTo(414);
            v = View(rig);
            Assert.Equal(HeatRingState.Locked, v.HeatRing);                   // 414 의 1단계가 풀기 전
            Assert.Equal(0, v.LockRemainTicks);
            rig.Step(false);
            v = View(rig);
            Assert.NotEqual(HeatRingState.Locked, v.HeatRing);
            Assert.Equal((0, 0), (v.LockRemainTicks, v.LockTotalTicks));
        }

        [Fact]
        public void GrazeReward_ShowsTheExemptRing()
        {
            HeatRig rig = HeatRig.Endless(Db);
            rig.Step(false);
            rig.World.HeatApplyGraze(P0, rig.Log);
            PlayerView v = View(rig);
            Assert.True(v.ExemptArmed);
            Assert.Equal(10200, v.HeatHu);
            Assert.Equal(HeatRingState.Critical, v.HeatRing);
            Assert.Equal(rig.H.ExemptArmed, v.ExemptArmed);
        }

        [Fact]
        public void ReadingTheViewEveryTick_DoesNotChangeTheHashes()
        {
            HeatRig read = HeatRig.Endless(Db), plain = HeatRig.Endless(Db);
            for (uint t = 0; t < 480; t++)
            {
                bool j = t < 240 || t > 430;
                View(read);
                read.Step(j);
                plain.Step(j);
                View(read);
                Assert.Equal(plain.World.ComputeStateHash(), read.World.ComputeStateHash());
            }
        }
    }
}

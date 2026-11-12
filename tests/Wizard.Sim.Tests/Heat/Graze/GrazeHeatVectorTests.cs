using System.Collections.Generic;
using Wizard.Content;
using Wizard.Sim.Events;
using Wizard.Sim.Heat;
using Xunit;
using static Wizard.Sim.Tests.Heat.Graze.GrazeHeatKit;

namespace Wizard.Sim.Tests.Heat.Graze
{
    // spec 01 14.7 의 열 보상 벡터 T-G02~T-G07. 세계를 돌리고 스침은 Graze 의 문으로 일으킨다(GrazeHeatKit).
    // 열은 Graze → RaiseGrazeAwarded → OnGrazeAwarded_Heat(CONTRACTS 3.2 이음 훅, T-50)로 그 순간에 반영된다.
    // 표의 LockBrokenByGraze(8.4 의사코드)는 계약의 이벤트 목록(CONTRACTS 3.3)에서 HeatUnlocked(A = 이유 Graze)다.
    public sealed class GrazeHeatVectorTests
    {
        private static ContentDb Db => HeatData.Baseline;

        // T-G01 의 given(기준값, 열 40)을 세계 실행으로 만들고 스친다. 마력탄 10발(틱 0~81) → 열 40, 틱 82 에 J 뗌.
        // 냉각은 81 + 9 + 12 = 102 부터라 틱 83 의 스침까지 열은 40 그대로다. 돌려주는 값: 스친 틱
        private static uint Heat40ThenGraze(HeatRig rig)
        {
            FireShotsThenRelease(rig, 10);
            Assert.Equal(83U, rig.Tick);
            Assert.Equal(4800, rig.H.HeatHu);
            uint t = rig.Tick;
            OfferGraze(rig, 100, 7);
            rig.Step(false);
            return t;
        }

        [Fact]
        public void S01_T_G02__ShotRightAfterTheGrazeKeepsHeat85AndIsCrit()
        {
            HeatRig rig = HeatRig.Endless(Db);
            uint g = Heat40ThenGraze(rig);

            // T-G01 의 열 쪽 then(전제): 열 85, 열 면제 대기
            Assert.Equal(1, CountAt(rig, SimEventType.GrazeAwarded, g));
            Assert.Equal(BaseCritStartHu, rig.H.HeatHu);
            Assert.True(rig.H.ExemptArmed);
            Assert.True(rig.World.HeatAt(0).ExemptArmed);
            Assert.Equal(1, CountAt(rig, SimEventType.HeatExemptArmed, g));
            Assert.Equal(HeatRingState.Critical, rig.World.HeatRingOf(0));

            // 마력탄 1발(다음 발사 가능 틱 90): 열 85 그대로, 임계
            rig.RunTo(90);
            Assert.Equal(BaseCritStartHu, rig.H.HeatHu);                 // 냉각 대기 중
            rig.Step(true);
            SimEvent shot = LastShot(rig);
            Assert.Equal(90U, shot.Tick);
            Assert.Equal(1, shot.C);                                     // 임계(85 + 0 ≥ 85)
            Assert.Equal(BaseCritStartHu, shot.V);
            Assert.Equal(BaseCritStartHu, rig.H.HeatHu);
            Assert.False(rig.H.ExemptArmed);
            SimEvent consumed = rig.Log.Single(SimEventType.HeatExemptConsumed);
            Assert.Equal((90U, shot.A), (consumed.Tick, consumed.A));
            Assert.Equal(1, CountAt(rig, SimEventType.CritShotFired, 90));
        }

        [Fact]
        public void S01_T_G03__TheShotAfterThatRaisesHeatTo89AndIsCrit()
        {
            HeatRig rig = HeatRig.Endless(Db);
            Heat40ThenGraze(rig);
            rig.RunTo(90);
            rig.Step(true);                                              // T-G02: 면제 발사
            rig.RunTo(99);
            rig.Step(true);                                              // 1발 더
            SimEvent shot = LastShot(rig);
            Assert.Equal(99U, shot.Tick);
            Assert.Equal(1, shot.C);
            Assert.Equal(BaseCritStartHu + MagicBoltHeatHu, shot.V);     // 89
            Assert.Equal(10680, rig.H.HeatHu);
            Assert.Equal(1, rig.Log.Count(SimEventType.HeatExemptConsumed));
        }

        [Fact]
        public void S01_T_G04__GrazeAt92KeepsHeatAndArmsTheExempt()
        {
            HeatRig rig = HeatRig.Endless(Db);
            FireShotsThenRelease(rig, 23);                               // 23발(틱 0~198) → 92
            Assert.Equal(11040, rig.H.HeatHu);
            uint g = rig.Tick;                                           // 200. 냉각은 198 + 21 = 219 부터
            OfferGraze(rig, 100, 7);
            rig.Step(false);

            Assert.Equal(1, CountAt(rig, SimEventType.GrazeAwarded, g));
            Assert.Equal(11040, rig.H.HeatHu);                           // 92 그대로(낮을 때만 올린다)
            Assert.True(rig.H.ExemptArmed);
            Assert.Equal(1, CountAt(rig, SimEventType.HeatExemptArmed, g));
            Assert.Equal(0, rig.Log.Count(SimEventType.HeatUnlocked));

            rig.RunTo(207);                                              // 다음 발사: 면제, 열 92 그대로
            rig.Step(true);
            SimEvent shot = LastShot(rig);
            Assert.Equal((207U, 11040L, 1), (shot.Tick, shot.V, shot.C));
            Assert.False(rig.H.Locked);
        }

        [Fact]
        public void S01_T_G05__GrazeWhileLockedBreaksTheLockToCritStartAndArmsTheExempt()
        {
            HeatRig rig = HeatRig.Endless(Db);
            FireUntilLockedThenRelease(rig);                             // 26발째(틱 225, 열 104) 잠금. UnlockTick 414
            Assert.True(rig.H.Locked);
            Assert.Equal(414U, rig.H.UnlockTick);
            uint g = rig.Tick;                                           // 227
            OfferGraze(rig, 100, 7);
            rig.Step(false);

            Assert.False(rig.H.Locked);
            Assert.Equal(BaseCritStartHu, rig.H.HeatHu);                 // 85
            Assert.True(rig.H.ExemptArmed);
            List<SimEvent> tick = At(rig, g);
            SimEvent unlocked = rig.Log.Single(SimEventType.HeatUnlocked);    // LockBrokenByGraze
            Assert.Equal(g, unlocked.Tick);
            Assert.Equal((int)HeatUnlockReason.Graze, unlocked.A);
            Assert.Equal(BaseCritStartHu, unlocked.B);
            Assert.Equal(1, CountAt(rig, SimEventType.HeatExemptArmed, g));
            // 순서: 열(잠금 해제 → 면제) → GrazeAwarded(T-50 결정 3)
            int iUnlock = IndexOf(tick, SimEventType.HeatUnlocked);
            int iArmed = IndexOf(tick, SimEventType.HeatExemptArmed);
            int iAward = IndexOf(tick, SimEventType.GrazeAwarded);
            Assert.True(iUnlock >= 0 && iUnlock < iArmed && iArmed < iAward);
            Assert.Equal(0, rig.World.HeatLockRemainTicks(0));

            // 풀린 뒤에는 잠금 시간이 끝나도(414) 다시 풀리지 않고, 다음 발사는 면제 + 임계다
            rig.RunTo(234);
            rig.Step(true);
            SimEvent shot = LastShot(rig);
            Assert.Equal((234U, (long)BaseCritStartHu, 1), (shot.Tick, shot.V, shot.C));
            rig.Step(false);
            rig.RunTo(420);
            Assert.Equal(1, rig.Log.Count(SimEventType.HeatUnlocked));
        }

        [Fact]
        public void S01_T_G06__Char1GrazeWhileLockedSetsHeatTo80()
        {
            HeatRig rig = HeatRig.Endless(HeatData.Dalgu);               // 임계 시작 80(9,600 HU)
            Assert.Equal(9600, rig.World.HeatCritStartHu(0));
            FireUntilLockedThenRelease(rig);
            Assert.True(rig.H.Locked);
            uint g = rig.Tick;
            OfferGraze(rig, 100, 7);
            rig.Step(false);

            Assert.False(rig.H.Locked);
            Assert.Equal(9600, rig.H.HeatHu);                            // 열 80
            Assert.True(rig.H.ExemptArmed);
            SimEvent unlocked = rig.Log.Single(SimEventType.HeatUnlocked);
            Assert.Equal((g, (int)HeatUnlockReason.Graze, 9600), (unlocked.Tick, unlocked.A, unlocked.B));
        }

        [Fact]
        public void S01_T_G07__TwoGrazesInARowExemptOnlyTheFirstShot()
        {
            HeatRig rig = HeatRig.Endless(Db);
            FireShotsThenRelease(rig, 10);                               // 열 40, 냉각은 102 부터
            OfferGraze(rig, 100, 1);                                     // 시전 1, 틱 83
            rig.Step(false);
            OfferGraze(rig, 101, 2);                                     // 다른 시전 2, 틱 84
            rig.Step(false);
            Assert.Equal(2, rig.Log.Count(SimEventType.GrazeAwarded));
            Assert.Equal(1, rig.Log.Count(SimEventType.HeatExemptArmed));   // 면제는 겹치지 않는다(새로 걸릴 때만 이벤트)
            Assert.True(rig.H.ExemptArmed);
            Assert.Equal(BaseCritStartHu, rig.H.HeatHu);

            rig.RunTo(90);
            rig.Step(true);                                              // 첫 발: 면제
            SimEvent first = LastShot(rig);
            Assert.Equal((90U, (long)BaseCritStartHu), (first.Tick, first.V));
            rig.RunTo(99);
            rig.Step(true);                                              // 둘째 발: 열 오름
            SimEvent second = LastShot(rig);
            Assert.Equal((99U, (long)(BaseCritStartHu + MagicBoltHeatHu)), (second.Tick, second.V));
            Assert.Equal(1, rig.Log.Count(SimEventType.HeatExemptConsumed));
            Assert.Equal(first.A, rig.Log.Single(SimEventType.HeatExemptConsumed).A);
        }
    }
}

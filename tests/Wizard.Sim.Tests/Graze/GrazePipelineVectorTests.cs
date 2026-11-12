using Wizard.Core;
using Wizard.Sim.Events;
using Wizard.Sim.Graze;
using Wizard.Sim.Input;
using Wizard.Sim.Tests.Player;
using Xunit;
using static Wizard.Sim.Tests.Graze.GrazeKit;

namespace Wizard.Sim.Tests.Graze
{
    // spec 01 14.7 중 세계를 돌려 보는 것(기준값 캐릭터, 한뎃벌). 스침 후보와 피격 후보는 Step 직전에 넣는다(GrazeKit).
    // 열·콤보·피뢰침은 그 상태를 가진 모듈(Heat, Combo, Characters)이 이음 훅 OnGrazeAwarded_* 에서 반영한다(T-50).
    // 그 모듈이 이 브랜치에 없으므로 여기서는 이 모듈의 상태와 사실(인정 수, 시전 묶음 기록, 가속과 이동, 이벤트)을 본다.
    // T-G01 의 "열 85, 열 면제 대기"는 T-G02(T-G01 직후의 발사)의 전제로 M1-SIM-10 이, "콤보 +2"는 M3-CMB-01 이 세계를 돌려 본다.
    public sealed class GrazePipelineVectorTests
    {
        private static PlayerRig Ready(uint tick = 10) => ReadyAt(tick, PlayerRig.Endless());

        // 기준값 걷기 5.0칸/초의 틱당 이동량(Raw). spec 01 2.3: (SpeedCenti × mulPermille × 2^32 + 3,000,000) / 6,000,000
        private const long WalkStepRaw = 357_913_941L;          // 500 × 1000
        private const long BuffedWalkStepRaw = 429_496_730L;    // 500 × 1200(스침 가속 +20%, 5.6)

        // 오른쪽으로 한 틱만 걷고(누른 틱에 이동, 다음 틱에 뗀다) 그 틱의 이동량을 돌려준다
        private static long WalkRightOneTick(PlayerRig rig)
        {
            long x0 = rig.P.Pos.X.Raw;
            rig.Step(PlayerRig.Press(LogicalKey.Right));
            long moved = rig.P.Pos.X.Raw - x0;
            rig.Step(PlayerRig.Release(LogicalKey.Right));
            Assert.Equal(x0 + moved, rig.P.Pos.X.Raw);                // 뗀 틱에는 움직이지 않는다
            return moved;
        }

        private static void AssertAwardedAt(PlayerRig rig, uint tick, int count)
        {
            Assert.Equal(tick, rig.World.GrazeAt(0).AwardTick);
            Assert.Equal(count, rig.World.GrazeAt(0).AwardCount);
        }

        [Fact]
        public void S01_T_G01__BulletEnteringTheGrazeCircleAwardsOnceWithA90TickBuff()
        {
            PlayerRig rig = Ready();
            rig.Log.Clear();
            uint t = rig.Tick;
            Offer(rig, BulletAt(rig, 100, 7, 700));
            rig.Step();

            AssertAwardedAt(rig, t, 1);
            SimEvent e = rig.Log.Single(SimEventType.GrazeAwarded);
            Assert.Equal(t, e.Tick);
            Assert.Equal(0, e.Slot.Value);
            Assert.Equal(7, e.A);                                      // 시전 묶음
            Assert.Equal((int)GrazeKind.Bullet, e.B);
            Assert.Equal(100, e.Source.Value);
            Assert.True(rig.World.GrazeCastRecorded(0, 7));
            Assert.Equal(t + 90U, rig.World.GrazeAt(0).BuffUntil);    // 가속 90틱(1.5초)
            rig.Log.Single(SimEventType.GrazeBuffStarted);
            // 같은 틱 안에서 보면 인정 수가 보인다(열·콤보가 읽는 값). Step 이 끝나면 다음 틱이라 0이다
            Assert.Equal(0, rig.World.GrazeAwardsThisTick(0));
        }

        [Fact]
        public void S01_T_G08__GrazeWith30TicksLeftRefreshesTheBuffWithoutStacking()
        {
            PlayerRig rig = Ready();
            Assert.Equal(WalkStepRaw, WalkRightOneTick(rig));         // 가속 전
            Offer(rig, BulletAt(rig, 100, 1, 700));
            uint t0 = rig.Tick;
            rig.Step();
            Assert.Equal(BuffedWalkStepRaw, WalkRightOneTick(rig));   // 가속 중: 이동 배율 1.2
            rig.RunTo(t0 + 60U);                                       // 남은 시간 30틱
            Assert.Equal(30U, rig.World.GrazeAt(0).BuffUntil - rig.Tick);
            Assert.True(rig.World.GrazeBuffActive(0));
            rig.Log.Clear();

            uint t1 = rig.Tick;
            Offer(rig, BulletAt(rig, 101, 2, 700));
            rig.Step();
            Assert.Equal(t1 + 90U, rig.World.GrazeAt(0).BuffUntil);   // 지금 + 90(갱신)
            Assert.Equal(0, rig.Log.Count(SimEventType.GrazeBuffStarted));
            Assert.Equal(0, rig.Log.Count(SimEventType.GrazeBuffEnded));
            Assert.Equal(BuffedWalkStepRaw, WalkRightOneTick(rig));   // 이동 배율은 1.2 그대로(중첩 없음, spec 01 5.6)

            // 가속은 기한 틱(t1 + 90) 전까지다. 기한 틱의 이동부터 가속이 없고 GrazeBuffEnded 는 그 틱에 한 번만 난다
            rig.RunTo(t1 + 88U);
            Assert.Equal(BuffedWalkStepRaw, WalkRightOneTick(rig));   // t1 + 88 의 이동(89 는 뗀 틱)
            Assert.Equal(t1 + 90U, rig.Tick);
            Assert.False(rig.World.GrazeBuffActive(0));
            Assert.Equal(0, rig.Log.Count(SimEventType.GrazeBuffEnded));
            Assert.Equal(WalkStepRaw, WalkRightOneTick(rig));         // t1 + 90 의 이동: 가속 없음
            SimEvent ended = rig.Log.Single(SimEventType.GrazeBuffEnded);
            Assert.Equal(t1 + 90U, ended.Tick);
            rig.RunTo(t1 + 200U);
            Assert.Equal(1, rig.Log.Count(SimEventType.GrazeBuffEnded));
        }

        [Fact]
        public void S01_T_G10__RingCastBulletsCrossingTheCircleOneAfterAnotherGrazeOnce()
        {
            PlayerRig rig = Ready();
            rig.Log.Clear();
            // 8방향 고리 탄 1회 시전(시전 묶음 9) 중 탄 3개가 차례로 감지 원을 지난다
            for (int k = 0; k < 3; k++)
            {
                Offer(rig, BulletAt(rig, 200 + k, 9, 650));
                rig.Step();
            }
            Assert.Equal(1, rig.Log.Count(SimEventType.GrazeAwarded));
            Assert.Equal(200, rig.Log.Single(SimEventType.GrazeAwarded).Source.Value);
        }

        [Fact]
        public void S01_T_G11__TwoCastsDetectedInTheSameTickGrazeTwice()
        {
            PlayerRig rig = Ready();
            rig.Log.Clear();
            uint t = rig.Tick;
            // 넣는 순서와 무관하게 위협 id 순으로 판정한다(8.3)
            Offer(rig, BulletAt(rig, 301, 2, 700));                   // 시전 B
            Offer(rig, BulletAt(rig, 300, 1, 650));                   // 시전 A
            rig.Step();

            AssertAwardedAt(rig, t, 2);                               // 콤보는 시전마다 +2(콤보 모듈이 읽는다)
            var awards = rig.Log.Of(SimEventType.GrazeAwarded);
            Assert.Equal(2, awards.Count);
            Assert.Equal(300, awards[0].Source.Value);
            Assert.Equal(1, awards[0].A);
            Assert.Equal(301, awards[1].Source.Value);
            Assert.Equal(2, awards[1].A);
            Assert.Equal(1, rig.Log.Count(SimEventType.GrazeBuffStarted));   // 보상(가속)은 중복돼도 결과가 같다
        }

        [Fact]
        public void S01_T_G19__ZoneIsGrazedOnlyAtTheDetonationTick()
        {
            PlayerRig rig = Ready();
            rig.Log.Clear();
            FixVec2 center = RightOf(rig.P.Pos, Milli(1300));         // 반지름 1.0, 경계 바깥 0.3
            Fix64 hit = rig.World.GrazeHitRadius(0);
            for (int k = 0; k < 5; k++)
            {
                Offer(rig, GrazeThreat.CircleZone(new EntityId(400), 3, center, Fix64.One, false, rig.P.Pos, hit));
                rig.Step();
            }
            Assert.Equal(0, rig.Log.Count(SimEventType.GrazeAwarded));    // 폭발 전에는 없다
            Assert.False(rig.World.GrazeCastRecorded(0, 3));

            uint t = rig.Tick;
            Offer(rig, GrazeThreat.CircleZone(new EntityId(400), 3, center, Fix64.One, true, rig.P.Pos, hit));
            rig.Step();
            SimEvent e = rig.Log.Single(SimEventType.GrazeAwarded);      // 폭발하는 순간
            Assert.Equal(t, e.Tick);
            Assert.Equal((int)GrazeKind.Zone, e.B);
        }

        [Fact]
        public void S01_T_G16__DetonatingZoneOutside05GrazesInTheWorld()
        {
            PlayerRig rig = Ready();
            rig.Log.Clear();
            FixVec2 center = RightOf(rig.P.Pos, Milli(1500));
            Offer(rig, GrazeThreat.CircleZone(new EntityId(410), 4, center, Fix64.One, true, rig.P.Pos, rig.World.GrazeHitRadius(0)));
            rig.Step();
            Assert.Equal(1, rig.Log.Count(SimEventType.GrazeAwarded));
        }

        [Fact]
        public void S01_T_G17__DetonatingZoneOutside07DoesNotGrazeInTheWorld()
        {
            PlayerRig rig = Ready();
            rig.Log.Clear();
            FixVec2 center = RightOf(rig.P.Pos, Milli(1700));
            Offer(rig, GrazeThreat.CircleZone(new EntityId(410), 4, center, Fix64.One, true, rig.P.Pos, rig.World.GrazeHitRadius(0)));
            rig.Step();
            Assert.Equal(0, rig.Log.Count(SimEventType.GrazeAwarded));
        }

        [Fact]
        public void S01_T_G20__DashingWolfBodyGrazes()
        {
            PlayerRig rig = Ready();
            rig.Log.Clear();
            // 돌진 중인 늑대의 몸 원(반지름 0.35 [시험값]). 판정 중심 거리 0.90 ≤ 0.6 + 0.35, > 0.15 + 0.35
            var dash = GrazeThreat.Dash(new EntityId(500), 11, RightOf(rig.P.Pos, Milli(900)), Milli(350));
            Offer(rig, dash);
            rig.Step();
            SimEvent e = rig.Log.Single(SimEventType.GrazeAwarded);
            Assert.Equal((int)GrazeKind.Dash, e.B);
            Assert.Equal(11, e.A);
        }

        [Fact]
        public void S01_T_G21__NoGrazeWhileInvulnerableAfterAHit()
        {
            PlayerRig rig = Ready();
            rig.World.QueuePlayerHit(P0, new EntityId(1), 1);
            rig.Step();
            Assert.True(rig.World.PlayerInvulnerable(0));
            rig.Log.Clear();
            rig.RunTo(rig.Tick + 10U);
            Offer(rig, BulletAt(rig, 600, 12, 700));
            rig.Step();
            Assert.Equal(0, rig.Log.Count(SimEventType.GrazeAwarded));
            Assert.False(rig.World.GrazeCastRecorded(0, 12));         // 인정하지 않은 시전은 무적이 끝나면 다시 스칠 수 있다
            Assert.Equal(0, rig.World.GrazeOfferCount(0));            // 후보는 그 틱에 비운다
        }

        [Fact]
        public void S01_T_G22__HitAndGrazeOfAnotherCastInTheSameTickIsOnlyAHit()
        {
            PlayerRig rig = Ready();
            rig.Log.Clear();
            // 탄 A(id 700, 시전 1)에 맞고 탄 B(id 650, 시전 2)가 감지. B 의 id 가 더 작아도 피격이 먼저다
            rig.World.QueuePlayerHit(P0, new EntityId(700), 1);
            Offer(rig, BulletAt(rig, 650, 2, 700));
            rig.Step();
            Assert.Equal(1, rig.Log.Count(SimEventType.PlayerHit));
            Assert.Equal(0, rig.Log.Count(SimEventType.GrazeAwarded));
            Assert.Equal(0, rig.Log.Count(SimEventType.GrazeBuffStarted));
            Assert.False(rig.World.GrazeCastRecorded(0, 2));
        }

        [Fact]
        public void S01_T_G24__BulletBlockedByTheIceScreenIsNotAGraze()
        {
            PlayerRig rig = Ready();
            rig.Log.Clear();
            GrazeThreat blocked = BulletAt(rig, 800, 13, 700);
            blocked.Blocked = true;                                    // 얼음 병풍이 막은 탄(wc 4.4)
            Offer(rig, blocked);
            rig.Step();
            Assert.Equal(0, rig.Log.Count(SimEventType.GrazeAwarded));
            Assert.False(rig.World.GrazeCastRecorded(0, 13));
        }

        [Fact]
        public void S01_T_G13__BulletAt073IsNothingInTheWorld()
        {
            PlayerRig rig = Ready();
            rig.Log.Clear();
            Offer(rig, BulletAt(rig, 900, 14, 730));
            rig.Step();
            Assert.Equal(0, rig.Log.Count(SimEventType.GrazeAwarded));
        }

        [Fact]
        public void S01_T_G15__BulletAt026IsNotAGrazeInTheWorld()
        {
            PlayerRig rig = Ready();
            rig.Log.Clear();
            Offer(rig, BulletAt(rig, 901, 15, 260));
            rig.Step();
            Assert.Equal(0, rig.Log.Count(SimEventType.GrazeAwarded));
            Assert.False(rig.World.GrazeCastRecorded(0, 15));          // 피격 원 안이면 시전도 기록하지 않는다
        }
    }
}

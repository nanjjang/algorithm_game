using Wizard.Content;
using Wizard.Core;
using Wizard.Sim.Graze;
using Wizard.Sim.Tests.World;
using Xunit;
using static Wizard.Sim.Tests.Graze.GrazeKit;

namespace Wizard.Sim.Tests.Graze
{
    // spec 01 14.7 중 판정 기하와 가속 기한의 값(상태 없는 규칙, GrazeRules).
    // 열 보상의 값(T-G02~T-G07)은 열을 가진 Heat 모듈의 것이다: 이음 훅 OnGrazeAwarded_Heat 를 구현하는 작업(M1-SIM-10)이 세계를 돌려 시험한다(T-50).
    public sealed class GrazeRulesVectorTests
    {
        // 기준값: 감지 0.6, 피격 0.15(data/sim/characters.json, spec 01 5.1)
        private static Fix64 BaseGraze => WorldData.Baseline.GrazeRadius;

        private static Fix64 BaseHit => WorldData.Baseline.HitRadius;

        // 초롱(D2): 감지 0.75(spec 01 8.1 1번, wc 4.2). 다섯 캐릭터의 데이터 줄은 아직 팩에 없다(M5-DAT-01)
        private static Fix64 ChorongGraze => Milli(750);

        private static readonly FixVec2 Origin = new FixVec2(Fix64.FromInt(12), Fix64.FromInt(7));

        private static bool BandAt(long dMilli, Fix64 grazeRadius) =>
            GrazeRules.InBand(Origin, RightOf(Origin, Milli(dMilli)), Milli(120), grazeRadius, BaseHit);

        [Fact]
        public void BaselineRadiiComeFromTheSpecTable()
        {
            Assert.Equal(Milli(600), BaseGraze);
            Assert.Equal(Milli(150), BaseHit);
        }

        [Fact]
        public void S01_T_G09__Char2BuffLasts120Ticks()
        {
            // 초롱의 가속 2.0초(spec 01 8.1 5번) = 120틱. 기준값은 1.5초 = 90틱(데이터)
            Assert.Equal(90, WorldData.Baseline.GrazeBuffTicks);
            Assert.Equal(1120U, GrazeRules.BuffUntil(1000U, 2 * SimVersion.TickRate));
            Assert.Equal(1090U, GrazeRules.BuffUntil(1000U, WorldData.Baseline.GrazeBuffTicks));
        }

        [Fact]
        public void S01_T_G12__BaselineDistance070IsGraze()
        {
            Assert.True(BandAt(700, BaseGraze));                       // 0.70 ≤ 0.6 + 0.12
        }

        [Fact]
        public void S01_T_G13__BaselineDistance073IsNothing()
        {
            Assert.False(BandAt(730, BaseGraze));                      // 0.73 > 0.72
        }

        [Fact]
        public void S01_T_G14__Char2Distance085IsGraze()
        {
            Assert.True(BandAt(850, ChorongGraze));                    // 0.85 ≤ 0.75 + 0.12
            Assert.False(BandAt(850, BaseGraze));
        }

        [Fact]
        public void S01_T_G15__Distance026IsInsideHitCircleNotGraze()
        {
            Fix64 d = Milli(260);
            Assert.True(d <= BaseHit + Milli(120));                    // 피격(0.26 ≤ 0.27)
            Assert.False(BandAt(260, BaseGraze));                      // 스침 아님
            Assert.True(BandAt(280, BaseGraze));                       // 피격 원 바로 밖은 스침
        }

        [Fact]
        public void S01_T_G16__ZoneEdgeOutside05IsGraze()
        {
            GrazeRules.CircleZone(Origin, Fix64.One, RightOf(Origin, Milli(1500)), BaseHit, out bool contains, out Fix64 edge);
            Assert.False(contains);
            Assert.Equal(Milli(500), edge);
            Assert.True(GrazeRules.ZoneEdge(contains, edge, BaseGraze));
        }

        [Fact]
        public void S01_T_G17__ZoneEdgeOutside07IsNothing()
        {
            GrazeRules.CircleZone(Origin, Fix64.One, RightOf(Origin, Milli(1700)), BaseHit, out bool contains, out Fix64 edge);
            Assert.False(contains);
            Assert.False(GrazeRules.ZoneEdge(contains, edge, BaseGraze));
        }

        [Fact]
        public void S01_T_G18__Char2ZoneEdgeOutside07IsGraze()
        {
            GrazeRules.CircleZone(Origin, Fix64.One, RightOf(Origin, Milli(1700)), BaseHit, out bool contains, out Fix64 edge);
            Assert.True(GrazeRules.ZoneEdge(contains, edge, ChorongGraze));   // 감지 0.75(15절 C6 [해석])
        }

        [Fact]
        public void ZonePlayerHitCircleTouchingTheZoneIsAHitNotAGraze()
        {
            // 경계 바깥 0.10: 피격 원(0.15)이 구역에 닿는다 → Contains → 스침 아님(8.3)
            GrazeRules.CircleZone(Origin, Fix64.One, RightOf(Origin, Milli(1100)), BaseHit, out bool contains, out Fix64 edge);
            Assert.True(contains);
            Assert.False(GrazeRules.ZoneEdge(contains, edge, BaseGraze));
            // 구역 안쪽(경계까지 음수)도 같다
            GrazeRules.CircleZone(Origin, Fix64.One, RightOf(Origin, Milli(500)), BaseHit, out contains, out edge);
            Assert.True(contains);
            Assert.False(GrazeRules.ZoneEdge(contains, edge, BaseGraze));
        }

        [Fact]
        public void BuffWithNoTicksDoesNothing()
        {
            Assert.Equal(1000U, GrazeRules.BuffUntil(1000U, 0));
            Assert.Equal(1000U, GrazeRules.BuffUntil(1000U, -5));
        }
    }
}

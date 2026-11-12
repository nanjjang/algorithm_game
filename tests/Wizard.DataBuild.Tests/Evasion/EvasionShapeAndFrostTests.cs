using System.Collections.Generic;
using Wizard.Content;
using Wizard.DataBuild.Validate.Evasion;
using Xunit;

namespace Wizard.DataBuild.Tests.Evasion
{
    // spec 05 11.3 벗어나는 거리 식(TV-ESC), 11.4 서리밭(TV-FROST)
    public sealed class EvasionShapeAndFrostTests
    {
        private const int Hit = EvasionR1Tests.Hit;
        private const int V = EvasionR1Tests.V;

        private static int Escape(EvasionAttack a)
        {
            int? e = EvasionCheck.EscapeMilli(a, Hit, out string? problem);
            Assert.Null(problem);
            return e!.Value;
        }

        [Fact]
        public void S05_TV_ESC_01__CircleRadius2_5()
        {
            Assert.Equal(2650, Escape(EvasionR1Tests.Circle(2.5m, 66)));
        }

        [Fact]
        public void S05_TV_ESC_02__BandWidths()
        {
            decimal[] widths = { 1.0m, 1.6m, 0.8m, 2.0m, 1.5m };
            int[] expected = { 650, 950, 550, 1150, 900 };
            for (int i = 0; i < widths.Length; i++)
            {
                Assert.Equal(expected[i], Escape(EvasionR1Tests.Band(widths[i], 60)));
            }
        }

        [Fact]
        public void S05_TV_ESC_03__AimLineWidth0_4()
        {
            Assert.Equal(350, Escape(EvasionR1Tests.AimLocked(0.4m, 60, 24)));
        }

        [Fact]
        public void S05_TV_ESC_04__Fan110Radius3()
        {
            // R·sin55°/(1+sin55°) + 0.15 = 1.5009 → 1.50(±0.005). 검산은 올림한 milli 칸 1501 을 쓴다
            decimal s = EvasionMath.Sin(55m * EvasionMath.Pi / 180m);
            Assert.InRange(s, 0.81915204m, 0.81915205m);
            int e = Escape(EvasionR1Tests.Fan(3.0m, 110m, 48));
            Assert.Equal(1501, e);
            Assert.InRange(e, 1495, 1505);
        }

        [Fact]
        public void S05_TV_ESC_05__Fan90Radius5()
        {
            int e = Escape(EvasionR1Tests.Fan(5m, 90m, 60));
            Assert.Equal(2222, e);                     // 2.2211 올림
            Assert.InRange(e, 2215, 2225);
        }

        [Fact]
        public void S05_TV_ESC_06__CrossOfTwoBands2_0()
        {
            // √2 × 1.15 = 1.626 → 1.63(±0.005)
            int e = EvasionMath.EscapeCrossMilli(2000, Hit);
            Assert.Equal(1627, e);
            Assert.InRange(e, 1625, 1635);
            Assert.Equal(1627, Escape(EvasionR1Tests.Cross(2.0m, 54)));
        }

        [Fact]
        public void S05_TV_ESC_07__CircleRadii()
        {
            decimal[] radii = { 1.8m, 2.6m, 1.2m, 1.5m, 2.0m, 3.0m, 1.0m };
            int[] expected = { 1950, 2750, 1350, 1650, 2150, 3150, 1150 };
            for (int i = 0; i < radii.Length; i++)
            {
                Assert.Equal(expected[i], Escape(EvasionR1Tests.Circle(radii[i], 60)));
            }
        }

        // 식이 없는 모양은 덮어쓰기 값 없이는 검산할 수 없다(조용히 지나가지 않는다)
        [Fact]
        public void RingWithoutOverride_IsReportedUnderR1()
        {
            var ring = new EvasionAttack { Owner = "test", Key = "ring", Kind = AttackDef.AttackKind.Zone, Shape = AttackDef.TelegraphShape.Ring, Radius = 3m, InnerRadius = 2m, TelegraphTicks = 60 };
            EvasionViolation v = Assert.Single(EvasionCheck.CheckAttack(ring, V, Hit));
            Assert.Equal("R1", v.Rule);
            Assert.Contains("escape_override_milli", v.Message);
            ring.EscapeOverrideMilli = 650;
            Assert.Empty(EvasionCheck.CheckAttack(ring, V, Hit));
        }

        [Fact]
        public void S05_TV_FROST_01__StanceSpeedsTimes0_7()
        {
            // 달구 2.632, 갈피 2.975, 그믐 3.094, 성에 3.15, 초롱 3.528
            Assert.Equal(2632, EvasionMath.FrostSpeedMilli(3760));
            Assert.Equal(2975, EvasionMath.FrostSpeedMilli(4250));
            Assert.Equal(3094, EvasionMath.FrostSpeedMilli(4420));
            Assert.Equal(3150, EvasionMath.FrostSpeedMilli(4500));
            Assert.Equal(3528, EvasionMath.FrostSpeedMilli(5040));
        }

        private static void FrostRow(EvasionAttack a, int dMilli, int telegraphMs, int needCenti, int marginCenti, bool pass)
        {
            a.FrostVariant = true;
            int vf = EvasionMath.FrostSpeedMilli(V);
            int e = Escape(a);
            Assert.InRange(e, dMilli - 5, dMilli + 5);
            Assert.InRange(EvasionMath.NeedMicros(e, vf), needCenti * 10000L - 5000, needCenti * 10000L + 5000);
            Assert.InRange(EvasionMath.MarginMicros(telegraphMs, e, vf), marginCenti * 10000L - 5000, marginCenti * 10000L + 5000);
            Assert.Equal(pass, EvasionMath.R1(telegraphMs, dMilli, vf));
            List<EvasionViolation> found = EvasionCheck.CheckAttack(a, V, Hit);
            if (pass)
            {
                Assert.Empty(found);
                a.FrostVariant = false;                // 같은 공격은 평소 V 로도 통과한다
                Assert.Empty(EvasionCheck.CheckAttack(a, V, Hit));
            }
            else
            {
                EvasionViolation v = Assert.Single(found);
                Assert.Equal("R1", v.Rule);
                Assert.Contains("V_frost", v.Message);
            }
        }

        [Fact]
        public void S05_TV_FROST_02__IcePillar() => FrostRow(EvasionR1Tests.Circle(1.0m, 48), 1150, 800, 69, 11, true);

        [Fact]
        public void S05_TV_FROST_03__HornCharge() => FrostRow(EvasionR1Tests.Band(1.6m, 54), 950, 900, 61, 29, true);

        [Fact]
        public void S05_TV_FROST_04__HornSwingAt1_0Fails() => FrostRow(EvasionR1Tests.Fan(5m, 90m, 60), 2220, 1000, 109, -9, false);

        [Fact]
        public void S05_TV_FROST_05__HornSwingAt1_2Passes() => FrostRow(EvasionR1Tests.Fan(5m, 90m, 72), 2220, 1200, 109, 11, true);

        private static readonly int[] FrostStances =
        {
            EvasionMath.FrostSpeedMilli(3760), EvasionMath.FrostSpeedMilli(4420), EvasionMath.FrostSpeedMilli(4250),
            EvasionMath.FrostSpeedMilli(4500), EvasionMath.FrostSpeedMilli(5040),
        };

        [Fact]
        public void S05_TV_FROST_06__HornWalk3_0OnFrostCatchesUp()
        {
            // 3.0 이상인 서리밭 위 자세 속도: 달구 2.63, 갈피 2.97 → 따라잡힘
            var caught = new List<int>();
            for (int i = 0; i < FrostStances.Length; i++)
            {
                if (FrostStances[i] <= 3000)
                {
                    caught.Add(i);
                }
            }
            Assert.Equal(new[] { 0, 2 }, caught);      // 달구(0), 갈피(2)
            var horn = new EvasionMover { Owner = "boss.frost_goat", ContactDamage = 1, MoveSpeedMilli = 3000, IsBoss = true, FrostMoveSpeedMilli = 3000 };
            EvasionViolation v = Assert.Single(EvasionCheck.CheckMover(horn, V));
            Assert.Equal("R2", v.Rule);
            Assert.Contains("V_frost", v.Message);
        }

        [Fact]
        public void S05_TV_FROST_07__HornWalk2_4OnFrostIsSlowerThanAll()
        {
            foreach (int s in FrostStances)
            {
                Assert.True(s > 2400);
            }
            Assert.InRange(EvasionMath.FrostSpeedMilli(V) - 2400, 225, 235);   // 0.23(±0.005)
            var horn = new EvasionMover { Owner = "boss.frost_goat", ContactDamage = 1, MoveSpeedMilli = 3000, IsBoss = true, FrostMoveSpeedMilli = 2400 };
            Assert.Empty(EvasionCheck.CheckMover(horn, V));
        }
    }
}

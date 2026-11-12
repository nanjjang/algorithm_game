using System.Collections.Generic;
using Wizard.Content;
using Wizard.DataBuild.Validate.Evasion;
using Xunit;

namespace Wizard.DataBuild.Tests.Evasion
{
    // spec 05 11.2 R1 검산(TV-R1-01~30). 공통 Given: V = min(자세 속도) = 3.76, 피격 반지름 0.15.
    // 각 줄: 모양에서 구한 벗어나는 거리가 표의 d 와 같고(±0.005), 캐릭터 다섯 칸 값 0.25 + d/v(±0.005), 달구 여유(±0.005),
    // 표의 정수식 (T − 300) × 3760 ≥ d × 1000 의 두 변, 검증기의 통과·실패가 표와 같다.
    public sealed class EvasionR1Tests
    {
        // spec 05 1.2 자세 속도 표: 달구 / 그믐 / 갈피 / 성에 / 초롱(milli 칸/초)
        internal static readonly int[] StanceSpeeds = { 3760, 4420, 4250, 4500, 5040 };
        internal const int V = 3760;
        internal const int Hit = 150;
        private const long ToleranceMicros = 5000;     // ±0.005초
        private const int ToleranceMilli = 5;          // ±0.005칸

        internal static EvasionAttack Circle(decimal r, int ticks) =>
            new EvasionAttack { Owner = "test", Key = "circle", Kind = AttackDef.AttackKind.Zone, Shape = AttackDef.TelegraphShape.Circle, Radius = r, TelegraphTicks = ticks };

        internal static EvasionAttack Band(decimal w, int ticks) =>
            new EvasionAttack { Owner = "test", Key = "band", Kind = AttackDef.AttackKind.Zone, Shape = AttackDef.TelegraphShape.Band, Width = w, TelegraphTicks = ticks };

        internal static EvasionAttack Fan(decimal r, decimal degrees, int ticks) =>
            new EvasionAttack { Owner = "test", Key = "fan", Kind = AttackDef.AttackKind.Zone, Shape = AttackDef.TelegraphShape.Fan, Radius = r, FanDegrees = degrees, TelegraphTicks = ticks };

        // 띠 두 개의 교차점(십자): 모양 하나로 적을 수 없어 escape_override_milli 로 둔다(spec 05 10.1 예외 줄)
        internal static EvasionAttack Cross(decimal w, int ticks) =>
            new EvasionAttack
            {
                Owner = "test", Key = "cross", Kind = AttackDef.AttackKind.Zone, Shape = AttackDef.TelegraphShape.Band, Width = w, TelegraphTicks = ticks,
                EscapeOverrideMilli = EvasionMath.EscapeCrossMilli(EvasionMath.CeilMilli(w), Hit),
            };

        internal static EvasionAttack AimLocked(decimal w, int telegraphTicks, int lockTicks) =>
            new EvasionAttack
            {
                Owner = "test", Key = "aim", Kind = AttackDef.AttackKind.AimedShot, Shape = AttackDef.TelegraphShape.AimLine, Width = w,
                TelegraphTicks = telegraphTicks, Lock = AttackDef.LockMode.LastNTicks, LockTicks = lockTicks,
            };

        // 표 한 줄. telegraphMs = 표의 예고(R1 이 세는 창), chars = 달구/그믐/갈피/성에/초롱 칸 값(1/100초), marginCenti = 달구 여유(1/1000초)
        private static void Row(EvasionAttack a, int dMilli, int telegraphMs, int[] charsCenti, int marginMilliSec, long lhs, long rhs, bool pass)
        {
            int? esc = EvasionCheck.EscapeMilli(a, Hit, out string? problem);
            Assert.Null(problem);
            Assert.NotNull(esc);
            Assert.InRange(esc!.Value, dMilli - ToleranceMilli, dMilli + ToleranceMilli);
            Assert.Equal(telegraphMs, EvasionMath.TicksToMs(EvasionCheck.WindowTicks(a)));

            for (int i = 0; i < charsCenti.Length; i++)
            {
                long need = EvasionMath.NeedMicros(esc.Value, StanceSpeeds[i]);
                Assert.InRange(need, charsCenti[i] * 10000L - ToleranceMicros, charsCenti[i] * 10000L + ToleranceMicros);
            }
            long margin = EvasionMath.MarginMicros(telegraphMs, esc.Value, V);
            Assert.InRange(margin, marginMilliSec * 1000L - ToleranceMicros, marginMilliSec * 1000L + ToleranceMicros);

            // 표의 정수식(d 는 표에 적힌 값)
            Assert.Equal(lhs, (long)(telegraphMs - 300) * V);
            Assert.Equal(rhs, (long)dMilli * 1000);
            Assert.Equal(pass, lhs >= rhs);
            Assert.Equal(pass, EvasionMath.R1(telegraphMs, dMilli, V));

            // 검증기(모양에서 구한 거리)
            List<EvasionViolation> found = EvasionCheck.CheckAttack(a, V, Hit);
            if (pass)
            {
                Assert.Empty(found);
            }
            else
            {
                EvasionViolation v = Assert.Single(found);
                Assert.Equal("R1", v.Rule);
            }
        }

        [Fact]
        public void S05_TV_R1_01__WolfDash() =>
            Row(Band(1.0m, 30), 650, 500, new[] { 42, 40, 40, 39, 38 }, 80, 752000, 650000, true);

        [Fact]
        public void S05_TV_R1_02__GolemSlam() =>
            Row(Circle(2.5m, 66), 2650, 1100, new[] { 95, 85, 87, 84, 78 }, 150, 3008000, 2650000, true);

        [Fact]
        public void S05_TV_R1_03__GolemSweep() =>
            Row(Fan(3.0m, 110m, 48), 1500, 800, new[] { 65, 59, 60, 58, 55 }, 150, 1880000, 1500000, true);

        [Fact]
        public void S05_TV_R1_04__LanternBug() =>
            Row(Circle(1.8m, 54), 1950, 900, new[] { 77, 69, 71, 68, 64 }, 130, 2256000, 1950000, true);

        [Fact]
        public void S05_TV_R1_05__KingLanternBug() =>
            Row(Circle(2.6m, 66), 2750, 1100, new[] { 98, 87, 90, 86, 80 }, 120, 3008000, 2750000, true);

        [Fact]
        public void S05_TV_R1_06__HogNeedleAfterLock() =>
            Row(AimLocked(0.4m, 60, 24), 350, 400, new[] { 34, 33, 33, 33, 32 }, 60, 376000, 350000, true);

        [Fact]
        public void S05_TV_R1_07__WeaselBurst() =>
            Row(Circle(1.2m, 42), 1350, 700, new[] { 61, 56, 57, 55, 52 }, 90, 1504000, 1350000, true);

        [Fact]
        public void S05_TV_R1_08__VineSeed() =>
            Row(Circle(1.5m, 60), 1650, 1000, new[] { 69, 62, 64, 62, 58 }, 310, 2632000, 1650000, true);

        [Fact]
        public void S05_TV_R1_09__BoarCharge() =>
            Row(Band(1.6m, 54), 950, 900, new[] { 50, 46, 47, 46, 44 }, 400, 2256000, 950000, true);

        [Fact]
        public void S05_TV_R1_10__ToadTongue() =>
            Row(Band(0.8m, 36), 550, 600, new[] { 40, 37, 38, 37, 36 }, 200, 1128000, 550000, true);

        [Fact]
        public void S05_TV_R1_11__BatQueenDiveAndPotMotherStrip() =>
            Row(Band(0.8m, 30), 550, 500, new[] { 40, 37, 38, 37, 36 }, 100, 752000, 550000, true);

        [Fact]
        public void S05_TV_R1_12__WhimLastBreath() =>
            Row(Circle(1.2m, 48), 1350, 800, new[] { 61, 56, 57, 55, 52 }, 190, 1880000, 1350000, true);

        [Fact]
        public void S05_TV_R1_13__KilnBoarSecondThirdCharge() =>
            Row(Band(2.0m, 42), 1150, 700, new[] { 56, 51, 52, 51, 48 }, 140, 1504000, 1150000, true);

        [Fact]
        public void S05_TV_R1_14__KilnBoarStomp() =>
            Row(Circle(2.0m, 54), 2150, 900, new[] { 82, 74, 76, 73, 68 }, 80, 2256000, 2150000, true);

        [Fact]
        public void S05_TV_R1_15__KilnBoarRockfall() =>
            Row(Circle(1.5m, 60), 1650, 1000, new[] { 69, 62, 64, 62, 58 }, 310, 2632000, 1650000, true);

        [Fact]
        public void S05_TV_R1_16__PhantomMirrorBurst() =>
            Row(Circle(1.5m, 48), 1650, 800, new[] { 69, 62, 64, 62, 58 }, 110, 1880000, 1650000, true);

        [Fact]
        public void S05_TV_R1_17__SporeQueenCloud() =>
            Row(Circle(2.0m, 60), 2150, 1000, new[] { 82, 74, 76, 73, 68 }, 180, 2632000, 2150000, true);

        [Fact]
        public void S05_TV_R1_18__SporeQueenLeap() =>
            Row(Circle(3.0m, 72), 3150, 1200, new[] { 109, 96, 99, 95, 88 }, 110, 3384000, 3150000, true);

        [Fact]
        public void S05_TV_R1_19__StormOwlGlide() =>
            Row(Band(2.0m, 54), 1150, 900, new[] { 56, 51, 52, 51, 48 }, 340, 2256000, 1150000, true);

        [Fact]
        public void S05_TV_R1_20__StormOwlCrossGlide() =>
            Row(Cross(2.0m, 54), 1630, 900, new[] { 68, 62, 63, 61, 57 }, 220, 2256000, 1630000, true);

        [Fact]
        public void S05_TV_R1_21__StormOwlLightning() =>
            Row(Circle(1.5m, 48), 1650, 800, new[] { 69, 62, 64, 62, 58 }, 110, 1880000, 1650000, true);

        [Fact]
        public void S05_TV_R1_22__StormOwlPlough() =>
            Row(Band(2.0m, 60), 1150, 1000, new[] { 56, 51, 52, 51, 48 }, 440, 2632000, 1150000, true);

        [Fact]
        public void S05_TV_R1_23__FrostGoatIcePillar() =>
            Row(Circle(1.0m, 48), 1150, 800, new[] { 56, 51, 52, 51, 48 }, 240, 1880000, 1150000, true);

        [Fact]
        public void S05_TV_R1_24__FrostGoatSwing() =>
            Row(Fan(5m, 90m, 60), 2220, 1000, new[] { 84, 75, 77, 74, 69 }, 160, 2632000, 2220000, true);

        [Fact]
        public void S05_TV_R1_25__FrostGoatCharge() =>
            Row(Band(1.6m, 54), 950, 900, new[] { 50, 46, 47, 46, 44 }, 400, 2256000, 950000, true);

        [Fact]
        public void S05_TV_R1_26__NightSerpentBiteStrip() =>
            Row(Band(1.5m, 48), 900, 800, new[] { 49, 45, 46, 45, 43 }, 310, 1880000, 900000, true);

        [Fact]
        public void S05_TV_R1_27__NightSerpentProwl() =>
            Row(Circle(2.0m, 54), 2150, 900, new[] { 82, 74, 76, 73, 68 }, 80, 2256000, 2150000, true);

        [Fact]
        public void S05_TV_R1_28__GolemSlamOldValueFailsLoad() =>
            Row(Circle(2.5m, 60), 2650, 1000, new[] { 95 }, 45, 2632000, 2650000, false);

        [Fact]
        public void S05_TV_R1_29__KilnBoarStompOldValueFailsLoad() =>
            Row(Circle(2.0m, 48), 2150, 800, new[] { 82 }, -20, 1880000, 2150000, false);

        // mon_calc.py 의 "R1 미달: 없음"과 같다: 10.3 표의 27줄(TV-R1-01~27)을 같은 검증기에 넣으면 어긴 줄이 없다
        [Fact]
        public void S05_TV_R1_30__NoRowBelowR1LikeMonCalc()
        {
            var table = new List<EvasionAttack>
            {
                Band(1.0m, 30), Circle(2.5m, 66), Fan(3.0m, 110m, 48), Circle(1.8m, 54), Circle(2.6m, 66), AimLocked(0.4m, 60, 24),
                Circle(1.2m, 42), Circle(1.5m, 60), Band(1.6m, 54), Band(0.8m, 36), Band(0.8m, 30), Circle(1.2m, 48),
                Band(2.0m, 42), Circle(2.0m, 54), Circle(1.5m, 60), Circle(1.5m, 48), Circle(2.0m, 60), Circle(3.0m, 72),
                Band(2.0m, 54), Cross(2.0m, 54), Circle(1.5m, 48), Band(2.0m, 60), Circle(1.0m, 48), Fan(5m, 90m, 60),
                Band(1.6m, 54), Band(1.5m, 48), Circle(2.0m, 54),
            };
            Assert.Equal(27, table.Count);
            var below = new List<int>();
            for (int i = 0; i < table.Count; i++)
            {
                if (EvasionCheck.CheckAttack(table[i], V, Hit).Count > 0)
                {
                    below.Add(i + 1);
                }
            }
            Assert.Empty(below);
        }
    }
}

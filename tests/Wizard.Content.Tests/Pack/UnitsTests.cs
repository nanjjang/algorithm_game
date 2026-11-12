using System;
using System.Globalization;
using System.Numerics;
using Wizard.DataBuild.Core;
using Xunit;

namespace Wizard.Content.Tests.Pack
{
    // DataBuild 의 단위 변환(ARCHITECTURE 8.2 2단계). 기대값은 spec 14 0.3·4.1·4.8 절과 CONTRACTS 0.3·2.2 의 환산값이다.
    // 반올림은 가장 가까운 정수, 같으면 짝수. 무작위 입력은 BigInteger 유리수 기준 구현과 대조한다(구현과 독립).
    public sealed class UnitsTests
    {
        [Fact]
        public void Spec14_0_3_UnitTable()
        {
            Assert.Equal(4_294_967_296L, Units.CellsToRaw(1m));             // 1칸 (Q32.32)
            Assert.Equal(67_108_864L, Units.PixelsToRaw(1m));               // 1px = 2^32 ÷ 64 = 2^26
            Assert.Equal(Units.CellsToRaw(1m), Units.PixelsToRaw(64m));     // 1칸 = 64px
            Assert.Equal(60, Units.SecondsToTicks(1m));                     // 1초 = 60틱
        }

        [Fact]
        public void Spec14_4_1_RoundedRawValues()
        {
            Assert.Equal(103_079_215_104L, Units.CellsToRaw(24m));          // 전장 가로 24칸
            Assert.Equal(60_129_542_144L, Units.CellsToRaw(14m));           // 전장 세로 14칸
            Assert.Equal(Units.CellsToRaw(24m), Units.PixelsToRaw(1536m));  // = 1536px
            Assert.Equal(Units.CellsToRaw(14m), Units.PixelsToRaw(896m));   // = 896px
            Assert.Equal(644_245_094L, Units.CellsToRaw(0.15m));            // 피격 판정 반지름 0.15칸
            Assert.Equal(644_245_094L, Units.PixelsToRaw(9.6m));            //   = 9.6px
            Assert.Equal(1_717_986_918L, Units.CellsToRaw(0.4m));           // 판정 중심 0.4칸
            Assert.Equal(1_717_986_918L, Units.PixelsToRaw(25.6m));         //   = 25.6px
        }

        [Fact]
        public void Spec14_4_8_RoundedRawValues()
        {
            Assert.Equal(515_396_076L, Units.CellsToRaw(0.12m));            // 0.12칸 = 7.68px
            Assert.Equal(515_396_076L, Units.PixelsToRaw(7.68m));
            Assert.Equal(773_094_113L, Units.CellsToRaw(0.18m));            // 0.18칸 = 11.52px
            Assert.Equal(773_094_113L, Units.PixelsToRaw(11.52m));
        }

        [Fact]
        public void Contracts0_3_IntegerUnits()
        {
            Assert.Equal(120, Units.HeatToHu(1m));                          // 열 1 = 120 HU
            Assert.Equal(36_000, Units.DegreesToCenti(360m));               // 한 바퀴 36,000 센티도
            Assert.Equal(9_000, Units.DegreesToCenti(90m));                 // N = 9000
            Assert.Equal(1_600, Units.RatioToPermille(1.6m));               // 분열 배수 천분율
            Assert.Equal(2_200, Units.RatioToPermille(2.2m));
            Assert.Equal(1_500, Units.RatioToPermille(1.5m));
            Assert.Equal(3_037_000_500L, Units.CellsToRaw(0.7071067811865475244m));   // FixMath.InvSqrt2Raw(반올림)
            Assert.Equal(470, Units.ToCenti(4.7m));                         // CONTRACTS 2.2 WalkSpeedCenti: 4.7 → 470
            Assert.Equal(200, Units.PercentToPermille(20m));                // 20% → 천분율 200
            Assert.Equal(2_000, Units.PercentToBp(20m));                    // 20% → 만분율 2000
        }

        [Fact]
        public void HalfEven_Ties()
        {
            Assert.Equal(0, Units.PercentToPermille(0.05m));                // 0.5 → 0
            Assert.Equal(2, Units.PercentToPermille(0.15m));                // 1.5 → 2
            Assert.Equal(2, Units.PercentToPermille(0.25m));                // 2.5 → 2
            Assert.Equal(4, Units.PercentToPermille(0.35m));                // 3.5 → 4
            Assert.Equal(-2, Units.PercentToPermille(-0.25m));              // −2.5 → −2
            Assert.Equal(-2, Units.PercentToPermille(-0.15m));              // −1.5 → −2
            Assert.Equal(2, Units.SecondsToTicks(0.025m));                  // 1.5틱 → 2
            Assert.Equal(1, Units.SecondsToTicks(0.0125m));                 // 0.75틱 → 1
            Assert.Equal(0m, Units.RoundHalfEven(1m, 1, 2));                // ½ → 0
            Assert.Equal(2m, Units.RoundHalfEven(3m, 1, 2));                // 1½ → 2
            Assert.Equal(2m, Units.RoundHalfEven(5m, 1, 2));                // 2½ → 2
            Assert.Equal(-2m, Units.RoundHalfEven(-3m, 1, 2));
            Assert.Equal(2m, Units.RoundHalfEven(7m, 1, 4));                // 1.75 → 2
            Assert.Equal(1m, Units.RoundHalfEven(5m, 1, 4));                // 1.25 → 1
            Assert.Equal(0m, Units.RoundHalfEven(0m, 1, 60));
        }

        [Fact]
        public void CellsPerSecond_DividesExactly()
        {
            // 4.7칸/초 → 4.7 × 2^32 ÷ 60 = 20,186,346,291.2 ÷ 60 = 336,439,104.85… → 336,439,105
            Assert.Equal(336_439_105L, Units.CellsPerSecondToRawPerTick(4.7m));
            Assert.Equal(Reference(4.7m, Units.RawPerCell, 60), Units.CellsPerSecondToRawPerTick(4.7m));
            Assert.Equal(71_582_788L, Units.CellsPerSecondToRawPerTick(1m)); // 2^32 ÷ 60 = 71,582,788.27 → 71,582,788
            Assert.Equal(Units.CellsToRaw(1m), Units.CellsPerSecondToRawPerTick(60m));
        }

        [Fact]
        public void TooPrecise_OrOutOfRange_IsDataBuildException()
        {
            // 가수가 넘쳐 자리를 버려야 하는 입력(28자리)은 반올림하지 않고 거절한다
            Assert.Throws<DataBuildException>(() => Units.CellsToRaw(0.7071067811865475244008443621m));
            Assert.Throws<DataBuildException>(() => Units.CellsToRaw(2_147_483_648m));          // 2^31칸 → 2^63 은 64비트 밖
            Assert.Equal(long.MinValue, Units.CellsToRaw(-2_147_483_648m));                      // −2^63 은 안
            Assert.Throws<DataBuildException>(() => Units.SecondsToTicks(40_000_000m));          // 24억 틱은 32비트 밖
            Assert.Throws<DataBuildException>(() => Units.CellsToRaw(decimal.MaxValue));
        }

        [Fact]
        public void Random_MatchesRationalReference()
        {
            var rng = new Random(20261112);
            (long Mul, long Div, Func<decimal, decimal> F)[] conversions =
            {
                (Units.RawPerCell, 1, v => Units.CellsToRaw(v)),
                (Units.RawPerPixel, 1, v => Units.PixelsToRaw(v)),
                (Units.RawPerCell, 60, v => Units.CellsPerSecondToRawPerTick(v)),
                (60, 1, v => Units.SecondsToTicks(v)),
                (120, 1, v => Units.HeatToHu(v)),
                (100, 1, v => Units.DegreesToCenti(v)),
                (10, 1, v => Units.PercentToPermille(v)),
                (100, 1, v => Units.PercentToBp(v)),
                (1000, 1, v => Units.RatioToPermille(v)),
                (100, 1, v => Units.ToCenti(v)),
            };
            for (int n = 0; n < 20000; n++)
            {
                int scale = rng.Next(0, 13);
                long mantissa = rng.NextInt64(0, 1_000_000_000_000L);
                bool negative = rng.Next(2) == 0;
                var v = new decimal((int)(mantissa & 0xFFFFFFFF), (int)(mantissa >> 32), 0, negative, (byte)scale);
                var c = conversions[n % conversions.Length];
                BigInteger expected = ReferenceBig(v, c.Mul, c.Div);
                decimal got;
                try
                {
                    got = c.F(v);
                }
                catch (DataBuildException)
                {
                    // 범위 밖이면 기준값도 그 정수형의 범위 밖이어야 한다
                    long max = c.Mul >= Units.RawPerPixel ? long.MaxValue : int.MaxValue;
                    long min = c.Mul >= Units.RawPerPixel ? long.MinValue : int.MinValue;
                    Assert.True(expected > max || expected < min, v.ToString(CultureInfo.InvariantCulture) + " 는 범위 안인데 거절했다");
                    continue;
                }
                Assert.Equal(expected, new BigInteger(got));
            }
        }

        private static long Reference(decimal v, long mul, long div) => (long)ReferenceBig(v, mul, div);

        // round_half_even(v × mul ÷ div) 를 유리수로 정확히
        private static BigInteger ReferenceBig(decimal v, long mul, long div)
        {
            int[] bits = decimal.GetBits(v);
            var mantissa = new BigInteger((uint)bits[0]) | (new BigInteger((uint)bits[1]) << 32) | (new BigInteger((uint)bits[2]) << 64);
            int scale = (bits[3] >> 16) & 0xFF;
            bool negative = bits[3] < 0;
            BigInteger num = mantissa * mul;
            BigInteger den = BigInteger.Pow(10, scale) * div;
            BigInteger q = BigInteger.DivRem(num, den, out BigInteger r);
            BigInteger twice = r * 2;
            if (twice > den || (twice == den && !q.IsEven))
            {
                q += 1;
            }
            return negative ? -q : q;
        }
    }
}

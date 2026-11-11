using Wizard.Core;
using Xunit;

namespace Wizard.Core.Tests
{
    // CONTRACTS 1.1. 표는 TableGen 이 정수로 만들었다. 여기서는 정확히 알려진 값과 성질을 본다.
    public sealed class FixMathTests
    {
        private const long One = 1L << 32;

        [Fact]
        public void Sqrt_ExactAndFloor()
        {
            Assert.Equal(Fix64.FromInt(2), FixMath.Sqrt(Fix64.FromInt(4)));
            Assert.Equal(Fix64.FromInt(5), FixMath.Sqrt(Fix64.FromInt(25)));
            Assert.Equal(6074000999L, FixMath.Sqrt(Fix64.FromInt(2)).Raw);   // √2 × 2^32 = 6,074,000,999.95
            Assert.Equal(Fix64.Zero, FixMath.Sqrt(Fix64.Zero));
            Assert.Equal(Fix64.Zero, FixMath.Sqrt(Fix64.FromInt(-4)));
            Assert.Equal(1L << 16, FixMath.Sqrt(Fix64.FromRaw(1)).Raw);       // √(2^-32) = 2^-16
            Fix64 big = FixMath.Sqrt(Fix64.MaxValue);
            Assert.True(big.Raw > 0);
        }

        [Fact]
        public void Sin_KnownValues()
        {
            Assert.Equal(0L, FixMath.Sin(Angle.FromCenti(0)).Raw);
            Assert.Equal(One, FixMath.Sin(Angle.FromCenti(9000)).Raw);
            Assert.Equal(One >> 1, FixMath.Sin(Angle.FromCenti(3000)).Raw);        // sin 30° = 0.5 정확
            Assert.Equal(FixMath.InvSqrt2Raw, FixMath.Sin(Angle.FromCenti(4500)).Raw);   // 1/√2 (CONTRACTS 0.3)
            Assert.Equal(0L, FixMath.Sin(Angle.FromCenti(18000)).Raw);
            Assert.Equal(-One, FixMath.Sin(Angle.FromCenti(27000)).Raw);
            Assert.Equal(One, FixMath.Cos(Angle.FromCenti(0)).Raw);
            Assert.Equal(-(One >> 1), FixMath.Cos(Angle.FromCenti(12000)).Raw);    // cos 120° = −0.5
            Assert.Equal(749613L, FixMath.Sin(Angle.FromCenti(1)).Raw);            // sin 0.01° × 2^32 = 749,613.2
        }

        [Fact]
        public void Sin_Symmetry()
        {
            for (int c = 0; c < Angle.Full; c += 7)
            {
                Assert.Equal(-FixMath.Sin(Angle.FromCenti(c)).Raw, FixMath.Sin(Angle.FromCenti(-c)).Raw);
                Assert.Equal(FixMath.Sin(Angle.FromCenti(c)).Raw, FixMath.Sin(Angle.FromCenti(18000 - c)).Raw);
            }
        }

        [Fact]
        public void Sin_IsMonotonicOnFirstQuadrant()
        {
            long prev = -1;
            for (int c = 0; c <= 9000; c++)
            {
                long v = FixMath.Sin(Angle.FromCenti(c)).Raw;
                Assert.True(v > prev, "sin table must strictly increase at " + c);
                prev = v;
            }
        }

        [Fact]
        public void Atan2_Axes()
        {
            Assert.Equal(0, FixMath.Atan2(Fix64.Zero, Fix64.Zero).Centi);
            Assert.Equal(0, FixMath.Atan2(Fix64.Zero, Fix64.One).Centi);
            Assert.Equal(9000, FixMath.Atan2(Fix64.One, Fix64.Zero).Centi);
            Assert.Equal(18000, FixMath.Atan2(Fix64.Zero, -Fix64.One).Centi);
            Assert.Equal(27000, FixMath.Atan2(-Fix64.One, Fix64.Zero).Centi);
            Assert.Equal(4500, FixMath.Atan2(Fix64.One, Fix64.One).Centi);
            Assert.Equal(13500, FixMath.Atan2(Fix64.One, -Fix64.One).Centi);
            Assert.Equal(22500, FixMath.Atan2(-Fix64.One, -Fix64.One).Centi);
            Assert.Equal(31500, FixMath.Atan2(-Fix64.One, Fix64.One).Centi);
            Assert.Equal(3000, FixMath.Atan2(Fix64.One, FixMath.Sqrt(Fix64.FromInt(3))).Centi);   // atan(1/√3) = 30°
        }

        [Fact]
        public void Atan2_InvertsSinCosForEveryCentidegree()
        {
            for (int c = 0; c < Angle.Full; c++)
            {
                Angle a = Angle.FromCenti(c);
                Assert.Equal(c, FixMath.Atan2(FixMath.Sin(a), FixMath.Cos(a)).Centi);
            }
        }

        [Fact]
        public void Atan2_HandlesExtremeMagnitudes()
        {
            Assert.Equal(18000 + 4500, FixMath.Atan2(Fix64.MinValue, Fix64.MinValue).Centi);
            Assert.Equal(4500, FixMath.Atan2(Fix64.MaxValue, Fix64.MaxValue).Centi);
            Assert.Equal(27000, FixMath.Atan2(Fix64.MinValue, Fix64.Zero).Centi);
        }

        [Fact]
        public void IntegerDivisionHelpers_AreTotal()
        {
            Assert.Equal(-3, FixMath.DivInt(-7, 2));
            Assert.Equal(-1, FixMath.ModInt(-7, 2));
            Assert.Equal(int.MinValue, FixMath.DivInt(int.MinValue, -1));
            Assert.Equal(0, FixMath.ModInt(int.MinValue, -1));
            Assert.Equal(long.MinValue, FixMath.DivLong(long.MinValue, -1));
            Assert.Equal(0L, FixMath.ModLong(long.MinValue, -1));
            Assert.Throws<SimInvariantException>(() => FixMath.DivInt(1, 0));
            Assert.Throws<SimInvariantException>(() => FixMath.ModInt(1, 0));
            Assert.Throws<SimInvariantException>(() => FixMath.DivLong(1, 0));
            Assert.Throws<SimInvariantException>(() => FixMath.ModLong(1, 0));
        }

        [Fact]
        public void MulDiv_Uses128BitIntermediate()
        {
            Assert.Equal(long.MaxValue, FixMath.MulDiv(long.MaxValue, 2, 2));
            Assert.Equal(-3L, FixMath.MulDiv(-7, 1, 2));
            Assert.Equal(long.MinValue, FixMath.MulDiv(long.MinValue, 1, -1));
            Assert.Throws<SimInvariantException>(() => FixMath.MulDiv(1, 1, 0));
        }

        [Fact]
        public void SaturateToInt()
        {
            Assert.Equal(int.MaxValue, FixMath.SaturateToInt(long.MaxValue));
            Assert.Equal(int.MinValue, FixMath.SaturateToInt(long.MinValue));
            Assert.Equal(-5, FixMath.SaturateToInt(-5));
        }

        // spec 02 11절 "강화 배율 표"의 Raw 값
        [Fact]
        public void PowTable_Enhance115_MatchesSpec02Table()
        {
            long[] expected =
            {
                4294967296L, 4939212390L, 5680094249L, 6532108386L, 7511924644L, 8638713341L, 9934520342L,
                11424698393L, 13138403152L, 15109163625L, 17375538169L, 19981868894L, 22979149228L, 26426021613L,
            };
            for (int n = 0; n < expected.Length; n++)
            {
                Assert.Equal(expected[n], FixMath.PowTable(PowBase.Enhance115, n).Raw);
            }
        }

        [Fact]
        public void PowTable_ClampsOutOfRangeExponents()
        {
            Assert.Equal(One, FixMath.PowTable(PowBase.Enhance115, -3).Raw);
            Assert.Equal(26426021613L, FixMath.PowTable(PowBase.Enhance115, 99).Raw);
            Assert.Equal(One, FixMath.PowTable(PowBase.Decay085, 0).Raw);
            Assert.Equal(3650722202L, FixMath.PowTable(PowBase.Decay085, 1).Raw);    // 0.85 × 2^32 = 3,650,722,201.6
            Assert.Equal(3951369912L, FixMath.PowTable(PowBase.Decay092, 1).Raw);    // 0.92 × 2^32 = 3,951,369,912.3
            Assert.Equal(2104533975L, FixMath.PowTable(PowBase.Chain07, 2).Raw);     // 0.49 × 2^32 = 2,104,533,975.0
        }
    }
}

using Wizard.Core;
using Xunit;

namespace Wizard.Core.Tests
{
    // CONTRACTS 1.1, spec 01 2.2. 기대값은 명세의 표와 손으로 계산한 정확값이다.
    public sealed class Fix64Tests
    {
        [Theory]
        [InlineData(15, 100, 644245094L)]      // 0.15 피격 반지름
        [InlineData(30, 100, 1288490189L)]     // 0.30 결투 피격·몸 반지름
        [InlineData(4, 10, 1717986918L)]       // 0.4 판정 중심 오프셋
        [InlineData(6, 10, 2576980378L)]       // 0.6 스침 감지
        [InlineData(75, 100, 3221225472L)]     // 0.75 스침 감지
        public void FromRatio_MatchesSpec01ConstantTable(long num, long den, long expectedRaw)
        {
            Assert.Equal(expectedRaw, Fix64.FromRatio(num, den).Raw);
        }

        [Fact]
        public void FromRatio_TiesRoundToEven()
        {
            long twoTo33 = 1L << 33;
            Assert.Equal(0L, Fix64.FromRatio(1, twoTo33).Raw);     // 0.5 Raw → 0 (짝수)
            Assert.Equal(2L, Fix64.FromRatio(3, twoTo33).Raw);     // 1.5 Raw → 2
            Assert.Equal(2L, Fix64.FromRatio(5, twoTo33).Raw);     // 2.5 Raw → 2
            Assert.Equal(-2L, Fix64.FromRatio(-3, twoTo33).Raw);   // −1.5 Raw → −2
            Assert.Equal(-2L, Fix64.FromRatio(3, -twoTo33).Raw);
        }

        [Fact]
        public void FromRatio_ZeroDenominatorThrows()
        {
            Assert.Throws<SimInvariantException>(() => Fix64.FromRatio(1, 0));
        }

        [Fact]
        public void FromPermille_IsRatioOf1000()
        {
            Assert.Equal(Fix64.FromRatio(1200, 1000), Fix64.FromPermille(1200));
            Assert.Equal(Fix64.One, Fix64.FromPermille(1000));
        }

        [Fact]
        public void Constants()
        {
            Assert.Equal(1L << 32, Fix64.One.Raw);
            Assert.Equal(1L << 31, Fix64.Half.Raw);
            Assert.Equal(0L, Fix64.Zero.Raw);
            Assert.Equal(long.MinValue, Fix64.MinValue.Raw);
            Assert.Equal(long.MaxValue, Fix64.MaxValue.Raw);
            Assert.Equal(-7L << 32, Fix64.FromInt(-7).Raw);
        }

        [Fact]
        public void Multiply_FloorsTowardNegativeInfinity()
        {
            Assert.Equal(Fix64.FromInt(12), Fix64.FromInt(3) * Fix64.FromInt(4));
            Assert.Equal(Fix64.FromInt(-3), Fix64.FromRatio(-3, 2) * Fix64.FromInt(2));
            Assert.Equal(-1L, (Fix64.FromRaw(-1) * Fix64.Half).Raw);   // −0.5 Raw → −1
            Assert.Equal(0L, (Fix64.FromRaw(1) * Fix64.Half).Raw);     // 0.5 Raw → 0
        }

        // spec 01 2.3: 대각선 한 축 = (vRaw × INV_SQRT2) >> 32. 표의 값(축 → 대각)
        [Theory]
        [InlineData(336439105L, 237898372L)]
        [InlineData(357913941L, 253083374L)]
        [InlineData(322122547L, 227775037L)]
        [InlineData(400863614L, 283453379L)]
        [InlineData(372230499L, 263206710L)]
        public void Multiply_ReproducesSpec01DiagonalSpeedTable(long axisRaw, long diagRaw)
        {
            Assert.Equal(diagRaw, (Fix64.FromRaw(axisRaw) * Fix64.FromRaw(FixMath.InvSqrt2Raw)).Raw);
        }

        [Fact]
        public void AddSubtractMultiply_WrapOnOverflow()
        {
            Assert.Equal(Fix64.MinValue, Fix64.MaxValue + Fix64.FromRaw(1));
            Assert.Equal(Fix64.MaxValue, Fix64.MinValue - Fix64.FromRaw(1));
            Assert.Equal(Fix64.MinValue, -Fix64.MinValue);
            Assert.Equal(unchecked(long.MaxValue * 2), (Fix64.MaxValue * 2).Raw);
        }

        [Fact]
        public void Divide_TruncatesTowardZero()
        {
            Assert.Equal(1431655765L, (Fix64.One / Fix64.FromInt(3)).Raw);
            Assert.Equal(-1431655765L, (Fix64.One / Fix64.FromInt(-3)).Raw);
            Assert.Equal(0L, (Fix64.FromRaw(-1) / Fix64.FromInt(2)).Raw);
            Assert.Equal(Fix64.FromInt(-4), Fix64.FromInt(-12) / Fix64.FromInt(3));
            Assert.Equal(-1L, (Fix64.FromRaw(-3) / 2).Raw);
        }

        [Fact]
        public void Divide_ZeroThrowsAndMinValueByMinusOneWraps()
        {
            Assert.Throws<SimInvariantException>(() => Fix64.One / Fix64.Zero);
            Assert.Throws<SimInvariantException>(() => Fix64.One / 0);
            Assert.Equal(Fix64.MinValue, Fix64.MinValue / Fix64.FromInt(-1));
            Assert.Equal(Fix64.MinValue, Fix64.MinValue / -1);
        }

        [Fact]
        public void Rounding()
        {
            Fix64 m15 = Fix64.FromRatio(-3, 2);
            Assert.Equal(-2, m15.FloorToInt());
            Assert.Equal(-1, m15.CeilToInt());
            Assert.Equal(-1, m15.RoundToInt());                      // .5는 양의 무한대 쪽
            Assert.Equal(3, Fix64.FromRatio(5, 2).RoundToInt());
            Assert.Equal(2, Fix64.FromRatio(5, 2).FloorToInt());
            Assert.Equal(3, Fix64.FromRatio(5, 2).CeilToInt());
            Assert.Equal(2, Fix64.FromInt(2).CeilToInt());
        }

        [Fact]
        public void MulPermille_Truncates()
        {
            Assert.Equal(5153960755L, Fix64.MulPermille(Fix64.One, 1200).Raw);     // 1.2 × 2^32 = 5,153,960,755.2
            Assert.Equal(-5153960755L, Fix64.MulPermille(-Fix64.One, 1200).Raw);
        }

        [Fact]
        public void AbsMinMaxClampCompare()
        {
            Assert.Equal(Fix64.FromInt(3), Fix64.Abs(Fix64.FromInt(-3)));
            Assert.Equal(Fix64.MinValue, Fix64.Abs(Fix64.MinValue));
            Assert.Equal(Fix64.FromInt(-1), Fix64.Min(Fix64.FromInt(-1), Fix64.One));
            Assert.Equal(Fix64.One, Fix64.Max(Fix64.FromInt(-1), Fix64.One));
            Assert.Equal(Fix64.One, Fix64.Clamp(Fix64.FromInt(5), Fix64.Zero, Fix64.One));
            Assert.Equal(Fix64.Zero, Fix64.Clamp(Fix64.FromInt(-5), Fix64.Zero, Fix64.One));
            Assert.True(Fix64.Half < Fix64.One && Fix64.One >= Fix64.Half && Fix64.Half != Fix64.One);
            Assert.Equal(-1, Fix64.Half.CompareTo(Fix64.One));
        }
    }
}

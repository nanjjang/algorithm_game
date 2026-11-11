using Wizard.Core;
using Xunit;

namespace Wizard.Core.Tests
{
    // CONTRACTS 1.2. 경계 벡터: 0 제수, MinValue ÷ −1, 부호 조합
    public sealed class WideTests
    {
        [Fact]
        public void Mul64_SignedEdges()
        {
            Wide.Mul64(long.MinValue, long.MinValue, out long hi, out ulong lo);
            Assert.Equal(1L << 62, hi);
            Assert.Equal(0UL, lo);
            Wide.Mul64(-1, 1, out hi, out lo);
            Assert.Equal(-1L, hi);
            Assert.Equal(ulong.MaxValue, lo);
            Wide.Mul64(long.MaxValue, -1, out hi, out lo);
            Assert.Equal(-1L, hi);
            Assert.Equal(unchecked((ulong)(-long.MaxValue)), lo);
            Wide.Mul64(0, long.MinValue, out hi, out lo);
            Assert.Equal(0L, hi);
            Assert.Equal(0UL, lo);
        }

        [Fact]
        public void UMul64_MaxTimesMax()
        {
            Wide.UMul64(ulong.MaxValue, ulong.MaxValue, out ulong hi, out ulong lo);
            Assert.Equal(ulong.MaxValue - 1UL, hi);
            Assert.Equal(1UL, lo);
        }

        [Fact]
        public void Div128By64_TruncatesAndWraps()
        {
            Wide.Mul64(1234567890123L, 987654321L, out long hi, out ulong lo);
            Assert.Equal(1234567890123L, Wide.Div128By64(hi, lo, 987654321L));
            Assert.Equal(-3L, Wide.Div128By64(-1, unchecked((ulong)-7L), 2));         // −7 / 2 = −3
            Assert.Equal(3L, Wide.Div128By64(-1, unchecked((ulong)-7L), -2));
            Assert.Equal(long.MinValue, Wide.Div128By64(-1, unchecked((ulong)long.MinValue), -1));   // MinValue ÷ −1
            Assert.Equal(0L, Wide.Div128By64(1, 0, 1));                               // 몫 2^64 → 아래 64비트
            Assert.Throws<SimInvariantException>(() => Wide.Div128By64(0, 1, 0));
        }

        [Fact]
        public void ShiftRight32AndCompare()
        {
            Assert.Equal(-1L, Wide.ShiftRight32(-1, ulong.MaxValue));
            Assert.Equal(1L << 32, Wide.ShiftRight32(1, 0));
            Assert.Equal(-1, Wide.Compare128(-1, ulong.MaxValue, 0, 0));
            Assert.Equal(1, Wide.Compare128(0, 2, 0, 1));
            Assert.Equal(0, Wide.Compare128(5, 5, 5, 5));
        }
    }
}

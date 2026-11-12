using System;
using Xunit;

namespace Wizard.Text.Tests
{
    // spec 14 2.5절 TextWidth·Fit·FormatInt 의 경계. 벡터는 S13TextMetricsVectorTests.
    public sealed class TextMetricsTests
    {
        [Theory]
        [InlineData("", 16, 0)]
        [InlineData("가", 16, 16)]
        [InlineData("a", 16, 8)]
        [InlineData("1", 16, 8)]
        [InlineData(" ", 16, 8)]                  // 공백·문장 부호는 절반 [임시: U-07]
        [InlineData(",", 16, 8)]
        [InlineData("…", 16, 8)]                  // "…" 는 기호 = 절반 [임시]
        [InlineData("제자 1", 16, 48)]            // 16 + 16 + 8 + 8
        [InlineData("ㄱㅏ", 12, 24)]              // 호환 자모도 한글 폭 [임시]
        [InlineData("가a", 12, 18)]
        public void WidthPx(string s, int fontPx, int expected)
        {
            Assert.Equal(expected, TextMetrics.WidthPx(s, fontPx));
        }

        [Fact]
        public void WidthPx_SurrogatePairIsOneHalfWidthGlyph()
        {
            Assert.Equal(8, TextMetrics.WidthPx("\U0001F600", 16));
        }

        [Fact]
        public void WidthPx_BadArguments()
        {
            Assert.Throws<ArgumentNullException>(() => TextMetrics.WidthPx(null!, 16));
            Assert.Throws<ArgumentOutOfRangeException>(() => TextMetrics.WidthPx("a", 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => TextMetrics.Ellipsize("a", 10, -2));
        }

        [Fact]
        public void Ellipsize_LongWandNameInNameSlot()
        {
            // spec 14 9절 U-19: "밑줄 긋는 참나무 지팡이"(13자)는 이름 칸 128px@16 에서 "…" 로 자른다.
            // 밑줄(32) 공백(8) 긋는(32) 공백(8) 참나(32) = 112, "무"를 더하면 128 + 8 > 128 → "밑줄 긋는 참나…" (120px)
            string cut = TextMetrics.Ellipsize("밑줄 긋는 참나무 지팡이", 128, 16);
            Assert.Equal("밑줄 긋는 참나…", cut);
            Assert.Equal(120, TextMetrics.WidthPx(cut, 16));
        }

        [Fact]
        public void Ellipsize_MixedHalfWidth()
        {
            // 40px@16: "abcdefgh"(64) → 4자(32) + …(8) = 40
            Assert.Equal("abcd…", TextMetrics.Ellipsize("abcdefgh", 40, 16));
            Assert.Equal("abcde", TextMetrics.Ellipsize("abcde", 40, 16));
        }

        [Fact]
        public void Ellipsize_TooNarrowGivesEllipsisOnly()
        {
            Assert.Equal("…", TextMetrics.Ellipsize("가나", 10, 16));
            Assert.Equal("…", TextMetrics.Ellipsize("가나", 0, 16));
            Assert.Equal("", TextMetrics.Ellipsize("", 0, 16));
        }

        [Fact]
        public void Ellipsize_DoesNotSplitSurrogatePair()
        {
            // "a😀b😀" = 8 × 4 = 32 > 24. 24 − 8 = 16 → "a😀" + "…"
            Assert.Equal("a\U0001F600…", TextMetrics.Ellipsize("a\U0001F600b\U0001F600", 24, 16));
            // 16 − 8 = 8 → "a" 만 들어간다(대리 쌍의 반쪽을 남기지 않는다)
            Assert.Equal("a…", TextMetrics.Ellipsize("a\U0001F600b\U0001F600", 16, 16));
        }

        [Theory]
        [InlineData(0L, "0")]
        [InlineData(7L, "7")]
        [InlineData(999L, "999")]
        [InlineData(1000L, "1,000")]
        [InlineData(48200L, "48,200")]
        [InlineData(1234567L, "1,234,567")]
        [InlineData(-1L, "−1")]
        [InlineData(-48200L, "−48,200")]
        [InlineData(long.MaxValue, "9,223,372,036,854,775,807")]
        [InlineData(long.MinValue, "−9,223,372,036,854,775,808")]
        public void FormatThousands(long v, string expected)
        {
            Assert.Equal(expected, TextMetrics.FormatThousands(v));
        }
    }
}

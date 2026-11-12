using Wizard.ViewLogic.Common;
using Xunit;

namespace Wizard.ViewLogic.Tests.Common
{
    // spec 14 7.7 TV-154~158, 2.5절 TextWidth·Fit·FormatInt 의사코드
    public sealed class UiTextVectorTests
    {
        private const int F16 = 16;

        [Fact]
        public void S14_TV_154__FormatInt48200()
        {
            Assert.Equal("48,200", UiText.FormatInt(48200));
        }

        [Fact]
        public void S14_TV_155__FormatInt999And1000And0()
        {
            Assert.Equal("999", UiText.FormatInt(999));
            Assert.Equal("1,000", UiText.FormatInt(1000));
            Assert.Equal("0", UiText.FormatInt(0));
        }

        [Fact]
        public void S14_TV_156__WidthOfDisciple1AtF16Is48()
        {
            // 16 + 16 + 8 + 8 = 48 [임시: 공백 절반]
            Assert.Equal(48, UiText.WidthPx("제자 1", F16));
        }

        [Fact]
        public void S14_TV_157__EightHangulNameFits128WithoutCut()
        {
            const string name = "가나다라마바사아";
            Assert.Equal(128, UiText.WidthPx(name, F16));
            Assert.Equal(name, UiText.Fit(name, F16, 128));
        }

        [Fact]
        public void S14_TV_158__LongWandNameIsCutWithEllipsisWithin128()
        {
            const string name = "밑줄 긋는 참나무 지팡이";
            Assert.Equal(160 + 24, UiText.WidthPx(name, F16));   // 한글 10 + 공백 3
            string fit = UiText.Fit(name, F16, 128);
            Assert.EndsWith("…", fit);
            Assert.True(UiText.WidthPx(fit, F16) <= 128);
            string kept = fit.Substring(0, fit.Length - 1);
            Assert.StartsWith(kept, name);
            // Fit 은 끝에서 한 자씩만 뺀다: 한 자를 더 남기면 넘친다
            string oneMore = name.Substring(0, kept.Length + 1) + "…";
            Assert.True(UiText.WidthPx(oneMore, F16) > 128);
            // 손 계산: "밑줄 긋는 참나"(한글 6 + 공백 2 = 112) + "…"(8) = 120
            Assert.Equal("밑줄 긋는 참나…", fit);
        }

        [Fact]
        public void FormattedNumberWidthCountsHalfWidthGlyphs()
        {
            // spec 13 TV-239 와 같은 값("48,200" 16px = 48). 그 벡터 자체는 M6-TXT-01 에 배정되어 있다
            Assert.Equal(48, UiText.WidthPx(UiText.FormatInt(48200), F16));
        }

        [Theory]
        [InlineData(-1L, "−1")]
        [InlineData(-48200L, "−48,200")]
        [InlineData(long.MaxValue, "9,223,372,036,854,775,807")]
        [InlineData(long.MinValue, "−9,223,372,036,854,775,808")]
        public void FormatIntSignAndExtremes(long v, string expected)
        {
            Assert.Equal(expected, UiText.FormatInt(v));
        }

        [Fact]
        public void EllipsisAndSymbolsAreHalfWidth()
        {
            Assert.Equal(8, UiText.WidthPx("…", F16));
            Assert.Equal(8, UiText.WidthPx("★", F16));
            Assert.Equal(16, UiText.WidthPx("ㄱ", F16));     // 자모 낱자는 한글
            Assert.Equal(0, UiText.WidthPx(string.Empty, F16));
        }

        [Fact]
        public void FitNeverSplitsSurrogatePairs()
        {
            string s = "ab\U0001F600cd";
            string fit = UiText.Fit(s, F16, 4 * 8);   // 반각 4자 폭
            Assert.Equal("ab\U0001F600…", fit);
        }

        [Fact]
        public void FitWithNoRoomLeavesOnlyTheEllipsis()
        {
            Assert.Equal("…", UiText.Fit("가나", F16, 8));
        }
    }
}

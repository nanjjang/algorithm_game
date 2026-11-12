using Xunit;

namespace Wizard.Text.Tests
{
    // spec 13 TV-238 ~ TV-240(ui 0, ui 1.4). 폭 규칙은 spec 14 2.5절: 한글 = 글자 크기, 숫자·영문·기호 = 절반.
    public sealed class S13TextMetricsVectorTests
    {
        private const int FontPx = 16;
        private const int NameSlotPx = 128;   // 이름 칸 = 한글 8자 폭(spec 14 2.5, ui 0)

        [Fact]
        public void S13_TV_238__NameSlotEightHangulFitsNineEllipsized()
        {
            const string eight = "가나다라마바사아";
            const string nine = "가나다라마바사아자";
            Assert.Equal(NameSlotPx, TextMetrics.WidthPx(eight, FontPx));
            Assert.Equal(eight, TextMetrics.Ellipsize(eight, NameSlotPx, FontPx));

            string cut = TextMetrics.Ellipsize(nine, NameSlotPx, FontPx);
            Assert.EndsWith("…", cut);
            // spec 14 2.5 Fit: "…"(절반 폭 8px)를 붙여 128px 안에 드는 가장 긴 앞부분 → 7자 112 + 8 = 120
            Assert.Equal("가나다라마바사…", cut);
            Assert.True(TextMetrics.WidthPx(cut, FontPx) <= NameSlotPx);
        }

        [Fact]
        public void S13_TV_239__WidthOfFormattedNumber()
        {
            Assert.Equal(48, TextMetrics.WidthPx("48,200", FontPx));
        }

        [Fact]
        public void S13_TV_240__FormatThousands()
        {
            Assert.Equal("48,200", TextMetrics.FormatThousands(48200));
        }
    }
}

using Wizard.ViewLogic.Presentation.Limits;
using Xunit;

namespace Wizard.ViewLogic.Tests.Presentation.Limits
{
    // DECISIONS 2.2·CLAUDE.md 2.5 위험색: 색상각 330~15도, 채도·명도 70% 이상(HSV). effects.md 0.1 의 색으로 확인한다
    public sealed class FlashColorTests
    {
        [Theory]
        [InlineData(0xFF3B30, true)]    // 위험 빨강
        [InlineData(0xFF2E88, true)]    // 위험 자홍
        [InlineData(0xFFFFFF, false)]   // 흰색(채도 0)
        [InlineData(0xFFD24A, false)]   // 금색
        [InlineData(0xFF8A1F, false)]   // 화염 주황(29도)
        [InlineData(0x8A5CFF, false)]   // 암흑 보라
        [InlineData(0x4FE3C1, false)]   // 비전 청록
        [InlineData(0x5A5F6B, false)]   // 식은 재색
        [InlineData(0x7A2040, false)]   // 탁하고 어두운 자주(명도 48%)
        [InlineData(0xB3B3B3, false)]
        public void DangerColorClassification(int rgb, bool danger)
        {
            Assert.Equal(danger, FlashColor.FromRgb(rgb).IsDanger);
        }

        [Fact]
        public void BoundariesAreInclusiveAt70Percent()
        {
            // 명도 = 179/255 = 70.2% ≥ 70, 채도 = 1 → 위험. 178/255 = 69.8% → 아님
            Assert.True(new FlashColor(179, 0, 0).IsDanger);
            Assert.False(new FlashColor(178, 0, 0).IsDanger);
            // 채도 70%: max 200, min 60 → 140/200 = 70% → 위험. min 61 → 69.5% → 아님
            Assert.True(new FlashColor(200, 60, 60).IsDanger);
            Assert.False(new FlashColor(200, 61, 61).IsDanger);
            // 색상각 15도: R255, G(=255·15/60 = 63.75) → G 63 은 14.8도 위험, G 66 은 15.5도 아님
            Assert.True(new FlashColor(255, 63, 0).IsDanger);
            Assert.False(new FlashColor(255, 66, 0).IsDanger);
            // 색상각 330도: R255, B 127 → 330.1도 위험. B 130 → 329.4도 아님
            Assert.True(new FlashColor(255, 0, 127).IsDanger);
            Assert.False(new FlashColor(255, 0, 130).IsDanger);
        }
    }
}

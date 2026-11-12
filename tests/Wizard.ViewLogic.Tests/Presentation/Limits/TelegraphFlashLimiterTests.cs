using Wizard.ViewLogic.Presentation.Limits;
using Xunit;

namespace Wizard.ViewLogic.Tests.Presentation.Limits
{
    // effects.md 14.2 "번쩍 한도와의 관계", spec 13 14.5 "예고의 흰 번쩍"
    public sealed class TelegraphFlashLimiterTests
    {
        [Fact]
        public void AfterAreaSumPasses25PercentInOneSecondFlashesBecomeBorders()
        {
            var t = new TelegraphFlashLimiter();
            MotionSettings s = MotionSettings.Default;
            Assert.Equal(FlashOutcome.Flash, t.Submit(0.10f, 0, s));
            Assert.Equal(FlashOutcome.Flash, t.Submit(0.10f, 100, s));
            Assert.Equal(FlashOutcome.Flash, t.Submit(0.10f, 200, s));     // 합 0.30 이 된 번쩍까지는 낸다
            Assert.Equal(FlashOutcome.BorderHighlight, t.Submit(0.01f, 300, s));   // 그 뒤의 번쩍은 테두리 강조
            Assert.Equal(FlashOutcome.BorderHighlight, t.Submit(0.01f, 999, s));
            Assert.Equal(FlashOutcome.Flash, t.Submit(0.05f, 1100, s));    // 0, 100 이 창을 나가 합 0.10
            Assert.Equal(2, TelegraphFlashLimiter.BorderFrames);
        }

        [Theory]
        [InlineData(MotionPreset.Reduced)]
        [InlineData(MotionPreset.Minimal)]
        public void ReducedAndMinimalAlwaysUseBorders(MotionPreset preset)
        {
            var t = new TelegraphFlashLimiter();
            Assert.Equal(FlashOutcome.BorderHighlight, t.Submit(0.01f, 0, MotionSettings.Of(preset)));
            Assert.Equal(0f, t.RecentAreaSum);
        }
    }
}

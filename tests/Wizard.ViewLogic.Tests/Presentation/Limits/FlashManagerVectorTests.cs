using Wizard.ViewLogic.Presentation.Limits;
using Xunit;

namespace Wizard.ViewLogic.Tests.Presentation.Limits
{
    // spec 13 18.6 TV-178~185, 14.1·14.2절(effects.md 0.3·0.4)
    public sealed class FlashManagerVectorTests
    {
        private const float Tol = 1e-6f;

        private static FlashRequest Full(float opacity, int riseMs = 50, int fallMs = 100) =>
            new FlashRequest(FlashColor.White, opacity, riseMs, fallMs, 1f, FlashSource.Other);

        [Fact]
        public void S13_TV_178__SecondFlashWithin400msBecomesBorder()
        {
            var m = new FlashManager();
            MotionSettings s = MotionSettings.Default;
            FlashDecision first = m.Submit(Full(0.35f), 0, s);
            Assert.Equal(FlashOutcome.Flash, first.Outcome);
            Assert.Equal(0.35f, first.Opacity, 6);

            FlashDecision second = m.Submit(Full(0.35f), 300, s);
            Assert.Equal(FlashOutcome.BorderHighlight, second.Outcome);
            Assert.Equal(PhotosensitivityLimits.BorderHighlightWidthPx, second.BorderWidthPx);
            Assert.Equal(8, second.BorderWidthPx);
            Assert.Equal(150, second.BorderMs);   // 0.15초 = 9 t
            Assert.False(second.CountedAsFullScreen);
        }

        [Fact]
        public void S13_TV_179__ThirdFlashInOneSecondBecomesBorder()
        {
            var m = new FlashManager();
            MotionSettings s = MotionSettings.Default;
            Assert.Equal(FlashOutcome.Flash, m.Submit(Full(0.35f), 0, s).Outcome);
            Assert.Equal(FlashOutcome.Flash, m.Submit(Full(0.35f), 500, s).Outcome);
            FlashDecision third = m.Submit(Full(0.35f), 900, s);
            Assert.Equal(FlashOutcome.BorderHighlight, third.Outcome);
            Assert.Equal(2, m.RecentFullFlashCount);
        }

        [Fact]
        public void S13_TV_180__After1001msWindowHoldsOnlyThe500msFlash()
        {
            var m = new FlashManager();
            MotionSettings s = MotionSettings.Default;
            m.Submit(Full(0.35f), 0, s);
            m.Submit(Full(0.35f), 500, s);
            Assert.Equal(FlashOutcome.BorderHighlight, m.Submit(Full(0.35f), 900, s).Outcome);   // TV-179

            FlashDecision d = m.Submit(Full(0.35f), 1001, s);
            Assert.Equal(FlashOutcome.Flash, d.Outcome);           // 창 안에는 500ms 하나, 간격 501ms
            Assert.True(d.CountedAsFullScreen);
            Assert.Equal(2, m.RecentFullFlashCount);                // 500, 1001
        }

        [Theory]
        [InlineData(MotionPreset.Full, FlashOutcome.Flash, 0.35f)]
        [InlineData(MotionPreset.Reduced, FlashOutcome.Flash, 0.15f)]
        [InlineData(MotionPreset.Minimal, FlashOutcome.BorderHighlight, 0f)]
        public void S13_TV_181__Opacity05CappedPerPreset(MotionPreset preset, FlashOutcome outcome, float opacity)
        {
            var m = new FlashManager();
            FlashDecision d = m.Submit(Full(0.5f), 0, MotionSettings.Of(preset));
            Assert.Equal(outcome, d.Outcome);
            Assert.Equal(opacity, d.Opacity, 6);
        }

        [Fact]
        public void S13_TV_182__FlashScale50MultipliesTheCappedOpacity()
        {
            var m = new FlashManager();
            var s = new MotionSettings(MotionPreset.Full, 100, 50, true);
            FlashDecision d = m.Submit(Full(0.5f), 0, s);
            Assert.Equal(FlashOutcome.Flash, d.Outcome);
            Assert.InRange(d.Opacity, 0.175f - Tol, 0.175f + Tol);   // min(0.5, 0.35) × 0.5 [임시: 곱]
        }

        [Theory]
        [InlineData(0xFF3B30)]   // 위험색 빨강(effects.md 0.1)
        [InlineData(0xFF2E88)]   // 위험색 자홍
        [InlineData(0xFF0000)]
        public void S13_TV_183__RedFullScreenFlashIsForbidden(int rgb)
        {
            var m = new FlashManager();
            var r = new FlashRequest(FlashColor.FromRgb(rgb), 0.2f, 50, 100, 1f, FlashSource.PlayerHit);
            FlashDecision d = m.Submit(r, 0, MotionSettings.Default);
            Assert.Equal(FlashOutcome.BorderHighlight, d.Outcome);
            Assert.Equal(0, m.RecentFullFlashCount);
        }

        [Fact]
        public void S13_TV_184__Local20PercentIsNotCountedButLocal25PercentIs()
        {
            var m = new FlashManager();
            MotionSettings s = MotionSettings.Default;
            var local20 = new FlashRequest(FlashColor.White, 0.6f, 30, 60, 0.20f, FlashSource.CombatEffect);
            FlashDecision a = m.Submit(local20, 0, s);
            Assert.Equal(FlashOutcome.Flash, a.Outcome);
            Assert.False(a.CountedAsFullScreen);
            Assert.Equal(0, m.RecentFullFlashCount);
            Assert.Equal(0.6f, a.Opacity, 6);   // 작은 국소 번쩍은 그대로 통과(14.2 의사코드)

            var local25 = new FlashRequest(FlashColor.White, 0.6f, 30, 60, 0.25f, FlashSource.CombatEffect);
            FlashDecision b = m.Submit(local25, 0, s);
            Assert.Equal(FlashOutcome.Flash, b.Outcome);
            Assert.True(b.CountedAsFullScreen);
            Assert.Equal(1, m.RecentFullFlashCount);
            Assert.Equal(0.35f, b.Opacity, 6);  // 전체 화면 번쩍으로 세므로 35% 한도를 받는다

            // 20% 는 횟수에 안 세므로 같은 시각의 25% 가 1회째였다. 바로 뒤의 25% 는 간격 0.4초에 걸린다
            Assert.Equal(FlashOutcome.BorderHighlight, m.Submit(local25, 100, s).Outcome);
            Assert.Equal(FlashOutcome.Flash, m.Submit(local20, 100, s).Outcome);
        }

        [Fact]
        public void S13_TV_185__RiseIsForcedTo50msAndFallTo100ms()
        {
            var m = new FlashManager();
            FlashDecision d = m.Submit(Full(0.3f, riseMs: 200, fallMs: 50), 0, MotionSettings.Default);
            Assert.Equal(FlashOutcome.Flash, d.Outcome);
            Assert.Equal(50, d.RiseMs);    // 켜짐 0.2초 → 0.05초
            Assert.Equal(100, d.FallMs);   // 꺼짐 0.05초 → 0.10초

            FlashDecision e = new FlashManager().Submit(Full(0.3f, riseMs: 20, fallMs: 400), 0, MotionSettings.Default);
            Assert.Equal(20, e.RiseMs);    // 이미 한도 안이면 그대로
            Assert.Equal(400, e.FallMs);
        }
    }
}

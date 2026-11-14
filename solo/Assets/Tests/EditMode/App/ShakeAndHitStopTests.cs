using NUnit.Framework;
using Wizard.Solo.App;

namespace Wizard.Solo.Tests.App
{
    /// <summary>08 요약서 14.1절의 흔들림 누적기(TV-186)와 그림 정지 배율. 폭은 기준 화면 px.</summary>
    public class ShakeAndHitStopTests
    {
        const float Small = 4f, Medium = 8f, Large = 12f, Huge = 16f;

        static float Amp(float px, int level = 0, float scale = 1f)
        {
            var s = new ShakeAccumulator();
            s.Add(px, 0.5f);
            return s.AmplitudePx(level, scale);
        }

        [Test]
        public void S13_TV_186__SizesGive4_8_12_16px()
        {
            Assert.AreEqual(4f, Amp(Small), 1e-3f);
            Assert.AreEqual(8f, Amp(Medium), 1e-3f);
            Assert.AreEqual(12f, Amp(Large), 1e-3f);
            Assert.AreEqual(16f, Amp(Huge), 1e-3f);
        }

        [Test]
        public void S13_TV_186__LargePlusLargeIs16()
        {
            var s = new ShakeAccumulator();
            s.Add(Large, 0.5f);
            s.Add(Large, 0.5f);
            Assert.AreEqual(16f, s.AmplitudePx(0, 1f), 1e-3f);
        }

        [Test]
        public void S13_TV_186__ReducedMediumIs2_4()
        {
            Assert.AreEqual(2.4f, Amp(Medium, level: 1), 1e-3f);
            Assert.AreEqual(0f, Amp(Huge, level: 2), "minimal = no shake");
        }

        [Test]
        public void S13_TV_186__SmallAfterTenthSecondIs1px()
        {
            var s = new ShakeAccumulator();
            s.Add(Small, 0.6f);
            s.Update(0.1f);
            Assert.AreEqual(0.25f, s.Accumulated, 1e-4f);
            Assert.AreEqual(1f, s.AmplitudePx(0, 1f), 1e-3f);
        }

        [Test]
        public void ShakeScaleMultiplies_AndCapsAt16px()
        {
            Assert.AreEqual(8f, Amp(Huge, 0, 0.5f), 1e-3f);
            Assert.AreEqual(16f, Amp(100f), 1e-3f, "request above the limit is clamped");
        }

        [Test]
        public void DurationCappedAt0_6s()
        {
            var s = new ShakeAccumulator();
            s.Add(Huge, 5f);
            Assert.AreEqual(0.6f, s.RemainingSeconds, 1e-5f);
            s.Update(0.3f);
            Assert.Greater(s.AmplitudePx(0, 1f), 0f);
            s.Update(0.31f);
            Assert.AreEqual(0f, s.AmplitudePx(0, 1f));
        }

        [Test]
        public void HitStop_DisabledIgnored_ReducedHalf_MinimalZero()
        {
            var h = new HitStopTimer();
            h.Request(0.1f, 0, enabled: false);
            Assert.IsFalse(h.Active);
            h.Request(0.1f, 2, enabled: true);
            Assert.IsFalse(h.Active);
            h.Request(0.1f, 1, enabled: true);
            Assert.AreEqual(0.05f, h.RemainingSeconds, 1e-5f);
            h.Reset();
            h.Request(0.1f, 0, enabled: true);
            Assert.AreEqual(0.1f, h.RemainingSeconds, 1e-5f);
        }

        [Test]
        public void HitStop_ZeroesDisplayDtUntilExpired()
        {
            var h = new HitStopTimer();
            h.Request(0.05f, 0, true);
            Assert.AreEqual(0f, h.Filter(1f / 60f));
            Assert.AreEqual(0f, h.Filter(1f / 60f));
            Assert.AreEqual(0f, h.Filter(1f / 60f));
            Assert.AreEqual(1f / 60f, h.Filter(1f / 60f), 1e-6f);
        }
    }
}

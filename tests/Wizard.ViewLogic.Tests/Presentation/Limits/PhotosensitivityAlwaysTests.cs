using System.Collections.Generic;
using Wizard.ViewLogic.Presentation.Limits;
using Xunit;

namespace Wizard.ViewLogic.Tests.Presentation.Limits
{
    // 수락 조건: "광과민 한도는 옵션과 무관하게 항상 적용된다"(spec 13 14.1, effects.md 0.3).
    // 모든 설정 조합과 마구잡이 요청 흐름에서 한도가 지켜지는지 본다.
    public sealed class PhotosensitivityAlwaysTests
    {
        private static IEnumerable<MotionSettings> AllSettings()
        {
            foreach (MotionPreset p in new[] { MotionPreset.Full, MotionPreset.Reduced, MotionPreset.Minimal })
            {
                foreach (int scale in new[] { 0, 1, 37, 50, 99, 100, 250, -5 })
                {
                    foreach (bool hitstop in new[] { true, false })
                    {
                        yield return new MotionSettings(p, scale, scale, hitstop);
                    }
                }
            }
        }

        [Fact]
        public void FullScreenFlashLimitsHoldForEverySetting()
        {
            foreach (MotionSettings s in AllSettings())
            {
                var m = new FlashManager();
                var rng = new DisplayRandom(99u);
                var starts = new List<long>();
                long now = 0;
                for (int i = 0; i < 3000; i++)
                {
                    now += rng.NextUInt() % 300u;
                    float opacity = rng.NextUnit() * 3f;                               // 순백 100% 넘는 요청까지
                    float coverage = 0.25f + rng.NextUnit() * 0.75f;
                    int rise = (int)(rng.NextUInt() % 500u);
                    int fall = (int)(rng.NextUInt() % 500u);
                    FlashColor color = i % 7 == 0 ? FlashColor.FromRgb(0xFF3B30) : new FlashColor((byte)rng.NextUInt(), (byte)rng.NextUInt(), (byte)rng.NextUInt());
                    FlashDecision d = m.Submit(new FlashRequest(color, opacity, rise, fall, coverage, FlashSource.Other), now, s);
                    if (d.Outcome != FlashOutcome.Flash)
                    {
                        Assert.Equal(8, d.BorderWidthPx);
                        Assert.Equal(150, d.BorderMs);
                        continue;
                    }
                    Assert.True(d.CountedAsFullScreen);
                    Assert.False(color.IsDanger);
                    Assert.True(d.Opacity > 0f && d.Opacity <= PhotosensitivityLimits.MaxFullFlashOpacity + 1e-6f);
                    Assert.True(d.Opacity <= MotionPresets.Of(s.Preset).FlashCap + 1e-6f);
                    Assert.True(d.RiseMs <= 50 && d.FallMs >= 100);
                    if (starts.Count > 0)
                    {
                        Assert.True(now - starts[starts.Count - 1] >= 400);
                    }
                    starts.Add(now);
                    int inWindow = 0;
                    foreach (long t in starts)
                    {
                        if (now - t < 1000)
                        {
                            inWindow++;
                        }
                    }
                    Assert.True(inWindow <= 2);
                }
                if (s.Preset == MotionPreset.Minimal || s.FlashScale == 0)
                {
                    Assert.Empty(starts);
                }
            }
        }

        [Fact]
        public void ShakeNeverExceeds16pxAndAlwaysEndsWithin600ms()
        {
            foreach (MotionSettings s in AllSettings())
            {
                var a = new ShakeAccumulator();
                var rng = new DisplayRandom(5u);
                for (int i = 0; i < 2000; i++)
                {
                    if (rng.NextUnit() < 0.3f)
                    {
                        a.Add((ShakeSize)(rng.NextUInt() % 4u));
                    }
                    a.Tick(1f / 60f);
                    Assert.True(a.AmplitudePx(s) <= 16f + 1e-4f);
                }
                a.Add(ShakeSize.XLarge);
                for (int f = 0; f < 36; f++)
                {
                    a.Tick(1f / 60f);
                }
                Assert.Equal(0f, a.AmplitudePx(s));
            }
        }

        [Fact]
        public void CameraZoomStaysInRangeForEveryPreset()
        {
            foreach (MotionPreset p in new[] { MotionPreset.Full, MotionPreset.Reduced, MotionPreset.Minimal })
            {
                foreach (float target in new[] { 0.5f, 1.0f, 1.08f, 1.12f, 2.0f })
                {
                    float z = MotionPresets.RitualZoom(target, p);
                    Assert.InRange(z, 1.0f, 1.12f);
                }
            }
        }
    }
}

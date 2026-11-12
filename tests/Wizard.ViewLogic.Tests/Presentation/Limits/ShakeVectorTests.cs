using Wizard.ViewLogic.Presentation.Limits;
using Xunit;

namespace Wizard.ViewLogic.Tests.Presentation.Limits
{
    // spec 13 18.6 TV-186~190, TV-196, 14.3절(effects.md 0.2·0.3)
    public sealed class ShakeVectorTests
    {
        [Theory]
        [InlineData(ShakeSize.Small, 4f)]
        [InlineData(ShakeSize.Medium, 8f)]
        [InlineData(ShakeSize.Large, 12f)]
        [InlineData(ShakeSize.XLarge, 16f)]
        public void S13_TV_186__SingleShakeGivesTableAmplitude(ShakeSize size, float px)
        {
            var a = new ShakeAccumulator();
            a.Add(size);
            Assert.InRange(a.AmplitudePx(MotionSettings.Default), px - 1e-3f, px + 1e-3f);
        }

        [Fact]
        public void S13_TV_187__LargePlusLargeStopsAtAccumulator1And16px()
        {
            var a = new ShakeAccumulator();
            a.Add(ShakeSize.Large);
            a.Add(ShakeSize.Large);
            Assert.Equal(1f, a.Accumulated);
            Assert.Equal(16f, a.AmplitudePx(MotionSettings.Default), 4);
        }

        [Fact]
        public void S13_TV_188__ReducedMediumIs2Point4px()
        {
            var a = new ShakeAccumulator();
            a.Add(ShakeSize.Medium);
            Assert.InRange(a.AmplitudePx(MotionSettings.Of(MotionPreset.Reduced)), 2.4f - 1e-3f, 2.4f + 1e-3f);
        }

        [Fact]
        public void S13_TV_189__MinimalXLargeIsZero()
        {
            var a = new ShakeAccumulator();
            a.Add(ShakeSize.XLarge);
            Assert.Equal(0f, a.AmplitudePx(MotionSettings.Of(MotionPreset.Minimal)));
        }

        [Fact]
        public void S13_TV_190__SmallThen100msLeavesAccumulator025And1px()
        {
            var a = new ShakeAccumulator();
            a.Add(ShakeSize.Small);
            a.Tick(0.1f);
            Assert.InRange(a.Accumulated, 0.25f - 1e-5f, 0.25f + 1e-5f);
            Assert.InRange(a.AmplitudePx(MotionSettings.Default), 1f - 1e-3f, 1f + 1e-3f);
        }

        [Fact]
        public void S13_TV_196__ShakeRequestOf800msIsCutTo600msOrLess()
        {
            Assert.Equal(600, PhotosensitivityLimits.ClampShakeMs(800));
            Assert.True(PhotosensitivityLimits.ClampShakeMs(800) <= 600);
            Assert.Equal(16f, PhotosensitivityLimits.ClampShakePx(20f));

            // 누적기로 내는 흔들림은 가장 센 상태(누적 1)에서도 0.6초 안에 0이 된다(감소 초당 2.5 → 0.4초, C-03·U-21)
            var a = new ShakeAccumulator();
            a.Add(ShakeSize.XLarge);
            a.Add(ShakeSize.XLarge);
            float t = 0f;
            const float dt = 1f / 60f;
            while (a.AmplitudePx(MotionSettings.Default) > 0f && t < 2f)
            {
                a.Tick(dt);
                t += dt;
            }
            Assert.True(t <= 0.6f, "shake lasted " + t + "s");
        }

        [Fact]
        public void ShakeScaleMultipliesAndNeverExceeds16px()
        {
            var a = new ShakeAccumulator();
            for (int i = 0; i < 10; i++)
            {
                a.Add(ShakeSize.XLarge);
            }
            Assert.Equal(16f, a.AmplitudePx(MotionSettings.Default), 4);
            Assert.Equal(8f, a.AmplitudePx(new MotionSettings(MotionPreset.Full, 50, 100, true)), 4);
            Assert.Equal(0f, a.AmplitudePx(new MotionSettings(MotionPreset.Full, 0, 100, true)));
            // 범위 밖 설정 값은 0~100 으로 잘린다(한도를 키울 수 없다)
            Assert.Equal(16f, a.AmplitudePx(new MotionSettings(MotionPreset.Full, 400, 100, true)), 4);
        }

        [Fact]
        public void AddValuesAreSqrtOfWidthOver16()
        {
            Assert.InRange(ShakeAccumulator.AddFor(ShakeSize.Small), 0.5f - 1e-6f, 0.5f + 1e-6f);
            Assert.InRange(ShakeAccumulator.AddFor(ShakeSize.Medium), 0.7071f - 1e-4f, 0.7071f + 1e-4f);
            Assert.InRange(ShakeAccumulator.AddFor(ShakeSize.Large), 0.8660f - 1e-4f, 0.8660f + 1e-4f);
            Assert.Equal(1f, ShakeAccumulator.AddFor(ShakeSize.XLarge));
        }

        [Fact]
        public void SampledOffsetNeverExceedsAmplitude()
        {
            var a = new ShakeAccumulator();
            a.Add(ShakeSize.Large);
            var rng = new DisplayRandom(12345u);
            float amp = a.AmplitudePx(MotionSettings.Default);
            for (int i = 0; i < 1000; i++)
            {
                Assert.True(a.SampleOffset(MotionSettings.Default, rng).Length() <= amp + 1e-4f);
            }
            Assert.Equal(System.Numerics.Vector2.Zero, a.SampleOffset(MotionSettings.Of(MotionPreset.Minimal), rng));
        }
    }
}

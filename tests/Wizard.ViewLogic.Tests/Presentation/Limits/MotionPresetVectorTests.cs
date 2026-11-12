using Wizard.ViewLogic.Presentation.Limits;
using Xunit;

namespace Wizard.ViewLogic.Tests.Presentation.Limits
{
    // spec 13 18.6 TV-193~195, 14.1·14.5절(effects.md 0.3·0.4·9.3)
    public sealed class MotionPresetVectorTests
    {
        [Theory]
        [InlineData(MotionPreset.Full, 1.08f)]
        [InlineData(MotionPreset.Reduced, 1.04f)]
        [InlineData(MotionPreset.Minimal, 1.00f)]
        public void S13_TV_193__RitualZoom108PerPreset(MotionPreset preset, float expected)
        {
            Assert.InRange(MotionPresets.RitualZoom(1.08f, preset), expected - 1e-5f, expected + 1e-5f);
        }

        [Fact]
        public void S13_TV_194__ZoomRateLimitStretchesDuration()
        {
            float rate = CameraZoomLimiter.RatePerSecond(1.00f, 1.08f, 1.8f);
            Assert.InRange(rate, 0.0444f - 1e-4f, 0.0444f + 1e-4f);
            Assert.True(rate <= PhotosensitivityLimits.MaxCameraZoomPerSecond);
            Assert.Equal(1.8f, CameraZoomLimiter.ClampDurationSeconds(1.00f, 1.08f, 1.8f));   // 통과: 그대로

            // [임시] 1.00 → 1.12 를 1.0초에 요청하면 1.2초로 늘린다
            Assert.InRange(CameraZoomLimiter.ClampDurationSeconds(1.00f, 1.12f, 1.0f), 1.2f - 1e-4f, 1.2f + 1e-4f);

            // 프레임 단위로 가도 초당 0.1배를 넘지 않고, 1.12 를 넘는 목표는 1.12 로 자른다
            float z = 1.00f;
            for (int i = 0; i < 60; i++)
            {
                float next = CameraZoomLimiter.Step(z, 1.30f, 1f / 60f);
                Assert.True(next - z <= 0.1f / 60f + 1e-6f);
                z = next;
            }
            Assert.InRange(z, 1.1f - 1e-4f, 1.1f + 1e-4f);
            Assert.Equal(1.12f, CameraZoomLimiter.ClampZoom(1.30f));
            Assert.Equal(1.00f, CameraZoomLimiter.ClampZoom(0.9f));
        }

        [Fact]
        public void S13_TV_195__ParticleBudgetFull1500Reduced800Minimal300()
        {
            ParticleBudgetTable t = ParticleBudgetJson.Table;
            Assert.Equal(1500, t.SceneTotal(ParticleScene.Battle, MotionPreset.Full));
            Assert.Equal(800, t.SceneTotal(ParticleScene.Battle, MotionPreset.Reduced));
            Assert.Equal(300, t.SceneTotal(ParticleScene.Battle, MotionPreset.Minimal));
            // 합계는 분류 몫의 합과 같다(effects.md 9.3 전투 화면 표)
            foreach (MotionPreset p in new[] { MotionPreset.Full, MotionPreset.Reduced, MotionPreset.Minimal })
            {
                Assert.Equal(t.SceneTotal(ParticleScene.Battle, p), t.Scene(ParticleScene.Battle).SumOfQuotas(p));
                Assert.Equal(t.SceneTotal(ParticleScene.Battle, p), new ParticleBudget(t, ParticleScene.Battle, p).Total);
            }
        }

        [Fact]
        public void PresetTableMatchesSpec13Section14_5()
        {
            MotionPresetValues f = MotionPresets.Of(MotionPreset.Full);
            MotionPresetValues r = MotionPresets.Of(MotionPreset.Reduced);
            MotionPresetValues m = MotionPresets.Of(MotionPreset.Minimal);
            Assert.Equal((1.0f, 0.35f, 1.0f, 1.0f), (f.ShakeMul, f.FlashCap, f.HitstopMul, f.ZoomMul));
            Assert.Equal((0.3f, 0.15f, 0.5f, 0.5f), (r.ShakeMul, r.FlashCap, r.HitstopMul, r.ZoomMul));
            Assert.Equal((0.0f, 0.0f, 0.0f, 0.0f), (m.ShakeMul, m.FlashCap, m.HitstopMul, m.ZoomMul));
            Assert.Equal((TelegraphFlash.Zone, IFrameStyle.Blink8Hz, OverlayMode.On, RitualLevel.Full), (f.Telegraph, f.IFrame, f.Overlay, f.Ritual));
            Assert.Equal((TelegraphFlash.ThickBorder, IFrameStyle.Translucent60, OverlayMode.Still, RitualLevel.Reduced), (r.Telegraph, r.IFrame, r.Overlay, r.Ritual));
            Assert.Equal((TelegraphFlash.ThickBorder, IFrameStyle.Translucent60, OverlayMode.Off, RitualLevel.CardOnly), (m.Telegraph, m.IFrame, m.Overlay, m.Ritual));
        }

        [Fact]
        public void DefaultSettingsAreFullWithEverythingOn()
        {
            MotionSettings d = MotionSettings.Default;
            Assert.Equal(MotionPreset.Full, d.Preset);
            Assert.Equal(100, d.ShakeScale);
            Assert.Equal(100, d.FlashScale);
            Assert.True(d.HitstopEnabled);
        }
    }
}

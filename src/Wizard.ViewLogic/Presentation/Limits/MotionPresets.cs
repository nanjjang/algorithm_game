using System;

namespace Wizard.ViewLogic.Presentation.Limits
{
    // spec 13 14.4·14.5절, effects.md 0.4절의 3단계 표. 값은 명세의 의사코드 그대로다(static readonly PresetValues).
    // 번쩍 상한 0.35 는 14.1절 한도와 같은 값이라 PhotosensitivityLimits 를 가리킨다.
    public static class MotionPresets
    {
        public static readonly MotionPresetValues Full = new MotionPresetValues(
            MotionPreset.Full, 1.0f, PhotosensitivityLimits.MaxFullFlashOpacity, 1.0f, 1.0f,
            TelegraphFlash.Zone, IFrameStyle.Blink8Hz, OverlayMode.On, RitualLevel.Full);

        public static readonly MotionPresetValues Reduced = new MotionPresetValues(
            MotionPreset.Reduced, 0.3f, 0.15f, 0.5f, 0.5f,
            TelegraphFlash.ThickBorder, IFrameStyle.Translucent60, OverlayMode.Still, RitualLevel.Reduced);

        public static readonly MotionPresetValues Minimal = new MotionPresetValues(
            MotionPreset.Minimal, 0.0f, 0.0f, 0.0f, 0.0f,
            TelegraphFlash.ThickBorder, IFrameStyle.Translucent60, OverlayMode.Off, RitualLevel.CardOnly);

        public static MotionPresetValues Of(MotionPreset preset)
        {
            switch (preset)
            {
                case MotionPreset.Full: return Full;
                case MotionPreset.Reduced: return Reduced;
                case MotionPreset.Minimal: return Minimal;
                default: throw new ArgumentOutOfRangeException(nameof(preset), preset, null);
            }
        }

        // spec 13 14.4: 그림 정지 길이 배율. hitstop_enabled 가 꺼져 있으면 0 (TV-191)
        public static float HitstopScale(in MotionSettings s) => !s.HitstopEnabled ? 0f : Of(s.Preset).HitstopMul;

        // spec 13 14.5 RitualZoom: 1 + (목표 − 1) × zoomMul. 줄임: 1.08 → 1.04, 최소: 1.00 (TV-193)
        // 목표는 먼저 14.1절의 1.00~1.12 로 자른다
        public static float RitualZoom(float target, MotionPreset preset)
        {
            float t = CameraZoomLimiter.ClampZoom(target);
            return 1f + (t - 1f) * Of(preset).ZoomMul;
        }
    }
}

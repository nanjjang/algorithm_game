using System;

namespace Wizard.ViewLogic.Presentation.Limits
{
    // 연출 한도가 읽는 설정 줄(spec 13 13.2: motion_preset, shake_scale, flash_scale, hitstop_enabled).
    // 설정 화면과 저장(M9-SET-01)이 이 값을 만들어 넘긴다. 낱개 조절은 0~100 으로 자른다.
    public readonly struct MotionSettings
    {
        public const int ScaleMax = 100;   // 백분율의 단위 환산 상수

        public readonly MotionPreset Preset;
        public readonly int ShakeScale;
        public readonly int FlashScale;
        public readonly bool HitstopEnabled;

        public MotionSettings(MotionPreset preset, int shakeScale, int flashScale, bool hitstopEnabled)
        {
            Preset = preset;
            ShakeScale = Math.Max(0, Math.Min(ScaleMax, shakeScale));
            FlashScale = Math.Max(0, Math.Min(ScaleMax, flashScale));
            HitstopEnabled = hitstopEnabled;
        }

        // spec 13 13.2 기본: Full, 흔들림 100, 번쩍 100, 그림 정지 켬
        public static MotionSettings Default => new MotionSettings(MotionPreset.Full, ScaleMax, ScaleMax, true);

        public static MotionSettings Of(MotionPreset preset) => new MotionSettings(preset, ScaleMax, ScaleMax, true);

        public float ShakeFraction => ShakeScale / (float)ScaleMax;

        public float FlashFraction => FlashScale / (float)ScaleMax;
    }
}

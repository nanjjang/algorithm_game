using System;
using System.Numerics;

namespace Wizard.ViewLogic.Presentation.Limits
{
    // spec 13 14.3 ShakeAccumulator, effects.md 0.2절. 흔들림은 누적값(0~1) 하나로 관리한다.
    //  - 사건마다 √(폭/16) 을 더한다(단독일 때 표의 폭이 나오는 값, U-21). 누적값은 1에서 멈춘다 → 여러 사건이 겹쳐도 16px 이하.
    //  - 폭 = 누적값² × 16px × 단계 배율(Full 1, Reduced 0.3, Minimal 0) × shake_scale/100.
    //  - 누적값은 초당 2.5씩 준다. 1에서 0까지 0.4초라 14.1절의 길이 한도 0.6초를 늘 지킨다(19절 C-03: 누적기를 규칙으로 쓴다).
    // 흔들리는 것은 전장 카메라뿐이다. HUD·메뉴·예고 판정·입력은 이 값을 읽지 않는다(spec 13 14.3, CONTRACTS 13).
    public sealed class ShakeAccumulator
    {
        public const float DecayPerSecond = 2.5f;

        // effects.md 0.2 표의 폭(기준 화면 px)
        public const int SmallPx = 4;
        public const int MediumPx = 8;
        public const int LargePx = 12;
        public const int XLargePx = 16;

        private float _acc;

        public float Accumulated => _acc;

        public void Add(ShakeSize size) => _acc = Math.Min(1f, _acc + AddFor(size));

        public void Tick(float dtSeconds)
        {
            if (dtSeconds > 0f)
            {
                _acc = Math.Max(0f, _acc - DecayPerSecond * dtSeconds);
            }
        }

        public void Reset() => _acc = 0f;

        public float AmplitudePx(in MotionSettings settings)
        {
            float presetMul = MotionPresets.Of(settings.Preset).ShakeMul;
            float px = _acc * _acc * PhotosensitivityLimits.MaxShakePx * presetMul * settings.ShakeFraction;
            return PhotosensitivityLimits.ClampShakePx(px);
        }

        // 이번 프레임의 카메라 어긋남(px). 방향은 표시 전용 난수로 고른다. 길이는 AmplitudePx 를 넘지 않는다.
        // 회전·확대 떨림은 만들지 않는다(14.1절)
        public Vector2 SampleOffset(in MotionSettings settings, DisplayRandom random)
        {
            float amp = AmplitudePx(settings);
            if (amp <= 0f)
            {
                return Vector2.Zero;
            }
            var v = new Vector2(random.NextSigned(), random.NextSigned());
            float len = v.Length();
            if (len > 1f)
            {
                v /= len;
            }
            return v * amp;
        }

        public static int NominalPx(ShakeSize size)
        {
            switch (size)
            {
                case ShakeSize.Small: return SmallPx;
                case ShakeSize.Medium: return MediumPx;
                case ShakeSize.Large: return LargePx;
                case ShakeSize.XLarge: return XLargePx;
                default: throw new ArgumentOutOfRangeException(nameof(size), size, null);
            }
        }

        // √(폭/16): 소 0.5, 중 0.7071, 대 0.8660, 특대 1.0 (spec 13 14.3 의사코드, U-21)
        public static float AddFor(ShakeSize size) => (float)Math.Sqrt(NominalPx(size) / (double)PhotosensitivityLimits.MaxShakePx);
    }
}

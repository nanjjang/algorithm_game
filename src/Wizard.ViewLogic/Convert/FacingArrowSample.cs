using Wizard.Core;
using Wizard.Sim.Events;

namespace Wizard.ViewLogic.Convert
{
    // 시야 화살표(spec 01 17.1 FacingArrowMode·Facing, GDD 3 "항상 발밑. 평소 반투명, 자세 중 불투명, 반전 중 다른 색").
    // PlayerView 의 값만 읽는다. 각도는 표시 좌표의 반시계 + 도(E = 0, N = 90)다.
    public readonly struct FacingArrowSample
    {
        public const float HalfTurnDegrees = 180f;        // 단위 환산(도 ↔ 라디안)

        public readonly float AngleDegrees;
        public readonly StanceMode Mode;

        public FacingArrowSample(float angleDegrees, StanceMode mode)
        {
            AngleDegrees = angleDegrees;
            Mode = mode;
        }

        // 평소(Normal)는 반투명, 자세·반전은 불투명
        public bool Opaque => Mode != StanceMode.Normal;

        // 반전 자세는 다른 색
        public bool Reversed => Mode == StanceMode.Reverse;

        public static float DegreesOf(Dir8 facing) => FixConvert.ToDegrees(Angle.FromDir8(facing));

        public static FacingArrowSample From(in PlayerView p) => new FacingArrowSample(DegreesOf(p.Facing), p.Stance);
    }
}

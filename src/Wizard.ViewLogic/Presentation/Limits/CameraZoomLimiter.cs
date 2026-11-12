using System;

namespace Wizard.ViewLogic.Presentation.Limits
{
    // spec 13 14.1 "카메라 확대: 1.00~1.12배, 초당 0.1배 이하의 속도"(effects.md 0.3).
    // 속도 상한을 넘는 요청은 목표를 바꾸지 않고 걸리는 시간을 늘린다 [임시, TV-194].
    public static class CameraZoomLimiter
    {
        public static float ClampZoom(float zoom)
        {
            if (float.IsNaN(zoom))
            {
                return PhotosensitivityLimits.MinCameraZoom;
            }
            return Math.Max(PhotosensitivityLimits.MinCameraZoom, Math.Min(PhotosensitivityLimits.MaxCameraZoom, zoom));
        }

        // from → to 를 요청한 시간(초)에 바꿀 때의 속도(배/초). 시간이 0 이하면 무한대
        public static float RatePerSecond(float from, float to, float seconds)
        {
            float delta = Math.Abs(ClampZoom(to) - ClampZoom(from));
            if (delta == 0f)
            {
                return 0f;
            }
            return seconds > 0f ? delta / seconds : float.PositiveInfinity;
        }

        // 한도 안에서 걸리는 시간(초): max(요청, |Δ| / 0.1). 1.00 → 1.12 를 1.0초에 요청하면 1.2초
        public static float ClampDurationSeconds(float from, float to, float requestedSeconds)
        {
            float delta = Math.Abs(ClampZoom(to) - ClampZoom(from));
            float minSeconds = delta / PhotosensitivityLimits.MaxCameraZoomPerSecond;
            float requested = requestedSeconds > 0f ? requestedSeconds : 0f;
            return Math.Max(requested, minSeconds);
        }

        // 한 프레임에 갈 수 있는 만큼만 목표 쪽으로 옮긴다(프레임 dt 초)
        public static float Step(float current, float target, float dtSeconds)
        {
            float c = ClampZoom(current);
            float t = ClampZoom(target);
            float maxStep = PhotosensitivityLimits.MaxCameraZoomPerSecond * (dtSeconds > 0f ? dtSeconds : 0f);
            float d = t - c;
            if (Math.Abs(d) <= maxStep)
            {
                return t;
            }
            return c + (d > 0f ? maxStep : -maxStep);
        }
    }
}

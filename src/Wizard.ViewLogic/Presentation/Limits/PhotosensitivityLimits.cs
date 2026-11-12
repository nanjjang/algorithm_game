using System;

namespace Wizard.ViewLogic.Presentation.Limits
{
    // spec 13 14.1절, effects.md 0.3절 — 옵션과 무관하게 **항상** 적용하는 광과민 안전 한도 [확정 규칙].
    // 조정할 기획 수치가 아니라 안전 규칙이라 데이터로 빼지 않는다(데이터로 두면 한도를 넘게 고칠 수 있다).
    // 설정(연출 줄이기, 낱개 조절)은 이 한도 안에서 더 줄일 수만 있다.
    public static class PhotosensitivityLimits
    {
        // 전체 화면 번쩍: 어느 1초 구간에서도 2회 이하, 사이 간격 0.4초 이상
        public const int FullFlashWindowMs = 1000;
        public const int MaxFullFlashesPerWindow = 2;
        public const int MinFullFlashGapMs = 400;

        // 세기: 덮는 색의 불투명도 35% 이하
        public const float MaxFullFlashOpacity = 0.35f;

        // 화면 25% 이상을 덮는 국소 번쩍은 전체 화면 번쩍으로 센다
        public const float FullFlashCoverage = 0.25f;

        // 모양: 0.05초 안에 켜지고 0.10초 이상에 걸쳐 꺼진다(켜짐 ≤ 3 t, 꺼짐 ≥ 6 t)
        public const int MaxFlashRiseMs = 50;
        public const int MinFlashFallMs = 100;

        // 한도를 넘는 요청의 대체: 테두리 강조(흰 선 8px, 0.15초 = 9 t)
        public const int BorderHighlightWidthPx = 8;
        public const int BorderHighlightMs = 150;

        // 흔들림: 폭 16px, 길이 0.6초 이하. 화면 회전·확대 떨림 금지
        public const float MaxShakePx = 16f;
        public const int MaxShakeMs = 600;

        // 카메라 확대: 1.00~1.12배, 초당 0.1배 이하
        public const float MinCameraZoom = 1.00f;
        public const float MaxCameraZoom = 1.12f;
        public const float MaxCameraZoomPerSecond = 0.1f;

        // 번쩍 모양 강제: rise = min(요청, 0.05초), fall = max(요청, 0.10초) (spec 13 14.2 의사코드, TV-185)
        public static int ClampFlashRiseMs(int requestedMs) => Math.Max(0, Math.Min(requestedMs, MaxFlashRiseMs));

        public static int ClampFlashFallMs(int requestedMs) => Math.Max(requestedMs, MinFlashFallMs);

        // 길이를 정해 들어온 흔들림 요청을 한도로 자른다(TV-196). 폭도 16px 을 넘지 못한다
        public static int ClampShakeMs(int requestedMs) => Math.Max(0, Math.Min(requestedMs, MaxShakeMs));

        public static float ClampShakePx(float requestedPx) => requestedPx > 0f ? Math.Min(requestedPx, MaxShakePx) : 0f;
    }
}

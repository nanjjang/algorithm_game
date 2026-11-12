using System;

namespace Wizard.ViewLogic.Common
{
    // spec 14 4.3절 "배율과 격자"(UI 1.1). 화면 그림은 1920×1080 기준 1배로 그리고 정수 배로만 키운다(점 필터, 보간 없음).
    public static class UiLayout
    {
        public const int ReferenceWidthPx = 1920;
        public const int ReferenceHeightPx = 1080;

        public const int GridPx = 8;               // 판·버튼의 위치·크기는 8px 의 배수
        public const int IconPaddingStepPx = 2;    // 아이콘 안 여백은 2px 단위
        public const int SafeMarginPx = 48;        // 가장자리 48px 안쪽에 글자와 누를 수 있는 것
        public const int HudSafeMarginPx = 16;     // 전투 HUD 만 16px 까지
        public const int MinHitSizePx = 32;        // 누를 수 있는 것의 최소 크기 32×32
        public const int MinListRowHeightPx = 48;  // 목록 줄 높이 48px 이상

        // function UiScale(W, H): s = min(W / 1920, H / 1080) (정수 나눗셈); return max(1, s)
        // 그 밖의 해상도는 [임시]: 정수 배 중 들어가는 최대, 남는 자리는 여백
        public static int UiScale(int windowWidthPx, int windowHeightPx) =>
            Math.Max(1, Math.Min(windowWidthPx / ReferenceWidthPx, windowHeightPx / ReferenceHeightPx));

        // function UiOrigin(W, H, s): ((W - 1920*s)/2, (H - 1080*s)/2)   // 가운데 맞춤 [임시]
        public static PixelPoint UiOrigin(int windowWidthPx, int windowHeightPx, int scale) =>
            new PixelPoint((windowWidthPx - ReferenceWidthPx * scale) / 2, (windowHeightPx - ReferenceHeightPx * scale) / 2);

        // "1920보다 작은 창은 내려 그리지 않고 1920×1080 창을 최소로 둔다". 창 크기 요청을 최소 크기 위로 올린다
        public static PixelSize ClampWindowSize(int requestedWidthPx, int requestedHeightPx) =>
            new PixelSize(Math.Max(requestedWidthPx, ReferenceWidthPx), Math.Max(requestedHeightPx, ReferenceHeightPx));

        public static bool IsOnGrid(int px) => px % GridPx == 0;
    }
}

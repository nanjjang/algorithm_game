using System;

namespace Wizard.ViewLogic.Common
{
    // spec 14 4.3절 "선택 괄호"(UI 1.3, GDD 13: 선택 표시는 노란 모서리 괄호로 통일), spec 13 1.3절.
    // 치수는 MAN-ui 의 K-40 항목(frame_ms 250, frame_offsets [[-4,-4],[-6,-6]], flip_for_other_corners)과 같은 값이다.
    // 괄호는 화면 그림 층에만 그린다. 전장 안의 적·탄·지팡이에는 그리지 않는다.
    public static class Brackets
    {
        public const int SmallMaxEdgePx = 64;     // 한 변 64px 이하 = 작은 것
        public const int MediumMaxEdgePx = 240;   // 65~240px = 보통, 그보다 크면 큰 것

        public const int SmallArmPx = 12;
        public const int MediumArmPx = 20;
        public const int LargeArmPx = 28;
        public const int SmallSpritePx = 16;
        public const int MediumSpritePx = 24;
        public const int LargeSpritePx = 32;

        public const int LineWidthPx = 4;         // 선 굵기
        public const int OutlinePx = 2;           // 바깥 먹색 테
        public const int GapPx = 4;               // 대상 가장자리에서 바깥으로

        public const int PulsePeriodMs = 500;     // 0.5초 주기로 2px 벌어졌다 오므라든다. 번쩍이지 않는다
        public const int PulseFrames = 2;
        public const int PulseFrameMs = PulsePeriodMs / PulseFrames;          // 250ms(MAN-ui frame_ms)
        public static int PulsePeriodTicks => UiTime.MsToUiTicks(PulsePeriodMs);   // 30t
        public static int PulseFrameTicks => UiTime.MsToUiTicks(PulseFrameMs);     // 15t

        public const int Frame0OffsetPx = -4;     // MAN-ui frame_offsets[0] = [-4,-4]
        public const int Frame1OffsetPx = -6;     // MAN-ui frame_offsets[1] = [-6,-6]

        // spec 14 4.3: e = max(w, h) [임시: "한 변"을 긴 변으로, U-08·U-16]
        public static BracketSize SizeFor(int widthPx, int heightPx)
        {
            int e = Math.Max(widthPx, heightPx);
            return e <= SmallMaxEdgePx ? BracketSize.Small : e <= MediumMaxEdgePx ? BracketSize.Medium : BracketSize.Large;
        }

        public static BracketMetrics MetricsOf(BracketSize size)
        {
            switch (size)
            {
                case BracketSize.Small: return new BracketMetrics(size, SmallArmPx, SmallSpritePx);
                case BracketSize.Medium: return new BracketMetrics(size, MediumArmPx, MediumSpritePx);
                case BracketSize.Large: return new BracketMetrics(size, LargeArmPx, LargeSpritePx);
                default: throw new ArgumentOutOfRangeException(nameof(size), size, null);
            }
        }

        // 커서가 그 대상에 온 뒤 지난 화면 틱 → 프레임(0, 1). 0~14t = 0, 15~29t = 1, 30t 에 다시 0
        public static int PulseFrame(long uiTicksSinceCursorMove)
        {
            if (uiTicksSinceCursorMove < 0)
            {
                return 0;
            }
            return (int)(uiTicksSinceCursorMove / PulseFrameTicks % PulseFrames);
        }

        // 프레임의 모서리 그림 어긋남(왼쪽 위 모서리 기준. 나머지 셋은 뒤집어 쓴다)
        public static PixelPoint PulseOffset(int frame)
        {
            switch (frame)
            {
                case 0: return new PixelPoint(Frame0OffsetPx, Frame0OffsetPx);
                case 1: return new PixelPoint(Frame1OffsetPx, Frame1OffsetPx);
                default: throw new ArgumentOutOfRangeException(nameof(frame), frame, null);
            }
        }
    }
}

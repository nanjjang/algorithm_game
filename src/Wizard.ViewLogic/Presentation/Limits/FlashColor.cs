using System;

namespace Wizard.ViewLogic.Presentation.Limits
{
    // 번쩍의 덮는 색(sRGB 8비트). 엔진 색 타입 대신 쓴다(Wizard.ViewLogic 은 UnityEngine 을 모른다).
    public readonly struct FlashColor
    {
        // DECISIONS 2.2 위험색: 색상각 330~15도이면서 채도 70% 이상, 명도 70% 이상(HSV)
        public const int DangerHueFromDeg = 330;
        public const int DangerHueToDeg = 15;
        public const int DangerMinSaturationPercent = 70;
        public const int DangerMinValuePercent = 70;

        private const int ChannelMax = 255;      // 단위 환산 상수
        private const int Percent = 100;         // 단위 환산 상수
        private const float SextantDeg = 60f;    // 색상환 한 칸(단위 환산 상수)
        private const float FullTurnDeg = 360f;  // 단위 환산 상수

        public readonly byte R;
        public readonly byte G;
        public readonly byte B;

        public FlashColor(byte r, byte g, byte b)
        {
            R = r;
            G = g;
            B = b;
        }

        public static FlashColor White => new FlashColor(ChannelMax, ChannelMax, ChannelMax);

        // 0xRRGGBB
        public static FlashColor FromRgb(int rgb) => new FlashColor((byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF));

        public bool IsDanger
        {
            get
            {
                int max = Math.Max(R, Math.Max(G, B));
                int min = Math.Min(R, Math.Min(G, B));
                int chroma = max - min;
                // 명도 = max / 255, 채도 = (max − min) / max. 정수로 비교한다(경계 70% 를 정확히)
                if (max * Percent < DangerMinValuePercent * ChannelMax)
                {
                    return false;
                }
                if (chroma == 0 || chroma * Percent < DangerMinSaturationPercent * max)
                {
                    return false;
                }
                float hue = HueDeg(max, chroma);
                return hue >= DangerHueFromDeg || hue <= DangerHueToDeg;
            }
        }

        private float HueDeg(int max, int chroma)
        {
            float h;
            if (max == R)
            {
                h = SextantDeg * ((G - B) / (float)chroma);
            }
            else if (max == G)
            {
                h = SextantDeg * (2f + (B - R) / (float)chroma);
            }
            else
            {
                h = SextantDeg * (4f + (R - G) / (float)chroma);
            }
            return h < 0f ? h + FullTurnDeg : h;
        }
    }
}

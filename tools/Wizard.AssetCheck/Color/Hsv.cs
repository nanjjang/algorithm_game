using System;

namespace Wizard.AssetCheck.Color
{
    // HSV(색상각 0~360, 채도·명도 0~100%). spec 14 4.2절 의사코드 그대로(결정론 코어 밖이라 실수 계산을 쓴다).
    // 채도·명도는 HSV 의 S 와 V 다(spec 14 4.2절: 그믐 후드 #7A336A = (313.5°, 58.2%, 47.8%) 로 맞는다)
    public readonly struct Hsv
    {
        private const double MaxChannel = 255.0;
        private const double Percent = 100.0;
        private const double DegreesPerSector = 60.0;
        private const double Sectors = 6.0;
        private const double GreenSector = 2.0;
        private const double BlueSector = 4.0;

        public readonly double H;
        public readonly double S;
        public readonly double V;

        public Hsv(double h, double s, double v)
        {
            H = h;
            S = s;
            V = v;
        }

        // d == 0(무채색)이면 색상각이 없다. 그때 H = 0 으로 둔다(spec 14 4.2절)
        public bool HasHue => S > 0;

        public static Hsv FromRgb(int rgb) => FromRgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);

        public static Hsv FromRgb(int r, int g, int b)
        {
            int mx = Math.Max(r, Math.Max(g, b));
            int mn = Math.Min(r, Math.Min(g, b));
            int d = mx - mn;
            double v = mx / MaxChannel * Percent;
            double s = mx == 0 ? 0 : (double)d / mx * Percent;
            double h;
            if (d == 0)
            {
                h = 0;
            }
            else if (mx == r)
            {
                double t = (double)(g - b) / d;
                t %= Sectors;
                if (t < 0)
                {
                    t += Sectors;   // 수학의 mod: 음수도 0~6 으로
                }
                h = DegreesPerSector * t;
            }
            else if (mx == g)
            {
                h = DegreesPerSector * ((double)(b - r) / d + GreenSector);
            }
            else
            {
                h = DegreesPerSector * ((double)(r - g) / d + BlueSector);
            }
            return new Hsv(h, s, v);
        }
    }
}

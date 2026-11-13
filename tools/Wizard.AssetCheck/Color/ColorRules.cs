using System;

namespace Wizard.AssetCheck.Color
{
    // 그림 반입용 색 규칙(spec 14 4.2절, spec 05 1.4·9.2·9.3절, DECISIONS 2.2).
    // 경계 수치는 조정할 기획 수치가 아니라 확정 규칙과 명세 의사코드의 값이라 데이터로 빼지 않는다
    // (PhotosensitivityLimits 와 같은 까닭: 데이터로 두면 규칙을 넘게 고칠 수 있다). 색은 0xRRGGBB.
    public static class ColorRules
    {
        // DECISIONS 2.2 위험색: 색상각 330~15도, 채도·명도 70% 이상. 경계 포함 [임시: spec 14 4.2절]
        public const double DangerHueFrom = 330;
        public const double DangerHueTo = 15;
        public const double DangerMinSaturation = 70;
        public const double DangerMinValue = 70;

        // "항상 채도 80% 이상과 흰 중심을 같이 쓴다"(DECISIONS 2.2). 흰 중심은 그림 검수 항목이다
        public const double ProperDangerMinSaturation = 80;

        // spec 05 1.4 COL-4: 적 몸에 쓰지 않는 색상각 280~15도
        public const double EnemyForbiddenHueFrom = 280;
        public const double EnemyForbiddenHueTo = 15;

        // spec 05 1.4 COL-5: 청록 150~180도(비전 주문 전용)는 적의 몸·눈·탄에 금지
        public const double TealHueFrom = 150;
        public const double TealHueTo = 180;

        // spec 05 1.4 COL-2: 몸의 채도 50% 이하
        public const double EnemyMaxSaturation = 50;

        // spec 05 1.4 COL-5: 밝은 하늘색(195~210도, 명도 90% 이상) 금지
        public const double SkyHueFrom = 195;
        public const double SkyHueTo = 210;
        public const double SkyMinValue = 90;

        // spec 05 1.4: 적 그림에 따로 허용된 색. 외곽선(MON 1.4), 눈(COL-7), 공격 준비의 무기 부위(COL-8)
        public const int EnemyOutline = 0x1B2440;
        public const int EnemyEyeWhite = 0xFFFFFF;
        public const int EnemyEyeYellow = 0xFFF2A0;
        public const int EnemyWeaponTint = 0xFF2E88;
        public const int Gold = 0xFFD24A;   // COL-6: 적 몸에 금색 덩어리 금지

        // 색상각 비교의 경계 허용(부동소수 오차). TV-COL-07: #3A2E2A 의 색상각은 정확히 15.0
        private const double HueEpsilon = 1e-9;

        public static bool InDangerRange(int rgb)
        {
            Hsv c = Hsv.FromRgb(rgb);
            return c.S >= DangerMinSaturation && c.V >= DangerMinValue && (c.H >= DangerHueFrom || c.H <= DangerHueTo);
        }

        public static bool IsProperDangerUse(int rgb) => InDangerRange(rgb) && Hsv.FromRgb(rgb).S >= ProperDangerMinSaturation;

        // spec 05 9.1 판정 함수 IsDangerColor(h, s, v) — spec 14 의 InDangerRange 와 같은 식이다
        public static bool IsDangerColor(int rgb) => InDangerRange(rgb);

        // 색상각이 [from, to] 안인가(from > to 이면 360 을 넘어 돈다). 무채색은 색상각이 없어 거짓
        public static bool HueIn(Hsv c, double from, double to)
        {
            if (!c.HasHue)
            {
                return false;
            }
            if (from <= to)
            {
                return c.H >= from - HueEpsilon && c.H <= to + HueEpsilon;
            }
            return c.H >= from - HueEpsilon || c.H <= to + HueEpsilon;
        }

        public static bool IsTeal(int rgb) => HueIn(Hsv.FromRgb(rgb), TealHueFrom, TealHueTo);

        // COL-4 의 경계값(정확히 280 또는 15도)인가. 경계는 경고만 낸다(spec 05 C-06, U-30)
        public static bool OnEnemyForbiddenHueEdge(Hsv c) =>
            c.HasHue && (Math.Abs(c.H - EnemyForbiddenHueFrom) <= HueEpsilon || Math.Abs(c.H - EnemyForbiddenHueTo) <= HueEpsilon);

        // CheckPlayerSideAsset(spec 14 4.2절): 알파 > 0 이면서 위험색 범위인 픽셀 수(0 이어야 통과)
        public static int CountDangerPixels(byte[] rgba)
        {
            int n = 0;
            for (int i = 0; i + 3 < rgba.Length; i += 4)
            {
                if (rgba[i + 3] > 0 && InDangerRange((rgba[i] << 16) | (rgba[i + 1] << 8) | rgba[i + 2]))
                {
                    n++;
                }
            }
            return n;
        }

        public static bool CheckPlayerSideAsset(byte[] rgba) => CountDangerPixels(rgba) == 0;

        // 적 몸의 색 하나를 본다(spec 05 1.4 COL-1~7, U-30: 위험색·청록은 오류, 나머지는 경고)
        public static EnemyColorVerdict CheckEnemyColor(int rgb)
        {
            if (rgb == EnemyOutline || rgb == EnemyEyeWhite || rgb == EnemyEyeYellow)
            {
                return EnemyColorVerdict.Ok;
            }
            if (rgb == EnemyWeaponTint)
            {
                return EnemyColorVerdict.WeaponTint;
            }
            Hsv c = Hsv.FromRgb(rgb);
            if (InDangerRange(rgb))
            {
                return EnemyColorVerdict.Danger;
            }
            if (HueIn(c, TealHueFrom, TealHueTo))
            {
                return EnemyColorVerdict.Teal;
            }
            if (rgb == Gold)
            {
                return EnemyColorVerdict.Gold;
            }
            if (OnEnemyForbiddenHueEdge(c))
            {
                return EnemyColorVerdict.ForbiddenHueEdge;
            }
            if (HueIn(c, EnemyForbiddenHueFrom, EnemyForbiddenHueTo))
            {
                return EnemyColorVerdict.ForbiddenHue;
            }
            if (HueIn(c, SkyHueFrom, SkyHueTo) && c.V >= SkyMinValue)
            {
                return EnemyColorVerdict.BrightSky;
            }
            if (c.S > EnemyMaxSaturation)
            {
                return EnemyColorVerdict.OverSaturated;
            }
            return EnemyColorVerdict.Ok;
        }

        public static bool IsEnemyError(EnemyColorVerdict v) => v == EnemyColorVerdict.Danger || v == EnemyColorVerdict.Teal;

        // WCAG 상대 휘도(spec 05 9.2 "상대 휘도 대비, WCAG 식")
        public static double RelativeLuminance(int rgb)
        {
            return 0.2126 * Linear((rgb >> 16) & 0xFF) + 0.7152 * Linear((rgb >> 8) & 0xFF) + 0.0722 * Linear(rgb & 0xFF);
        }

        public static double ContrastRatio(int a, int b)
        {
            double la = RelativeLuminance(a);
            double lb = RelativeLuminance(b);
            double hi = Math.Max(la, lb);
            double lo = Math.Min(la, lb);
            return (hi + 0.05) / (lo + 0.05);
        }

        // 채널마다 (1 − amount)배(spec 05 9.3 "40% 어둡게"). amount 는 0~1. 반올림은 가장 가까운 정수(같으면 짝수)
        public static int Darken(int rgb, double amount) =>
            Map(rgb, c => c * (1 - amount));

        // top 을 alpha 만큼 덮는다(spec 05 9.3 "빨강 70% 덮기")
        public static int Over(int bottom, int top, double alpha)
        {
            int r = Mix((bottom >> 16) & 0xFF, (top >> 16) & 0xFF, alpha);
            int g = Mix((bottom >> 8) & 0xFF, (top >> 8) & 0xFF, alpha);
            int b = Mix(bottom & 0xFF, top & 0xFF, alpha);
            return (r << 16) | (g << 8) | b;
        }

        public static string Hex(int rgb) => "#" + rgb.ToString("X6", System.Globalization.CultureInfo.InvariantCulture);

        private static int Mix(int bottom, int top, double alpha) => Clamp(Math.Round(top * alpha + bottom * (1 - alpha), MidpointRounding.ToEven));

        private static int Map(int rgb, Func<int, double> f)
        {
            int r = Clamp(Math.Round(f((rgb >> 16) & 0xFF), MidpointRounding.ToEven));
            int g = Clamp(Math.Round(f((rgb >> 8) & 0xFF), MidpointRounding.ToEven));
            int b = Clamp(Math.Round(f(rgb & 0xFF), MidpointRounding.ToEven));
            return (r << 16) | (g << 8) | b;
        }

        private static int Clamp(double v) => (int)Math.Max(0, Math.Min(255, v));

        private static double Linear(int channel)
        {
            double c = channel / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }
    }
}

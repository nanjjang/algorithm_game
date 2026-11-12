using System;
using System.Text;

namespace Wizard.ViewLogic.Common
{
    // spec 14 2.5절 "폭과 자르기", spec 13 1.4절. 화면 그림 층의 글자 폭은 글꼴을 재지 않고 이 규칙으로 센다.
    //   폭: 한글 한 자 = 글자 크기, 그 밖(숫자·영문·공백·문장 부호·기호, "…" 포함) = 글자 크기의 절반 [임시: spec 14 U-07]
    //   넘침: 줄이지 않고 끝을 "…"로 자른다. 전체 글은 툴팁
    //   숫자: 세 자리마다 쉼표. 음수 부호는 '−'(U+2212) [임시: spec 14 2.5]
    // CONTRACTS 5절의 Wizard.Text.TextMetrics(M6-TXT-01)와 같은 규칙이다. 그 작업이 들어오면 둘 중 하나로 모은다(결과 보고에 올림).
    public static class UiText
    {
        public const string Ellipsis = "…";
        public const char MinusSign = '−';
        public const char ThousandsSeparator = ',';
        public const int DigitsPerGroup = 3;

        private const int HalfUnitsFull = 2;
        private const int HalfUnitsHalf = 1;

        // 한글(완성형, 자모, 호환 자모, 자모 확장)
        public static bool IsHangul(int codePoint) =>
            (codePoint >= 0xAC00 && codePoint <= 0xD7A3)
            || (codePoint >= 0x1100 && codePoint <= 0x11FF)
            || (codePoint >= 0x3130 && codePoint <= 0x318F)
            || (codePoint >= 0xA960 && codePoint <= 0xA97F)
            || (codePoint >= 0xD7B0 && codePoint <= 0xD7FF);

        // function TextWidth(s, size): sum over ch: IsHangul(ch) ? size : size/2. 반 글자 단위로 세어 정수를 지킨다
        public static int WidthPx(string text, int fontPx)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }
            long half = 0;
            int i = 0;
            while (i < text.Length)
            {
                int cp = CodePointAt(text, i, out int len);
                half += IsHangul(cp) ? HalfUnitsFull : HalfUnitsHalf;
                i += len;
            }
            return (int)(half * fontPx / HalfUnitsFull);
        }

        // function Fit(s, size, maxW):
        //     if TextWidth(s,size) <= maxW: return s
        //     e = TextWidth("…", size)
        //     while s != "" and TextWidth(s,size) + e > maxW: s = s[:-1]
        //     return s + "…"
        // 한 자 = 코드 포인트 하나(서로게이트 쌍을 가르지 않는다)
        public static string Fit(string text, int fontPx, int maxWidthPx)
        {
            if (WidthPx(text, fontPx) <= maxWidthPx)
            {
                return text;
            }
            int e = WidthPx(Ellipsis, fontPx);
            int end = text.Length;
            while (end > 0 && WidthPx(text.Substring(0, end), fontPx) + e > maxWidthPx)
            {
                end = PreviousBoundary(text, end);
            }
            return text.Substring(0, end) + Ellipsis;
        }

        public static bool Fits(string text, int fontPx, int maxWidthPx) => WidthPx(text, fontPx) <= maxWidthPx;

        // function FormatInt(n): 부호 + 절댓값을 오른쪽부터 3자리마다 ','. 문화권을 보지 않는다
        public static string FormatInt(long value)
        {
            ulong magnitude = value < 0 ? (ulong)(-(value + 1)) + 1UL : (ulong)value;
            const ulong Ten = 10;
            var digits = new StringBuilder();
            int count = 0;
            do
            {
                if (count > 0 && count % DigitsPerGroup == 0)
                {
                    digits.Append(ThousandsSeparator);
                }
                digits.Append((char)('0' + (int)(magnitude % Ten)));
                magnitude /= Ten;
                count++;
            }
            while (magnitude != 0);
            if (value < 0)
            {
                digits.Append(MinusSign);
            }
            char[] chars = digits.ToString().ToCharArray();
            Array.Reverse(chars);
            return new string(chars);
        }

        private static int CodePointAt(string s, int i, out int length)
        {
            if (char.IsHighSurrogate(s[i]) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]))
            {
                length = 2;
                return char.ConvertToUtf32(s[i], s[i + 1]);
            }
            length = 1;
            return s[i];
        }

        private static int PreviousBoundary(string s, int end)
        {
            if (end >= 2 && char.IsLowSurrogate(s[end - 1]) && char.IsHighSurrogate(s[end - 2]))
            {
                return end - 2;
            }
            return end - 1;
        }
    }
}

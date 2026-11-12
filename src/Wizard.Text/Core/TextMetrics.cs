using System;
using System.Text;

namespace Wizard.Text
{
    // CONTRACTS 5절, spec 14 2.5절(UI 1.4). 화면 글자의 폭과 자르기, 숫자 형식. 결과는 정수 px 다.
    //   폭: 한글 한 자 = 글자 크기, 그 밖(숫자, 영문, 공백, 문장 부호, 기호) = 글자 크기의 절반(정수 나눗셈) [임시: U-07].
    //       글자는 유니코드 코드 포인트 단위로 센다(대리 쌍은 한 자). 한글 = 음절 + 자모 블록 [임시].
    //   자르기: 칸을 넘으면 줄이지 않고 끝을 "…"(U+2026, 기호라 절반 폭 [임시])로 자른다. 전체 글은 툴팁이 보여 준다(표시 계층).
    //   숫자: 세 자리마다 ','. 음수 부호는 '−'(U+2212) [임시: 글꼴에 있는 기호].
    public static class TextMetrics
    {
        public const string Ellipsis = "…";
        private const char MinusSign = '−';
        private const int GroupSize = 3;
        private const int Decimal = 10;

        public static int WidthPx(string s, int fontPx)
        {
            if (s == null)
            {
                throw new ArgumentNullException(nameof(s));
            }
            CheckFont(fontPx);
            int width = 0;
            int i = 0;
            while (i < s.Length)
            {
                width += GlyphWidth(s, ref i, fontPx);
            }
            return width;
        }

        // spec 14 2.5 Fit: 들어가면 그대로, 아니면 "…" 를 붙여 maxPx 안에 드는 가장 긴 앞부분 + "…".
        // maxPx 가 "…" 하나보다 좁으면 "…" 만 돌려준다(의사코드 그대로).
        public static string Ellipsize(string s, int maxPx, int fontPx)
        {
            if (s == null)
            {
                throw new ArgumentNullException(nameof(s));
            }
            CheckFont(fontPx);
            if (WidthPx(s, fontPx) <= maxPx)
            {
                return s;
            }
            int budget = maxPx - WidthPx(Ellipsis, fontPx);
            int width = 0;
            int keep = 0;   // 남길 UTF-16 길이(대리 쌍을 가르지 않는다)
            int i = 0;
            while (i < s.Length)
            {
                int w = GlyphWidth(s, ref i, fontPx);
                if (width + w > budget)
                {
                    break;
                }
                width += w;
                keep = i;
            }
            return s.Substring(0, keep) + Ellipsis;
        }

        public static string FormatThousands(long v)
        {
            // long.MinValue 의 절댓값도 담기게 ulong 으로 센다
            ulong magnitude = v < 0 ? unchecked(0UL - (ulong)v) : (ulong)v;
            var digits = new StringBuilder(32);
            int count = 0;
            do
            {
                if (count > 0 && count % GroupSize == 0)
                {
                    digits.Append(',');
                }
                digits.Append((char)('0' + (int)(magnitude % Decimal)));
                magnitude /= Decimal;
                count++;
            }
            while (magnitude != 0);
            if (v < 0)
            {
                digits.Append(MinusSign);
            }
            var sb = new StringBuilder(digits.Length);
            for (int k = digits.Length - 1; k >= 0; k--)
            {
                sb.Append(digits[k]);
            }
            return sb.ToString();
        }

        // 위치 i 의 글자 하나의 폭을 돌려주고 i 를 다음 글자로 옮긴다
        private static int GlyphWidth(string s, ref int i, int fontPx)
        {
            char c = s[i];
            if (char.IsHighSurrogate(c) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]))
            {
                i += 2;
                return fontPx / 2;
            }
            i++;
            return IsHangul(c) ? fontPx : fontPx / 2;
        }

        // 한글 음절, 자모, 호환 자모, 자모 확장 A·B 블록 [임시]. 유니코드 블록 경계(코드 포인트)로 적는다
        // (한글 글자 리터럴은 names-scan 이 막는다)
        private static readonly int[] HangulBlocks =
        {
            0xAC00, 0xD7A3,
            0x1100, 0x11FF,
            0x3130, 0x318F,
            0xA960, 0xA97F,
            0xD7B0, 0xD7FF,
        };

        private static bool IsHangul(char c)
        {
            for (int k = 0; k < HangulBlocks.Length; k += 2)
            {
                if (c >= HangulBlocks[k] && c <= HangulBlocks[k + 1])
                {
                    return true;
                }
            }
            return false;
        }

        private static void CheckFont(int fontPx)
        {
            if (fontPx <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(fontPx), "font size must be positive");
            }
        }
    }
}

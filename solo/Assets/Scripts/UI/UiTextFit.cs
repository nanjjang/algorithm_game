using System.Text;

namespace Wizard.Solo.UI
{
    /// <summary>
    /// 글자 폭과 자르기(순수 함수, 08 요약서 1.4절). 화면 글자의 폭은 글꼴을 재지 않고 이 규칙으로 센다.
    /// 폭: 한글·기호(아스키 밖의 글자) 1자 = 글자 크기, 숫자·영문·아스키 문장 부호·공백 1자 = 글자 크기의 절반.
    /// 넘침: 줄이지 않고 끝을 "…"로 자른다(전체 글은 툴팁 — UiLabel이 붙인다).
    /// "48,200" 16px → 48px(TV-239). 이름 칸은 한글 8자 폭(16px → 128px), 9자면 "…"(TV-238).
    /// </summary>
    public static class UiTextFit
    {
        /// <summary>말줄임표(U+2026). 기호이므로 1자 = 글자 크기.</summary>
        public const string Ellipsis = "…";
        /// <summary>이름 칸 폭 = 한글 8자.</summary>
        public const int NameFieldChars = 8;

        /// <summary>한 자의 폭이 절반인가: 아스키(숫자·영문·공백·문장 부호)와 반각 형태(U+FF61~U+FFDC).</summary>
        public static bool IsHalfWidth(int codePoint) =>
            codePoint < 0x80 || (codePoint >= 0xFF61 && codePoint <= 0xFFDC);

        /// <summary>글의 폭(px). null·빈 글은 0.</summary>
        public static int WidthPx(string text, int fontPx)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            long half = 0;
            int i = 0;
            while (i < text.Length)
            {
                int cp = CodePointAt(text, i, out int len);
                half += IsHalfWidth(cp) ? 1 : 2;
                i += len;
            }
            return (int)(half * fontPx / 2);
        }

        public static bool Fits(string text, int fontPx, int maxWidthPx) => WidthPx(text, fontPx) <= maxWidthPx;

        /// <summary>
        /// maxWidthPx 안에 들어가게 끝을 "…"로 자른다. 들어가면 그대로. 한 자 = 코드 포인트 하나(서로게이트 쌍을 가르지 않는다).
        /// "…"조차 들어가지 않으면 "…"만 돌려준다.
        /// </summary>
        public static string Fit(string text, int fontPx, int maxWidthPx)
        {
            if (string.IsNullOrEmpty(text) || WidthPx(text, fontPx) <= maxWidthPx) return text ?? string.Empty;
            int e = WidthPx(Ellipsis, fontPx);
            int end = text.Length;
            while (end > 0 && WidthPx(text.Substring(0, end), fontPx) + e > maxWidthPx)
                end = PreviousBoundary(text, end);
            return text.Substring(0, end) + Ellipsis;
        }

        /// <summary>잘렸는가(Fit의 결과가 원문과 다른가).</summary>
        public static bool IsTruncated(string text, int fontPx, int maxWidthPx) =>
            !string.IsNullOrEmpty(text) && WidthPx(text, fontPx) > maxWidthPx;

        /// <summary>이름 칸 폭(px) = 한글 8자 × 글자 크기.</summary>
        public static int NameFieldWidthPx(int fontPx) => NameFieldChars * fontPx;

        /// <summary>이름 칸(캐릭터 카드·초상 옆·칭호 띠·알림의 이름)에 맞춰 자른다.</summary>
        public static string FitName(string name, int fontPx) => Fit(name, fontPx, NameFieldWidthPx(fontPx));

        /// <summary>세 자리 쉼표(문화권을 보지 않는다). 화면은 보통 ctx.Names.Number를 쓰고, 이름 표가 없을 때 이것을 쓴다.</summary>
        public static string FormatThousands(long value)
        {
            bool neg = value < 0;
            ulong mag = neg ? (ulong)(-(value + 1)) + 1UL : (ulong)value;
            var sb = new StringBuilder();
            int count = 0;
            do
            {
                if (count > 0 && count % 3 == 0) sb.Append(',');
                sb.Append((char)('0' + (int)(mag % 10UL)));
                mag /= 10UL;
                count++;
            } while (mag != 0);
            if (neg) sb.Append('-');
            char[] chars = sb.ToString().ToCharArray();
            System.Array.Reverse(chars);
            return new string(chars);
        }

        static int CodePointAt(string s, int i, out int length)
        {
            if (char.IsHighSurrogate(s[i]) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]))
            {
                length = 2;
                return char.ConvertToUtf32(s[i], s[i + 1]);
            }
            length = 1;
            return s[i];
        }

        static int PreviousBoundary(string s, int end)
        {
            if (end >= 2 && char.IsLowSurrogate(s[end - 1]) && char.IsHighSurrogate(s[end - 2])) return end - 2;
            return end - 1;
        }
    }
}

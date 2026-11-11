using System;
using System.Globalization;

namespace Wizard.TestKit.Shared
{
    // 64비트 상태 해시와 씨앗의 글자 표현. 해시는 소문자 16진 16자리(앞에 0x 없음)로 쓴다.
    // 씨앗은 "0x" 로 시작하는 16진(1~16자리) 또는 십진 문자열이다. JSON 숫자로 쓰지 않는다(u64 가 long 을 넘는다).
    public static class HashText
    {
        public static string Format(ulong hash)
        {
            return hash.ToString("x16", CultureInfo.InvariantCulture);
        }

        public static bool TryParseHash(string? text, out ulong hash)
        {
            hash = 0;
            if (text == null || text.Length != 16)
            {
                return false;
            }
            foreach (char c in text)
            {
                bool ok = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f');
                if (!ok)
                {
                    return false;
                }
            }
            return ulong.TryParse(text, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out hash);
        }

        public static string FormatSeed(ulong seed)
        {
            return "0x" + seed.ToString("x16", CultureInfo.InvariantCulture);
        }

        public static bool TryParseSeed(string? text, out ulong seed)
        {
            seed = 0;
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }
            string s = text!;
            if (s.StartsWith("0x", StringComparison.Ordinal))
            {
                string hex = s.Substring(2);
                if (hex.Length < 1 || hex.Length > 16)
                {
                    return false;
                }
                foreach (char c in hex)
                {
                    bool ok = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
                    if (!ok)
                    {
                        return false;
                    }
                }
                return ulong.TryParse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out seed);
            }
            foreach (char c in s)
            {
                if (c < '0' || c > '9')
                {
                    return false;
                }
            }
            return ulong.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out seed);
        }
    }
}

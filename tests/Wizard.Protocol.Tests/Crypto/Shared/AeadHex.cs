using System;
using System.Globalization;
using System.Text;

namespace Wizard.Protocol.Tests.Crypto.Shared
{
    // 픽스처의 16진 문자열(소문자, 구분자 없음)과 보고용 표기.
    public static class AeadHex
    {
        public static bool TryParse(string? text, out byte[] bytes)
        {
            bytes = Array.Empty<byte>();
            if (text == null || (text.Length & 1) != 0)
            {
                return false;
            }
            var b = new byte[text.Length / 2];
            for (int i = 0; i < b.Length; i++)
            {
                int hi = Nibble(text[2 * i]);
                int lo = Nibble(text[2 * i + 1]);
                if (hi < 0 || lo < 0)
                {
                    return false;
                }
                b[i] = (byte)((hi << 4) | lo);
            }
            bytes = b;
            return true;
        }

        public static string Format(ReadOnlySpan<byte> data)
        {
            var sb = new StringBuilder(data.Length * 2);
            foreach (byte x in data)
            {
                sb.Append(x.ToString("x2", CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        // 픽스처는 소문자만 쓴다(같은 내용이면 같은 바이트)
        private static int Nibble(char c)
        {
            if (c >= '0' && c <= '9')
            {
                return c - '0';
            }
            if (c >= 'a' && c <= 'f')
            {
                return c - 'a' + 10;
            }
            return -1;
        }
    }
}

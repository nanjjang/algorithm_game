using System;
using System.Globalization;

namespace Wizard.TestKit.Shared.Arith
{
    // 산술 경계 벡터의 값 하나. 64비트 패턴으로 비교한다.
    // 글자 꼴: 십진(음수 가능, long 범위 또는 양수 ulong 범위) 또는 "0x" + 16진 1~16자리(비트 그대로).
    // 결과는 십진으로 쓴다. 0xffffffff 와 -1 은 64비트 패턴이 다르므로 int 결과는 "-1" 처럼 십진으로 적는다.
    public readonly struct ArithValue : IEquatable<ArithValue>
    {
        private ArithValue(ulong bits, string text)
        {
            Bits = bits;
            Text = text;
        }

        public ulong Bits { get; }

        public string Text { get; }

        public static ArithValue FromInt64(long v) => new ArithValue(unchecked((ulong)v), v.ToString(CultureInfo.InvariantCulture));

        public static ArithValue FromUInt64(ulong v) => new ArithValue(v, v.ToString(CultureInfo.InvariantCulture));

        public static ArithValue FromInt32(int v) => FromInt64(v);

        public static bool TryParse(string? text, out ArithValue value)
        {
            value = default;
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }
            string s = text!;
            if (s.StartsWith("0x", StringComparison.Ordinal))
            {
                if (HashText.TryParseSeed(s, out ulong bits))
                {
                    value = new ArithValue(bits, s);
                    return true;
                }
                return false;
            }
            bool negative = s.StartsWith("-", StringComparison.Ordinal);
            string digits = negative ? s.Substring(1) : s;
            if (digits.Length == 0 || (digits.Length > 1 && digits[0] == '0'))
            {
                return false;
            }
            foreach (char c in digits)
            {
                if (c < '0' || c > '9')
                {
                    return false;
                }
            }
            if (negative)
            {
                if (digits == "0")
                {
                    return false;
                }
                if (long.TryParse(s, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long l))
                {
                    value = new ArithValue(unchecked((ulong)l), s);
                    return true;
                }
                return false;
            }
            if (ulong.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out ulong u))
            {
                value = new ArithValue(u, s);
                return true;
            }
            return false;
        }

        public static ArithValue Parse(string text)
        {
            if (!TryParse(text, out ArithValue v))
            {
                throw new FormatException("not an arith value: '" + text + "'");
            }
            return v;
        }

        public long ToInt64() => unchecked((long)Bits);

        public ulong ToUInt64() => Bits;

        // int 범위 밖이면 FormatException(넘겨줄 값을 잘못 적은 것이다)
        public int ToInt32()
        {
            long v = ToInt64();
            if (v < int.MinValue || v > int.MaxValue)
            {
                throw new FormatException("arith value '" + Text + "' does not fit in int");
            }
            return (int)v;
        }

        public bool Equals(ArithValue other) => Bits == other.Bits;

        public override bool Equals(object? obj) => obj is ArithValue other && Equals(other);

        public override int GetHashCode() => Bits.GetHashCode();

        public override string ToString() => Text ?? "0";
    }
}

using System;
using System.Globalization;

namespace Wizard.Core
{
    // CONTRACTS 1.1, spec 01 2.2, T-06. Q32.32 고정소수점. 값 = Raw / 2^32.
    // 덧셈·뺄셈·곱셈은 모든 빌드에서 래핑한다. 나눗셈은 0 쪽 버림이고 제수 0은 코드가 먼저 검사한다.
    public readonly struct Fix64 : IEquatable<Fix64>, IComparable<Fix64>
    {
        public readonly long Raw;

        public const int FracBits = 32;
        public const long OneRaw = 1L << 32;

        public static readonly Fix64 Zero = new Fix64(0L);
        public static readonly Fix64 One = new Fix64(OneRaw);
        public static readonly Fix64 Half = new Fix64(OneRaw >> 1);
        public static readonly Fix64 MinValue = new Fix64(long.MinValue);
        public static readonly Fix64 MaxValue = new Fix64(long.MaxValue);

        private Fix64(long raw)
        {
            Raw = raw;
        }

        public static Fix64 FromRaw(long raw) => new Fix64(raw);

        public static Fix64 FromInt(int value) => new Fix64((long)value << FracBits);

        // 가장 가까운 Raw, 같으면 짝수(CONTRACTS 0.3). denominator == 0 → SimInvariantException.
        public static Fix64 FromRatio(long numerator, long denominator)
            => new Fix64(Wide.DivRoundHalfEven(numerator >> 32, (ulong)numerator << 32, denominator));

        public static Fix64 FromPermille(int permille) => FromRatio(permille, 1000);

        // 음의 무한대 쪽
        public int FloorToInt() => (int)(Raw >> FracBits);

        public int CeilToInt() => (int)(unchecked(Raw + (OneRaw - 1L)) >> FracBits);

        // .5는 올림(양의 무한대 쪽)
        public int RoundToInt() => (int)(unchecked(Raw + (OneRaw >> 1)) >> FracBits);

        public static Fix64 operator +(Fix64 a, Fix64 b) => new Fix64(unchecked(a.Raw + b.Raw));

        public static Fix64 operator -(Fix64 a, Fix64 b) => new Fix64(unchecked(a.Raw - b.Raw));

        public static Fix64 operator -(Fix64 a) => new Fix64(unchecked(0L - a.Raw));

        // 128비트 곱 뒤 산술 오른쪽 시프트 32(내림)
        public static Fix64 operator *(Fix64 a, Fix64 b)
        {
            Wide.Mul64(a.Raw, b.Raw, out long hi, out ulong lo);
            return new Fix64(Wide.ShiftRight32(hi, lo));
        }

        // (a.Raw << 32) / b.Raw 를 128비트로, 0 쪽 버림. b == 0이면 SimInvariantException
        public static Fix64 operator /(Fix64 a, Fix64 b)
        {
            if (b.Raw == 0L)
            {
                throw SimInvariantException.DivideByZero();
            }
            return new Fix64(Wide.Div128By64(a.Raw >> 32, (ulong)a.Raw << 32, b.Raw));
        }

        public static Fix64 operator *(Fix64 a, int b) => new Fix64(unchecked(a.Raw * b));

        // 0 쪽 버림. b == 0이면 SimInvariantException
        public static Fix64 operator /(Fix64 a, int b) => new Fix64(FixMath.DivLong(a.Raw, b));

        public static bool operator <(Fix64 a, Fix64 b) => a.Raw < b.Raw;
        public static bool operator >(Fix64 a, Fix64 b) => a.Raw > b.Raw;
        public static bool operator <=(Fix64 a, Fix64 b) => a.Raw <= b.Raw;
        public static bool operator >=(Fix64 a, Fix64 b) => a.Raw >= b.Raw;
        public static bool operator ==(Fix64 a, Fix64 b) => a.Raw == b.Raw;
        public static bool operator !=(Fix64 a, Fix64 b) => a.Raw != b.Raw;

        // MinValue 의 절댓값은 래핑되어 MinValue 다.
        public static Fix64 Abs(Fix64 a) => a.Raw < 0L ? new Fix64(unchecked(0L - a.Raw)) : a;

        public static Fix64 Min(Fix64 a, Fix64 b) => a.Raw <= b.Raw ? a : b;

        public static Fix64 Max(Fix64 a, Fix64 b) => a.Raw >= b.Raw ? a : b;

        public static Fix64 Clamp(Fix64 v, Fix64 lo, Fix64 hi)
        {
            if (v.Raw < lo.Raw)
            {
                return lo;
            }
            return v.Raw > hi.Raw ? hi : v;
        }

        // FixMath.MulDiv(a.Raw, permille, 1000). 128비트 곱, 0 쪽 버림
        public static Fix64 MulPermille(Fix64 a, int permille) => new Fix64(FixMath.MulDiv(a.Raw, permille, 1000L));

        public bool Equals(Fix64 other) => Raw == other.Raw;

        public override bool Equals(object? obj) => obj is Fix64 other && other.Raw == Raw;

        public override int GetHashCode() => unchecked((int)Raw ^ (int)(Raw >> 32));

        public int CompareTo(Fix64 other) => Raw < other.Raw ? -1 : (Raw > other.Raw ? 1 : 0);

        // 디버그용. 실수로 바꾸지 않는다(실수 변환은 Wizard.ViewLogic.FixConvert 에만 있다).
        public override string ToString() => Raw.ToString(CultureInfo.InvariantCulture) + "r";
    }
}

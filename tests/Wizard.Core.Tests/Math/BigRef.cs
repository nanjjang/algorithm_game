using System.Numerics;

namespace Wizard.Core.Tests.Math
{
    // CONTRACTS 1.1·1.2 의 정의를 BigInteger 로 그대로 옮긴 기준 구현. 코어 코드를 부르지 않는다.
    // 부호 있는 나눗셈은 절댓값끼리 나눈 뒤 부호를 붙여 "0 쪽 버림"을 직접 만든다(BigInteger 의 반올림 규칙에 기대지 않는다).
    internal static class BigRef
    {
        public static readonly BigInteger Two32 = BigInteger.One << 32;
        public static readonly BigInteger Two64 = BigInteger.One << 64;

        // 아래 64비트를 부호 있는 long 으로(래핑)
        public static long Low64(BigInteger x)
        {
            BigInteger m = x % Two64;
            if (m.Sign < 0)
            {
                m += Two64;
            }
            return unchecked((long)(ulong)m);
        }

        public static ulong Low64U(BigInteger x) => unchecked((ulong)Low64(x));

        // 0 쪽 버림
        public static BigInteger TruncDiv(BigInteger n, BigInteger d)
        {
            BigInteger q = BigInteger.Abs(n) / BigInteger.Abs(d);
            return (n.Sign < 0) != (d.Sign < 0) ? -q : q;
        }

        // 음의 무한대 쪽 내림
        public static BigInteger FloorDiv(BigInteger n, BigInteger d)
        {
            BigInteger q = TruncDiv(n, d);
            if (q * d != n && ((n.Sign < 0) != (d.Sign < 0)))
            {
                q -= 1;
            }
            return q;
        }

        // 128비트 (hi, lo)의 값
        public static BigInteger From128(long hi, ulong lo) => new BigInteger(hi) * Two64 + new BigInteger(lo);

        // Fix64 곱: 128비트 곱 뒤 산술 오른쪽 시프트 32(내림), 아래 64비트
        public static long FixMul(long a, long b) => Low64(FloorDiv(new BigInteger(a) * b, Two32));

        // Fix64 나눗셈: (a × 2^32) / b 를 0 쪽 버림, 아래 64비트. b ≠ 0
        public static long FixDiv(long a, long b) => Low64(TruncDiv(new BigInteger(a) * Two32, b));

        // a × b / c, 0 쪽 버림, 아래 64비트. c ≠ 0
        public static long MulDiv(long a, long b, long c) => Low64(TruncDiv(new BigInteger(a) * b, c));

        // floor(√n), n ≥ 0
        public static BigInteger ISqrt(BigInteger n)
        {
            if (n.Sign <= 0)
            {
                return BigInteger.Zero;
            }
            BigInteger x = BigInteger.One << (int)((n.GetBitLength() + 1) / 2);
            while (true)
            {
                BigInteger y = (x + n / x) >> 1;
                if (y >= x)
                {
                    return x;
                }
                x = y;
            }
        }

        // FixMath.Sqrt: Raw = floor(√(v.Raw × 2^32)), v ≤ 0 → 0
        public static long FixSqrt(long raw) => raw <= 0 ? 0L : (long)ISqrt(new BigInteger(raw) * Two32);

        // FixVec2.Normalized: len = Sqrt(LengthSq), 성분 = (성분.Raw × 2^32) / len.Raw(0 쪽 버림), len = 0 → (0,0)
        public static void Normalize(long x, long y, out long nx, out long ny)
        {
            long lsq = Low64(new BigInteger(FixMul(x, x)) + FixMul(y, y));
            long len = FixSqrt(lsq);
            if (len == 0)
            {
                nx = 0;
                ny = 0;
                return;
            }
            nx = FixDiv(x, len);
            ny = FixDiv(y, len);
        }
    }
}

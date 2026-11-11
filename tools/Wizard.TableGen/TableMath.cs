using System.Numerics;

namespace Wizard.TableGen
{
    // 표를 만드는 계산. 정수(BigInteger)만 쓴다. 실수형과 실수 라이브러리 함수(Math.Sin 등)를 쓰지 않는다
    // (M0-BT-03 수락 2. tools/ci/gen-check.sh 가 이 파일과 Program.cs 에 실수형·Math 호출이 없는지 본다).
    public static class TableMath
    {
        public const int PrecisionBits = 256;
        public const int QuarterCenti = 9000;      // 90.00도
        public const int HalfTurnCenti = 18000;    // 180.00도 = π 라디안
        public const int RawFracBits = 32;         // Fix64 Q32.32

        // sin(k 센티도) × 2^32, k = 0..9000 (9,001칸). 가장 가까운 정수, 같으면 짝수. CONTRACTS 1.1.
        public static long[] SinRaw()
        {
            BigInteger one = BigInteger.One << PrecisionBits;
            BigInteger pi = Pi(one);
            var table = new long[QuarterCenti + 1];
            for (int k = 0; k <= QuarterCenti; k++)
            {
                BigInteger x = pi * k / HalfTurnCenti;   // 라디안, 2^256 배율
                BigInteger s = SinScaled(x, one);
                table[k] = (long)RoundDiv(s, BigInteger.One << (PrecisionBits - RawFracBits));
            }
            return table;
        }

        // (num/den)^n × 2^32, n = 0..count-1. 정확한 유리수를 반올림(같으면 짝수).
        public static long[] PowRaw(int num, int den, int count)
        {
            var table = new long[count];
            for (int n = 0; n < count; n++)
            {
                BigInteger p = BigInteger.Pow(num, n) << RawFracBits;
                BigInteger q = BigInteger.Pow(den, n);
                table[n] = (long)RoundDiv(p, q);
            }
            return table;
        }

        // π × one. 마친 공식 π = 16·atan(1/5) − 4·atan(1/239)
        public static BigInteger Pi(BigInteger one) => 16 * ArcTanInverse(5, one) - 4 * ArcTanInverse(239, one);

        // atan(1/n) × one
        private static BigInteger ArcTanInverse(int n, BigInteger one)
        {
            BigInteger n2 = (BigInteger)n * n;
            BigInteger term = one / n;
            BigInteger sum = term;
            for (int k = 1; !term.IsZero; k++)
            {
                term /= n2;
                BigInteger part = term / (2 * k + 1);
                sum += (k & 1) == 1 ? -part : part;
            }
            return sum;
        }

        // sin(x/one) × one, 테일러 급수
        private static BigInteger SinScaled(BigInteger x, BigInteger one)
        {
            BigInteger x2 = x * x / one;
            BigInteger term = x;
            BigInteger sum = x;
            for (int n = 1; !term.IsZero; n++)
            {
                term = -(term * x2 / one) / ((2 * n) * (2 * n + 1));
                sum += term;
            }
            return sum;
        }

        // v / d 를 가장 가까운 정수로, 같으면 짝수. d > 0
        public static BigInteger RoundDiv(BigInteger v, BigInteger d)
        {
            BigInteger q = BigInteger.DivRem(v, d, out BigInteger r);
            if (r.Sign < 0)
            {
                q -= 1;
                r += d;
            }
            BigInteger twice = r * 2;
            if (twice > d || (twice == d && !q.IsEven))
            {
                q += 1;
            }
            return q;
        }
    }
}

using System;
using System.Numerics;

namespace Wizard.Core.Tests.Angles
{
    // CONTRACTS 1.1, DECISIONS-TECH T-07 의 정의를 코어 코드 없이 옮긴 기준 구현.
    // sin 표: round(sin(k 센티도) × 2^32), 가장 가까운 정수(같으면 짝수). π(마친 공식)와 sin(테일러 급수)을 2^-200 정밀도의 BigInteger 로 계산한다.
    // Atan2: 계약 문장 그대로(팔분면 접기 → [0, 4500] 전부를 훑어 sin(a)·x ≤ cos(a)·y 인 가장 큰 a → a 와 a+1 의 |외적| 비교 → 펴기).
    // 코어는 이분 탐색을 쓰고 여기서는 전수 탐색을 쓴다. 실수(float, double)는 쓰지 않는다.
    internal static class TrigReference
    {
        public const int Quarter = 9000;
        public const int Octant = 4500;
        private const int ScaleBits = 200;
        private const int DropBits = ScaleBits - 32;

        // 기준 표. [k] = round_half_even(sin(k/100 도) × 2^32), k = 0..9000
        public static readonly long[] SinRaw = BuildSinTable();

        // 2^32 의 1/2 와 거리(2^-200 단위)가 가장 가까웠던 칸의 여유. 기준 계산의 오차(수천 단위)보다 훨씬 커야 동률 판정이 믿을 만하다.
        public static BigInteger MinHalfMargin { get; private set; }

        private static long[] BuildSinTable()
        {
            BigInteger one = BigInteger.One << ScaleBits;
            BigInteger pi = 16 * ArctanInv(5, one) - 4 * ArctanInv(239, one);
            var table = new long[Quarter + 1];
            BigInteger half = BigInteger.One << (DropBits - 1);
            BigInteger unit = BigInteger.One << DropBits;
            BigInteger minMargin = unit;
            for (int k = 0; k <= Quarter; k++)
            {
                BigInteger x = pi * k / 18000;
                BigInteger s = SinSeries(x, one);
                BigInteger q = s >> DropBits;
                BigInteger rem = s - (q << DropBits);
                BigInteger margin = BigInteger.Abs(rem - half);
                if (margin < minMargin)
                {
                    minMargin = margin;
                }
                if (rem > half || (rem == half && !q.IsEven))
                {
                    q += 1;
                }
                table[k] = (long)q;
            }
            MinHalfMargin = minMargin;
            return table;
        }

        // atan(1/n) × one
        private static BigInteger ArctanInv(int n, BigInteger one)
        {
            BigInteger n2 = (BigInteger)n * n;
            BigInteger power = one / n;
            BigInteger sum = power;
            int k = 1;
            while (!power.IsZero)
            {
                power /= n2;
                BigInteger term = power / (2 * k + 1);
                sum = (k & 1) == 1 ? sum - term : sum + term;
                k++;
            }
            return sum;
        }

        // sin(x / one) × one, 0 ≤ x ≤ π/2 × one
        private static BigInteger SinSeries(BigInteger x, BigInteger one)
        {
            BigInteger term = x;
            BigInteger sum = x;
            BigInteger x2 = x * x / one;
            int n = 1;
            while (!term.IsZero)
            {
                term = -(term * x2 / one) / ((2 * n) * (2 * n + 1));
                sum += term;
                n++;
            }
            return sum;
        }

        // 0..35999 의 sin Raw. 대칭만 쓴다.
        public static long Sin(int centi)
        {
            int c = ((centi % 36000) + 36000) % 36000;
            if (c <= 9000)
            {
                return SinRaw[c];
            }
            if (c <= 18000)
            {
                return SinRaw[18000 - c];
            }
            if (c <= 27000)
            {
                return -SinRaw[c - 18000];
            }
            return -SinRaw[36000 - c];
        }

        public static long Cos(int centi) => Sin(centi + 9000);

        // 계약의 Atan2. 결과 센티도 0..35999
        public static int Atan2(long y, long x)
        {
            if (x == 0L && y == 0L)
            {
                return 0;
            }
            UInt128 ax = Abs(x);
            UInt128 ay = Abs(y);
            int theta = ay <= ax ? OctantAngle(ax, ay) : Quarter - OctantAngle(ay, ax);
            int c;
            if (x >= 0L)
            {
                c = y >= 0L ? theta : 36000 - theta;
            }
            else
            {
                c = y >= 0L ? 18000 - theta : 18000 + theta;
            }
            return c % 36000;
        }

        private static UInt128 Abs(long v) => v < 0L ? (UInt128)(0UL - (ulong)v) : (UInt128)(ulong)v;

        private static int OctantAngle(UInt128 ax, UInt128 ay)
        {
            int best = -1;
            for (int a = 0; a <= Octant; a++)
            {
                if ((UInt128)(ulong)SinRaw[a] * ax <= (UInt128)(ulong)SinRaw[Quarter - a] * ay)
                {
                    best = a;
                }
            }
            if (best < 0)
            {
                throw new InvalidOperationException("a = 0 은 언제나 조건을 만족해야 한다");
            }
            if (best == Octant)
            {
                return best;
            }
            return CrossAbs(best + 1, ax, ay) < CrossAbs(best, ax, ay) ? best + 1 : best;
        }

        private static UInt128 CrossAbs(int a, UInt128 ax, UInt128 ay)
        {
            UInt128 s = (UInt128)(ulong)SinRaw[a] * ax;
            UInt128 c = (UInt128)(ulong)SinRaw[Quarter - a] * ay;
            return s >= c ? s - c : c - s;
        }
    }
}

namespace Wizard.Core
{
    // CONTRACTS 1.1. 실수 함수는 쓰지 않는다. 표(SinTable, PowTables)는 tools/Wizard.TableGen 이 만든다.
    // 나눗셈은 전 함수다: 제수 0 → SimInvariantException, MinValue ÷ −1 → MinValue(래핑), MinValue % −1 → 0.
    // 세 런타임(CoreCLR, Mono, IL2CPP)과 두 아키텍처의 동작 차이를 런타임에 맡기지 않는다.
    public static class FixMath
    {
        public const long InvSqrt2Raw = 3037000500L;   // 1/√2. 코어 전체에서 이 값 하나(CONTRACTS 0.3)

        private const int QuarterCenti = 9000;
        private const int HalfCenti = 18000;
        private const int ThreeQuarterCenti = 27000;
        private const int OctantCenti = 4500;

        // 정수 제곱근(내림). v ≤ 0이면 0. 결과 Raw = floor(sqrt(v.Raw × 2^32)).
        public static Fix64 Sqrt(Fix64 v)
        {
            if (v.Raw <= 0L)
            {
                return Fix64.Zero;
            }
            ulong nHi = (ulong)v.Raw >> 32;
            ulong nLo = (ulong)v.Raw << 32;
            // v.Raw < 2^63 이므로 v.Raw × 2^32 < 2^95, 제곱근 < 2^48.
            ulong lo = 0UL;
            ulong hi = (1UL << 48) - 1UL;
            while (lo < hi)
            {
                ulong mid = lo + ((hi - lo + 1UL) >> 1);
                Wide.UMul64(mid, mid, out ulong pHi, out ulong pLo);
                if (Wide.UCompare128(pHi, pLo, nHi, nLo) <= 0)
                {
                    lo = mid;
                }
                else
                {
                    hi = mid - 1UL;
                }
            }
            return Fix64.FromRaw((long)lo);
        }

        // 9,001칸 1/4바퀴 표 + 대칭. 보간 없음.
        public static Fix64 Sin(Angle a)
        {
            int c = a.Centi;
            if (c <= QuarterCenti)
            {
                return Fix64.FromRaw(SinTable.Raw[c]);
            }
            if (c <= HalfCenti)
            {
                return Fix64.FromRaw(SinTable.Raw[HalfCenti - c]);
            }
            if (c <= ThreeQuarterCenti)
            {
                return Fix64.FromRaw(-SinTable.Raw[c - HalfCenti]);
            }
            return Fix64.FromRaw(-SinTable.Raw[Angle.Full - c]);
        }

        public static Fix64 Cos(Angle a) => Sin(a + QuarterCenti);

        // 수학의 atan2(y가 위로 느는 좌표). (0,0)이면 0. 전장 벡터에는 Angle.OfVector 를 쓴다.
        // 팔분면으로 접은 뒤 [0, 4500]에서 sin(a)·x ≤ cos(a)·y 인 가장 큰 a 를 이분 탐색으로 찾고(128비트 정수 비교),
        // a 와 a+1 가운데 외적의 절댓값이 작은 쪽(같으면 a)을 고른 뒤 원래 팔분면으로 편다(CONTRACTS 1.1).
        public static Angle Atan2(Fix64 y, Fix64 x)
        {
            if (x.Raw == 0L && y.Raw == 0L)
            {
                return Angle.FromCenti(0);
            }
            ulong ax = x.Raw < 0L ? 0UL - (ulong)x.Raw : (ulong)x.Raw;
            ulong ay = y.Raw < 0L ? 0UL - (ulong)y.Raw : (ulong)y.Raw;
            int theta = ay <= ax ? OctantAngle(ax, ay) : QuarterCenti - OctantAngle(ay, ax);
            int c;
            if (x.Raw >= 0L)
            {
                c = y.Raw >= 0L ? theta : Angle.Full - theta;
            }
            else
            {
                c = y.Raw >= 0L ? HalfCenti - theta : HalfCenti + theta;
            }
            return Angle.FromCenti(c);
        }

        // 0 ≤ ay ≤ ax, ax > 0. 결과는 [0, 4500].
        private static int OctantAngle(ulong ax, ulong ay)
        {
            int lo = 0, hi = OctantCenti;
            while (lo < hi)
            {
                int mid = lo + ((hi - lo + 1) >> 1);
                if (SinXLeCosY(mid, ax, ay))
                {
                    lo = mid;
                }
                else
                {
                    hi = mid - 1;
                }
            }
            if (lo == OctantCenti)
            {
                return lo;
            }
            CrossAbs(lo, ax, ay, out ulong aHi, out ulong aLo);
            CrossAbs(lo + 1, ax, ay, out ulong bHi, out ulong bLo);
            return Wide.UCompare128(bHi, bLo, aHi, aLo) < 0 ? lo + 1 : lo;
        }

        private static bool SinXLeCosY(int a, ulong ax, ulong ay)
        {
            Wide.UMul64((ulong)SinTable.Raw[a], ax, out ulong sHi, out ulong sLo);
            Wide.UMul64((ulong)SinTable.Raw[QuarterCenti - a], ay, out ulong cHi, out ulong cLo);
            return Wide.UCompare128(sHi, sLo, cHi, cLo) <= 0;
        }

        // |sin(a)·x − cos(a)·y|
        private static void CrossAbs(int a, ulong ax, ulong ay, out ulong hi, out ulong lo)
        {
            Wide.UMul64((ulong)SinTable.Raw[a], ax, out ulong sHi, out ulong sLo);
            Wide.UMul64((ulong)SinTable.Raw[QuarterCenti - a], ay, out ulong cHi, out ulong cLo);
            if (Wide.UCompare128(sHi, sLo, cHi, cLo) >= 0)
            {
                Wide.USub128(sHi, sLo, cHi, cLo, out hi, out lo);
            }
            else
            {
                Wide.USub128(cHi, cLo, sHi, sLo, out hi, out lo);
            }
        }

        // a × b / c 를 128비트로, 0 쪽 버림. c == 0이면 SimInvariantException
        public static long MulDiv(long a, long b, long c)
        {
            if (c == 0L)
            {
                throw SimInvariantException.DivideByZero();
            }
            Wide.Mul64(a, b, out long hi, out ulong lo);
            return Wide.Div128By64(hi, lo, c);
        }

        // 1.15^n, 0.85^d, 0.92^d, 0.7^k — 미리 만든 표. 지수가 표 범위 밖이면 끝값으로 고정(음수 → 0번, 넘침 → 마지막)
        public static Fix64 PowTable(PowBase which, int exponent)
        {
            long[] table = PowTables.For(which);
            int i = exponent < 0 ? 0 : (exponent >= table.Length ? table.Length - 1 : exponent);
            return Fix64.FromRaw(table[i]);
        }

        // 0 쪽 버림. b == 0 → SimInvariantException. int.MinValue / −1 → int.MinValue(래핑)
        public static int DivInt(int a, int b)
        {
            if (b == 0)
            {
                throw SimInvariantException.DivideByZero();
            }
            if (b == -1)
            {
                return unchecked(0 - a);
            }
            return a / b;
        }

        public static long DivLong(long a, long b)
        {
            if (b == 0L)
            {
                throw SimInvariantException.DivideByZero();
            }
            if (b == -1L)
            {
                return unchecked(0L - a);
            }
            return a / b;
        }

        // 부호는 피제수를 따른다. b == 0 → SimInvariantException. MinValue % −1 → 0
        public static int ModInt(int a, int b)
        {
            if (b == 0)
            {
                throw SimInvariantException.DivideByZero();
            }
            if (b == -1)
            {
                return 0;
            }
            return a % b;
        }

        public static long ModLong(long a, long b)
        {
            if (b == 0L)
            {
                throw SimInvariantException.DivideByZero();
            }
            if (b == -1L)
            {
                return 0L;
            }
            return a % b;
        }

        // [int.MinValue, int.MaxValue]로 자른다(적 체력, 체력 배율 적용 피해 전용. CONTRACTS 1.1)
        public static int SaturateToInt(long v)
        {
            if (v > int.MaxValue)
            {
                return int.MaxValue;
            }
            return v < int.MinValue ? int.MinValue : (int)v;
        }
    }
}

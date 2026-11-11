namespace Wizard.Core
{
    // CONTRACTS 1.2. netstandard2.1 에는 Int128 과 Math.BigMul(long, long) 이 없어 직접 구현한다.
    // 128비트 값은 (hi, lo) 둘로 다룬다. hi 는 부호 있는 윗 64비트, lo 는 아랫 64비트다.
    // 원시 '/'·'%' 는 이 클래스와 FixMath 의 나눗셈 도우미 안에서만, 제수 검사를 마친 뒤에 쓴다(분석기가 허용).
    public static class Wide
    {
        private const ulong Low32Mask = 0xFFFF_FFFFUL;

        // 부호 있는 64×64 → 128
        public static void Mul64(long a, long b, out long hi, out ulong lo)
        {
            ulong ua = a < 0 ? 0UL - (ulong)a : (ulong)a;
            ulong ub = b < 0 ? 0UL - (ulong)b : (ulong)b;
            UMul64(ua, ub, out ulong h, out ulong l);
            if ((a < 0) != (b < 0))
            {
                Negate128(ref h, ref l);
            }
            hi = (long)h;
            lo = l;
        }

        public static void UMul64(ulong a, ulong b, out ulong hi, out ulong lo)
        {
            ulong aLo = a & Low32Mask, aHi = a >> 32;
            ulong bLo = b & Low32Mask, bHi = b >> 32;
            ulong ll = aLo * bLo;
            ulong lh = aLo * bHi;
            ulong hl = aHi * bLo;
            ulong hh = aHi * bHi;
            ulong mid = (ll >> 32) + (lh & Low32Mask) + (hl & Low32Mask);
            lo = (mid << 32) | (ll & Low32Mask);
            hi = unchecked(hh + (lh >> 32) + (hl >> 32) + (mid >> 32));
        }

        // 128비트 산술 오른쪽 시프트 32 뒤 아래 64비트
        public static long ShiftRight32(long hi, ulong lo) => (long)((lo >> 32) | ((ulong)hi << 32));

        // 0 쪽 버림. divisor == 0 → SimInvariantException. 몫이 64비트를 넘으면 아래 64비트(래핑).
        public static long Div128By64(long hi, ulong lo, long divisor)
        {
            if (divisor == 0)
            {
                throw SimInvariantException.DivideByZero();
            }
            bool negN = hi < 0;
            ulong nHi = (ulong)hi, nLo = lo;
            if (negN)
            {
                Negate128(ref nHi, ref nLo);
            }
            bool negD = divisor < 0;
            ulong d = negD ? 0UL - (ulong)divisor : (ulong)divisor;
            UDivRem(nHi, nLo, d, out ulong qHi, out ulong qLo, out _);
            if (negN != negD)
            {
                Negate128(ref qHi, ref qLo);
            }
            return (long)qLo;
        }

        public static int Compare128(long aHi, ulong aLo, long bHi, ulong bLo)
        {
            if (aHi != bHi)
            {
                return aHi < bHi ? -1 : 1;
            }
            if (aLo != bLo)
            {
                return aLo < bLo ? -1 : 1;
            }
            return 0;
        }

        // 가장 가까운 정수, 같으면 짝수(CONTRACTS 0.3 "십진 → Raw 반올림"). 몫이 64비트를 넘으면 래핑.
        internal static long DivRoundHalfEven(long hi, ulong lo, long divisor)
        {
            if (divisor == 0)
            {
                throw SimInvariantException.DivideByZero();
            }
            bool negN = hi < 0;
            ulong nHi = (ulong)hi, nLo = lo;
            if (negN)
            {
                Negate128(ref nHi, ref nLo);
            }
            bool negD = divisor < 0;
            ulong d = negD ? 0UL - (ulong)divisor : (ulong)divisor;
            UDivRem(nHi, nLo, d, out ulong qHi, out ulong qLo, out ulong rem);
            ulong rest = d - rem;   // rem < d 이므로 양수
            if (rem > rest || (rem == rest && (qLo & 1UL) == 1UL))
            {
                qLo = unchecked(qLo + 1UL);
                if (qLo == 0UL)
                {
                    qHi = unchecked(qHi + 1UL);
                }
            }
            if (negN != negD)
            {
                Negate128(ref qHi, ref qLo);
            }
            return (long)qLo;
        }

        internal static void Negate128(ref ulong hi, ref ulong lo)
        {
            lo = unchecked(~lo + 1UL);
            hi = unchecked(~hi + (lo == 0UL ? 1UL : 0UL));
        }

        internal static int UCompare128(ulong aHi, ulong aLo, ulong bHi, ulong bLo)
        {
            if (aHi != bHi)
            {
                return aHi < bHi ? -1 : 1;
            }
            if (aLo != bLo)
            {
                return aLo < bLo ? -1 : 1;
            }
            return 0;
        }

        // 부호 없는 128 - 128 (a >= b 전제)
        internal static void USub128(ulong aHi, ulong aLo, ulong bHi, ulong bLo, out ulong hi, out ulong lo)
        {
            lo = unchecked(aLo - bLo);
            ulong borrow = aLo < bLo ? 1UL : 0UL;
            hi = unchecked(aHi - bHi - borrow);
        }

        // 부호 없는 128 ÷ 64. d != 0 은 호출자가 확인했다.
        internal static void UDivRem(ulong nHi, ulong nLo, ulong d, out ulong qHi, out ulong qLo, out ulong rem)
        {
            qHi = nHi / d;
            ulong r = nHi % d;          // r < d
            if (r == 0UL)
            {
                qLo = nLo / d;
                rem = nLo % d;
                return;
            }
            // (r:nLo) ÷ d, r < d 이므로 몫은 64비트에 든다. 비트 단위 나눗셈.
            ulong q = 0UL;
            for (int i = 63; i >= 0; i--)
            {
                bool carry = (r >> 63) != 0UL;
                r = (r << 1) | ((nLo >> i) & 1UL);
                q <<= 1;
                if (carry || r >= d)
                {
                    r = unchecked(r - d);
                    q |= 1UL;
                }
            }
            qLo = q;
            rem = r;
        }
    }
}

namespace Wizard.Core
{
    // CONTRACTS 1.3, T-09. PCG32 XSH-RR(O'Neill 2014). 가변 구조체: 두 필드가 상태 해시와 스냅숏에 들어간다.
    public struct Pcg32
    {
        private const ulong Multiplier = 6364136223846793005UL;

        public ulong State;
        public ulong Inc;

        // inc = (seq << 1) | 1; state = 0; Next(); state += seed; Next();
        public Pcg32(ulong seed, ulong seq)
        {
            State = 0UL;
            Inc = (seq << 1) | 1UL;
            NextU32();
            State = unchecked(State + seed);
            NextU32();
        }

        public uint NextU32()
        {
            ulong old = State;
            State = unchecked(old * Multiplier + Inc);
            uint xorShifted = (uint)(((old >> 18) ^ old) >> 27);
            int rot = (int)(old >> 59);
            return (xorShifted >> rot) | (xorShifted << ((-rot) & 31));
        }

        // 거부법: threshold = (uint)(-exclusiveMax) % exclusiveMax. exclusiveMax < 1이면 SimInvariantException
        public int NextInt(int exclusiveMax)
        {
            if (exclusiveMax < 1)
            {
                throw new SimInvariantException("Pcg32.NextInt: exclusiveMax < 1");
            }
            return (int)NextBounded((uint)exclusiveMax);
        }

        public int NextRange(int minInclusive, int maxInclusive)
        {
            if (maxInclusive < minInclusive)
            {
                throw new SimInvariantException("Pcg32.NextRange: max < min");
            }
            long span = (long)maxInclusive - minInclusive + 1L;   // 1 .. 2^32
            if (span > uint.MaxValue)
            {
                return unchecked((int)NextU32());
            }
            return unchecked((int)(minInclusive + (long)NextBounded((uint)span)));
        }

        // NextInt(denominator) < numerator
        public bool Chance(int numerator, int denominator) => NextInt(denominator) < numerator;

        // [0,1): Raw = NextU32()
        public Fix64 NextFix01() => Fix64.FromRaw(NextU32());

        private uint NextBounded(uint bound)
        {
            uint threshold = (uint)FixMath.ModLong(unchecked(0U - bound), bound);
            while (true)
            {
                uint r = NextU32();
                if (r >= threshold)
                {
                    return (uint)FixMath.ModLong(r, bound);
                }
            }
        }
    }
}

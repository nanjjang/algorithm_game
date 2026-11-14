using System;

namespace Wizard.Solo
{
    /// <summary>난수 흐름. 판 씨앗에서 흐름별로 갈라 쓴다(서로의 소비가 다른 흐름을 흔들지 않게).</summary>
    public enum RngStream
    {
        WavePlan = 1, Spawn = 2, EnemyAi = 3, Boss = 4, Loot = 5, Drops = 6, Combat = 7, ArtUnlock = 8, Weather = 9, Starfall = 10, Misc = 11,
        Count = 12
    }

    /// <summary>PCG32(XSH-RR). 시뮬레이션의 모든 난수는 이것으로 뽑는다. 표시용 난수는 UnityEngine.Random.</summary>
    public sealed class Pcg32
    {
        ulong _state;
        readonly ulong _inc;

        public Pcg32(ulong seed, ulong sequence = 54u)
        {
            _state = 0u;
            _inc = (sequence << 1) | 1u;
            NextU32();
            _state += seed;
            NextU32();
        }

        /// <summary>판 씨앗에서 흐름을 갈라 낸다. 같은 (씨앗, 흐름, 보조)는 같은 수열.</summary>
        public static Pcg32 Derive(ulong runSeed, RngStream stream, int aux = 0)
        {
            ulong s = runSeed ^ (0x9E3779B97F4A7C15UL * (ulong)((int)stream + 1));
            s ^= (ulong)(uint)aux * 0xBF58476D1CE4E5B9UL;
            s ^= s >> 31;
            return new Pcg32(s, (ulong)stream * 2654435761UL + (ulong)(uint)aux);
        }

        public uint NextU32()
        {
            ulong old = _state;
            _state = unchecked(old * 6364136223846793005UL + _inc);
            uint xorshifted = (uint)(((old >> 18) ^ old) >> 27);
            int rot = (int)(old >> 59);
            return (xorshifted >> rot) | (xorshifted << ((-rot) & 31));
        }

        /// <summary>[min, maxExclusive) 균등 정수. 치우침 없이 뽑는다.</summary>
        public int Range(int min, int maxExclusive)
        {
            if (maxExclusive <= min) return min;
            uint bound = (uint)(maxExclusive - min);
            uint threshold = (uint)(-(int)bound) % bound;
            while (true)
            {
                uint r = NextU32();
                if (r >= threshold) return min + (int)(r % bound);
            }
        }

        /// <summary>[0, 1) 실수.</summary>
        public float Value() => (NextU32() >> 8) * (1f / 16777216f);
        /// <summary>[min, max) 실수.</summary>
        public float Range(float min, float max) => min + (max - min) * Value();
        /// <summary>만분율 확률 판정. roll = Range(0, 10000) &lt; bp.</summary>
        public bool RollBp(int bp) => Range(0, 10000) < bp;
        /// <summary>백분율 확률 판정. Range(0, 100) &lt; percent.</summary>
        public bool RollPercent(int percent) => Range(0, 100) < percent;
    }
}

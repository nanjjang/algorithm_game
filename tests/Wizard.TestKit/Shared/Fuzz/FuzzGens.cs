using System;
using System.Collections.Generic;
using Wizard.Core;

namespace Wizard.TestKit.Shared.Fuzz
{
    // 자주 쓰는 입력 생성기. 길이·범위는 포함 구간이다.
    public static class FuzzGens
    {
        public static FuzzGen<byte[]> Bytes(int minLength, int maxLength)
        {
            CheckRange(minLength, maxLength);
            return (ref Pcg32 rng) =>
            {
                int n = rng.NextRange(minLength, maxLength);
                var b = new byte[n];
                for (int i = 0; i < n; i++)
                {
                    b[i] = (byte)rng.NextInt(256);
                }
                return b;
            };
        }

        public static FuzzGen<int> Int32(int min, int max)
        {
            if (max < min)
            {
                throw new ArgumentException("max < min");
            }
            return (ref Pcg32 rng) => rng.NextRange(min, max);
        }

        // 64비트 전 범위(경계값이 자주 나오게 1/8 은 0, ±1, MinValue, MaxValue 가운데서 고른다)
        public static FuzzGen<long> Int64Any()
        {
            long[] edges = { 0L, 1L, -1L, long.MinValue, long.MaxValue, long.MinValue + 1, long.MaxValue - 1 };
            return (ref Pcg32 rng) =>
            {
                if (rng.NextInt(8) == 0)
                {
                    return edges[rng.NextInt(edges.Length)];
                }
                ulong hi = rng.NextU32();
                ulong lo = rng.NextU32();
                return unchecked((long)((hi << 32) | lo));
            };
        }

        public static FuzzGen<List<T>> ListOf<T>(FuzzGen<T> element, int minCount, int maxCount)
        {
            CheckRange(minCount, maxCount);
            return (ref Pcg32 rng) =>
            {
                int n = rng.NextRange(minCount, maxCount);
                var list = new List<T>(n);
                for (int i = 0; i < n; i++)
                {
                    list.Add(element(ref rng));
                }
                return list;
            };
        }

        private static void CheckRange(int min, int max)
        {
            if (min < 0 || max < min)
            {
                throw new ArgumentException("need 0 <= min <= max");
            }
        }
    }
}

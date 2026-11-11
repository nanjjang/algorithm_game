using System.Numerics;
using Wizard.Core;
using Xunit;

namespace Wizard.Core.Tests.Math
{
    // M1-CORE-01 수락 1: 곱셈(128비트 곱 뒤 산술 시프트, 내림)과 나눗셈(0 쪽 버림)이
    // BigInteger 기준 구현과 무작위 100만 쌍 + 경계값(최소, 최대, ±1, 0)의 모든 쌍에서 일치한다.
    // 나머지 128비트 도우미, MulDiv, Sqrt, Normalized, MulPermille 도 같은 기준과 맞춘다.
    // 난수는 씨앗을 고정한 SplitMix64 다(실행마다 같은 입력).
    public sealed class BigIntegerReferenceTests
    {
        private const int MillionPairs = 1000000;
        private const int ManyCases = 200000;

        // 최소, 최대, ±1, 0 과 그 이웃, Q32.32 의 1·½ 경계, int 경계
        internal static readonly long[] Boundaries =
        {
            long.MinValue, long.MinValue + 1, long.MinValue + 2, long.MinValue / 2,
            -(1L << 32) - 1, -(1L << 32), -(1L << 32) + 1, -(1L << 31), (long)int.MinValue - 1,
            -3, -2, -1, 0, 1, 2, 3,
            (long)int.MaxValue + 1, 1L << 31, (1L << 32) - 1, 1L << 32, (1L << 32) + 1, 3L << 31,
            long.MaxValue / 2, long.MaxValue - 2, long.MaxValue - 1, long.MaxValue,
        };

        internal struct SplitMix64
        {
            private ulong _state;

            public SplitMix64(ulong seed)
            {
                _state = seed;
            }

            public ulong Next()
            {
                ulong z = unchecked(_state += 0x9E3779B97F4A7C15UL);
                z = unchecked((z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL);
                z = unchecked((z ^ (z >> 27)) * 0x94D049BB133111EBUL);
                return z ^ (z >> 31);
            }

            // 크기가 고르게 섞인 64비트 값: 전체 비트, 무작위 비트 길이, 0 근처, 경계 근처
            public long NextMixed()
            {
                ulong r = Next();
                switch ((int)(r & 7UL))
                {
                    case 0:
                    case 1:
                    case 2:
                        return unchecked((long)Next());
                    case 3:
                    case 4:
                    case 5:
                    {
                        long v = unchecked((long)(Next() >> (int)((r >> 3) & 63UL)));
                        return ((r >> 9) & 1UL) == 0UL ? v : unchecked(-v);
                    }
                    case 6:
                        return unchecked((long)(Next() % 4001UL)) - 2000L;
                    default:
                        return unchecked(Boundaries[(int)((r >> 3) % (ulong)Boundaries.Length)] + (long)((r >> 16) % 5UL) - 2L);
                }
            }
        }

        private static void CheckMul(long a, long b)
        {
            long expected = BigRef.FixMul(a, b);
            long actual = (Fix64.FromRaw(a) * Fix64.FromRaw(b)).Raw;
            if (expected != actual)
            {
                Assert.Fail("mul " + a + " * " + b + ": expected " + expected + ", got " + actual);
            }
        }

        private static void CheckDiv(long a, long b)
        {
            if (b == 0)
            {
                Assert.Throws<SimInvariantException>(() => Fix64.FromRaw(a) / Fix64.FromRaw(b));
                return;
            }
            long expected = BigRef.FixDiv(a, b);
            long actual = (Fix64.FromRaw(a) / Fix64.FromRaw(b)).Raw;
            if (expected != actual)
            {
                Assert.Fail("div " + a + " / " + b + ": expected " + expected + ", got " + actual);
            }
        }

        [Fact]
        public void MulAndDiv_MatchBigInteger_OnAllBoundaryPairs()
        {
            foreach (long a in Boundaries)
            {
                foreach (long b in Boundaries)
                {
                    CheckMul(a, b);
                    CheckDiv(a, b);
                }
            }
        }

        [Fact]
        public void MulAndDiv_MatchBigInteger_OnOneMillionRandomPairs()
        {
            var rng = new SplitMix64(0x5EED_C0DE_0001UL);
            for (int i = 0; i < MillionPairs; i++)
            {
                long a = rng.NextMixed();
                long b = rng.NextMixed();
                CheckMul(a, b);
                CheckDiv(a, b);
            }
        }

        [Fact]
        public void IntOperators_MatchReference()
        {
            var rng = new SplitMix64(0x5EED_C0DE_0002UL);
            int[] intEdges = { int.MinValue, int.MinValue + 1, -2, -1, 0, 1, 2, int.MaxValue };
            for (int i = 0; i < ManyCases; i++)
            {
                long a = i < Boundaries.Length * intEdges.Length ? Boundaries[i / intEdges.Length] : rng.NextMixed();
                int b = i < Boundaries.Length * intEdges.Length ? intEdges[i % intEdges.Length] : unchecked((int)rng.NextMixed());
                Assert.Equal(BigRef.Low64(new BigInteger(a) * b), (Fix64.FromRaw(a) * b).Raw);
                if (b == 0)
                {
                    Assert.Throws<SimInvariantException>(() => Fix64.FromRaw(a) / b);
                }
                else
                {
                    Assert.Equal(BigRef.Low64(BigRef.TruncDiv(a, b)), (Fix64.FromRaw(a) / b).Raw);
                }
            }
        }

        [Fact]
        public void Wide_MatchesBigInteger()
        {
            var rng = new SplitMix64(0x5EED_C0DE_0003UL);
            for (int i = 0; i < ManyCases; i++)
            {
                long a = rng.NextMixed();
                long b = rng.NextMixed();
                BigInteger p = new BigInteger(a) * b;

                Wide.Mul64(a, b, out long hi, out ulong lo);
                Assert.Equal(p, BigRef.From128(hi, lo));

                ulong ua = unchecked((ulong)a), ub = unchecked((ulong)b);
                Wide.UMul64(ua, ub, out ulong uhi, out ulong ulo);
                BigInteger up = new BigInteger(ua) * ub;
                Assert.Equal(up, new BigInteger(uhi) * BigRef.Two64 + ulo);

                long nHi = rng.NextMixed();
                ulong nLo = rng.Next();
                long d = rng.NextMixed();
                BigInteger n = BigRef.From128(nHi, nLo);
                Assert.Equal(BigRef.Low64(BigRef.FloorDiv(n, BigRef.Two32)), Wide.ShiftRight32(nHi, nLo));
                if (d == 0)
                {
                    Assert.Throws<SimInvariantException>(() => Wide.Div128By64(nHi, nLo, d));
                }
                else
                {
                    Assert.Equal(BigRef.Low64(BigRef.TruncDiv(n, d)), Wide.Div128By64(nHi, nLo, d));
                }

                // 몫이 64비트에 드는 나눗셈(|hi| < |d|)도 따로 많이 본다
                if (d != 0)
                {
                    long smallHi = (long)BigInteger.Remainder(nHi, d);
                    BigInteger sn = BigRef.From128(smallHi, nLo);
                    Assert.Equal(BigRef.Low64(BigRef.TruncDiv(sn, d)), Wide.Div128By64(smallHi, nLo, d));
                }

                long bHi = (i & 1) == 0 ? nHi : rng.NextMixed();
                ulong bLo = (i & 2) == 0 ? nLo : rng.Next();
                int expectedCmp = n.CompareTo(BigRef.From128(bHi, bLo));
                Assert.Equal(expectedCmp, Wide.Compare128(nHi, nLo, bHi, bLo));
            }
        }

        [Fact]
        public void MulDiv_AndMulPermille_MatchBigInteger()
        {
            var rng = new SplitMix64(0x5EED_C0DE_0004UL);
            foreach (long a in Boundaries)
            {
                foreach (long b in Boundaries)
                {
                    foreach (long c in Boundaries)
                    {
                        CheckMulDiv(a, b, c);
                    }
                }
            }
            for (int i = 0; i < ManyCases; i++)
            {
                CheckMulDiv(rng.NextMixed(), rng.NextMixed(), rng.NextMixed());
                long a = rng.NextMixed();
                int permille = unchecked((int)rng.NextMixed());
                Assert.Equal(BigRef.MulDiv(a, permille, 1000), Fix64.MulPermille(Fix64.FromRaw(a), permille).Raw);
            }
        }

        private static void CheckMulDiv(long a, long b, long c)
        {
            if (c == 0)
            {
                Assert.Throws<SimInvariantException>(() => FixMath.MulDiv(a, b, c));
                return;
            }
            long expected = BigRef.MulDiv(a, b, c);
            long actual = FixMath.MulDiv(a, b, c);
            if (expected != actual)
            {
                Assert.Fail("muldiv " + a + " * " + b + " / " + c + ": expected " + expected + ", got " + actual);
            }
        }

        [Fact]
        public void Sqrt_MatchesBigInteger()
        {
            var rng = new SplitMix64(0x5EED_C0DE_0005UL);
            foreach (long v in Boundaries)
            {
                Assert.Equal(BigRef.FixSqrt(v), FixMath.Sqrt(Fix64.FromRaw(v)).Raw);
            }
            for (int i = 0; i < ManyCases; i++)
            {
                long v = rng.NextMixed();
                Assert.Equal(BigRef.FixSqrt(v), FixMath.Sqrt(Fix64.FromRaw(v)).Raw);
            }
        }

        // M1-CORE-01 수락 6: Normalized 의 성분 = X.Raw × 2^32 / len.Raw(128비트, 0 쪽 버림)
        [Fact]
        public void Normalized_MatchesBigInteger()
        {
            var rng = new SplitMix64(0x5EED_C0DE_0006UL);
            for (int i = 0; i < ManyCases; i++)
            {
                long x, y;
                if ((i & 1) == 0)
                {
                    // 전장 크기(정수부 ±2^12 이내)
                    x = unchecked((long)rng.Next()) >> 19;
                    y = unchecked((long)rng.Next()) >> 19;
                }
                else
                {
                    x = rng.NextMixed();
                    y = rng.NextMixed();
                }
                BigRef.Normalize(x, y, out long ex, out long ey);
                FixVec2 n = new FixVec2(Fix64.FromRaw(x), Fix64.FromRaw(y)).Normalized();
                Assert.Equal(ex, n.X.Raw);
                Assert.Equal(ey, n.Y.Raw);
            }
        }

        [Fact]
        public void Normalized_SpecificValues()
        {
            Assert.Equal(FixVec2.Zero, FixVec2.Zero.Normalized());
            Assert.Equal(FixVec2.Zero, new FixVec2(Fix64.FromRaw(1), Fix64.Zero).Normalized());   // LengthSq 가 0으로 내림
            FixVec2 n = new FixVec2(Fix64.FromInt(3), Fix64.FromInt(4)).Normalized();
            Assert.Equal(2576980377L, n.X.Raw);    // 0.6 × 2^32 = 2,576,980,377.6 → 0 쪽 버림
            Assert.Equal(3435973836L, n.Y.Raw);    // 0.8 × 2^32 = 3,435,973,836.8
            FixVec2 m = new FixVec2(Fix64.FromInt(-3), Fix64.FromInt(-4)).Normalized();
            Assert.Equal(-2576980377L, m.X.Raw);
            Assert.Equal(-3435973836L, m.Y.Raw);
        }
    }
}

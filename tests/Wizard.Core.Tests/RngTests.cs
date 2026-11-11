using System;
using Wizard.Core;
using Xunit;

namespace Wizard.Core.Tests
{
    // CONTRACTS 1.3, T-09
    public sealed class RngTests
    {
        // PCG32 참조 구현(pcg32-demo, pcg32_srandom_r(42u, 54u))의 첫 출력 여섯 개
        [Fact]
        public void Pcg32_MatchesReferenceVector()
        {
            var rng = new Pcg32(42UL, 54UL);
            uint[] expected = { 0xa15c02b7u, 0x7b47f409u, 0xba1d3330u, 0x83d2f293u, 0xbfa4784bu, 0xcbed606eu };
            foreach (uint e in expected)
            {
                Assert.Equal(e, rng.NextU32());
            }
        }

        [Fact]
        public void Pcg32_IsAValueType_CopyForksTheStream()
        {
            var a = new Pcg32(7, 1);
            Pcg32 b = a;
            Assert.Equal(a.NextU32(), b.NextU32());
            Assert.Equal(a.State, b.State);
            Assert.Equal(a.Inc, b.Inc);
        }

        [Fact]
        public void NextInt_StaysInRangeAndRejectsBadBound()
        {
            var rng = new Pcg32(1, 2);
            var seen = new bool[7];
            for (int i = 0; i < 10000; i++)
            {
                int v = rng.NextInt(7);
                Assert.InRange(v, 0, 6);
                seen[v] = true;
            }
            Assert.All(seen, Assert.True);
            Assert.Throws<SimInvariantException>(() => new Pcg32(1, 2).NextInt(0));
            Assert.Throws<SimInvariantException>(() => new Pcg32(1, 2).NextInt(-5));
            Assert.Equal(0, new Pcg32(9, 9).NextInt(1));
        }

        [Fact]
        public void NextRange_IsInclusive()
        {
            var rng = new Pcg32(3, 4);
            bool sawMin = false, sawMax = false;
            for (int i = 0; i < 5000; i++)
            {
                int v = rng.NextRange(-2, 2);
                Assert.InRange(v, -2, 2);
                sawMin |= v == -2;
                sawMax |= v == 2;
            }
            Assert.True(sawMin && sawMax);
            Assert.Equal(5, new Pcg32(1, 1).NextRange(5, 5));
            Assert.Throws<SimInvariantException>(() => new Pcg32(1, 1).NextRange(3, 2));
            int full = new Pcg32(1, 1).NextRange(int.MinValue, int.MaxValue);   // 2^32 폭
            Assert.InRange(full, int.MinValue, int.MaxValue);
        }

        [Fact]
        public void ChanceAndFix01()
        {
            var rng = new Pcg32(5, 6);
            Assert.False(new Pcg32(5, 6).Chance(0, 10));
            Assert.True(new Pcg32(5, 6).Chance(10, 10));
            Fix64 f = rng.NextFix01();
            Assert.InRange(f.Raw, 0L, (1L << 32) - 1);
        }

        [Fact]
        public void SplitMix64_KnownValue()
        {
            Assert.Equal(0xE220A8397B1DCDAFUL, RngStreams.SplitMix64(0UL));
        }

        [Fact]
        public void Derive_IsDeterministicAndSeparatesStreams()
        {
            Pcg32 a = RngStreams.Derive(123456789UL, RngStream.WaveSpawn, 7);
            Pcg32 b = RngStreams.Derive(123456789UL, RngStream.WaveSpawn, 7);
            for (int i = 0; i < 16; i++)
            {
                Assert.Equal(a.NextU32(), b.NextU32());
            }
            Assert.NotEqual(Seq(RngStreams.Derive(1, RngStream.WaveSpawn, 7)), Seq(RngStreams.Derive(1, RngStream.EnemyAi, 7)));
            Assert.NotEqual(Seq(RngStreams.Derive(1, RngStream.WaveSpawn, 7)), Seq(RngStreams.Derive(1, RngStream.WaveSpawn, 8)));
            Assert.NotEqual(Seq(RngStreams.Derive(1, RngStream.Loot, 7, 0)), Seq(RngStreams.Derive(1, RngStream.Loot, 7, 0xFFFF)));
        }

        // 계약의 식: Pcg32(SplitMix64(seed ^ (stream << 48) ^ ((uint)wave << 16) ^ (ushort)sub), seq: stream)
        [Fact]
        public void Derive_FollowsContractFormula()
        {
            ulong seed = 0xDEADBEEFCAFEBABEUL;
            Pcg32 expected = new Pcg32(RngStreams.SplitMix64(seed ^ (4UL << 48) ^ (3UL << 16) ^ 2UL), 4UL);
            Pcg32 actual = RngStreams.Derive(seed, RngStream.Loot, 3, 2);
            Assert.Equal(expected.State, actual.State);
            Assert.Equal(expected.Inc, actual.Inc);
        }

        private static string Seq(Pcg32 rng)
        {
            var parts = new uint[4];
            for (int i = 0; i < parts.Length; i++)
            {
                parts[i] = rng.NextU32();
            }
            return string.Join(",", parts);
        }
    }
}

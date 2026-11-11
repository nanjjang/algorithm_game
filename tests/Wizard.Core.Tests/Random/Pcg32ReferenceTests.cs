using Wizard.Core;
using Xunit;

namespace Wizard.Core.Tests.Random
{
    // spec 01 3.3, 14.1. CONTRACTS 1.3. PCG32 XSH-RR가 공개 기준 구현(pcg-c-basic의 pcg32-demo)과 같은가.
    public sealed class Pcg32ReferenceTests
    {
        [Fact]
        public void S01_T_C09__Seed42Seq54FirstSixOutputsMatchReference()
        {
            var rng = new Pcg32(42UL, 54UL);
            uint[] expected = { 0xa15c02b7u, 0x7b47f409u, 0xba1d3330u, 0x83d2f293u, 0xbfa4784bu, 0xcbed606eu };
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.Equal(expected[i], rng.NextU32());
            }
        }

        // pcg32-demo 1회차는 위 여섯 출력 뒤에 같은 생성기로 동전 65번(pcg32_boundedrand_r(2), 1이면 H)과
        // 주사위 33번(pcg32_boundedrand_r(6) + 1)을 찍는다. pcg32_boundedrand_r는 threshold = -bound % bound,
        // r >= threshold일 때 r % bound인 거부법이라 NextInt와 같은 방식이다. 출력 문자열은 데모의 공개 출력이다.
        [Fact]
        public void Pcg32Demo_CoinsAndRollsMatchBoundedRandReference()
        {
            var rng = new Pcg32(42UL, 54UL);
            for (int i = 0; i < 6; i++)
            {
                rng.NextU32();
            }

            const string coins = "HHTTTHTHHHTHTTTHHHHHTTTHHHTHTHTHTTHTTTHHHHHHTTTTHHTTTTTHTTTTTTTHT";
            for (int i = 0; i < coins.Length; i++)
            {
                char got = rng.NextInt(2) == 1 ? 'H' : 'T';
                Assert.Equal(coins[i], got);
            }

            int[] rolls = { 3, 4, 1, 1, 2, 2, 3, 2, 4, 3, 2, 4, 3, 3, 5, 2, 3, 1, 3, 1, 5, 1, 4, 1, 5, 6, 4, 6, 6, 2, 6, 3, 3 };
            for (int i = 0; i < rolls.Length; i++)
            {
                Assert.Equal(rolls[i], rng.NextInt(6) + 1);
            }
        }

        // 구현과 따로 spec 01 3.3절의 글을 옮긴 참조 모형과, 여러 씨앗·흐름에서 출력과 상태가 같다.
        [Fact]
        public void MatchesSpecTextReferenceModelAcrossSeeds()
        {
            ulong[] seeds = { 0UL, 1UL, 42UL, 0xFFFFFFFFFFFFFFFFUL, 0x8000000000000000UL, 0x0123456789ABCDEFUL };
            ulong[] seqs = { 0UL, 1UL, 54UL, 12UL, 0x7FFFFFFFFFFFFFFFUL, 0xFFFFFFFFFFFFFFFFUL };
            foreach (ulong seed in seeds)
            {
                foreach (ulong seq in seqs)
                {
                    RngTestSupport.ReferenceSeed(seed, seq, out ulong refState, out ulong refInc);
                    var rng = new Pcg32(seed, seq);
                    Assert.Equal(refInc, rng.Inc);
                    Assert.Equal(refState, rng.State);
                    for (int i = 0; i < 64; i++)
                    {
                        Assert.Equal(RngTestSupport.ReferenceNext(ref refState, refInc), rng.NextU32());
                        Assert.Equal(refState, rng.State);
                    }
                }
            }
        }

        // seq의 맨 위 비트는 inc에서 떨어진다((seq << 1) | 1). 기준 구현과 같은 성질이다.
        [Fact]
        public void SeqTopBitIsDroppedLikeReference()
        {
            var a = new Pcg32(9UL, 5UL);
            var b = new Pcg32(9UL, 5UL | 0x8000000000000000UL);
            Assert.Equal(a.Inc, b.Inc);
            Assert.Equal(a.State, b.State);
        }

        [Fact]
        public void StateForOutputHelperProducesRequestedOutput()
        {
            uint[] outputs = { 0u, 1u, 2u, 0x3FFFFFFCu, 0x3FFFFFFDu, 0x7FFFFFFEu, 0x7FFFFFFFu, 0x80000000u, 0xFFFFFFFFu, 0xa15c02b7u };
            foreach (uint o in outputs)
            {
                for (int rot = 0; rot < 32; rot++)
                {
                    var rng = RngTestSupport.WithNextOutput(o, 1UL, rot);
                    Assert.Equal(o, rng.NextU32());
                }
            }
        }
    }
}

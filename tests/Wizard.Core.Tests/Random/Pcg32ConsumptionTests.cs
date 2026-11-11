using System;
using Wizard.Core;
using Xunit;

namespace Wizard.Core.Tests.Random
{
    // spec 01 3.3 "NextInt(n): 편향 없는 거부 방식 하나로 고정", CONTRACTS 1.3.
    //
    // 소비 횟수(호출 하나가 NextU32를 몇 번 부르는가). 흐름의 소비는 세계 상태이므로 이 표가 바뀌면 재실행이 어긋난다.
    //
    // | 호출                         | 소비 | 값 |
    // |------------------------------|------|----|
    // | NextU32()                    | 1    | r |
    // | NextInt(n), n >= 1           | 1 + k | t = (2^32 - n) mod n. r < t 이면 버리고 다시 뽑는다(k = 버린 수). 받은 r의 r mod n |
    // | NextInt(n), n < 1            | 0    | SimInvariantException(상태를 바꾸지 않는다) |
    // | NextRange(min, max), 폭 s = max - min + 1 < 2^32 | NextInt와 같은 규칙(경계 s, uint) | min + (r mod s) |
    // | NextRange(int.MinValue, int.MaxValue), s = 2^32 | 1 | unchecked((int)r) |
    // | NextRange(min, max), max < min | 0  | SimInvariantException |
    // | Chance(num, den)             | NextInt(den)와 같다. num이 0 이하나 den 이상이어도 줄여 넘기지 않는다 | NextInt(den) < num |
    // | NextFix01()                  | 1    | Raw = r, [0, 1) |
    //
    // n이 1이거나 2의 거듭제곱이면 t = 0이라 언제나 1번이다. 버림 확률은 t / 2^32이고 int 경계에서 가장 큰 것은 n = 2^30 + 1(약 1/4),
    // NextRange의 uint 경계에서는 s = 2^31 + 1(약 1/2)이다. 받는 r은 [t, 2^32)이고 그 길이 2^32 - t가 n의 배수라 나머지 편향이 없다.
    public sealed class Pcg32ConsumptionTests
    {
        [Fact]
        public void NextU32_NextFix01_ConsumeExactlyOne()
        {
            var start = new Pcg32(42UL, 54UL);

            Pcg32 a = start;
            a.NextU32();
            Assert.Equal(1, RngTestSupport.StepsBetween(start, a));

            Pcg32 b = start;
            Fix64 f = b.NextFix01();
            Assert.Equal(1, RngTestSupport.StepsBetween(start, b));
            Assert.Equal((long)0xa15c02b7u, f.Raw);

            Fix64 top = RngTestSupport.WithNextOutput(0xFFFFFFFFu).NextFix01();
            Assert.Equal(4294967295L, top.Raw);
            Assert.True(top.Raw < Fix64.One.Raw);
            Assert.Equal(0L, RngTestSupport.WithNextOutput(0u).NextFix01().Raw);
        }

        // 기준 수열(42, 54) 위의 고정값: 열한째 0x32db86fe와 열두째 0x1dc035f9가 t = 0x3FFFFFFD보다 작아 버려지고
        // 열셋째 0xed786826을 받는다. 3,984,091,174 mod 1,073,741,825 = 762,865,699. 다음 출력은 열넷째 0x3822441d.
        [Fact]
        public void NextInt_RejectsBelowThreshold_OnReferenceSequence()
        {
            var rng = new Pcg32(42UL, 54UL);
            for (int i = 0; i < 10; i++)
            {
                rng.NextU32();
            }
            Pcg32 before = rng;
            Assert.Equal(762865699, rng.NextInt(1073741825));
            Assert.Equal(3, RngTestSupport.StepsBetween(before, rng));
            Assert.Equal(0x3822441du, rng.NextU32());
        }

        // 기준 수열의 둘째 0x7b47f409(2,068,314,121)는 s = 2^31 + 1의 t = 2^31 - 1보다 작아 버려지고 셋째 0xba1d3330을 받는다.
        // 3,122,475,824 - 2,147,483,649 = 974,992,175, 값 = int.MinValue + 974,992,175. 다음 출력은 넷째 0x83d2f293.
        [Fact]
        public void NextRange_RejectsBelowThreshold_OnReferenceSequence()
        {
            var rng = new Pcg32(42UL, 54UL);
            rng.NextU32();
            Pcg32 before = rng;
            Assert.Equal(int.MinValue + 974992175, rng.NextRange(int.MinValue, 0));
            Assert.Equal(2, RngTestSupport.StepsBetween(before, rng));
            Assert.Equal(0x83d2f293u, rng.NextU32());
        }

        // 경계 바로 아래 r = t - 1은 버리고, r = t는 받는다. 출력을 정확히 지정한 상태로 확인한다.
        [Theory]
        [InlineData(3)]
        [InlineData(5)]
        [InlineData(6)]
        [InlineData(7)]
        [InlineData(10)]
        [InlineData(1000)]
        [InlineData(10000)]
        [InlineData(1073741825)]   // 2^30 + 1, t = 0x3FFFFFFD
        [InlineData(1431655766)]
        [InlineData(int.MaxValue)] // t = 2
        public void NextInt_ThresholdBoundary(int n)
        {
            uint bound = (uint)n;
            uint t = (uint)(((1UL << 32) - bound) % bound);
            Assert.True(t > 0u);
            Assert.Equal(0UL, ((1UL << 32) - t) % bound);   // 받는 구간의 길이가 n의 배수

            // r = t: 한 번에 받는다
            Pcg32 accept = RngTestSupport.WithNextOutput(t);
            Pcg32 acceptStart = accept;
            Assert.Equal((int)(t % bound), accept.NextInt(n));
            Assert.Equal(1, RngTestSupport.StepsBetween(acceptStart, accept));

            // r = 0xFFFFFFFF: 받는다
            Pcg32 top = RngTestSupport.WithNextOutput(0xFFFFFFFFu);
            Assert.Equal((int)(0xFFFFFFFFu % bound), top.NextInt(n));

            // r = t - 1: 버리고 다음 출력부터 다시 본다
            Pcg32 reject = RngTestSupport.WithNextOutput(t - 1u);
            Pcg32 model = reject;
            Pcg32 rejectStart = reject;
            int got = reject.NextInt(n);
            int consumed = 0;
            uint r;
            do
            {
                r = model.NextU32();
                consumed++;
            }
            while (r < t);
            Assert.True(consumed >= 2);
            Assert.Equal((int)(r % bound), got);
            Assert.Equal(consumed, RngTestSupport.StepsBetween(rejectStart, reject));
        }

        // n이 1이거나 2의 거듭제곱이면 t = 0이라 r = 0도 받는다(언제나 1번 소비).
        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(4)]
        [InlineData(1024)]
        [InlineData(1 << 30)]
        public void NextInt_PowerOfTwo_NeverRejects(int n)
        {
            Pcg32 rng = RngTestSupport.WithNextOutput(0u);
            Pcg32 start = rng;
            Assert.Equal(0, rng.NextInt(n));
            Assert.Equal(1, RngTestSupport.StepsBetween(start, rng));

            Pcg32 top = RngTestSupport.WithNextOutput(0xFFFFFFFFu);
            Assert.Equal(n - 1, top.NextInt(n));
        }

        [Fact]
        public void NextInt_ManyDraws_ConsumptionEqualsModel()
        {
            int[] bounds = { 1, 2, 3, 6, 7, 100, 1000, 10000, 1073741825, int.MaxValue };
            var rng = new Pcg32(0x0123456789ABCDEFUL, 7UL);
            Pcg32 model = rng;
            for (int i = 0; i < 4000; i++)
            {
                int n = bounds[i % bounds.Length];
                uint t = (uint)(((1UL << 32) - (uint)n) % (uint)n);
                uint r;
                do
                {
                    r = model.NextU32();
                }
                while (r < t);
                Assert.Equal((int)(r % (uint)n), rng.NextInt(n));
                Assert.Equal(model.State, rng.State);
            }
        }

        [Fact]
        public void NextRange_ConsumptionAndValueEqualModel()
        {
            var rng = new Pcg32(0xDEADBEEFUL, 3UL);
            Pcg32 model = rng;
            int[][] ranges =
            {
                new[] { -2, 2 }, new[] { 5, 5 }, new[] { 0, 0 }, new[] { -1000, 1000 },
                new[] { int.MinValue, 0 }, new[] { int.MinValue, int.MinValue }, new[] { int.MaxValue - 3, int.MaxValue },
                new[] { int.MinValue + 1, int.MaxValue },
            };
            for (int i = 0; i < 2000; i++)
            {
                int[] range = ranges[i % ranges.Length];
                long span = (long)range[1] - range[0] + 1L;
                uint bound = (uint)span;
                uint t = (uint)(((1UL << 32) - bound) % bound);
                uint r;
                do
                {
                    r = model.NextU32();
                }
                while (r < t);
                int expected = unchecked((int)(range[0] + (long)(r % bound)));
                int got = rng.NextRange(range[0], range[1]);
                Assert.Equal(expected, got);
                Assert.InRange(got, range[0], range[1]);
                Assert.Equal(model.State, rng.State);
            }
        }

        // 폭 2^32는 거부 없이 한 번이다.
        [Fact]
        public void NextRange_FullSpan_ConsumesOne()
        {
            Pcg32 rng = RngTestSupport.WithNextOutput(0u);
            Pcg32 start = rng;
            rng.NextRange(int.MinValue, int.MaxValue);
            Assert.Equal(1, RngTestSupport.StepsBetween(start, rng));

            var seq = new Pcg32(42UL, 54UL);
            Pcg32 seqStart = seq;
            for (int i = 0; i < 100; i++)
            {
                seq.NextRange(int.MinValue, int.MaxValue);
            }
            Assert.Equal(100, RngTestSupport.StepsBetween(seqStart, seq, 128));
        }

        // Chance는 확률이 0이나 1이어도 NextInt(den)만큼 소비한다. 줄여 넘기면 그 뒤의 흐름이 판정 결과에 따라 달라진다.
        [Fact]
        public void Chance_AlwaysConsumesLikeNextInt()
        {
            var start = new Pcg32(5UL, 6UL);

            Pcg32 never = start;
            Assert.False(never.Chance(0, 10));
            Assert.Equal(1, RngTestSupport.StepsBetween(start, never));

            Pcg32 always = start;
            Assert.True(always.Chance(10, 10));
            Assert.Equal(1, RngTestSupport.StepsBetween(start, always));

            Pcg32 a = start;
            Pcg32 b = start;
            for (int i = 0; i < 2000; i++)
            {
                int num = (i % 1003) - 1;
                bool chance = a.Chance(num, 1000);
                bool reference = b.NextInt(1000) < num;
                Assert.Equal(reference, chance);
                Assert.Equal(b.State, a.State);
            }
        }

        [Fact]
        public void BadArguments_ThrowWithoutConsuming()
        {
            var start = new Pcg32(1UL, 2UL);

            Pcg32 zero = start;
            Assert.Throws<SimInvariantException>(() => zero.NextInt(0));
            Pcg32 neg = start;
            Assert.Throws<SimInvariantException>(() => neg.NextInt(-5));
            Pcg32 min = start;
            Assert.Throws<SimInvariantException>(() => min.NextInt(int.MinValue));
            Pcg32 range = start;
            Assert.Throws<SimInvariantException>(() => range.NextRange(3, 2));
            Pcg32 chance = start;
            Assert.Throws<SimInvariantException>(() => chance.Chance(1, 0));

            // 람다는 사본을 잡으므로 상태 확인은 직접 부른 뒤 잡는다
            Pcg32 direct = start;
            try
            {
                direct.NextInt(0);
            }
            catch (SimInvariantException)
            {
            }
            Assert.Equal(start.State, direct.State);
            Assert.Equal(start.Inc, direct.Inc);

            Pcg32 directRange = start;
            try
            {
                directRange.NextRange(int.MaxValue, int.MinValue);
            }
            catch (SimInvariantException)
            {
            }
            Assert.Equal(start.State, directRange.State);
        }

        // 받는 값이 경계마다 고르게 나오는지 대략 본다(정확한 무편향은 위의 경계 시험과 받는 구간 길이로 본다).
        [Fact]
        public void NextInt_CoversRangeEvenly()
        {
            var rng = new Pcg32(11UL, 13UL);
            var counts = new int[7];
            const int draws = 70000;
            for (int i = 0; i < draws; i++)
            {
                counts[rng.NextInt(7)]++;
            }
            foreach (int c in counts)
            {
                Assert.InRange(c, 9400, 10600);
            }
        }

        [Fact]
        public void Draws_DoNotAllocate()
        {
            var rng = new Pcg32(42UL, 54UL);
            long sink = 0;
            for (int i = 0; i < 1000; i++)
            {
                sink += rng.NextInt(7) + rng.NextRange(-3, 3) + (rng.Chance(1, 3) ? 1 : 0) + rng.NextFix01().Raw + rng.NextU32();
                sink += RngStreams.Derive(1UL, RngStream.EnemyAi, i, i).NextU32();
            }

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 100000; i++)
            {
                sink += rng.NextInt(7) + rng.NextRange(-3, 3) + (rng.Chance(1, 3) ? 1 : 0) + rng.NextFix01().Raw + rng.NextU32();
                sink += RngStreams.Derive(1UL, RngStream.EnemyAi, i, i).NextU32();
            }
            long after = GC.GetAllocatedBytesForCurrentThread();
            Assert.Equal(0L, after - before);
            Assert.NotEqual(0L, sink);
        }
    }
}

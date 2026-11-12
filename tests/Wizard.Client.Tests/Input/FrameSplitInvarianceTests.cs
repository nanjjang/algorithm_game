using System;
using System.Collections.Generic;
using System.Linq;
using Wizard.Client;
using Wizard.Core;
using Wizard.Sim.Input;
using Xunit;

namespace Wizard.Client.Tests.Input
{
    // 수락 조건: 같은 물리 이벤트 열을 프레임 60/120/144/불규칙으로 나눠 넣어도 같은 InputEvent 열이 나온다.
    // 경로는 ARCHITECTURE 6.1·CONTRACTS 6.5의 무한 웨이브 경로 그대로다:
    //   프레임마다 Drain → Quantize → TickInputQueue.Enqueue, 틱마다 TakeForTick → (InputRecorder 몫) 순번 → InputEvent.
    // 순번 매기기는 이 테스트가 SeqCounter로 흉내 낸다(InputRecorder는 M1-SIM-07). 입력 폴더에는 순번 코드가 없다(다른 테스트가 확인).
    // 기대값은 구현과 따로 정수로 셈한다: tickSeconds = 1/60이면 1/256틱 단위 = floor(µs × 15,360 / 1,000,000).
    public sealed class FrameSplitInvarianceTests
    {
        private const long UsPerSecond = 1_000_000;
        private const long TicksPerSecond = 60;
        private const long UnitsPerSecond = TicksPerSecond * InputQuantizer.OffsetsPerTick;   // 15,360
        private const long EpochUs = 2_000_000_123;                                             // 0이 아닌 epoch

        private readonly struct PhysEvent
        {
            public readonly long Us;          // epoch 기준 마이크로초
            public readonly LogicalKey Key;
            public readonly bool Down;

            public PhysEvent(long us, LogicalKey key, bool down)
            {
                Us = us;
                Key = key;
                Down = down;
            }
        }

        public static TheoryData<ulong, int> Cases => new TheoryData<ulong, int>
        {
            { 1UL, 0 }, { 2UL, 0 }, { 3UL, 3 }, { 4UL, 2 }, { 5UL, 4 },
        };

        [Theory]
        [MemberData(nameof(Cases))]
        public void SameInputEventsForEveryFrameSplit(ulong seed, int delay)
        {
            PhysEvent[] events = Generate(seed, 3000);
            InputEvent[] expected = Reference(events, delay, out int expectedOverflow);
            Assert.True(expectedOverflow > 0, "the generated stream must exercise the 9th-key drop");
            Assert.True(expected.Length > 2000);

            long endUs = events[events.Length - 1].Us + UsPerSecond;
            var schedules = new Dictionary<string, long[]>
            {
                ["60Hz"] = Regular(UsPerSecond / 60.0, 3_210, endUs),
                ["120Hz"] = Regular(UsPerSecond / 120.0, 777, endUs),
                ["144Hz"] = Regular(UsPerSecond / 144.0, 5_001, endUs),
                ["irregular"] = Irregular(seed, endUs),
                ["one-frame"] = new[] { endUs },
            };
            foreach (KeyValuePair<string, long[]> s in schedules)
            {
                InputEvent[] got = Pipeline(events, s.Value, delay, out int overflow, out int late);
                Assert.True(expected.Length == got.Length, $"{s.Key}: count {got.Length} != {expected.Length}");
                for (int i = 0; i < expected.Length; i++)
                {
                    Assert.True(Same(expected[i], got[i]), $"{s.Key}: event {i} differs");
                }
                Assert.Equal(expectedOverflow, overflow);
                Assert.Equal(0, late);
            }
        }

        // 물리 이벤트 열: 같은 순간의 동시 누름, 1/256틱 안의 연타, 한 틱에 9개 넘는 몰림, 긴 간격이 섞인다
        private static PhysEvent[] Generate(ulong seed, int count)
        {
            var rng = new Pcg32(seed, 54UL);
            var list = new List<PhysEvent>(count);
            int state = 0;
            long us = 1_000;
            for (int i = 0; i < count; i++)
            {
                int r = rng.NextInt(100);
                if (r < 15)
                {
                    // 같은 순간(gap 0)
                }
                else if (r < 45)
                {
                    us += rng.NextRange(1, 400);
                }
                else
                {
                    us += rng.NextRange(1_000, 60_000);
                }
                if ((us * UnitsPerSecond) % UsPerSecond == 0)
                {
                    us++;     // 1/256틱 경계에 정확히 놓인 시각은 피한다(기대값을 정수로 셈할 때 실수 반올림과 다툴 수 있다)
                }
                var key = (LogicalKey)rng.NextInt(KeyState.KeyCount);
                bool down = (state & (1 << (int)key)) == 0;
                state ^= 1 << (int)key;
                list.Add(new PhysEvent(us, key, down));
            }
            return list.ToArray();
        }

        private static InputEvent[] Reference(PhysEvent[] events, int delay, out int overflow)
        {
            var rows = events.Select((e, i) =>
            {
                long units = e.Us * UnitsPerSecond / UsPerSecond;
                return new { Tick = (uint)(units / InputQuantizer.OffsetsPerTick + delay), Offset = (byte)(units % InputQuantizer.OffsetsPerTick), e.Key, e.Down, Arrival = i };
            }).ToArray();
            var result = new List<InputEvent>();
            var seq = new SeqCounter();
            overflow = 0;
            foreach (var tick in rows.GroupBy(r => r.Tick).OrderBy(g => g.Key))
            {
                var sorted = tick.OrderBy(r => r.Offset).ThenBy(r => InputOrder.KeyClass(r.Key)).ThenBy(r => r.Arrival).ToArray();
                overflow += Math.Max(0, sorted.Length - InputOrder.MaxEventsPerTickPerPlayer);
                foreach (var r in sorted.Take(InputOrder.MaxEventsPerTickPerPlayer))
                {
                    result.Add(new InputEvent(r.Tick, r.Offset, r.Key, r.Down, seq.Next()));
                }
            }
            return result.ToArray();
        }

        private static InputEvent[] Pipeline(PhysEvent[] events, long[] frames, int delay, out int overflow, out int late)
        {
            var clock = new InputClock(EpochUs / (double)UsPerSecond, 1.0 / TicksPerSecond);
            var quantizer = new InputQuantizer();
            var queue = new TickInputQueue();
            var raw = new RawKeyEvent[events.Length];
            var quantized = new QuantizedKey[events.Length];
            var taken = new QuantizedKey[InputOrder.MaxEventsPerTickPerPlayer];
            var result = new List<InputEvent>();
            var seq = new SeqCounter();
            int next = 0;
            uint tick = 0;
            foreach (long frameUs in frames)
            {
                // Drain: 이 프레임까지 들어온 이벤트(하드웨어 시각 < 프레임 시각)
                int n = 0;
                while (next < events.Length && events[next].Us < frameUs)
                {
                    PhysEvent e = events[next++];
                    raw[n++] = new RawKeyEvent(e.Key, e.Down, (EpochUs + e.Us) / (double)UsPerSecond);
                }
                int q = quantizer.Quantize(raw.AsSpan(0, n), clock, delay, quantized);
                queue.Enqueue(quantized.AsSpan(0, q));

                // 유효 틱 t는 하드웨어 틱 t − D가 끝난 뒤에 돈다(그 틱의 키가 다 들어온 뒤)
                while ((tick - (long)delay + 1) * UsPerSecond <= frameUs * TicksPerSecond)
                {
                    int k = queue.TakeForTick(tick, taken);
                    for (int i = 0; i < k; i++)
                    {
                        QuantizedKey x = taken[i];
                        result.Add(new InputEvent(x.Tick, x.Offset, x.Key, x.Down, seq.Next()));
                    }
                    tick++;
                }
            }
            Assert.Equal(events.Length, next);
            Assert.Equal(0, queue.Count);
            overflow = queue.DroppedOverflowCount;
            late = queue.DroppedLateCount;
            return result.ToArray();
        }

        private static long[] Regular(double periodUs, long phaseUs, long endUs)
        {
            var frames = new List<long>();
            for (int i = 0; ; i++)
            {
                long f = phaseUs + (long)Math.Floor(i * periodUs);
                frames.Add(f);
                if (f >= endUs)
                {
                    return frames.ToArray();
                }
            }
        }

        private static long[] Irregular(ulong seed, long endUs)
        {
            var rng = new Pcg32(seed, 99UL);
            var frames = new List<long>();
            long f = 0;
            while (f < endUs)
            {
                int r = rng.NextInt(10);
                f += r == 0 ? rng.NextRange(100, 900) : r < 8 ? rng.NextRange(4_000, 20_000) : rng.NextRange(30_000, 120_000);
                frames.Add(f);
            }
            return frames.ToArray();
        }

        private static bool Same(in InputEvent a, in InputEvent b) =>
            a.Tick == b.Tick && a.Offset == b.Offset && a.Key == b.Key && a.Down == b.Down && a.Seq == b.Seq;
    }
}

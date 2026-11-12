using System;
using System.Linq;
using Wizard.Client;
using Wizard.Sim.Input;
using Xunit;

namespace Wizard.Client.Tests.Input
{
    // CONTRACTS 6.5: TakeForTick(tick)은 유효 틱이 tick인 것을 (Offset, KeyClass) 순으로 꺼내고 9번째부터는 버린다.
    public sealed class TickInputQueueTests
    {
        private static QuantizedKey K(uint tick, byte offset, LogicalKey key, bool down = true) => new QuantizedKey(tick, offset, key, down);

        [Fact]
        public void TakesOnlyThatTickInOffsetThenKeyClassOrder()
        {
            var queue = new TickInputQueue();
            queue.Enqueue(new[]
            {
                K(5, 200, LogicalKey.Up),
                K(6, 0, LogicalKey.Fire),
                K(5, 10, LogicalKey.Skill),
                K(5, 10, LogicalKey.Right),
                K(5, 10, LogicalKey.Stance),
            });
            queue.Enqueue(new[] { K(5, 0, LogicalKey.Pause), K(5, 10, LogicalKey.Down) });

            QuantizedKey[] got = Take(queue, 5);
            Assert.Equal(new[]
            {
                K(5, 0, LogicalKey.Pause),
                K(5, 10, LogicalKey.Stance),
                K(5, 10, LogicalKey.Right),   // 같은 분류(방향)는 넣은 순서
                K(5, 10, LogicalKey.Down),
                K(5, 10, LogicalKey.Skill),   // 방향이 K보다 먼저(spec 01 4.3 확정 규칙)
                K(5, 200, LogicalKey.Up),
            }, got);
            Assert.Equal(1, queue.Count);
            Assert.Equal(new[] { K(6, 0, LogicalKey.Fire) }, Take(queue, 6));
            Assert.Equal(0, queue.Count);
            Assert.Equal(0, queue.DroppedLateCount);
            Assert.Equal(0, queue.DroppedOverflowCount);
        }

        [Fact]
        public void NinthAndLaterKeysOfATickAreDropped()
        {
            var queue = new TickInputQueue();
            // 10개. 정렬 뒤 마지막 둘(오프셋 250의 K, 오프셋 251의 방향)이 버려진다
            queue.Enqueue(new[]
            {
                K(9, 251, LogicalKey.Left),
                K(9, 250, LogicalKey.Skill),
                K(9, 1, LogicalKey.Up), K(9, 1, LogicalKey.Up, false),
                K(9, 2, LogicalKey.Up), K(9, 2, LogicalKey.Up, false),
                K(9, 3, LogicalKey.Fire), K(9, 3, LogicalKey.Fire, false),
                K(9, 4, LogicalKey.Fire), K(9, 250, LogicalKey.Fire, false),
            });
            QuantizedKey[] got = Take(queue, 9);
            Assert.Equal(InputOrder.MaxEventsPerTickPerPlayer, got.Length);
            Assert.Equal(new[]
            {
                K(9, 1, LogicalKey.Up), K(9, 1, LogicalKey.Up, false),
                K(9, 2, LogicalKey.Up), K(9, 2, LogicalKey.Up, false),
                K(9, 3, LogicalKey.Fire), K(9, 3, LogicalKey.Fire, false),
                K(9, 4, LogicalKey.Fire), K(9, 250, LogicalKey.Fire, false),
            }, got);
            Assert.Equal(2, queue.DroppedOverflowCount);
            Assert.Equal(0, queue.Count);       // 다음 틱으로 미루지 않는다(spec 01 4.3)
            Assert.Empty(Take(queue, 10));
        }

        [Fact]
        public void ExactlyEightAreAllKept()
        {
            var queue = new TickInputQueue();
            queue.Enqueue(Enumerable.Range(0, 8).Select(i => K(3, (byte)i, LogicalKey.Fire, i % 2 == 0)).ToArray());
            Assert.Equal(8, Take(queue, 3).Length);
            Assert.Equal(0, queue.DroppedOverflowCount);
        }

        [Fact]
        public void KeysOfPassedTicksAreDroppedAndCountedFutureKeysStay()
        {
            var queue = new TickInputQueue();
            queue.Enqueue(new[] { K(2, 0, LogicalKey.Up), K(3, 5, LogicalKey.Left), K(4, 7, LogicalKey.Down), K(9, 0, LogicalKey.Right) });
            Assert.Equal(new[] { K(4, 7, LogicalKey.Down) }, Take(queue, 4));
            Assert.Equal(2, queue.DroppedLateCount);
            Assert.Equal(1, queue.Count);
            Assert.Empty(Take(queue, 5));
            Assert.Equal(new[] { K(9, 0, LogicalKey.Right) }, Take(queue, 9));
        }

        [Fact]
        public void GrowsBeyondInitialCapacity()
        {
            var queue = new TickInputQueue();
            for (uint t = 0; t < 200; t++)
            {
                queue.Enqueue(new[] { K(t, 9, LogicalKey.Fire), K(t, 3, LogicalKey.Fire, false) });
            }
            Assert.Equal(400, queue.Count);
            for (uint t = 0; t < 200; t++)
            {
                Assert.Equal(new[] { K(t, 3, LogicalKey.Fire, false), K(t, 9, LogicalKey.Fire) }, Take(queue, t));
            }
            Assert.Equal(0, queue.Count);
        }

        [Fact]
        public void RejectsSmallOutputAndUnknownKeys()
        {
            var queue = new TickInputQueue();
            Assert.Throws<ArgumentException>(() => queue.TakeForTick(0, new QuantizedKey[InputOrder.MaxEventsPerTickPerPlayer - 1]));
            Assert.Throws<ArgumentException>(() => queue.Enqueue(new[] { new QuantizedKey(0, 0, (LogicalKey)12, true) }));
            Assert.Equal(0, queue.Count);
        }

        private static QuantizedKey[] Take(TickInputQueue queue, uint tick)
        {
            var buffer = new QuantizedKey[InputOrder.MaxEventsPerTickPerPlayer];
            int n = queue.TakeForTick(tick, buffer);
            return buffer.Take(n).ToArray();
        }
    }
}

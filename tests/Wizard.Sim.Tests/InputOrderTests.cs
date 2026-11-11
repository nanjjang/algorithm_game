using System;
using System.Linq;
using Wizard.Core;
using Wizard.Sim.Input;
using Xunit;

namespace Wizard.Sim.Tests
{
    // CONTRACTS 3.1, 0.3, T-12. 정렬 키 (틱, 오프셋, 키 분류, 슬롯, 순번). 키 분류: 자세·반전 0, 방향 1, J 2, K 3, 일시정지 4
    public sealed class InputOrderTests
    {
        private static PlayerInput In(byte slot, uint tick, byte offset, LogicalKey key, uint seq, bool down = true) =>
            new PlayerInput(new PlayerSlot(slot), new InputEvent(tick, offset, key, down, seq));

        [Theory]
        [InlineData(LogicalKey.Stance, 0)]
        [InlineData(LogicalKey.Reverse, 0)]
        [InlineData(LogicalKey.Up, 1)]
        [InlineData(LogicalKey.Left, 1)]
        [InlineData(LogicalKey.Down, 1)]
        [InlineData(LogicalKey.Right, 1)]
        [InlineData(LogicalKey.Fire, 2)]
        [InlineData(LogicalKey.Skill, 3)]
        [InlineData(LogicalKey.Pause, 4)]
        public void KeyClass_MatchesContract(LogicalKey key, int expected)
        {
            Assert.Equal(expected, InputOrder.KeyClass(key));
        }

        [Fact]
        public void KeyCodes_AreSpec01Codes()
        {
            Assert.Equal(0, (int)LogicalKey.Up);
            Assert.Equal(1, (int)LogicalKey.Left);
            Assert.Equal(2, (int)LogicalKey.Down);
            Assert.Equal(3, (int)LogicalKey.Right);
            Assert.Equal(5, (int)LogicalKey.Skill);
        }

        [Fact]
        public void SameOffset_DirectionBeforeSkill()
        {
            PlayerInput k = In(0, 10, 128, LogicalKey.Skill, 1);
            PlayerInput dir = In(0, 10, 128, LogicalKey.Right, 2);
            Assert.True(InputOrder.Compare(dir, k) < 0);
            var arr = new[] { k, dir };
            InputOrder.Sort(arr);
            Assert.Equal(LogicalKey.Right, arr[0].Event.Key);
        }

        [Fact]
        public void Compare_UsesFullKeyInOrder()
        {
            Assert.True(InputOrder.Compare(In(1, 9, 255, LogicalKey.Pause, 9), In(0, 10, 0, LogicalKey.Stance, 1)) < 0);   // 틱
            Assert.True(InputOrder.Compare(In(1, 10, 3, LogicalKey.Pause, 9), In(0, 10, 4, LogicalKey.Stance, 1)) < 0);    // 오프셋
            Assert.True(InputOrder.Compare(In(1, 10, 4, LogicalKey.Stance, 9), In(0, 10, 4, LogicalKey.Up, 1)) < 0);       // 분류
            Assert.True(InputOrder.Compare(In(0, 10, 4, LogicalKey.Up, 9), In(1, 10, 4, LogicalKey.Left, 1)) < 0);         // 슬롯
            Assert.True(InputOrder.Compare(In(0, 10, 4, LogicalKey.Up, 1), In(0, 10, 4, LogicalKey.Left, 2)) < 0);         // 순번
            Assert.Equal(0, InputOrder.Compare(In(0, 10, 4, LogicalKey.Up, 1), In(0, 10, 4, LogicalKey.Up, 1)));
        }

        [Fact]
        public void Sort_IsATotalOrderIndependentOfArrivalOrder()
        {
            var rng = new Pcg32(99, 1);
            var inputs = new PlayerInput[64];
            for (int i = 0; i < inputs.Length; i++)
            {
                inputs[i] = In((byte)rng.NextInt(2), (uint)rng.NextInt(3), (byte)rng.NextInt(4), (LogicalKey)rng.NextInt(9), (uint)(i + 1));
            }
            var a = (PlayerInput[])inputs.Clone();
            var b = inputs.Reverse().ToArray();
            InputOrder.Sort(a);
            InputOrder.Sort(b);
            Assert.Equal(a.Select(x => (x.Slot.Value, x.Event.Seq)), b.Select(x => (x.Slot.Value, x.Event.Seq)));
            for (int i = 1; i < a.Length; i++)
            {
                Assert.True(InputOrder.Compare(a[i - 1], a[i]) < 0);
            }
        }

        [Fact]
        public void InputEvent_TimeAndPackedKey()
        {
            var e = new InputEvent(3, 17, LogicalKey.Skill, true, 1);
            Assert.Equal(3L * 256 + 17, e.Time);
            Assert.Equal((byte)0x15, e.PackKey());
            Assert.True(InputEvent.TryUnpackKey(0x15, out LogicalKey key, out bool down));
            Assert.Equal(LogicalKey.Skill, key);
            Assert.True(down);
            Assert.True(InputEvent.TryUnpackKey(0x08, out key, out down));
            Assert.Equal(LogicalKey.Pause, key);
            Assert.False(down);
            Assert.False(InputEvent.TryUnpackKey(0x09, out _, out _));   // LogicalKey 범위 밖
            Assert.False(InputEvent.TryUnpackKey(0x21, out _, out _));   // 쓰지 않는 비트
        }

        [Fact]
        public void SeqCounter_StartsAtOne()
        {
            var c = new SeqCounter();
            Assert.Equal(1u, c.Next());
            Assert.Equal(2u, c.Next());
            Assert.Equal(2u, c.Last);
        }

        [Fact]
        public void MaxEventsPerTick_IsEight()
        {
            Assert.Equal(8, InputOrder.MaxEventsPerTickPerPlayer);
        }
    }
}

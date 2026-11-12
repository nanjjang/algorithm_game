using System;
using System.Linq;
using Wizard.Client;
using Wizard.Sim.Input;
using Xunit;

namespace Wizard.Client.Tests.Input
{
    // DECISIONS 1.19, spec 01 4.1·4.2, spec 13 13.4: 기본·대체 배치, 물리 키 둘 이상 묶임, "논리 키 상태가 바뀔 때만 이벤트".
    public sealed class KeyBindingsTests
    {
        [Fact]
        public void DefaultLayoutIsDecision119()
        {
            KeyBindings b = KeyBindings.Default;
            Assert.Equal(PhysicalKey.W, b.Get(LogicalKey.Up, KeyBindings.PrimarySlot));
            Assert.Equal(PhysicalKey.A, b.Get(LogicalKey.Left, KeyBindings.PrimarySlot));
            Assert.Equal(PhysicalKey.S, b.Get(LogicalKey.Down, KeyBindings.PrimarySlot));
            Assert.Equal(PhysicalKey.D, b.Get(LogicalKey.Right, KeyBindings.PrimarySlot));
            Assert.Equal(PhysicalKey.J, b.Get(LogicalKey.Fire, KeyBindings.PrimarySlot));
            Assert.Equal(PhysicalKey.K, b.Get(LogicalKey.Skill, KeyBindings.PrimarySlot));
            Assert.Equal(PhysicalKey.LeftShift, b.Get(LogicalKey.Stance, KeyBindings.PrimarySlot));
            Assert.Equal(PhysicalKey.Space, b.Get(LogicalKey.Reverse, KeyBindings.PrimarySlot));
            Assert.Equal(PhysicalKey.Escape, b.Get(LogicalKey.Pause, KeyBindings.PrimarySlot));
        }

        [Fact]
        public void AlternateLayoutIsArrowsAndZxcv()
        {
            KeyBindings b = KeyBindings.Alternate;
            Assert.Equal(PhysicalKey.UpArrow, b.Get(LogicalKey.Up, KeyBindings.PrimarySlot));
            Assert.Equal(PhysicalKey.LeftArrow, b.Get(LogicalKey.Left, KeyBindings.PrimarySlot));
            Assert.Equal(PhysicalKey.DownArrow, b.Get(LogicalKey.Down, KeyBindings.PrimarySlot));
            Assert.Equal(PhysicalKey.RightArrow, b.Get(LogicalKey.Right, KeyBindings.PrimarySlot));
            Assert.Equal(PhysicalKey.Z, b.Get(LogicalKey.Fire, KeyBindings.PrimarySlot));
            Assert.Equal(PhysicalKey.X, b.Get(LogicalKey.Skill, KeyBindings.PrimarySlot));
            Assert.Equal(PhysicalKey.C, b.Get(LogicalKey.Stance, KeyBindings.PrimarySlot));
            Assert.Equal(PhysicalKey.V, b.Get(LogicalKey.Reverse, KeyBindings.PrimarySlot));
            Assert.Equal(PhysicalKey.Escape, b.Get(LogicalKey.Pause, KeyBindings.PrimarySlot));
        }

        [Fact]
        public void WithReturnsNewLayoutAndLeavesOriginal()
        {
            KeyBindings b = KeyBindings.Default.With(LogicalKey.Up, KeyBindings.SecondarySlot, PhysicalKey.UpArrow);
            Assert.Equal(PhysicalKey.UpArrow, b.Get(LogicalKey.Up, KeyBindings.SecondarySlot));
            Assert.Equal(PhysicalKey.None, KeyBindings.Default.Get(LogicalKey.Up, KeyBindings.SecondarySlot));
            Assert.Equal(KeyState.Bit(LogicalKey.Up), b.MaskFor(PhysicalKey.UpArrow));
            Assert.Equal(KeyMask.None, KeyBindings.Default.MaskFor(PhysicalKey.UpArrow));
            Assert.Equal(KeyMask.None, b.MaskFor(PhysicalKey.None));

            KeyBindings cleared = b.With(LogicalKey.Up, KeyBindings.SecondarySlot, PhysicalKey.None);
            Assert.Equal(KeyMask.None, cleared.MaskFor(PhysicalKey.UpArrow));
        }

        [Fact]
        public void EmptyLayoutBindsNothing()
        {
            KeyBindings e = KeyBindings.Empty();
            foreach (PhysicalKey p in Enum.GetValues(typeof(PhysicalKey)).Cast<PhysicalKey>())
            {
                Assert.Equal(KeyMask.None, e.MaskFor(p));
            }
        }

        [Fact]
        public void RejectsOutOfRangeArguments()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => KeyBindings.Default.Get((LogicalKey)9, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => KeyBindings.Default.Get(LogicalKey.Up, 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => KeyBindings.Default.With(LogicalKey.Up, 0, (PhysicalKey)KeyBindings.PhysicalKeyCapacity));
        }

        [Fact]
        public void PhysicalKeyNumbersFitTheTrackerBitsAndAreUnique()
        {
            ushort[] values = Enum.GetValues(typeof(PhysicalKey)).Cast<PhysicalKey>().Select(p => (ushort)p).ToArray();
            Assert.All(values, v => Assert.True(v < KeyBindings.PhysicalKeyCapacity));
            Assert.Equal(values.Length, values.Distinct().Count());
        }

        // spec 01 4.2: W와 방향키 위가 같은 논리 키에 묶여 있으면 하나라도 눌려 있는 동안 눌린 것. 바뀔 때만 이벤트
        [Fact]
        public void TwoPhysicalKeysOnOneLogicalKeyEmitOnlyOnLogicalChange()
        {
            var tracker = new LogicalKeyTracker(KeyBindings.Default.With(LogicalKey.Up, KeyBindings.SecondarySlot, PhysicalKey.UpArrow));
            var output = new RawKeyEvent[LogicalKeyTracker.MaxEventsPerChange];

            Assert.Equal(1, tracker.OnPhysicalKey(PhysicalKey.W, true, 1.0, output));
            Assert.Equal(new RawKeyEvent(LogicalKey.Up, true, 1.0), output[0]);
            Assert.Equal(0, tracker.OnPhysicalKey(PhysicalKey.UpArrow, true, 1.1, output));   // 이미 눌린 논리 키
            Assert.Equal(0, tracker.OnPhysicalKey(PhysicalKey.W, true, 1.15, output));        // 자동 반복
            Assert.Equal(0, tracker.OnPhysicalKey(PhysicalKey.W, false, 1.2, output));        // 방향키 위가 아직 눌림
            Assert.Equal(KeyState.Bit(LogicalKey.Up), tracker.Current);
            Assert.Equal(1, tracker.OnPhysicalKey(PhysicalKey.UpArrow, false, 1.3, output));
            Assert.Equal(new RawKeyEvent(LogicalKey.Up, false, 1.3), output[0]);
            Assert.Equal(KeyMask.None, tracker.Current);
        }

        [Fact]
        public void UnboundKeysAndStrayReleasesMakeNoEvents()
        {
            var tracker = new LogicalKeyTracker(KeyBindings.Default);
            var output = new RawKeyEvent[LogicalKeyTracker.MaxEventsPerChange];
            Assert.Equal(0, tracker.OnPhysicalKey(PhysicalKey.Q, true, 1.0, output));
            Assert.Equal(0, tracker.OnPhysicalKey(PhysicalKey.J, false, 1.0, output));
            Assert.Equal(0, tracker.OnPhysicalKey(PhysicalKey.None, true, 1.0, output));
            Assert.True(tracker.IsPhysicalDown(PhysicalKey.Q));
            Assert.Equal(KeyMask.None, tracker.Current);
        }

        [Fact]
        public void OnePhysicalKeyOnTwoLogicalKeysEmitsBothInKeyNumberOrder()
        {
            var tracker = new LogicalKeyTracker(KeyBindings.Default.With(LogicalKey.Fire, KeyBindings.SecondarySlot, PhysicalKey.K));
            var output = new RawKeyEvent[LogicalKeyTracker.MaxEventsPerChange];
            Assert.Equal(2, tracker.OnPhysicalKey(PhysicalKey.K, true, 2.0, output));
            Assert.Equal(new RawKeyEvent(LogicalKey.Fire, true, 2.0), output[0]);
            Assert.Equal(new RawKeyEvent(LogicalKey.Skill, true, 2.0), output[1]);
        }

        [Fact]
        public void RebindWhileHeldEmitsTheDifference()
        {
            var tracker = new LogicalKeyTracker(KeyBindings.Default);
            var output = new RawKeyEvent[LogicalKeyTracker.MaxEventsPerChange];
            tracker.OnPhysicalKey(PhysicalKey.J, true, 1.0, output);
            tracker.OnPhysicalKey(PhysicalKey.Z, true, 1.0, output);
            Assert.Equal(KeyState.Bit(LogicalKey.Fire), tracker.Current);

            // 대체 배치: J는 풀리고 Z가 발사다. 발사는 그대로 눌림이라 이벤트가 없다
            Assert.Equal(0, tracker.SetBindings(KeyBindings.Alternate, 1.5, output));
            Assert.Same(KeyBindings.Alternate, tracker.Bindings);
            Assert.Equal(1, tracker.OnPhysicalKey(PhysicalKey.Z, false, 1.6, output));
            Assert.Equal(new RawKeyEvent(LogicalKey.Fire, false, 1.6), output[0]);

            tracker.OnPhysicalKey(PhysicalKey.X, true, 1.7, output);
            // 기본 배치로 돌아가면 아직 누르고 있는 J가 다시 발사가 되고, X는 아무것도 아니라 비기가 풀린다
            Assert.Equal(2, tracker.SetBindings(KeyBindings.Default, 1.8, output));
            Assert.Equal(new RawKeyEvent(LogicalKey.Fire, true, 1.8), output[0]);
            Assert.Equal(new RawKeyEvent(LogicalKey.Skill, false, 1.8), output[1]);
        }

        [Fact]
        public void ReleaseAllEmitsUpsInKeyNumberOrder()
        {
            var tracker = new LogicalKeyTracker(KeyBindings.Default);
            var output = new RawKeyEvent[LogicalKeyTracker.MaxEventsPerChange];
            tracker.OnPhysicalKey(PhysicalKey.Space, true, 1.0, output);
            tracker.OnPhysicalKey(PhysicalKey.D, true, 1.0, output);
            tracker.OnPhysicalKey(PhysicalKey.J, true, 1.0, output);
            Assert.Equal(3, tracker.ReleaseAll(3.0, output));
            Assert.Equal(new[]
            {
                new RawKeyEvent(LogicalKey.Right, false, 3.0),
                new RawKeyEvent(LogicalKey.Fire, false, 3.0),
                new RawKeyEvent(LogicalKey.Reverse, false, 3.0),
            }, output.Take(3).ToArray());
            Assert.Equal(KeyMask.None, tracker.Current);
            Assert.False(tracker.IsPhysicalDown(PhysicalKey.J));
            Assert.Equal(0, tracker.ReleaseAll(3.1, output));
        }

        [Fact]
        public void HighNumberedPhysicalKeysWork()
        {
            var tracker = new LogicalKeyTracker(KeyBindings.Default.With(LogicalKey.Pause, KeyBindings.SecondarySlot, PhysicalKey.CapsLock));
            var output = new RawKeyEvent[LogicalKeyTracker.MaxEventsPerChange];
            Assert.Equal(1, tracker.OnPhysicalKey(PhysicalKey.CapsLock, true, 4.0, output));
            Assert.Equal(new RawKeyEvent(LogicalKey.Pause, true, 4.0), output[0]);
            Assert.True(tracker.IsPhysicalDown(PhysicalKey.CapsLock));
            Assert.False(tracker.IsPhysicalDown(PhysicalKey.Delete));
        }

        [Fact]
        public void TrackerRejectsSmallOutput()
        {
            var tracker = new LogicalKeyTracker(KeyBindings.Default);
            Assert.Throws<ArgumentException>(() => tracker.OnPhysicalKey(PhysicalKey.W, true, 1.0, new RawKeyEvent[1]));
            Assert.Throws<ArgumentNullException>(() => new LogicalKeyTracker(null!));
        }

        // 입력원 → 양자화 → 대기 줄의 한 줄 흐름: 같은 오프셋의 방향과 K는 방향이 먼저 나온다
        [Fact]
        public void TrackerQuantizerQueueEndToEnd()
        {
            var tracker = new LogicalKeyTracker(KeyBindings.Default);
            var raw = new RawKeyEvent[LogicalKeyTracker.MaxEventsPerChange * 2];
            int n = 0;
            double t = 100 * (1.0 / 60.0) + 0.0001;
            n += tracker.OnPhysicalKey(PhysicalKey.K, true, t, raw.AsSpan(n));
            n += tracker.OnPhysicalKey(PhysicalKey.D, true, t + 0.000001, raw.AsSpan(n));
            Assert.Equal(2, n);

            var quantized = new QuantizedKey[n];
            new InputQuantizer().Quantize(raw.AsSpan(0, n), new InputClock(0.0, 1.0 / 60.0), 3, quantized);
            var queue = new TickInputQueue();
            queue.Enqueue(quantized);
            var taken = new QuantizedKey[InputOrder.MaxEventsPerTickPerPlayer];
            Assert.Equal(2, queue.TakeForTick(103, taken));
            Assert.Equal(LogicalKey.Right, taken[0].Key);
            Assert.Equal(LogicalKey.Skill, taken[1].Key);
            Assert.Equal(taken[0].Offset, taken[1].Offset);
        }
    }
}

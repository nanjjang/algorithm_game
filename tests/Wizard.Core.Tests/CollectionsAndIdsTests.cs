using System;
using System.Collections.Generic;
using System.Linq;
using Wizard.Core;
using Xunit;

namespace Wizard.Core.Tests
{
    // CONTRACTS 1.5
    public sealed class CollectionsAndIdsTests
    {
        private readonly struct ByKey : IComparer<(int Key, int Order)>
        {
            public int Compare((int Key, int Order) a, (int Key, int Order) b) => a.Key.CompareTo(b.Key);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(15)]
        [InlineData(16)]
        [InlineData(17)]
        [InlineData(100)]
        [InlineData(1000)]
        public void DetSort_IsStableAndSorted(int n)
        {
            var rng = new Pcg32((ulong)n, 3);
            var items = new (int Key, int Order)[n + 4];
            for (int i = 0; i < items.Length; i++)
            {
                items[i] = (rng.NextInt(10), i);
            }
            var expected = items.Skip(2).Take(n).OrderBy(x => x.Key).ToArray();   // LINQ OrderBy 는 안정 정렬(기준)
            var head = items[0];
            var tail = items[items.Length - 1];
            DetSort.Stable(items, 2, n, default(ByKey), new (int, int)[n]);
            Assert.Equal(expected, items.Skip(2).Take(n).ToArray());
            Assert.Equal(head, items[0]);
            Assert.Equal(tail, items[items.Length - 1]);
        }

        [Fact]
        public void DetSort_RejectsSmallScratchAndBadRange()
        {
            var items = new (int, int)[40];
            Assert.Throws<SimInvariantException>(() => DetSort.Stable(items, 0, 40, default(ByKey), new (int, int)[10]));
            Assert.Throws<SimInvariantException>(() => DetSort.Stable(items, 30, 20, default(ByKey), new (int, int)[40]));
        }

        [Fact]
        public void SortedIntList_KeepsOrderWithoutDuplicates()
        {
            var list = new SortedIntList(1);
            Assert.True(list.Add(5));
            Assert.True(list.Add(-1));
            Assert.True(list.Add(3));
            Assert.False(list.Add(3));
            Assert.Equal(new[] { -1, 3, 5 }, Enumerable.Range(0, list.Count).Select(i => list[i]).ToArray());
            Assert.True(list.Contains(3));
            Assert.True(list.Remove(-1));
            Assert.False(list.Remove(42));
            var copy = new SortedIntList();
            copy.CopyFrom(list);
            list.Clear();
            Assert.Equal(0, list.Count);
            Assert.Equal(2, copy.Count);
            Assert.Equal(5, copy[1]);
            Assert.Throws<SimInvariantException>(() => copy[2]);
        }

        [Fact]
        public void RingBuffer_OverwritesOldestAndDeepCopies()
        {
            var ring = new RingBuffer<int>(3);
            for (int i = 1; i <= 5; i++)
            {
                ring.Push(i);
            }
            Assert.Equal(3, ring.Count);
            Assert.Equal(new[] { 3, 4, 5 }, new[] { ring[0], ring[1], ring[2] });
            var copy = new RingBuffer<int>(3);
            copy.CopyFrom(ring);
            ring.Push(6);
            Assert.Equal(3, copy[0]);
            Assert.Equal(4, ring[0]);
            Assert.Throws<SimInvariantException>(() => copy.CopyFrom(new RingBuffer<int>(4)));
            Assert.Throws<SimInvariantException>(() => new RingBuffer<int>(0));
        }

        [Fact]
        public void UuidBytes_UsesNetworkOrder()
        {
            var id = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");
            var bytes = new byte[16];
            UuidBytes.Write(id, bytes);
            Assert.Equal(new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88, 0x99, 0xaa, 0xbb, 0xcc, 0xdd, 0xee, 0xff }, bytes);
            Assert.Equal(id, UuidBytes.Read(bytes));
            Assert.NotEqual(id.ToByteArray(), bytes);            // ToByteArray 의 혼합 순서와 다르다
        }

        [Theory]
        [InlineData("block.fireball", true)]
        [InlineData("char.1", true)]
        [InlineData("cosmetic_cat.kill_effect", true)]
        [InlineData("a.b.c", true)]
        [InlineData("block", false)]
        [InlineData("Block.fireball", false)]
        [InlineData("1block.x", false)]
        [InlineData("block..x", false)]
        [InlineData("block.", false)]
        [InlineData("block._x", false)]
        [InlineData("block.fire-ball", false)]
        [InlineData("", false)]
        public void ContentId_Pattern(string text, bool ok)
        {
            Assert.Equal(ok, ContentId.TryParse(text, out ContentId id));
            if (ok)
            {
                Assert.Equal(text, id.Value);
            }
        }

        [Fact]
        public void ContentId_RejectsTooLong()
        {
            Assert.True(ContentId.TryParse("a." + new string('b', 62), out _));
            Assert.False(ContentId.TryParse("a." + new string('b', 63), out _));
        }

        [Fact]
        public void EntityIdAndSlot()
        {
            Assert.True(EntityId.None.IsNone);
            Assert.True(new EntityId(1).CompareTo(new EntityId(2)) < 0);
            Assert.Equal(255, PlayerSlot.None.Value);
            Assert.True(new PlayerSlot(1) != PlayerSlot.None);
        }

        [Fact]
        public void Result_SuccessAndFail()
        {
            Result<int> ok = Result<int>.Success(3);
            Result<int> fail = Result<int>.Fail(ErrorCode.NotEnoughGold);
            Assert.True(ok.Ok);
            Assert.Equal(3, ok.Value);
            Assert.False(fail.Ok);
            Assert.Equal(ErrorCode.NotEnoughGold, fail.Error.Code);
            Assert.Equal(205, (int)ErrorCode.NotEnoughGold);     // 번호는 바꾸지 않는다
        }
    }
}

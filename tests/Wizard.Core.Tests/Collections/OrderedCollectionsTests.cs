using System;
using System.Collections.Generic;
using Wizard.Core;
using Xunit;

namespace Wizard.Core.Tests.Collections
{
    // CONTRACTS 1.5, 0.1. M1-CORE-05 수락 조건 2·4(RingBuffer 부분).
    // 삽입·삭제·순회의 순서를 단순한 기준 모형(id 로 색인한 bool 배열을 0부터 훑기 = 정의상 오름차순)과 대조한다.
    public sealed class OrderedCollectionsTests
    {
        private const int MaxId = 400;

        internal struct Body
        {
            public int Hp;
            public long PosRaw;
            public byte Kind;
        }

        // ── SortedIntList ──────────────────────────────────────────────

        [Fact]
        public void SortedIntList_RandomOpsMatchAscendingModel()
        {
            var rng = new Pcg32(51UL, 2);
            var list = new SortedIntList(1);
            var present = new bool[2 * MaxId + 1];   // 값 v 는 칸 v + MaxId
            for (int op = 0; op < 50_000; op++)
            {
                int v = rng.NextInt(2 * MaxId + 1) - MaxId;   // 음수도 넣는다
                switch (rng.NextInt(4))
                {
                    case 0:
                    case 1:
                        Assert.Equal(!present[v + MaxId], list.Add(v));
                        present[v + MaxId] = true;
                        break;
                    case 2:
                        Assert.Equal(present[v + MaxId], list.Remove(v));
                        present[v + MaxId] = false;
                        break;
                    default:
                        Assert.Equal(present[v + MaxId], list.Contains(v));
                        break;
                }
                if (op % 97 == 0)
                {
                    AssertMatches(list, present);
                }
            }
            AssertMatches(list, present);
        }

        [Fact]
        public void SortedIntList_CopyFromIsDeep()
        {
            var original = new SortedIntList(2);
            original.Add(3);
            original.Add(1);
            original.Add(2);
            var copy = new SortedIntList(1);
            copy.CopyFrom(original);
            copy.Remove(2);
            copy.Add(10);
            copy.Add(-5);
            Assert.Equal(new[] { 1, 2, 3 }, ToArray(original));
            Assert.Equal(new[] { -5, 1, 3, 10 }, ToArray(copy));
            original.Clear();
            Assert.Equal(new[] { -5, 1, 3, 10 }, ToArray(copy));
        }

        [Fact]
        public void SortedIntList_SaveContinueRestoreContinueIsSame()
        {
            var list = new SortedIntList();
            var rng = new Pcg32(8UL, 4);
            RandomOps(list, ref rng, 500);
            var saved = new SortedIntList();
            saved.CopyFrom(list);
            Pcg32 resume = rng;
            Pcg32 r1 = resume;
            RandomOps(list, ref r1, 500);
            int[] first = ToArray(list);
            list.CopyFrom(saved);
            Pcg32 r2 = resume;
            RandomOps(list, ref r2, 500);
            Assert.Equal(first, ToArray(list));
        }

        // ── RingBuffer ─────────────────────────────────────────────────

        [Fact]
        public void RingBuffer_IsSealedClass()
        {
            Type t = typeof(RingBuffer<>);
            Assert.True(t.IsClass);
            Assert.True(t.IsSealed);
        }

        // 오름차순 id 를 밀어 넣으면 색인 0(가장 오래된 것)부터의 순회가 id 오름차순이다. 덮어써도 마찬가지
        [Fact]
        public void RingBuffer_IterationIsOldestFirstSoAscendingIdsStayAscending()
        {
            var rng = new Pcg32(17UL, 6);
            for (int c = 0; c < 2_000; c++)
            {
                int cap = 1 + rng.NextInt(20);
                var ring = new RingBuffer<int>(cap);
                int pushes = rng.NextInt(3 * cap + 1);
                for (int id = 1; id <= pushes; id++)
                {
                    ring.Push(id);
                }
                int expectedCount = pushes < cap ? pushes : cap;
                Assert.Equal(expectedCount, ring.Count);
                Assert.Equal(cap, ring.Capacity);
                for (int i = 0; i < ring.Count; i++)
                {
                    Assert.Equal(pushes - expectedCount + 1 + i, ring[i]);   // 남는 것은 마지막 cap 개, 오래된 것부터
                }
                if (rng.NextInt(4) == 0)
                {
                    ring.Clear();
                    Assert.Equal(0, ring.Count);
                    ring.Push(42);
                    Assert.Equal(42, ring[0]);
                }
            }
        }

        // 수락 4: CopyFrom 은 깊은 복사다. 복사본을 바꿔도 원본이 바뀌지 않는다(반대도)
        [Fact]
        public void RingBuffer_CopyFromIsDeep()
        {
            var original = new RingBuffer<Body>(4);
            for (int i = 1; i <= 6; i++)
            {
                original.Push(new Body { Hp = i, PosRaw = i * 1000L, Kind = (byte)i });
            }
            var copy = new RingBuffer<Body>(4);
            copy.CopyFrom(original);
            Assert.Equal(new[] { 3, 4, 5, 6 }, Hps(copy));

            copy.Push(new Body { Hp = 99 });
            copy.Push(new Body { Hp = 98 });
            Assert.Equal(new[] { 3, 4, 5, 6 }, Hps(original));          // 복사본을 바꿔도 원본은 그대로
            Assert.Equal(new[] { 5, 6, 99, 98 }, Hps(copy));
            Assert.Equal(3000L, original[0].PosRaw);

            copy.Clear();
            Assert.Equal(4, original.Count);
            original.Push(new Body { Hp = 7 });
            Assert.Equal(0, copy.Count);                                  // 원본을 바꿔도 복사본은 그대로
        }

        [Fact]
        public void RingBuffer_CopyFromRejectsOtherCapacity()
        {
            Assert.Throws<SimInvariantException>(() => new RingBuffer<int>(3).CopyFrom(new RingBuffer<int>(4)));
            Assert.Throws<SimInvariantException>(() => new RingBuffer<int>(0));
            Assert.Throws<SimInvariantException>(() => new RingBuffer<int>(2)[0]);
        }

        [Fact]
        public void RingBuffer_SaveContinueRestoreContinueIsSame()
        {
            var ring = new RingBuffer<int>(7);
            for (int i = 1; i <= 10; i++)
            {
                ring.Push(i);
            }
            var saved = new RingBuffer<int>(7);
            saved.CopyFrom(ring);
            for (int i = 11; i <= 15; i++)
            {
                ring.Push(i);
            }
            int[] first = Ints(ring);
            ring.CopyFrom(saved);
            for (int i = 11; i <= 15; i++)
            {
                ring.Push(i);
            }
            Assert.Equal(first, Ints(ring));
        }

        // ── FixedPool ──────────────────────────────────────────────────

        [Fact]
        public void FixedPool_RandomOpsMatchAscendingModel()
        {
            var rng = new Pcg32(77UL, 8);
            const int cap = 64;
            var pool = new FixedPool<Body>(cap);
            var present = new bool[MaxId + 1];
            var hp = new int[MaxId + 1];
            int count = 0;
            for (int op = 0; op < 50_000; op++)
            {
                int id = 1 + rng.NextInt(MaxId);
                switch (rng.NextInt(5))
                {
                    case 0:
                    case 1:
                        if (present[id])
                        {
                            Assert.Throws<SimInvariantException>(() => pool.TryAdd(id, new Body()));   // 같은 id 는 상태가 깨진 것
                            break;
                        }
                        int value = rng.NextInt(1_000_000);
                        bool added = pool.TryAdd(id, new Body { Hp = value });
                        Assert.Equal(count < cap, added);                // 가득 차면 false(예외가 아니다)
                        if (added)
                        {
                            present[id] = true;
                            hp[id] = value;
                            count++;
                        }
                        break;
                    case 2:
                        Assert.Equal(present[id], pool.Remove(id));
                        if (present[id])
                        {
                            present[id] = false;
                            count--;
                        }
                        break;
                    case 3:
                        if (pool.Count > 0)
                        {
                            int index = rng.NextInt(pool.Count);
                            int removed = pool.IdAt(index);
                            pool.RemoveAt(index);
                            Assert.True(present[removed]);
                            present[removed] = false;
                            count--;
                        }
                        break;
                    default:
                        int at = pool.IndexOf(id);
                        Assert.Equal(present[id], at >= 0);
                        Assert.Equal(present[id], pool.Contains(id));
                        if (at >= 0)
                        {
                            pool[at].Hp += 1;                             // ref 로 제자리에서 고친다
                            hp[id] += 1;
                        }
                        break;
                }
                Assert.Equal(count, pool.Count);
                if (op % 89 == 0)
                {
                    AssertMatches(pool, present, hp);
                }
            }
            AssertMatches(pool, present, hp);
        }

        // 생성 순서대로 1씩 느는 id 를 넣고 사이사이 지운다: 순회는 언제나 id 오름차순
        [Fact]
        public void FixedPool_MonotonicIdsWithRemovalsIterateAscending()
        {
            var pool = new FixedPool<Body>(8);
            int next = 1;
            var rng = new Pcg32(3UL, 3);
            for (int step = 0; step < 5_000; step++)
            {
                if (!pool.IsFull && rng.NextInt(3) != 0)
                {
                    Assert.True(pool.TryAdd(next, new Body { Hp = next }));
                    next++;
                }
                else if (pool.Count > 0)
                {
                    pool.RemoveAt(rng.NextInt(pool.Count));
                }
                for (int i = 1; i < pool.Count; i++)
                {
                    Assert.True(pool.IdAt(i - 1) < pool.IdAt(i));
                }
                for (int i = 0; i < pool.Count; i++)
                {
                    Assert.Equal(pool.IdAt(i), pool[i].Hp);              // 값이 자기 id 와 함께 움직인다
                }
            }
        }

        // 앞에서부터 돌며 지울 때는 같은 색인을 다시 본다(뒤가 당겨진다)
        [Fact]
        public void FixedPool_RemoveWhileIteratingForward()
        {
            var pool = new FixedPool<Body>(16);
            for (int id = 1; id <= 12; id++)
            {
                pool.TryAdd(id, new Body { Hp = id });
            }
            for (int i = 0; i < pool.Count;)
            {
                if (pool[i].Hp % 3 != 1)
                {
                    pool.RemoveAt(i);
                    continue;
                }
                i++;
            }
            Assert.Equal(new[] { 1, 4, 7, 10 }, Ids(pool));
        }

        [Fact]
        public void FixedPool_CopyFromIsDeep()
        {
            var original = new FixedPool<Body>(5);
            original.TryAdd(2, new Body { Hp = 20 });
            original.TryAdd(5, new Body { Hp = 50 });
            original.TryAdd(9, new Body { Hp = 90 });
            var copy = new FixedPool<Body>(5);
            copy.CopyFrom(original);
            copy[0].Hp = -1;
            copy.Remove(5);
            copy.TryAdd(1, new Body { Hp = 10 });
            Assert.Equal(new[] { 2, 5, 9 }, Ids(original));
            Assert.Equal(20, original[0].Hp);
            Assert.Equal(new[] { 1, 2, 9 }, Ids(copy));
            original.Clear();
            Assert.Equal(3, copy.Count);
            Assert.Throws<SimInvariantException>(() => copy.CopyFrom(new FixedPool<Body>(6)));
        }

        [Fact]
        public void FixedPool_RejectsBrokenState()
        {
            Assert.Throws<SimInvariantException>(() => new FixedPool<Body>(0));
            var pool = new FixedPool<Body>(2);
            Assert.Throws<SimInvariantException>(() => pool.TryAdd(0, new Body()));
            Assert.Throws<SimInvariantException>(() => pool.TryAdd(-3, new Body()));
            Assert.Throws<SimInvariantException>(() => pool.IdAt(0));
            Assert.Throws<SimInvariantException>(() => pool.RemoveAt(0));
            Assert.True(pool.TryAdd(1, new Body()));
            Assert.True(pool.TryAdd(2, new Body()));
            Assert.False(pool.TryAdd(3, new Body()));                    // 가득 참: 규칙상의 "안 됨"
            Assert.Equal(-1, pool.IndexOf(3));
        }

        [Fact]
        public void FixedPool_SaveContinueRestoreContinueIsSame()
        {
            var pool = new FixedPool<Body>(32);
            var rng = new Pcg32(21UL, 7);
            RandomOps(pool, ref rng, 400);
            var saved = new FixedPool<Body>(32);
            saved.CopyFrom(pool);
            Pcg32 resume = rng;
            Pcg32 r1 = resume;
            RandomOps(pool, ref r1, 400);
            (int[] ids, int[] hps) first = (Ids(pool), PoolHps(pool));
            pool.CopyFrom(saved);
            Pcg32 r2 = resume;
            RandomOps(pool, ref r2, 400);
            Assert.Equal(first.ids, Ids(pool));
            Assert.Equal(first.hps, PoolHps(pool));
        }

        // Step 안에서 쓰는 연산은 할당하지 않는다(생성자만 할당한다)
        [Fact]
        public void FixedPoolAndRingBuffer_OpsAllocateZeroBytes()
        {
            var pool = new FixedPool<Body>(64);
            var other = new FixedPool<Body>(64);
            var ring = new RingBuffer<Body>(16);
            var ringCopy = new RingBuffer<Body>(16);
            Exercise(pool, other, ring, ringCopy);                       // 데우기(JIT)
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int round = 0; round < 4; round++)
            {
                Exercise(pool, other, ring, ringCopy);
            }
            long after = GC.GetAllocatedBytesForCurrentThread();
            Assert.Equal(0L, after - before);
        }

        private static void Exercise(FixedPool<Body> pool, FixedPool<Body> other, RingBuffer<Body> ring, RingBuffer<Body> ringCopy)
        {
            pool.Clear();
            for (int id = 64; id >= 1; id--)
            {
                pool.TryAdd(id, new Body { Hp = id });                   // 앞에 끼워 넣는 길
            }
            pool.TryAdd(65, new Body());                                  // 가득 참
            for (int id = 2; id <= 64; id += 2)
            {
                pool.Remove(id);
            }
            pool.RemoveAt(0);
            pool[0].Hp++;
            other.CopyFrom(pool);
            for (int i = 0; i < 40; i++)
            {
                ring.Push(new Body { Hp = i });
            }
            ringCopy.CopyFrom(ring);
            ring.Clear();
        }

        // ── 공용 ───────────────────────────────────────────────────────

        private static void RandomOps(SortedIntList list, ref Pcg32 rng, int ops)
        {
            for (int i = 0; i < ops; i++)
            {
                int v = rng.NextInt(200);
                if (rng.NextInt(3) == 0)
                {
                    list.Remove(v);
                }
                else
                {
                    list.Add(v);
                }
            }
        }

        private static void RandomOps(FixedPool<Body> pool, ref Pcg32 rng, int ops)
        {
            for (int i = 0; i < ops; i++)
            {
                int id = 1 + rng.NextInt(100);
                int r = rng.NextInt(3);
                if (r == 0)
                {
                    pool.Remove(id);
                }
                else if (!pool.Contains(id))
                {
                    pool.TryAdd(id, new Body { Hp = rng.NextInt(1000) });
                }
                else
                {
                    pool[pool.IndexOf(id)].Hp += r;
                }
            }
        }

        private static void AssertMatches(SortedIntList list, bool[] present)
        {
            var expected = new List<int>();
            for (int k = 0; k < present.Length; k++)
            {
                if (present[k])
                {
                    expected.Add(k - MaxId);
                }
            }
            Assert.Equal(expected.ToArray(), ToArray(list));
        }

        private static void AssertMatches(FixedPool<Body> pool, bool[] present, int[] hp)
        {
            var ids = new List<int>();
            var hps = new List<int>();
            for (int id = 0; id < present.Length; id++)
            {
                if (present[id])
                {
                    ids.Add(id);
                    hps.Add(hp[id]);
                }
            }
            Assert.Equal(ids.ToArray(), Ids(pool));
            Assert.Equal(hps.ToArray(), PoolHps(pool));
        }

        private static int[] ToArray(SortedIntList list)
        {
            var a = new int[list.Count];
            for (int i = 0; i < a.Length; i++)
            {
                a[i] = list[i];
            }
            return a;
        }

        private static int[] Ids(FixedPool<Body> pool)
        {
            var a = new int[pool.Count];
            for (int i = 0; i < a.Length; i++)
            {
                a[i] = pool.IdAt(i);
            }
            return a;
        }

        private static int[] PoolHps(FixedPool<Body> pool)
        {
            var a = new int[pool.Count];
            for (int i = 0; i < a.Length; i++)
            {
                a[i] = pool[i].Hp;
            }
            return a;
        }

        private static int[] Hps(RingBuffer<Body> ring)
        {
            var a = new int[ring.Count];
            for (int i = 0; i < a.Length; i++)
            {
                a[i] = ring[i].Hp;
            }
            return a;
        }

        private static int[] Ints(RingBuffer<int> ring)
        {
            var a = new int[ring.Count];
            for (int i = 0; i < a.Length; i++)
            {
                a[i] = ring[i];
            }
            return a;
        }
    }
}

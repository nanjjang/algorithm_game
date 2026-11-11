using System;
using System.Collections.Generic;
using System.Reflection;
using Wizard.Core;
using Xunit;

namespace Wizard.Core.Tests.Collections
{
    // CONTRACTS 1.5, DECISIONS-TECH T-11. M1-CORE-05 수락 조건 1·4(DetSort 부분).
    public sealed class DetSortTests
    {
        internal struct Item
        {
            public int Key;
            public int Order;   // 넣은 순서. 안정성 판정에만 쓴다(비교자는 보지 않는다)
        }

        internal readonly struct ByKey : IComparer<Item>
        {
            public int Compare(Item a, Item b) => a.Key < b.Key ? -1 : (a.Key > b.Key ? 1 : 0);
        }

        private const int Cases = 100_000;

        // 수락 1: 동률 원소가 든 무작위 배열 10만 건. 정렬됐고, 같은 키는 넣은 순서를 지키고, 원소를 잃거나 겹치지 않는다.
        [Fact]
        public void Stable_IsStableOn100kRandomArraysWithTies()
        {
            var rng = new Pcg32(0x5EED_05UL, 11);
            var items = new Item[512 + 4];
            var scratch = new Item[512];
            var seen = new bool[512];
            for (int c = 0; c < Cases; c++)
            {
                // 길이: 대부분 0..199(삽입 정렬 구간 16 과 병합 여러 겹을 지난다), 가끔 512 까지
                int n = c % 64 == 0 ? rng.NextInt(513) : rng.NextInt(200);
                int start = rng.NextInt(3);
                int keyRange = 1 + rng.NextInt(c % 3 == 0 ? 4 : 40);   // 키 범위가 좁을수록 동률이 많다
                int total = start + n + 2;
                for (int i = 0; i < total; i++)
                {
                    items[i].Key = i >= start && i < start + n ? rng.NextInt(keyRange) - (keyRange >> 1) : int.MinValue + i;
                    items[i].Order = i;
                }
                DetSort.Stable(items, start, n, default(ByKey), scratch);
                AssertSortedStable(items, start, n, seen, c);
                for (int i = 0; i < start; i++)
                {
                    Assert.Equal(i, items[i].Order);                    // 범위 앞은 그대로
                }
                for (int i = start + n; i < total; i++)
                {
                    Assert.Equal(i, items[i].Order);                    // 범위 뒤도 그대로
                }
            }
        }

        [Fact]
        public void Stable_IsStableOnOneLargeArrayWithTies()
        {
            const int n = 100_000;
            var rng = new Pcg32(7UL, 5);
            var items = new Item[n];
            for (int i = 0; i < n; i++)
            {
                items[i] = new Item { Key = rng.NextInt(97), Order = i };
            }
            DetSort.Stable(items, 0, n, default(ByKey), new Item[n]);
            AssertSortedStable(items, 0, n, new bool[n], 0);
        }

        [Fact]
        public void InsertionStable_IsStableOnRandomArraysWithTies()
        {
            var rng = new Pcg32(99UL, 3);
            var items = new Item[64];
            var seen = new bool[64];
            for (int c = 0; c < 20_000; c++)
            {
                int n = rng.NextInt(65);
                for (int i = 0; i < n; i++)
                {
                    items[i] = new Item { Key = rng.NextInt(5), Order = i };
                }
                DetSort.InsertionStable(new Span<Item>(items, 0, n), default(ByKey));
                AssertSortedStable(items, 0, n, seen, c);
            }
        }

        // 같은 입력을 두 번 정렬하면 바이트까지 같다(결정론: 두 번 실행)
        [Fact]
        public void Stable_SameInputTwiceGivesSameOrder()
        {
            var a = new Item[3000];
            var b = new Item[3000];
            var rng = new Pcg32(1234UL, 1);
            for (int i = 0; i < a.Length; i++)
            {
                a[i] = new Item { Key = rng.NextInt(10), Order = i };
                b[i] = a[i];
            }
            DetSort.Stable(a, 0, a.Length, default(ByKey), new Item[a.Length]);
            DetSort.Stable(b, 0, b.Length, default(ByKey), new Item[b.Length]);
            for (int i = 0; i < a.Length; i++)
            {
                Assert.Equal(a[i].Key, b[i].Key);
                Assert.Equal(a[i].Order, b[i].Order);
            }
        }

        // 수락 4: 비교자는 구조체 제네릭 매개변수다(인터페이스로 받으면 박싱된다)
        [Fact]
        public void ComparerParameterIsStructConstrainedGeneric()
        {
            foreach (string name in new[] { nameof(DetSort.Stable), nameof(DetSort.InsertionStable) })
            {
                MethodInfo m = typeof(DetSort).GetMethod(name, BindingFlags.Public | BindingFlags.Static)!;
                Assert.True(m.IsGenericMethodDefinition);
                Type[] args = m.GetGenericArguments();
                Assert.Equal(2, args.Length);
                Type comparer = args[1];
                Assert.True((comparer.GenericParameterAttributes & GenericParameterAttributes.NotNullableValueTypeConstraint) != 0, name);
                Type[] constraints = comparer.GetGenericParameterConstraints();
                Assert.Contains(constraints, t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IComparer<>));
                ParameterInfo p = Array.Find(m.GetParameters(), x => x.Name == "comparer")!;
                Assert.Equal(comparer, p.ParameterType);                 // 인터페이스 형식이 아니라 제네릭 매개변수 그대로
            }
        }

        // 수락 4: 호출에서 할당 0바이트(병합 경로와 삽입 경로 둘 다)
        [Fact]
        public void Calls_AllocateZeroBytes()
        {
            const int n = 4096;
            var items = new Item[n];
            var scratch = new Item[n];
            var small = new Item[16];
            Fill(items, small, 1);
            DetSort.Stable(items, 0, n, default(ByKey), scratch);       // 데우기(JIT)
            DetSort.InsertionStable(new Span<Item>(small), default(ByKey));
            Fill(items, small, 2);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int round = 0; round < 8; round++)
            {
                Fill(items, small, (ulong)round + 3);
                DetSort.Stable(items, 0, n, default(ByKey), scratch);
                DetSort.Stable(items, 5, 10, default(ByKey), scratch);
                DetSort.InsertionStable(new Span<Item>(small), default(ByKey));
            }
            long after = GC.GetAllocatedBytesForCurrentThread();
            Assert.Equal(0L, after - before);
        }

        [Fact]
        public void Stable_RejectsSmallScratchAndBadRange()
        {
            var items = new Item[40];
            Assert.Throws<SimInvariantException>(() => DetSort.Stable(items, 0, 40, default(ByKey), new Item[10]));
            Assert.Throws<SimInvariantException>(() => DetSort.Stable(items, 30, 20, default(ByKey), new Item[40]));
            Assert.Throws<SimInvariantException>(() => DetSort.Stable(items, -1, 2, default(ByKey), new Item[40]));
            Assert.Throws<SimInvariantException>(() => DetSort.Stable(items, 0, -1, default(ByKey), new Item[40]));
        }

        private static void Fill(Item[] items, Item[] small, ulong seed)
        {
            var rng = new Pcg32(seed, 9);
            for (int i = 0; i < items.Length; i++)
            {
                items[i].Key = rng.NextInt(50);
                items[i].Order = i;
            }
            for (int i = 0; i < small.Length; i++)
            {
                small[i].Key = rng.NextInt(4);
                small[i].Order = i;
            }
        }

        // 기대값은 정의에서 바로 나온다: 키가 줄지 않고, 키가 같으면 넣은 순서(Order)가 는다, 넣은 원소 전부가 한 번씩 있다
        private static void AssertSortedStable(Item[] items, int start, int n, bool[] seen, int caseNo)
        {
            Array.Clear(seen, 0, seen.Length);
            for (int i = start; i < start + n; i++)
            {
                int order = items[i].Order - start;
                if (order < 0 || order >= n)
                {
                    Assert.Fail("case " + caseNo + ": element from outside the range at " + i);
                }
                if (seen[order])
                {
                    Assert.Fail("case " + caseNo + ": duplicated element at " + i);
                }
                seen[order] = true;
                if (i > start)
                {
                    Item prev = items[i - 1];
                    Item cur = items[i];
                    if (prev.Key > cur.Key)
                    {
                        Assert.Fail("case " + caseNo + ": not sorted at " + i);
                    }
                    if (prev.Key == cur.Key && prev.Order >= cur.Order)
                    {
                        Assert.Fail("case " + caseNo + ": not stable at " + i);
                    }
                }
            }
        }
    }
}

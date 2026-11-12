using System;
using Wizard.Sim;
using Xunit;

namespace Wizard.Perf.Tests
{
    // PIPELINE 7.5: 스냅숏 저장 + 복원은 할당 0바이트(CONTRACTS 3.2: SaveSnapshot 은 값 복사, 버퍼는 CreateSnapshotBuffer 로 한 번 만든다).
    // 되감기의 꼴로 잰다: 저장 → 한 틱 → 복원 → 해시가 저장한 때와 같다.
    [Collection(AllocationCollection.Name)]
    public sealed class SnapshotAllocationTests
    {
        private const int Pairs = 400;

        [Fact]
        public void Duel2p_SaveLoadAllocatesZeroBytes() => AssertZero(PerfScenario.Duel2p);

        [Fact]
        public void PracticeProto_SaveLoadAllocatesZeroBytes() => AssertZero(PerfScenario.PracticeProto);

        [Fact]
        public void Endless1p_SaveLoadAllocatesZeroBytes() => AssertZero(PerfScenario.Endless1p);

        private static void AssertZero(PerfScenario s)
        {
            StepFeed feed = PerfFixtures.Feed(s, PerfFixtures.WarmupTicks + Pairs * 2 + 2);
            SimWorld world = ScenarioInfo.Create(s);
            var sink = new CountingSink();
            while (world.Tick < (uint)PerfFixtures.WarmupTicks)
            {
                feed.StepAt(world, sink);
            }
            SimSnapshot snap = world.CreateSnapshotBuffer();
            world.SaveSnapshot(snap);
            world.LoadSnapshot(snap);
            long allocated = 0;
            using NoGcWindow window = NoGcWindow.Open();
            for (int i = 0; i < Pairs; i++)
            {
                ulong hash = world.ComputeStateHash();
                uint tick = world.Tick;
                long a0 = GC.GetAllocatedBytesForCurrentThread();
                world.SaveSnapshot(snap);
                long a1 = GC.GetAllocatedBytesForCurrentThread();
                feed.StepAt(world, sink);
                long a2 = GC.GetAllocatedBytesForCurrentThread();
                world.LoadSnapshot(snap);
                long a3 = GC.GetAllocatedBytesForCurrentThread();
                allocated += (a1 - a0) + (a3 - a2);
                Assert.Equal(tick, world.Tick);
                Assert.Equal(hash, world.ComputeStateHash());
                feed.StepAt(world, sink);
                feed.StepAt(world, sink);
            }
            Assert.True(window.Valid, "a GC ran inside the allocation window");
            Assert.Equal(0L, allocated);
        }
    }
}

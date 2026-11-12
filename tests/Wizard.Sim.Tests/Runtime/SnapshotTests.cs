using System;
using Wizard.Content;
using Wizard.Core;
using Wizard.Sim.Input;
using Xunit;

namespace Wizard.Sim.Tests.Runtime
{
    // CONTRACTS 3.2, T-14: SaveSnapshot/LoadSnapshot 은 값 복사이고 할당이 없다. 복원 뒤 같은 입력으로 진행하면
    // 저장하지 않고 진행한 것과 같은 해시가 나온다. 스냅숏은 같은 모양의 다른 세계에도 실린다(확정 → 예측)
    public sealed class SnapshotTests
    {
        private static readonly ContentDb Db = WorldKit.EmptyDb();

        // 모듈 흉내: 틱마다 보관 흐름 하나를 소비하고 개체 id 를 준다(상태가 틱마다 달라지게)
        private static void StepOnce(SimWorld world, PlayerInput[] buffer, ref uint seq, RecordingSink sink)
        {
            int n = WorldKit.FillInputs(buffer, world.Tick, world.PlayerCount, ref seq);
            world.HeldRng((world.Tick & 1U) == 0U ? RngStream.EnemyAi : RngStream.Mutation).NextU32();
            if (world.Tick % 3U == 0U)
            {
                world.NewEntityId();
            }
            world.Step(buffer.AsSpan(0, n), ReadOnlySpan<RunCommand>.Empty, sink);
        }

        [Fact]
        public void NewBuffer_IsEmptyAtTickZero()
        {
            SimWorld world = WorldKit.NewEndless(Db);
            Assert.Equal(0U, world.CreateSnapshotBuffer().Tick);
        }

        [Fact]
        public void LoadAfterSave_ReplaysToSameHashes()
        {
            SimWorld world = WorldKit.NewDuel(Db);
            var buffer = new PlayerInput[16];
            var sink = new RecordingSink();
            uint seq = 0;
            for (int i = 0; i < 20; i++)
            {
                StepOnce(world, buffer, ref seq, sink);
            }
            SimSnapshot snap = world.CreateSnapshotBuffer();
            world.SaveSnapshot(snap);
            Assert.Equal(20U, snap.Tick);
            ulong savedHash = world.ComputeStateHash();
            uint savedSeq = seq;

            var forward = new ulong[30];
            for (int i = 0; i < forward.Length; i++)
            {
                StepOnce(world, buffer, ref seq, sink);
                forward[i] = world.ComputeStateHash();
            }

            world.LoadSnapshot(snap);
            Assert.Equal(20U, world.Tick);
            Assert.Equal(savedHash, world.ComputeStateHash());
            seq = savedSeq;
            for (int i = 0; i < forward.Length; i++)
            {
                StepOnce(world, buffer, ref seq, sink);
                Assert.Equal(forward[i], world.ComputeStateHash());
            }
        }

        [Fact]
        public void Snapshot_IsAValueCopy()
        {
            SimWorld world = WorldKit.NewEndless(Db);
            SimSnapshot snap = world.CreateSnapshotBuffer();
            world.SaveSnapshot(snap);
            ulong h = world.ComputeStateHash();
            // 저장한 뒤 세계를 바꿔도 스냅숏은 그대로다
            world.HeldRng(RngStream.WaveSpawn).NextU32();
            world.NewEntityId();
            world.Step(ReadOnlySpan<PlayerInput>.Empty, ReadOnlySpan<RunCommand>.Empty, new RecordingSink());
            Assert.Equal(0U, snap.Tick);
            world.LoadSnapshot(snap);
            Assert.Equal(h, world.ComputeStateHash());
            // 불러온 뒤 세계를 바꿔도 스냅숏은 그대로다
            world.HeldRng(RngStream.WaveSpawn).NextU32();
            world.LoadSnapshot(snap);
            Assert.Equal(h, world.ComputeStateHash());
        }

        [Fact]
        public void Snapshot_MovesBetweenWorldsOfTheSameShape()
        {
            SimWorld confirmed = WorldKit.NewDuel(Db, 77UL);
            SimWorld predicted = WorldKit.NewDuel(Db, 77UL);
            var buffer = new PlayerInput[16];
            var sink = new RecordingSink();
            uint seq = 0;
            for (int i = 0; i < 12; i++)
            {
                StepOnce(confirmed, buffer, ref seq, sink);
            }
            SimSnapshot snap = predicted.CreateSnapshotBuffer();
            confirmed.SaveSnapshot(snap);
            predicted.LoadSnapshot(snap);
            Assert.Equal(confirmed.ComputeStateHash(), predicted.ComputeStateHash());
            Assert.Equal(12U, predicted.Tick);
        }

        [Fact]
        public void Snapshot_RejectsWorldsOfAnotherShape()
        {
            SimWorld duel = WorldKit.NewDuel(Db, 1UL);
            SimSnapshot snap = duel.CreateSnapshotBuffer();
            Assert.Throws<ArgumentException>(() => WorldKit.NewEndless(Db, 1UL).SaveSnapshot(snap));
            Assert.Throws<ArgumentException>(() => WorldKit.NewDuel(Db, 2UL).LoadSnapshot(snap));
            Assert.Throws<ArgumentException>(() => WorldKit.NewDuel(WorldKit.EmptyDb(), 1UL).LoadSnapshot(snap));
            Assert.Throws<ArgumentNullException>(() => duel.SaveSnapshot(null!));
        }

        [Fact]
        public void SaveAndLoad_AllocateNothing()
        {
            SimWorld world = WorldKit.NewDuel(Db);
            SimSnapshot snap = world.CreateSnapshotBuffer();
            world.SaveSnapshot(snap);
            world.LoadSnapshot(snap);

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++)
            {
                world.SaveSnapshot(snap);
                world.LoadSnapshot(snap);
            }
            Assert.Equal(0L, GC.GetAllocatedBytesForCurrentThread() - before);
        }
    }
}

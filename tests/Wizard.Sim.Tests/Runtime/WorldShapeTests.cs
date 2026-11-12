using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using Wizard.Content;
using Wizard.Core;
using Wizard.Sim.Duel;
using Wizard.Sim.Input;
using Xunit;

namespace Wizard.Sim.Tests.Runtime
{
    // T-05 플레이어 배열, CONTRACTS 3.2 Step 할당 0, 결정론(두 번 실행, 여러 세계 동시 실행), 가변 정적 상태 없음(T-42)
    public sealed class WorldShapeTests
    {
        private static readonly ContentDb Db = WorldKit.EmptyDb();

        [Fact]
        public void PlayerArray_LengthByMode()
        {
            SimWorld endless = WorldKit.NewEndless(Db);
            SimWorld duel = WorldKit.NewDuel(Db);
            SimWorld practice = WorldKit.NewPractice(Db);
            Assert.Equal(1, endless.PlayerCount);
            Assert.Equal(2, duel.PlayerCount);
            Assert.Equal(1, practice.PlayerCount);
            Assert.Equal(SimMode.Endless, endless.Mode);
            Assert.Equal(SimMode.Duel, duel.Mode);
            Assert.Equal(SimMode.Practice, practice.Mode);
            Assert.Equal(2, duel.View.PlayerCount);
            for (int i = 0; i < duel.PlayerCount; i++)
            {
                Assert.Equal(new PlayerSlot((byte)i), duel.SlotAt(i));
            }
            duel.View.Player(new PlayerSlot(1));
            Assert.Throws<ArgumentOutOfRangeException>(() => endless.View.Player(new PlayerSlot(1)));
        }

        [Fact]
        public void Create_ValidatesArguments()
        {
            SimConfig endlessConfig = SimConfig.ForEndless(Db);
            SimConfig duelConfig = SimConfig.ForDuel(Db, new DuelRoomOptions());
            Assert.Throws<ArgumentException>(() => SimWorld.CreateEndless(Db, duelConfig, WorldKit.Endless(1)));
            Assert.Throws<ArgumentException>(() => SimWorld.CreateDuel(Db, endlessConfig, WorldKit.Duel(1)));
            Assert.Throws<ArgumentNullException>(() => SimWorld.CreateEndless(null!, endlessConfig, WorldKit.Endless(1)));
            Assert.Throws<ArgumentNullException>(() => SimWorld.CreateEndless(Db, endlessConfig, null!));

            DuelSetup one = WorldKit.Duel(1);
            one.Players = new[] { one.Players[0] };
            Assert.Throws<ArgumentException>(() => SimWorld.CreateDuel(Db, duelConfig, one));
            DuelSetup swapped = WorldKit.Duel(1);
            swapped.Players = new[] { swapped.Players[1], swapped.Players[0] };
            Assert.Throws<ArgumentException>(() => SimWorld.CreateDuel(Db, duelConfig, swapped));
        }

        [Fact]
        public void Outcome_OnlyAfterIsOverAndForItsMode()
        {
            SimWorld endless = SimWorld.CreateEndless(Db, SimConfig.ForEndless(Db), new EndlessSetup { Seed = 3, StartWave = 11 });
            SimWorld duel = WorldKit.NewDuel(Db);
            Assert.False(endless.IsOver);
            Assert.Throws<InvalidOperationException>(() => endless.GetOutcome());
            Assert.Throws<InvalidOperationException>(() => endless.GetDuelOutcome());
            Assert.Throws<InvalidOperationException>(() => duel.GetOutcome());

            endless.Step(ReadOnlySpan<PlayerInput>.Empty, ReadOnlySpan<RunCommand>.Empty, new RecordingSink());
            endless.MarkOver();
            Waves.RunOutcome o = endless.GetOutcome();
            Assert.Equal(1U, o.EndTick);
            Assert.Equal(11, o.StartWave);
            Assert.Equal(endless.ComputeStateHash(), o.FinalStateHash);

            duel.MarkOver();
            DuelOutcome d = duel.GetDuelOutcome();
            Assert.Equal(0U, d.EndTick);
            Assert.Equal(duel.ComputeStateHash(), d.FinalStateHash);
        }

        [Fact]
        public void EntityIds_StartAtOneAndIncrease()
        {
            SimWorld world = WorldKit.NewEndless(Db);
            Assert.Equal(1, world.NewEntityId().Value);
            Assert.Equal(2, world.NewEntityId().Value);
        }

        [Fact]
        public void Step_AllocatesNothing()
        {
            SimWorld world = WorldKit.NewDuel(Db);
            var sink = new RecordingSink(16);
            var buffer = new PlayerInput[16];
            var commands = new RunCommand[2];
            uint seq = 0;

            void One()
            {
                int n = WorldKit.FillInputs(buffer, world.Tick, world.PlayerCount, ref seq);
                commands[0] = WorldKit.Cmd(world.Tick, ++seq, RunCommandOp.Heal);     // 받는 모듈이 없어 거절 이벤트가 나는 길까지 지난다
                sink.Clear();
                world.Step(buffer.AsSpan(0, n), commands.AsSpan(0, 1), sink);
            }

            One();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++)
            {
                One();
            }
            Assert.Equal(0L, GC.GetAllocatedBytesForCurrentThread() - before);

            before = GC.GetAllocatedBytesForCurrentThread();
            ulong h = 0;
            for (int i = 0; i < 100; i++)
            {
                h ^= world.ComputeStateHash();
            }
            Assert.Equal(0L, GC.GetAllocatedBytesForCurrentThread() - before);
            Assert.NotEqual(0UL, h | 1UL);
        }

        private static ulong[] Run(ContentDb db, int ticks)
        {
            SimWorld world = WorldKit.NewDuel(db, 0xABCDEFUL);
            var sink = new RecordingSink(16);
            var buffer = new PlayerInput[16];
            var hashes = new ulong[ticks];
            uint seq = 0;
            for (int t = 0; t < ticks; t++)
            {
                int n = WorldKit.FillInputs(buffer, world.Tick, world.PlayerCount, ref seq);
                if (world.Tick % 5U == 0U)
                {
                    world.HeldRng(RngStream.DuelWild).NextU32();
                    world.NewEntityId();
                }
                world.Step(buffer.AsSpan(0, n), ReadOnlySpan<RunCommand>.Empty, sink);
                sink.Clear();
                hashes[t] = world.ComputeStateHash();
            }
            return hashes;
        }

        [Fact]
        public void TwoRuns_GiveSameHashes()
        {
            Assert.Equal(Run(Db, 300), Run(Db, 300));
        }

        // 세계끼리 나누는 상태가 없다: 여러 세계를 여러 스레드에서 동시에 돌려도 혼자 돌린 것과 같다(T-42)
        [Fact]
        public void ConcurrentWorlds_MatchSingleRun()
        {
            ulong[] single = Run(Db, 400);
            const int Count = 8;
            var results = new ulong[Count][];
            var threads = new Thread[Count];
            for (int i = 0; i < Count; i++)
            {
                int k = i;
                threads[i] = new Thread(() => results[k] = Run(Db, 400));
            }
            foreach (Thread t in threads)
            {
                t.Start();
            }
            foreach (Thread t in threads)
            {
                t.Join();
            }
            foreach (ulong[] r in results)
            {
                Assert.Equal(single, r);
            }
        }

        [Fact]
        public void SimAssembly_HasNoMutableStaticFields()
        {
            Assembly sim = typeof(SimWorld).Assembly;
            var bad = sim.GetTypes()
                .Where(t => t.GetCustomAttribute<CompilerGeneratedAttribute>() == null)
                .SelectMany(t => t.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    .Where(f => !f.IsLiteral && !f.IsInitOnly && f.GetCustomAttribute<CompilerGeneratedAttribute>() == null)
                    .Select(f => t.FullName + "." + f.Name))
                .ToList();
            Assert.Empty(bad);
        }
    }
}

using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using Wizard.Content;
using Wizard.Core;
using Wizard.Sim.Duel;
using Wizard.Sim.Input;
using Wizard.TestKit;
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

        // 결투 세계 하나와 그 세계를 한 틱 나아가게 하는 동작. 받는 모듈이 없는 명령을 넣어 거절 이벤트가 나는 길까지 지난다
        private static Action DuelTicker(out SimWorld world)
        {
            SimWorld w = WorldKit.NewDuel(Db);
            world = w;
            var sink = new RecordingSink(16);
            var buffer = new PlayerInput[16];
            var commands = new RunCommand[2];
            uint seq = 0;
            return () =>
            {
                int n = WorldKit.FillInputs(buffer, w.Tick, w.PlayerCount, ref seq);
                commands[0] = WorldKit.Cmd(w.Tick, ++seq, RunCommandOp.Heal);
                sink.Clear();
                w.Step(buffer.AsSpan(0, n), commands.AsSpan(0, 1), sink);
            };
        }

        // 같은 각본을 새 세계로 여러 번 돌려 가장 작은 값이 0인지 본다(AllocationProbe. 한 번 재기는 병렬 실행에서 가끔 흔들렸다, FIX-1)
        [Fact]
        public void Step_AllocatesNothing()
        {
            SimWorld? last = null;
            long least = AllocationProbe.LeastBytes(() =>
            {
                Action one = DuelTicker(out SimWorld world);
                last = world;
                one();
                return () =>
                {
                    for (int i = 0; i < 1000; i++)
                    {
                        one();
                    }
                };
            }, () => Assert.Equal(1001U, last!.Tick));
            Assert.Equal(0L, least);
        }

        [Fact]
        public void ComputeStateHash_AllocatesNothing()
        {
            ulong h = 0;
            long least = AllocationProbe.LeastBytes(() =>
            {
                Action one = DuelTicker(out SimWorld world);
                for (int i = 0; i < 1001; i++)
                {
                    one();
                }
                h ^= world.ComputeStateHash();
                return () =>
                {
                    for (int i = 0; i < 100; i++)
                    {
                        h ^= world.ComputeStateHash();
                    }
                };
            });
            Assert.Equal(0L, least);
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

        // ── AllocationProbe 자체(FIX-1). TestKit 의 SelfTests 는 이 작업의 소유 경로가 아니어서 이 묶음에 둔다

        private sealed class Target
        {
            public int[]? Buffer;
        }

        // 각본이 실행마다 할당하면 최솟값이 0이 아니다
        [Fact]
        public void AllocationProbe_ScriptThatAllocatesEveryRun_IsNotZero()
        {
            object? keep = null;
            long least = AllocationProbe.LeastBytes(() => () => keep = new byte[100]);
            Assert.True(least >= 100, "least = " + least);
            GC.KeepAlive(keep);
        }

        // 드문 경로(1,000번째 반복에서만)의 할당도 모든 실행에 있으므로 걸린다
        [Fact]
        public void AllocationProbe_RarePathAllocation_IsNotZero()
        {
            object? keep = null;
            long least = AllocationProbe.LeastBytes(() => () =>
            {
                for (int i = 0; i < 1000; i++)
                {
                    if (i == 999)
                    {
                        keep = new object();
                    }
                }
            });
            Assert.True(least > 0, "least = " + least);
            GC.KeepAlive(keep);
        }

        // 대상마다 처음 한 번 만드는 버퍼: 새 대상으로 돌리면 실행마다 할당하므로 걸린다
        [Fact]
        public void AllocationProbe_LazyBufferPerNewTarget_IsNotZero()
        {
            Target? last = null;
            long least = AllocationProbe.LeastBytes(() =>
            {
                var target = new Target();
                last = target;
                return () => target.Buffer ??= new int[16];
            });
            Assert.True(least >= 16 * sizeof(int), "least = " + least);
            Assert.NotNull(last!.Buffer);
        }

        // 한 실행에만 있는 할당(각본과 무관한 일회성 바이트를 흉내 낸다)은 걸러지고, arrange 의 할당은 세지 않는다
        [Fact]
        public void AllocationProbe_OneOffAllocationAndArrange_AreNotCounted()
        {
            int run = 0;
            object? keep = null;
            long least = AllocationProbe.LeastBytes(4, () =>
            {
                var big = new byte[4096];                                        // arrange 의 할당
                int mine = run++;
                return () =>
                {
                    if (mine == 1)
                    {
                        keep = new byte[256];
                    }
                    big[0] = 1;
                };
            });
            Assert.Equal(0L, least);
            Assert.Equal(4, run);
            GC.KeepAlive(keep);
        }

        // verify 는 실행마다 재는 구간 밖에서 불린다
        [Fact]
        public void AllocationProbe_VerifyRunsAfterEachRunOutsideTheWindow()
        {
            int acted = 0;
            var verified = new System.Collections.Generic.List<int>();
            long least = AllocationProbe.LeastBytes(3, () => () => acted++, () => verified.Add(acted));
            Assert.Equal(0L, least);
            Assert.Equal(new[] { 1, 2, 3 }, verified);
        }

        [Fact]
        public void AllocationProbe_RejectsBadArguments()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => AllocationProbe.LeastBytes(1, () => () => { }));
            Assert.Throws<ArgumentNullException>(() => AllocationProbe.LeastBytes(null!));
            Assert.Throws<InvalidOperationException>(() => AllocationProbe.LeastBytes(() => null!));
        }
    }
}

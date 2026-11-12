using System;
using System.Collections.Generic;
using Wizard.Content;
using Wizard.Core;
using Wizard.Sim.Events;
using Wizard.Sim.Input;
using Wizard.Sim.Tests.Player;
using Xunit;

namespace Wizard.Sim.Tests.Proto
{
    // 시제품이 도는 연습 세계의 결정론(CLAUDE.md 8절: 두 번 실행, 스냅숏 저장·복원 뒤 진행, 직렬화 왕복, Step 할당 0).
    // 각본은 시험 쪽 난수로 한 번 만들어 두고(이동, 자세, J, K, 조립 바꾸기) 같은 배열을 여러 세계에 넣는다.
    public sealed class ProtoDeterminismTests
    {
        private const int Ticks = 1800;

        private sealed class Script
        {
            public readonly PlayerInput[][] Inputs = new PlayerInput[Ticks][];
            public readonly RunCommand[][] Commands = new RunCommand[Ticks][];
        }

        private static readonly LogicalKey[] Keys =
        {
            LogicalKey.Up, LogicalKey.Down, LogicalKey.Left, LogicalKey.Right, LogicalKey.Fire, LogicalKey.Skill, LogicalKey.Stance,
        };

        private static Script MakeScript(int seed)
        {
            var rnd = new Random(seed);
            var s = new Script();
            var held = new bool[16];
            uint seq = 0;
            for (int t = 0; t < Ticks; t++)
            {
                var cmds = new List<RunCommand>();
                if (rnd.Next(120) == 0)
                {
                    bool core = rnd.Next(2) == 0;
                    int b = core ? (rnd.Next(2) == 0 ? (int)BlockId.MagicBolt : (int)BlockId.Fireball)
                                 : (rnd.Next(2) == 0 ? 0 : (int)BlockId.SplitThree);
                    cmds.Add(new RunCommand((uint)t, ++seq, ProtoRig.P0, RunCommandOp.SetSlot, core ? 0 : 1, b));
                }
                var keys = new List<PlayerInput>();
                int n = rnd.Next(10) < 7 ? 0 : rnd.Next(1, 4);
                for (int k = 0; k < n; k++)
                {
                    LogicalKey key = Keys[rnd.Next(Keys.Length)];
                    bool down = !held[(int)key];
                    held[(int)key] = down;
                    keys.Add(new PlayerInput(ProtoRig.P0, new InputEvent((uint)t, (byte)(k * 60 + rnd.Next(50)), key, down, 0)));
                }
                PlayerInput[] arr = keys.ToArray();
                InputOrder.Sort(arr);
                for (int k = 0; k < arr.Length; k++)
                {
                    InputEvent e = arr[k].Event;
                    arr[k] = new PlayerInput(ProtoRig.P0, new InputEvent(e.Tick, e.Offset, e.Key, e.Down, ++seq));
                }
                s.Inputs[t] = arr;
                s.Commands[t] = cmds.ToArray();
            }
            return s;
        }

        private static SimWorld NewWorld() =>
            SimWorld.CreatePractice(ProtoRig.Db, ProtoRig.PracticeConfig(), new PracticeSetup());

        private static void StepAt(SimWorld w, Script s, ISimEventSink sink)
        {
            int t = (int)w.Tick;
            w.Step(s.Inputs[t], s.Commands[t], sink);
        }

        // 틱마다(Step 뒤) 해시
        private static ulong[] HashesOf(Script s, ISimEventSink sink)
        {
            SimWorld w = NewWorld();
            var h = new ulong[Ticks];
            for (int t = 0; t < Ticks; t++)
            {
                StepAt(w, s, sink);
                h[t] = w.ComputeStateHash();
            }
            return h;
        }

        [Fact]
        public void SameScriptTwiceGivesTheSameHashEveryTick()
        {
            Script s = MakeScript(7);
            var log = new EventLog();
            ulong[] a = HashesOf(s, log);
            ulong[] b = HashesOf(s, new CountingSink());
            Assert.Equal(a, b);
            // 각본이 시제품의 길을 실제로 지난다
            Assert.True(log.Count(SimEventType.ProjectileSpawned) > 0);
            Assert.True(log.Count(SimEventType.EnemyProjectileFire) > 0);
            Assert.True(log.Count(SimEventType.EnemySpawn) > 1);
            Assert.True(log.Count(SimEventType.ArtCast) > 0);
            Assert.True(log.Count(SimEventType.CompileChanged) > 0);
            Assert.True(log.Count(SimEventType.PlayerHit) + log.Count(SimEventType.GrazeAwarded) > 0);
        }

        [Fact]
        public void SnapshotSaveAdvanceLoadReplaysTheSameHashes()
        {
            Script s = MakeScript(11);
            ulong[] straight = HashesOf(s, new CountingSink());
            var sink = new CountingSink();
            foreach (int at in new[] { 0, 1, 37, 300, 777, 1501 })
            {
                foreach (int k in new[] { 1, 7, 16 })
                {
                    SimWorld w = NewWorld();
                    SimSnapshot snap = w.CreateSnapshotBuffer();
                    while (w.Tick < at)
                    {
                        StepAt(w, s, sink);
                    }
                    w.SaveSnapshot(snap);
                    for (int j = 0; j < k; j++)
                    {
                        StepAt(w, s, sink);
                    }
                    w.LoadSnapshot(snap);
                    Assert.Equal((uint)at, w.Tick);
                    int end = Math.Min(Ticks, at + 40);
                    while (w.Tick < end)
                    {
                        StepAt(w, s, sink);
                        Assert.Equal(straight[w.Tick - 1], w.ComputeStateHash());
                    }
                }
            }
        }

        [Fact]
        public void WriteStateIntoAFreshWorldContinuesTheSame()
        {
            Script s = MakeScript(23);
            var sink = new CountingSink();
            foreach (int at in new[] { 0, 1, 250, 999 })
            {
                SimWorld a = NewWorld();
                while (a.Tick < at)
                {
                    StepAt(a, s, sink);
                }
                var bytes = new ByteWriter();
                a.WriteState(bytes);
                SimWorld b = NewWorld();
                b.ReadState(bytes.ToArray());
                Assert.Equal(a.ComputeStateHash(), b.ComputeStateHash());
                int end = Math.Min(Ticks, at + 300);
                while (a.Tick < end)
                {
                    StepAt(a, s, sink);
                    StepAt(b, s, sink);
                    Assert.Equal(a.ComputeStateHash(), b.ComputeStateHash());
                }
                var wa = new ByteWriter();
                var wb = new ByteWriter();
                a.WriteState(wa);
                b.WriteState(wb);
                Assert.Equal(wa.ToArray(), wb.ToArray());
            }
        }

        [Fact]
        public void PrototypeStateIsInTheHash()
        {
            // 같은 각본에서 조립만 다르게 바꾸면 해시가 갈라진다(조립·탄이 상태 절에 들어간다)
            SimWorld a = NewWorld();
            SimWorld b = NewWorld();
            var sink = new CountingSink();
            a.Step(ReadOnlySpan<PlayerInput>.Empty, ReadOnlySpan<RunCommand>.Empty, sink);
            b.Step(ReadOnlySpan<PlayerInput>.Empty, ReadOnlySpan<RunCommand>.Empty, sink);
            Assert.Equal(a.ComputeStateHash(), b.ComputeStateHash());
            var c = new[] { new RunCommand(1, 1, ProtoRig.P0, RunCommandOp.SetSlot, 1, (int)BlockId.SplitThree) };
            b.Step(ReadOnlySpan<PlayerInput>.Empty, c, sink);
            a.Step(ReadOnlySpan<PlayerInput>.Empty, ReadOnlySpan<RunCommand>.Empty, sink);
            Assert.NotEqual(a.ComputeStateHash(), b.ComputeStateHash());
        }

        // Step 이 관리 힙에 할당하면 같은 각본을 돌릴 때마다 0보다 크다. 새 세계로 세 번 돌려 가장 작은 값을 본다
        // (다른 시험이 병렬로 돌 때 이 스레드에 잡히는 런타임 쪽 일회성 할당을 거른다. GrazePipelineTests 와 같은 방법, FIX-1)
        [Fact]
        public void StepDoesNotAllocateAfterWarmUp()
        {
            Script s = MakeScript(5);
            long least = long.MaxValue;
            for (int run = 0; run < 3; run++)
            {
                SimWorld w = NewWorld();
                var sink = new CountingSink();
                while (w.Tick < 200)
                {
                    StepAt(w, s, sink);                                            // 첫 Step 의 캐시(전장 기하)는 여기서
                }
                long before = GC.GetAllocatedBytesForCurrentThread();
                while (w.Tick < Ticks)
                {
                    StepAt(w, s, sink);
                }
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.True(sink.Count > 0);
                least = Math.Min(least, allocated);
            }
            Assert.Equal(0L, least);
        }
    }
}

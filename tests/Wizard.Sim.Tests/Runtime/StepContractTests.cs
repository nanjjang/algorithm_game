using System;
using Wizard.Content;
using Wizard.Core;
using Wizard.Sim.Events;
using Wizard.Sim.Input;
using Xunit;

namespace Wizard.Sim.Tests.Runtime
{
    // CONTRACTS 3.2 Step 의 전제(모든 빌드에서 검사, 어기면 SimInvariantException)와 0단계 명령 처리, CanApply.
    public sealed class StepContractTests
    {
        private static readonly ContentDb Db = WorldKit.EmptyDb();

        private static void AssertRejectedWithoutChange(SimWorld world, PlayerInput[] inputs, RunCommand[] commands)
        {
            ulong hash = world.ComputeStateHash();
            uint tick = world.Tick;
            var sink = new RecordingSink();
            Assert.Throws<SimInvariantException>(() => world.Step(inputs, commands, sink));
            Assert.Equal(tick, world.Tick);
            Assert.Equal(hash, world.ComputeStateHash());
            Assert.Equal(0, sink.Count);
        }

        [Fact]
        public void Step_AdvancesTickByOne()
        {
            SimWorld world = WorldKit.NewEndless(Db);
            var sink = new RecordingSink();
            Assert.Equal(0U, world.Tick);
            world.Step(ReadOnlySpan<PlayerInput>.Empty, ReadOnlySpan<RunCommand>.Empty, sink);
            world.Step(new[] { WorldKit.In(0, 1, 0, LogicalKey.Up, true, 1) }, ReadOnlySpan<RunCommand>.Empty, sink);
            Assert.Equal(2U, world.Tick);
            Assert.Equal(2U, world.View.Tick);
        }

        [Fact]
        public void Step_RejectsInputOfAnotherTick()
        {
            SimWorld world = WorldKit.NewEndless(Db);
            AssertRejectedWithoutChange(world, new[] { WorldKit.In(0, 1, 0, LogicalKey.Up, true, 1) }, Array.Empty<RunCommand>());
        }

        [Fact]
        public void Step_RejectsInputsOutOfInputOrder()
        {
            SimWorld world = WorldKit.NewEndless(Db);
            // 같은 오프셋이면 방향(분류 1)이 K(분류 3)보다 먼저다. 거꾸로 주면 위반
            AssertRejectedWithoutChange(world, new[]
            {
                WorldKit.In(0, 0, 5, LogicalKey.Skill, true, 1),
                WorldKit.In(0, 0, 5, LogicalKey.Left, true, 2),
            }, Array.Empty<RunCommand>());
            // 오프셋 역행
            AssertRejectedWithoutChange(world, new[]
            {
                WorldKit.In(0, 0, 9, LogicalKey.Up, true, 1),
                WorldKit.In(0, 0, 3, LogicalKey.Down, true, 2),
            }, Array.Empty<RunCommand>());
            // 똑같은 이벤트 둘
            PlayerInput same = WorldKit.In(0, 0, 3, LogicalKey.Down, true, 2);
            AssertRejectedWithoutChange(world, new[] { same, same }, Array.Empty<RunCommand>());

            // 바른 순서는 받는다
            world.Step(new[]
            {
                WorldKit.In(0, 0, 5, LogicalKey.Left, true, 1),
                WorldKit.In(0, 0, 5, LogicalKey.Skill, true, 2),
            }, ReadOnlySpan<RunCommand>.Empty, new RecordingSink());
            Assert.Equal(1U, world.Tick);
        }

        [Fact]
        public void Step_AllowsEightEventsPerPlayerAndRejectsNine()
        {
            SimWorld world = WorldKit.NewDuel(Db);
            // 두 사람이 각각 8개(합 16)는 된다
            var ok = new PlayerInput[16];
            for (int i = 0; i < 8; i++)
            {
                ok[i * 2] = WorldKit.In(0, 0, (byte)i, LogicalKey.Up, (i & 1) == 0, (uint)(i + 1));
                ok[i * 2 + 1] = WorldKit.In(1, 0, (byte)i, LogicalKey.Up, (i & 1) == 0, (uint)(i + 1));
            }
            world.Step(ok, ReadOnlySpan<RunCommand>.Empty, new RecordingSink());
            Assert.Equal(1U, world.Tick);

            var nine = new PlayerInput[9];
            for (int i = 0; i < 9; i++)
            {
                nine[i] = WorldKit.In(1, 1, (byte)i, LogicalKey.Up, (i & 1) == 0, (uint)(i + 1));
            }
            AssertRejectedWithoutChange(world, nine, Array.Empty<RunCommand>());
        }

        [Fact]
        public void Step_RejectsSlotOutsideTheWorldAndUnknownKey()
        {
            SimWorld world = WorldKit.NewEndless(Db);
            AssertRejectedWithoutChange(world, new[] { WorldKit.In(1, 0, 0, LogicalKey.Up, true, 1) }, Array.Empty<RunCommand>());
            AssertRejectedWithoutChange(world, new[] { new PlayerInput(PlayerSlot.None, new InputEvent(0, 0, LogicalKey.Up, true, 1)) },
                Array.Empty<RunCommand>());
            AssertRejectedWithoutChange(world, new[] { WorldKit.In(0, 0, 0, (LogicalKey)9, true, 1) }, Array.Empty<RunCommand>());
            AssertRejectedWithoutChange(world, Array.Empty<PlayerInput>(), new[] { WorldKit.Cmd(0, 1, RunCommandOp.Heal, slot: 1) });
        }

        [Fact]
        public void Step_RejectsCommandsOfAnotherTickOrOutOfSeqOrder()
        {
            SimWorld world = WorldKit.NewEndless(Db);
            AssertRejectedWithoutChange(world, Array.Empty<PlayerInput>(), new[] { WorldKit.Cmd(1, 1, RunCommandOp.Heal) });
            AssertRejectedWithoutChange(world, Array.Empty<PlayerInput>(), new[]
            {
                WorldKit.Cmd(0, 5, RunCommandOp.Heal),
                WorldKit.Cmd(0, 4, RunCommandOp.Depart),
            });
            AssertRejectedWithoutChange(world, Array.Empty<PlayerInput>(), new[]
            {
                WorldKit.Cmd(0, 5, RunCommandOp.Heal),
                WorldKit.Cmd(0, 5, RunCommandOp.Depart),
            });
            // 명령 검사가 입력 검사와 함께 상태를 바꾸기 전에 끝난다: 명령이 맞아도 입력이 틀리면 명령은 적용되지 않는다
            AssertRejectedWithoutChange(world, new[] { WorldKit.In(0, 7, 0, LogicalKey.Up, true, 2) },
                new[] { WorldKit.Cmd(0, 1, RunCommandOp.Heal) });
        }

        [Fact]
        public void Step_NullSinkIsAnArgumentError()
        {
            SimWorld world = WorldKit.NewEndless(Db);
            Assert.Throws<ArgumentNullException>(() => world.Step(ReadOnlySpan<PlayerInput>.Empty, ReadOnlySpan<RunCommand>.Empty, null!));
        }

        // 받는 모듈이 없는 명령은 적용하지 않고 DebugCommandRejected(A = op, B = 순번)만 낸다(CONTRACTS 3.1)
        [Fact]
        public void UnclaimedCommand_IsRejectedWithDebugEvent()
        {
            SimWorld world = WorldKit.NewEndless(Db);
            var sink = new RecordingSink();
            world.Step(ReadOnlySpan<PlayerInput>.Empty, new[]
            {
                WorldKit.Cmd(0, 3, RunCommandOp.PickCard, a: 1),
                WorldKit.Cmd(0, 4, RunCommandOp.DebugSkipWave),
            }, sink);
            Assert.Equal(2, sink.Count);
            Assert.Equal(SimEventType.DebugCommandRejected, sink[0].Type);
            Assert.Equal(new PlayerSlot(0), sink[0].Slot);
            Assert.Equal(0U, sink[0].Tick);
            Assert.Equal((int)RunCommandOp.PickCard, sink[0].A);
            Assert.Equal(3, sink[0].B);
            Assert.Equal((int)RunCommandOp.DebugSkipWave, sink[1].A);
            Assert.Equal(4, sink[1].B);
            Assert.Equal(1U, world.Tick);
        }

        [Fact]
        public void CanApply_DoesNotChangeStateAndRejectsDebugOpsWhenDisabled()
        {
            SimWorld world = WorldKit.NewEndless(Db);
            Assert.False(world.Config.AllowDebugOps);
            ulong hash = world.ComputeStateHash();
            foreach (RunCommandOp op in Enum.GetValues(typeof(RunCommandOp)))
            {
                Assert.False(world.CanApply(WorldKit.Cmd(0, 1, op)));
            }
            Assert.False(world.CanApply(WorldKit.Cmd(0, 1, RunCommandOp.Heal, slot: 3)));        // 이 세계에 없는 슬롯
            Assert.Equal(hash, world.ComputeStateHash());
            Assert.Equal(0U, world.Tick);

            SimWorld practice = WorldKit.NewPractice(Db);
            Assert.True(practice.Config.AllowDebugOps);
            ulong ph = practice.ComputeStateHash();
            Assert.False(practice.CanApply(WorldKit.Cmd(0, 1, RunCommandOp.DebugInvulnerable)));  // 받는 모듈(Runtime/Debug)이 아직 없다
            Assert.Equal(ph, practice.ComputeStateHash());
        }
    }
}

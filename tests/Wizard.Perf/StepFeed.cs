using System;
using Wizard.Core;
using Wizard.Sim;
using Wizard.Sim.Events;
using Wizard.Sim.Input;

namespace Wizard.Perf
{
    // 각본을 Step 에 그대로 넣을 열로 바꿔 둔 것. 틱 t 의 키는 InputOrder 로 (안정) 정렬한 뒤 플레이어별 순번을 매긴다.
    // 명령은 같은 틱의 그 플레이어 키보다 앞 순번이다(InputRecorder 와 같은 규칙, CONTRACTS 3.1·3.8).
    // StepAt 은 배열의 구간만 넘기므로 관리 힙에 할당하지 않는다(벤치가 재는 할당은 Step 의 것뿐이다).
    internal sealed class StepFeed
    {
        private readonly PlayerInput[] _inputs;
        private readonly int[] _inputStart;
        private readonly RunCommand[] _commands;
        private readonly int[] _commandStart;

        private StepFeed(int ticks, PlayerInput[] inputs, int[] inputStart, RunCommand[] commands, int[] commandStart)
        {
            Ticks = ticks;
            _inputs = inputs;
            _inputStart = inputStart;
            _commands = commands;
            _commandStart = commandStart;
        }

        public int Ticks { get; }

        public static StepFeed From(KeyScript script)
        {
            int ticks = script.Ticks;
            var inputs = new PlayerInput[script.KeyCountTotal];
            var inputStart = new int[ticks + 1];
            int commandTotal = 0;
            for (int t = 0; t < ticks; t++)
            {
                commandTotal += script.DraftsAt(t).Length;
            }
            var commands = new RunCommand[commandTotal];
            var commandStart = new int[ticks + 1];
            var seq = new uint[script.Players];
            int ni = 0, nc = 0;
            for (int t = 0; t < ticks; t++)
            {
                inputStart[t] = ni;
                commandStart[t] = nc;
                ReadOnlySpan<RunCommandDraft> drafts = script.DraftsAt(t);
                for (int i = 0; i < drafts.Length; i++)
                {
                    commands[nc++] = new RunCommand((uint)t, ++seq[0], new PlayerSlot(0), drafts[i].Op, drafts[i].A, drafts[i].B);
                }
                ReadOnlySpan<ScriptKey> keys = script.KeysAt(t);
                int first = ni;
                for (int i = 0; i < keys.Length; i++)
                {
                    ScriptKey k = keys[i];
                    inputs[ni++] = new PlayerInput(new PlayerSlot(k.Slot), new InputEvent(k.Tick, k.Offset, k.Key, k.Down, 0));
                }
                Span<PlayerInput> tick = new Span<PlayerInput>(inputs, first, ni - first);
                InputOrder.Sort(tick);
                for (int i = 0; i < tick.Length; i++)
                {
                    PlayerInput p = tick[i];
                    tick[i] = new PlayerInput(p.Slot, new InputEvent(p.Event.Tick, p.Event.Offset, p.Event.Key, p.Event.Down, ++seq[p.Slot.Value]));
                }
            }
            inputStart[ticks] = ni;
            commandStart[ticks] = nc;
            return new StepFeed(ticks, inputs, inputStart, commands, commandStart);
        }

        // 틱 t 에 0단계 명령이 있는가. 0단계의 주문 컴파일은 Step 할당 0의 유일한 예외라(CONTRACTS 3.2 SpellTable) 할당은 이 틱을 따로 센다
        public bool HasCommandsAt(uint tick) => tick < (uint)Ticks && _commandStart[tick + 1] > _commandStart[tick];

        // world.Tick 의 열로 한 틱 진행한다. 각본보다 멀리 가면 InvalidOperationException(벤치 설정의 결함)
        public void StepAt(SimWorld world, ISimEventSink sink)
        {
            uint t = world.Tick;
            if (t >= (uint)Ticks)
            {
                throw new InvalidOperationException("perf: the script is shorter than the run");
            }
            int i0 = _inputStart[t], i1 = _inputStart[t + 1];
            int c0 = _commandStart[t], c1 = _commandStart[t + 1];
            world.Step(new ReadOnlySpan<PlayerInput>(_inputs, i0, i1 - i0), new ReadOnlySpan<RunCommand>(_commands, c0, c1 - c0), sink);
        }
    }
}

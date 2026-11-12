using System;
using System.Collections.Generic;
using Wizard.Content;
using Wizard.Core;
using Wizard.Sim;
using Wizard.Sim.Input;

namespace Wizard.BalanceLab.Core
{
    // 연습 세계 하나를 틱 단위로 돌리는 도구용 장치(봇 입력). 세계 설정은 PracticeSession 과 같다(ForPractice(ForEndless)).
    // 한 틱의 명령은 키보다 앞 순번, 키는 InputOrder 로 정렬한 뒤 순번을 매긴다(CONTRACTS 3.1).
    internal sealed class PracticeRig
    {
        public static readonly PlayerSlot Slot0 = new PlayerSlot(0);

        private readonly List<RunCommand> _commands = new List<RunCommand>();
        private readonly List<PlayerInput> _inputs = new List<PlayerInput>();
        private uint _seq;

        public PracticeRig(ContentDb db)
        {
            World = NewWorld(db);
        }

        public SimWorld World { get; }

        public EventLog Log { get; } = new EventLog();

        public uint Tick => World.Tick;

        public static SimWorld NewWorld(ContentDb db) =>
            SimWorld.CreatePractice(db, SimConfig.ForPractice(db, SimConfig.ForEndless(db)), new PracticeSetup());

        public void Command(RunCommandOp op, int a, int b) => _commands.Add(new RunCommand(World.Tick, 0, Slot0, op, a, b));

        public void Key(LogicalKey key, bool down, byte offset = 0) =>
            _inputs.Add(new PlayerInput(Slot0, new InputEvent(World.Tick, offset, key, down, 0)));

        public void Step()
        {
            var commands = new RunCommand[_commands.Count];
            for (int i = 0; i < commands.Length; i++)
            {
                RunCommand c = _commands[i];
                commands[i] = new RunCommand(c.Tick, ++_seq, c.Slot, c.Op, c.A, c.B);
            }
            PlayerInput[] inputs = _inputs.ToArray();
            InputOrder.Sort(inputs);
            for (int i = 0; i < inputs.Length; i++)
            {
                InputEvent e = inputs[i].Event;
                inputs[i] = new PlayerInput(Slot0, new InputEvent(e.Tick, e.Offset, e.Key, e.Down, ++_seq));
            }
            _commands.Clear();
            _inputs.Clear();
            World.Step(inputs, commands, Log);
        }
    }
}

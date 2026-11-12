using System;
using System.Collections.Generic;
using Wizard.Core;
using Wizard.Sim.Events;
using Wizard.Sim.Input;

namespace Wizard.Sim.Tests.Determinism
{
    // 입력 벡터(spec 01 14.2) 시험의 작은 몰이꾼. 한 틱의 키를 받아 InputOrder 로 정렬하고 순번을 매겨(InputRecorder 와 같은 순서) Step 한다.
    // 세계는 저장소 팩의 연습 세계(판정기와 임시 기술 ①②⑥이 붙는 유일한 세계, M1-SIM-09)다.
    internal sealed class DetRig : ISimEventSink
    {
        public static readonly PlayerSlot P0 = new PlayerSlot(0);

        public readonly SimWorld World;
        public readonly List<SimEvent> Events = new List<SimEvent>();
        private uint _seq;

        public DetRig()
        {
            World = SimWorld.CreatePractice(DetKit.Db, SimConfig.ForPractice(DetKit.Db, SimConfig.ForEndless(DetKit.Db)), new PracticeSetup());
        }

        public static (LogicalKey Key, bool Down, byte Offset) Press(LogicalKey key, byte offset = 0) => (key, true, offset);

        public static (LogicalKey Key, bool Down, byte Offset) Release(LogicalKey key, byte offset = 0) => (key, false, offset);

        public void Emit(in SimEvent e) => Events.Add(e);

        // 이번 틱의 키(순서는 아무렇게나. 정렬해서 넣는다)
        public void Step(params (LogicalKey Key, bool Down, byte Offset)[] keys)
        {
            var inputs = new PlayerInput[keys.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                inputs[i] = new PlayerInput(P0, new InputEvent(World.Tick, keys[i].Offset, keys[i].Key, keys[i].Down, 0));
            }
            InputOrder.Sort(inputs);
            for (int i = 0; i < inputs.Length; i++)
            {
                InputEvent e = inputs[i].Event;
                inputs[i] = new PlayerInput(P0, new InputEvent(e.Tick, e.Offset, e.Key, e.Down, ++_seq));
            }
            World.Step(inputs, ReadOnlySpan<RunCommand>.Empty, this);
        }

        public void RunTo(uint tick)
        {
            while (World.Tick < tick)
            {
                Step();
            }
        }

        public List<SimEvent> Of(SimEventType type) => Events.FindAll(e => e.Type == type);

        public List<SimEvent> At(SimEventType type, uint tick) => Events.FindAll(e => e.Type == type && e.Tick == tick);
    }
}

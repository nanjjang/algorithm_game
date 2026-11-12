using System;
using System.Linq;
using System.Security.Cryptography;
using Wizard.Content;
using Wizard.Core;
using Wizard.Sim.Duel;
using Wizard.Sim.Events;
using Wizard.Sim.Input;

namespace Wizard.Sim.Tests.Runtime
{
    // 뼈대 시험의 도우미. 절이 하나도 없는 팩(CONTRACTS 11.2 형식)으로 세계를 만든다. 뼈대는 팩을 읽지 않는다
    internal static class WorldKit
    {
        public static ContentDb EmptyDb()
        {
            var w = new ByteWriter();
            w.U8((byte)'W'); w.U8((byte)'Z'); w.U8((byte)'P'); w.U8((byte)'K');
            w.U16(1);
            w.U32(1);
            for (int i = 0; i < 16; i++)
            {
                w.U8(0);
            }
            w.U16(0);
            byte[] head = w.ToArray();
            byte[] all = head.Concat(SHA256.HashData(head)).ToArray();
            return ContentDb.Build(ContentPack.Load(all));
        }

        public static EndlessSetup Endless(ulong seed) => new EndlessSetup { Seed = seed, StartWave = 1 };

        public static SimWorld NewEndless(ContentDb db, ulong seed = 0x1234_5678_9ABC_DEF0UL) =>
            SimWorld.CreateEndless(db, SimConfig.ForEndless(db), Endless(seed));

        public static DuelSetup Duel(ulong seed) => new DuelSetup
        {
            Seed = seed,
            Players = new[]
            {
                new DuelPlayerSetup { Slot = new PlayerSlot(0) },
                new DuelPlayerSetup { Slot = new PlayerSlot(1) },
            },
        };

        public static SimWorld NewDuel(ContentDb db, ulong seed = 0xDEC0_DE00_0000_0001UL) =>
            SimWorld.CreateDuel(db, SimConfig.ForDuel(db, new DuelRoomOptions()), Duel(seed));

        public static SimWorld NewPractice(ContentDb db) =>
            SimWorld.CreatePractice(db, SimConfig.ForPractice(db, SimConfig.ForEndless(db)), new PracticeSetup());

        public static PlayerInput In(byte slot, uint tick, byte offset, LogicalKey key, bool down, uint seq) =>
            new PlayerInput(new PlayerSlot(slot), new InputEvent(tick, offset, key, down, seq));

        public static RunCommand Cmd(uint tick, uint seq, RunCommandOp op, byte slot = 0, int a = 0, int b = 0) =>
            new RunCommand(tick, seq, new PlayerSlot(slot), op, a, b);

        // 결정론 시험용 입력: 틱마다 정해진 꼴(누름·뗌 번갈아). 같은 틱의 이벤트는 InputOrder 순이다
        public static int FillInputs(PlayerInput[] buffer, uint tick, int playerCount, ref uint seq)
        {
            int n = 0;
            for (int p = 0; p < playerCount; p++)
            {
                bool down = (tick & 1U) == 0U;
                buffer[n++] = In((byte)p, tick, (byte)(tick & 0x7FU), LogicalKey.Up, down, ++seq);
                buffer[n++] = In((byte)p, tick, (byte)((tick & 0x7FU) + 1U), LogicalKey.Fire, down, ++seq);
            }
            InputOrder.Sort(buffer.AsSpan(0, n));
            return n;
        }
    }

    // 미리 잡은 배열에 담는 sink(할당 없음)
    internal sealed class RecordingSink : ISimEventSink
    {
        private readonly SimEvent[] _events;

        public RecordingSink(int capacity = 1024)
        {
            _events = new SimEvent[capacity];
        }

        public int Count { get; private set; }

        public SimEvent this[int index] => _events[index];

        public void Clear() => Count = 0;

        public void Emit(in SimEvent e)
        {
            if (Count < _events.Length)
            {
                _events[Count] = e;
            }
            Count++;
        }
    }
}

using System;
using System.Collections.Generic;
using Wizard.Content;
using Wizard.Core;
using Wizard.Sim.Events;
using Wizard.Sim.Input;
using Wizard.Sim.Player;
using Wizard.Sim.Tests.World;
using Xunit;

namespace Wizard.Sim.Tests.Player
{
    // Player 시험의 도우미. 저장소 data/ 로 만든 팩(기준값 캐릭터, 한뎃벌·돌마당)으로 세계를 만들고,
    // 틱마다 키 이벤트를 InputOrder 로 정렬해 순번을 매긴 뒤(CONTRACTS 3.1: 정렬한 뒤 순번) Step 한다.
    internal sealed class PlayerRig
    {
        public readonly SimWorld World;
        public readonly EventLog Log = new EventLog();
        private uint _seq;

        private PlayerRig(SimWorld world)
        {
            World = world;
        }

        public static ContentDb Db => WorldData.RepoDb;

        public static PlayerRig Endless(ulong seed = 1UL) =>
            new PlayerRig(SimWorld.CreateEndless(Db, SimConfig.ForEndless(Db), new EndlessSetup { Seed = seed, StartWave = 1 }));

        public static PlayerRig Duel(ulong seed = 1UL) =>
            new PlayerRig(SimWorld.CreateDuel(Db, SimConfig.ForDuel(Db, new Wizard.Sim.Duel.DuelRoomOptions()), new DuelSetup
            {
                Seed = seed,
                Players = new[]
                {
                    new DuelPlayerSetup { Slot = new PlayerSlot(0) },
                    new DuelPlayerSetup { Slot = new PlayerSlot(1) },
                },
            }));

        public PlayerState P => World.PlayerAt(0);

        public PlayerInputResult LastInput => World.PlayerLastInput(0);

        public uint Tick => World.Tick;

        // 이번 틱(World.Tick)의 키 이벤트로 한 틱 진행한다. 슬롯 0
        public void Step(params Key[] keys) => StepSlots(Array.ConvertAll(keys, k => (k, (byte)0)));

        public void StepSlots(params (Key key, byte slot)[] keys)
        {
            var inputs = new PlayerInput[keys.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                Key k = keys[i].key;
                inputs[i] = new PlayerInput(new PlayerSlot(keys[i].slot), new InputEvent(World.Tick, k.Offset, k.LogicalKey, k.Down, (uint)(i + 1)));
            }
            InputOrder.Sort(inputs);
            for (int i = 0; i < inputs.Length; i++)
            {
                InputEvent e = inputs[i].Event;
                inputs[i] = new PlayerInput(inputs[i].Slot, new InputEvent(e.Tick, e.Offset, e.Key, e.Down, ++_seq));
            }
            World.Step(inputs, ReadOnlySpan<RunCommand>.Empty, Log);
        }

        // 키 없이 World.Tick == tick 이 될 때까지
        public void RunTo(uint tick)
        {
            Assert.True(World.Tick <= tick);
            while (World.Tick < tick)
            {
                Step();
            }
        }

        public static Key Press(LogicalKey key, byte offset = 0) => new Key(key, true, offset);

        public static Key Release(LogicalKey key, byte offset = 0) => new Key(key, false, offset);
    }

    internal readonly struct Key
    {
        public readonly LogicalKey LogicalKey;
        public readonly bool Down;
        public readonly byte Offset;

        public Key(LogicalKey key, bool down, byte offset)
        {
            LogicalKey = key;
            Down = down;
            Offset = offset;
        }
    }

    // 표시 이벤트를 모두 담는다(시험용, 할당 시험에서는 쓰지 않는다)
    internal sealed class EventLog : ISimEventSink
    {
        public readonly List<SimEvent> Events = new List<SimEvent>();

        public void Emit(in SimEvent e) => Events.Add(e);

        public void Clear() => Events.Clear();

        public int Count(SimEventType type) => Events.FindAll(e => e.Type == type).Count;

        public List<SimEvent> Of(SimEventType type) => Events.FindAll(e => e.Type == type);

        public SimEvent Single(SimEventType type)
        {
            List<SimEvent> all = Of(type);
            Assert.Single(all);
            return all[0];
        }
    }

    // 고정 크기 sink(할당 시험용)
    internal sealed class CountingSink : ISimEventSink
    {
        public int Count;

        public void Emit(in SimEvent e) => Count++;
    }
}

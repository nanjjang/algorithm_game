using System;
using System.Collections.Generic;
using Wizard.Content;
using Wizard.Core;
using Wizard.Sim.Events;
using Wizard.Sim.Input;
using Wizard.Sim.Proto;
using Wizard.Sim.Tests.Player;
using Wizard.Sim.Tests.World;
using Xunit;

namespace Wizard.Sim.Tests.Proto
{
    // 시제품(M1-SIM-09) 시험의 도우미. 저장소 data/ 로 만든 팩으로 연습 세계를 만들고, 틱마다 명령과 키를 넣어 Step 한다.
    // 키는 InputOrder 로 정렬한 뒤 순번을 매긴다(CONTRACTS 3.1). 명령은 같은 틱의 키보다 앞 순번이다.
    internal sealed class ProtoRig
    {
        public static readonly PlayerSlot P0 = new PlayerSlot(0);

        public readonly SimWorld World;
        public readonly EventLog Log = new EventLog();
        private uint _seq;
        private readonly List<RunCommand> _commands = new List<RunCommand>();

        public ProtoRig(SimWorld world)
        {
            World = world;
        }

        public static ContentDb Db => WorldData.RepoDb;

        public static SimConfig PracticeConfig() => SimConfig.ForPractice(Db, SimConfig.ForEndless(Db));

        public static ProtoRig Practice(SimConfig? config = null) =>
            new ProtoRig(SimWorld.CreatePractice(Db, config ?? PracticeConfig(), new PracticeSetup()));

        public uint Tick => World.Tick;

        public Wizard.Sim.Player.PlayerState P => World.PlayerAt(0);

        // 다음 Step 에 넣을 명령(이번 틱)
        public void Command(RunCommandOp op, int a, int b) =>
            _commands.Add(new RunCommand(World.Tick, ++_seq, P0, op, a, b));

        public void SetSlot(int slot, BlockId block) => Command(RunCommandOp.SetSlot, slot, (int)block);

        public void Step(params Key[] keys)
        {
            var inputs = new PlayerInput[keys.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                inputs[i] = new PlayerInput(P0, new InputEvent(World.Tick, keys[i].Offset, keys[i].LogicalKey, keys[i].Down, 0));
            }
            InputOrder.Sort(inputs);
            for (int i = 0; i < inputs.Length; i++)
            {
                InputEvent e = inputs[i].Event;
                inputs[i] = new PlayerInput(P0, new InputEvent(e.Tick, e.Offset, e.Key, e.Down, ++_seq));
            }
            RunCommand[] commands = _commands.ToArray();
            _commands.Clear();
            World.Step(inputs, commands, Log);
        }

        public void RunTo(uint tick)
        {
            Assert.True(World.Tick <= tick);
            while (World.Tick < tick)
            {
                Step();
            }
        }

        // 키를 한 틱 눌렀다 다음 틱에 뗀다(두 Step)
        public void Tap(LogicalKey key, byte offset = 0)
        {
            Step(PlayerRig.Press(key, offset));
            Step(PlayerRig.Release(key));
        }

        public EntityId PlaceSlime(long xMilli, long yMilli) =>
            World.ProtoPlaceSlime(new FixVec2(Fix64.FromRatio(xMilli, 1000), Fix64.FromRatio(yMilli, 1000)), Log);

        public int SlimeIndex(EntityId id)
        {
            for (int i = 0; i < World.ProtoEnemyCount; i++)
            {
                if (World.ProtoEnemyAt(i).Id == id)
                {
                    return i;
                }
            }
            return -1;
        }

        public List<SimEvent> At(SimEventType type, uint tick) => Log.Events.FindAll(e => e.Type == type && e.Tick == tick);

        public static Fix64 Milli(long milli) => Fix64.FromRatio(milli, 1000);

        public static Fix64 Cells(long whole) => Fix64.FromInt((int)whole);

        public static ProtoEnemyView Turret(SimWorld w)
        {
            for (int i = 0; i < ProtoReadout.EnemyCount(w); i++)
            {
                ProtoEnemyView v = ProtoReadout.Enemy(w, i);
                if (v.Kind == ProtoEnemyKind.Turret)
                {
                    return v;
                }
            }
            throw new InvalidOperationException("no turret");
        }
    }
}

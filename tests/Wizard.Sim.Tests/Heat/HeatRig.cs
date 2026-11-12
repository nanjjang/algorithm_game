using System;
using System.Collections.Generic;
using Wizard.Content;
using Wizard.Core;
using Wizard.Sim.Events;
using Wizard.Sim.Heat;
using Wizard.Sim.Input;
using Wizard.Sim.Tests.Player;
using Xunit;

namespace Wizard.Sim.Tests.Heat
{
    // 과열 시험의 도우미. 세계를 만들고 J 발사 주문(코어 블록의 열과 간격, 팩에서 읽는다)을 넣은 뒤,
    // 틱마다 J 를 누르거나 떼며 Step 한다. 슬롯마다 J 를 따로 다룬다(결투는 두 명).
    internal sealed class HeatRig
    {
        public readonly SimWorld World;
        public readonly ContentDb Db;
        public readonly EventLog Log = new EventLog();
        private readonly bool[] _jDown;
        private uint _seq;

        private HeatRig(ContentDb db, SimWorld world)
        {
            Db = db;
            World = world;
            _jDown = new bool[world.PlayerCount];
        }

        public static HeatRig Endless(ContentDb db, BlockId core = BlockId.MagicBolt, ulong seed = 1UL)
        {
            var rig = new HeatRig(db, SimWorld.CreateEndless(db, SimConfig.ForEndless(db), new EndlessSetup { Seed = seed, StartWave = 1 }));
            rig.Arm(0, core);
            return rig;
        }

        public static HeatRig Duel(ContentDb db, BlockId core = BlockId.MagicBolt, ulong seed = 1UL)
        {
            var setup = new DuelSetup
            {
                Seed = seed,
                Players = new[] { new DuelPlayerSetup { Slot = new PlayerSlot(0) }, new DuelPlayerSetup { Slot = new PlayerSlot(1) } },
            };
            var rig = new HeatRig(db, SimWorld.CreateDuel(db, SimConfig.ForDuel(db, new Wizard.Sim.Duel.DuelRoomOptions()), setup));
            rig.Arm(0, core);
            rig.Arm(1, core);
            return rig;
        }

        // 광장의 발사 스위치(SimConfig.FireEnabled = false, spec 01 7.3)만 끈 무한 웨이브 세계
        public static HeatRig FireDisabled(ContentDb db)
        {
            SimConfig config = SimConfig.ForEndless(db);
            config.FireEnabled = false;
            var rig = new HeatRig(db, SimWorld.CreateEndless(db, config, new EndlessSetup { Seed = 1UL, StartWave = 1 }));
            rig.Arm(0, BlockId.MagicBolt);
            return rig;
        }

        // 코어 블록 하나짜리 주문: 열 = 블록 열, 간격 = 블록 간격(spec 02 4.2, spec 01 2.5)
        public void Arm(int slot, BlockId core)
        {
            BlockDef d = Db.Block(core);
            World.SetHeatShot(new PlayerSlot((byte)slot), core, d.HeatHu, d.IntervalTicks);
        }

        public HeatState H => World.HeatAt(0);

        public HeatState HeatOf(int slot) => World.HeatAt(slot);

        public uint Tick => World.Tick;

        public bool JDown(int slot = 0) => _jDown[slot];

        // 이번 틱(World.Tick)에 슬롯 0의 J 를 wantDown 으로 맞추고 한 틱 진행한다. 바뀔 때만 이벤트를 넣는다
        public void Step(bool wantDown) => StepAll(wantDown);

        // 슬롯마다 J 를 맞추고 한 틱
        public void StepAll(params bool[] wantDown)
        {
            var inputs = new List<PlayerInput>();
            for (int s = 0; s < wantDown.Length; s++)
            {
                if (wantDown[s] != _jDown[s])
                {
                    _jDown[s] = wantDown[s];
                    inputs.Add(new PlayerInput(new PlayerSlot((byte)s), new InputEvent(World.Tick, 0, LogicalKey.Fire, wantDown[s], ++_seq)));
                }
            }
            World.Step(inputs.ToArray(), ReadOnlySpan<RunCommand>.Empty, Log);
        }

        // 이번 틱에 J 를 눌렀다가 같은 틱에 뗀다(spec 01 7.3 JPressedThisTick)
        public void Tap()
        {
            Assert.False(_jDown[0]);
            var inputs = new[]
            {
                new PlayerInput(new PlayerSlot(0), new InputEvent(World.Tick, 10, LogicalKey.Fire, true, ++_seq)),
                new PlayerInput(new PlayerSlot(0), new InputEvent(World.Tick, 20, LogicalKey.Fire, false, ++_seq)),
            };
            World.Step(inputs, ReadOnlySpan<RunCommand>.Empty, Log);
        }

        // 상태를 다른 세계에서 읽어 온 뒤(ReadState) 그 세계의 J 상태와 순번을 맞춘다
        public void SyncKeysFrom(HeatRig other)
        {
            Array.Copy(other._jDown, _jDown, _jDown.Length);
            _seq = other._seq;
        }

        // J 상태를 그대로 두고 tick 이 될 때까지
        public void RunTo(uint tick)
        {
            Assert.True(World.Tick <= tick);
            while (World.Tick < tick)
            {
                Step(_jDown[0]);
            }
        }

        public List<SimEvent> Shots(int slot = 0) =>
            Log.Events.FindAll(e => e.Type == SimEventType.ShotFired && e.Slot.Value == slot);

        // 허수아비 피해(spec 01 7.8 조건): 코어 블록의 기본 피해 × (임계 2 / 아니면 1). 피해 공식은 컴파일러의 것이라
        // 이 시험은 명세 7.8의 검산 조건(마력탄만, 강화 +0, 변형 없음, 임계 배율 2)을 그대로 쓴다
        public static int Damage(SimEvent shot, int baseDamage) => shot.C == 1 ? baseDamage * 2 : baseDamage;
    }
}

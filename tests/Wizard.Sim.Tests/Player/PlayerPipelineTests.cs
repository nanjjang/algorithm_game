using System;
using System.Collections.Generic;
using Wizard.Content;
using Wizard.Core;
using Wizard.Sim.Events;
using Wizard.Sim.Input;
using Wizard.Sim.Player;
using Wizard.Sim.Tests.Runtime;
using Wizard.TestKit;
using Xunit;
using static Wizard.Sim.Tests.Player.PlayerRig;

namespace Wizard.Sim.Tests.Player
{
    // 작업 카드의 수락: 같은 틱·같은 오프셋의 방향과 K 는 방향이 먼저 적용된다, 100번대·360번대 표시 이벤트가 정해진 때 한 번씩만.
    // 그리고 Player 절의 결정론(두 번 실행, 스냅숏, 직렬화 왕복, 할당 0), 빈 팩에서 모듈이 꺼지는 것.
    public sealed class PlayerPipelineTests
    {
        // 방향 이벤트가 적용되는 순간 K 가 아직 눌리지 않았는지 본다
        private sealed class ProbeSink : ISimEventSink
        {
            public SimWorld? World;
            public readonly List<bool> SkillDownAtFacingChange = new List<bool>();

            public void Emit(in SimEvent e)
            {
                if (e.Type == SimEventType.FacingChanged)
                {
                    SkillDownAtFacingChange.Add(World!.PlayerAt(0).Keys.IsDown(LogicalKey.Skill));
                }
            }
        }

        [Fact]
        public void SameTickSameOffset_DirectionIsAppliedBeforeK()
        {
            PlayerRig rig = Endless();
            rig.RunTo(5);
            // 도착 순서는 K 가 먼저(순번이 더 작다)지만 정렬 규칙(spec 01 4.3, CONTRACTS 3.1)이 방향을 먼저 둔다
            var inputs = new[]
            {
                new PlayerInput(new PlayerSlot(0), new InputEvent(5, 50, LogicalKey.Skill, true, 1)),
                new PlayerInput(new PlayerSlot(0), new InputEvent(5, 50, LogicalKey.Right, true, 2)),
            };
            Assert.Throws<SimInvariantException>(() => rig.World.Step(inputs, ReadOnlySpan<RunCommand>.Empty, new ProbeSink()));
            InputOrder.Sort(inputs);
            Assert.Equal(LogicalKey.Right, inputs[0].Event.Key);

            var probe = new ProbeSink { World = rig.World };
            rig.World.Step(inputs, ReadOnlySpan<RunCommand>.Empty, probe);
            Assert.Equal(new[] { false }, probe.SkillDownAtFacingChange.ToArray());   // 방향을 적용할 때 K 는 아직 아니다
            Assert.Equal(LogicalKey.Skill, rig.LastInput.Key);                      // K 가 마지막으로 처리되었다
            Assert.Equal(Dir8.E, rig.P.Facing);
        }

        [Fact]
        public void SameTickSameOffset_InStanceTheDirectionReachesTheJudgeBeforeK()
        {
            PlayerRig rig = Endless();
            rig.Step(Press(LogicalKey.Stance));                                     // 잠긴 S
            var inputs = new[]
            {
                new PlayerInput(new PlayerSlot(0), new InputEvent(1, 7, LogicalKey.Skill, true, 1)),
                new PlayerInput(new PlayerSlot(0), new InputEvent(1, 7, LogicalKey.Down, true, 2)),
            };
            InputOrder.Sort(inputs);
            Assert.Equal(LogicalKey.Down, inputs[0].Event.Key);
            Assert.Equal(LogicalKey.Skill, inputs[1].Event.Key);
            rig.World.Step(inputs, ReadOnlySpan<RunCommand>.Empty, rig.Log);
            Assert.Equal(LogicalKey.Skill, rig.LastInput.Key);                      // 판정기는 방향(앞) 뒤에 K 를 받는다
            Assert.True(rig.P.Keys.IsDown(LogicalKey.Down));
            Assert.True(rig.P.Keys.TryGetLastPress(LogicalKey.Down, out long downAt));
            Assert.True(rig.P.Keys.TryGetLastPress(LogicalKey.Skill, out long skillAt));
            Assert.Equal(downAt, skillAt);                                          // 같은 입력 시각
        }

        [Fact]
        public void SameTickSameOffset_StanceKeyBeforeDirectionMakesItACommand()
        {
            // 자세 키(분류 0)가 방향(분류 1)보다 먼저라, 같은 순간의 Up 은 자세 안의 입력이 된다(spec 01 4.3 [임시])
            PlayerRig rig = Endless();
            rig.Step(Press(LogicalKey.Up, 30), Press(LogicalKey.Stance, 30));
            Assert.Equal(StanceMode.Stance, rig.P.Stance);
            Assert.Equal(Dir8.S, rig.P.StanceLockedFacing);                         // Up 이 먼저였다면 N 이다
            Assert.Equal(LogicalKey.Up, rig.LastInput.Key);
            Assert.True(rig.LastInput.HasDirection);
            Assert.Equal(Wizard.Sim.Arts.CmdDir.Back, rig.LastInput.Direction);
        }

        [Fact]
        public void IgnoredEventsEmitNothingAndAreNotAccepted()
        {
            PlayerRig rig = Endless();
            rig.Step(Press(LogicalKey.Right));
            rig.Log.Clear();
            rig.Step(Press(LogicalKey.Right));                                      // 이미 눌린 키의 누름
            Assert.False(rig.LastInput.Accepted);
            rig.Step(Release(LogicalKey.Up));                                       // 안 눌린 키의 뗌
            Assert.False(rig.LastInput.Accepted);
            Assert.Empty(rig.Log.Events);

            // 반전 자세 중 Shift 누름·뗌은 변화 없음(spec 01 5.5 [임시] U7)
            rig.Step(Press(LogicalKey.Reverse));
            rig.Log.Clear();
            rig.Step(Press(LogicalKey.Stance));
            rig.Step(Release(LogicalKey.Stance));
            Assert.Empty(rig.Log.Events);
            Assert.Equal(StanceMode.Reverse, rig.P.Stance);
        }

        [Fact]
        public void StanceAndFacingEventsMatchTransitionsOneToOne()
        {
            PlayerRig rig = Endless();
            rig.Step(Press(LogicalKey.Right));                  // 시야 S → E          : Facing 1
            rig.Step(Press(LogicalKey.Stance));                 // 진입(잠긴 E)          : Entered 1, Reset 1
            rig.Step(Press(LogicalKey.Reverse));                // 반전(잠긴 W)          : Flipped 1, Facing 2, Reset 2
            rig.Step(Release(LogicalKey.Reverse));              // 자세(잠긴 W)          : Flipped 2, Reset 3
            rig.Step(Release(LogicalKey.Stance));               // 해제, Right 눌림 → E : Exited 1, Facing 3, Reset 4
            rig.Step(Press(LogicalKey.Down, 3));                // SE                    : Facing 4
            Assert.Equal(1, rig.Log.Count(SimEventType.StanceEntered));
            Assert.Equal(2, rig.Log.Count(SimEventType.ReverseFlipped));
            Assert.Equal(1, rig.Log.Count(SimEventType.StanceExited));
            Assert.Equal(4, rig.Log.Count(SimEventType.CommandReset));
            List<SimEvent> facing = rig.Log.Of(SimEventType.FacingChanged);
            Assert.Equal(new[] { (int)Dir8.E, (int)Dir8.W, (int)Dir8.E, (int)Dir8.SE }, facing.ConvertAll(e => e.A).ToArray());
            Assert.All(rig.Log.Events, e => Assert.Equal(0, e.Slot.Value));
        }

        [Fact]
        public void DuelPlayersStartFacingEachOtherAndEmitWithTheirSlot()
        {
            PlayerRig rig = PlayerRig.Duel();
            rig.Step();
            PlayerState left = rig.World.PlayerAt(0);
            PlayerState right = rig.World.PlayerAt(1);
            Assert.Equal(Dir8.E, left.Facing);
            Assert.Equal(Dir8.W, right.Facing);
            Assert.Equal(6L * 4_294_967_296L, left.Pos.X.Raw);                     // 돌마당 시작 위치(spec 01 3.7)
            Assert.Equal(18L * 4_294_967_296L, right.Pos.X.Raw);
            rig.StepSlots((Press(LogicalKey.Stance), (byte)1), (Press(LogicalKey.Up), (byte)0));
            Assert.Equal(1, rig.Log.Single(SimEventType.StanceEntered).Slot.Value);
            Assert.Equal(0, rig.Log.Single(SimEventType.FacingChanged).Slot.Value);
            Assert.Equal(Dir8.W, rig.World.PlayerAt(1).StanceLockedFacing);
        }

        [Fact]
        public void EmptyPackTurnsTheModuleOff()
        {
            ContentDb db = WorldKit.EmptyDb();
            SimWorld world = WorldKit.NewEndless(db);
            Assert.False(world.PlayerModuleOn);
            var sink = new RecordingSink();
            world.Step(new[] { WorldKit.In(0, 0, 0, LogicalKey.Right, true, 1) }, ReadOnlySpan<RunCommand>.Empty, sink);
            Assert.False(world.PlayersSpawned);
            Assert.Equal(0, sink.Count);
        }

        // ── 결정론

        // 틱마다의 키 꼴(정해진 의사 난수). 같은 틱에 한 키는 한 번만, 플레이어당 8개 이하
        private static Key[][] Script(int ticks, uint seed)
        {
            LogicalKey[] keys =
            {
                LogicalKey.Up, LogicalKey.Left, LogicalKey.Down, LogicalKey.Right,
                LogicalKey.Stance, LogicalKey.Reverse, LogicalKey.Fire, LogicalKey.Skill,
            };
            var held = new bool[keys.Length];
            var rng = new Pcg32(seed, 7UL);
            var script = new Key[ticks][];
            for (int t = 0; t < ticks; t++)
            {
                var list = new List<Key>();
                int n = rng.NextInt(4);
                var used = new bool[keys.Length];
                for (int j = 0; j < n; j++)
                {
                    int k = rng.NextInt(keys.Length);
                    if (used[k])
                    {
                        continue;
                    }
                    used[k] = true;
                    held[k] = !held[k];
                    list.Add(new Key(keys[k], held[k], (byte)rng.NextInt(256)));
                }
                script[t] = list.ToArray();
            }
            return script;
        }

        private static ulong[] RunScript(PlayerRig rig, Key[][] script, int from, int to)
        {
            var hashes = new ulong[to - from];
            for (int t = from; t < to; t++)
            {
                if (t % 37 == 0)
                {
                    rig.World.QueuePlayerHit(new PlayerSlot(0), new EntityId(t + 1), 1 + t % 3);
                }
                rig.Step(script[t]);
                hashes[t - from] = rig.World.ComputeStateHash();
            }
            return hashes;
        }

        [Fact]
        public void TwoRunsGiveSameHashes()
        {
            Key[][] script = Script(900, 11U);
            ulong[] a = RunScript(Endless(5UL), script, 0, 900);
            ulong[] b = RunScript(Endless(5UL), script, 0, 900);
            Assert.Equal(a, b);
            Assert.True(new HashSet<ulong>(a).Count > 100);                         // 상태가 실제로 움직였다
        }

        [Fact]
        public void SnapshotLoadReplaysToSameHashes()
        {
            Key[][] script = Script(800, 23U);
            PlayerRig rig = Endless(9UL);
            RunScript(rig, script, 0, 300);
            SimSnapshot snap = rig.World.CreateSnapshotBuffer();
            rig.World.SaveSnapshot(snap);
            ulong saved = rig.World.ComputeStateHash();
            ulong[] forward = RunScript(rig, script, 300, 800);
            rig.World.LoadSnapshot(snap);
            Assert.Equal(saved, rig.World.ComputeStateHash());
            Assert.Equal(forward, RunScript(rig, script, 300, 800));
        }

        [Fact]
        public void WriteStateReadStateRoundTripContinuesIdentically()
        {
            Key[][] script = Script(600, 31U);
            PlayerRig a = Endless(3UL);
            RunScript(a, script, 0, 250);
            var w = new ByteWriter();
            a.World.WriteState(w);
            byte[] bytes = w.ToArray();

            PlayerRig b = Endless(3UL);                                              // 한 번도 Step 하지 않은 세계(캐시가 없다)
            Assert.NotEqual(a.World.ComputeStateHash(), b.World.ComputeStateHash());
            b.World.ReadState(bytes);
            Assert.Equal(a.World.ComputeStateHash(), b.World.ComputeStateHash());
            var w2 = new ByteWriter();
            b.World.WriteState(w2);
            Assert.Equal(bytes, w2.ToArray());
            Assert.Equal(RunScript(a, script, 250, 600), RunScript(b, script, 250, 600));
        }

        [Fact]
        public void ReadStateRejectsPlayerOfAnotherCharacter()
        {
            PlayerRig a = Endless(3UL);
            a.Step();
            var w = new ByteWriter();
            a.World.WriteState(w);
            var setup = new EndlessSetup { Seed = 3UL, StartWave = 1 };
            setup.Wand.Character = CharacterId.Char1;
            SimWorld other = SimWorld.CreateEndless(Db, SimConfig.ForEndless(Db), setup);
            Assert.Throws<SimInvariantException>(() => other.ReadState(w.ToArray()));
        }

        // 같은 각본을 새 세계로 여러 번 돌려 가장 작은 값이 0인지 본다(AllocationProbe. 한 번 재기는 병렬 실행에서 가끔 흔들렸다, FIX-1)
        [Fact]
        public void StepDoesNotAllocateAfterWarmUp()
        {
            Key[][] script = Script(1200, 47U);
            CountingSink? sink = null;
            long least = AllocationProbe.LeastBytes(() =>
            {
                SimWorld world = Endless(1UL).World;
                var runSink = new CountingSink();
                sink = runSink;
                var buffer = new PlayerInput[8];
                uint seq = 0;
                int tick = 0;

                void One()
                {
                    Key[] keys = script[tick % script.Length];
                    for (int i = 0; i < keys.Length; i++)
                    {
                        buffer[i] = new PlayerInput(new PlayerSlot(0),
                            new InputEvent(world.Tick, keys[i].Offset, keys[i].LogicalKey, keys[i].Down, (uint)i));
                    }
                    InputOrder.Sort(buffer.AsSpan(0, keys.Length));
                    for (int i = 0; i < keys.Length; i++)
                    {
                        InputEvent e = buffer[i].Event;
                        buffer[i] = new PlayerInput(buffer[i].Slot, new InputEvent(e.Tick, e.Offset, e.Key, e.Down, ++seq));
                    }
                    if (tick % 29 == 0)
                    {
                        world.QueuePlayerHit(new PlayerSlot(0), new EntityId(tick + 1), 1);
                    }
                    world.Step(buffer.AsSpan(0, keys.Length), ReadOnlySpan<RunCommand>.Empty, runSink);
                    tick++;
                }

                One();                                                               // 첫 Step 이 캐시를 만든다(초기화 훅이 없다)
                return () =>
                {
                    for (int i = 0; i < 1000; i++)
                    {
                        One();
                    }
                };
            }, () => Assert.True(sink!.Count > 0));
            Assert.Equal(0L, least);
        }
    }
}

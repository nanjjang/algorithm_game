using System;
using System.Linq;
using Wizard.Client;
using Wizard.Content;
using Wizard.Core;
using Wizard.ReplayCli;
using Wizard.Sim.Events;
using Wizard.Sim.Input;
using Xunit;

namespace Wizard.Sim.Tests.Determinism
{
    // spec 01 14.2 입력 이벤트 벡터(T-I01~T-I05, T-I07~T-I12). 정렬 키는 CONTRACTS 3.1 의 (틱, 오프셋, 키 분류, 슬롯, 순번), 분류는 자세·반전 0, 방향 1, J 2, K 3, 일시정지 4.
    // 세계 쪽 확인은 연습 세계(판정기 + 임시 기술, M1-SIM-09)로 한다: 자세 중 아래(S)를 보면 위 키 = 뒤, 커맨드 "뒤, K" = 반동탄(Recoil),
    // 커맨드 없는 K = 마력 방출(Burst)(spec 03 12절). 판정기가 방향을 받으면 CmdDirInput 이 난다.
    public sealed class InputEventVectorTests
    {
        private static PlayerInput In(uint tick, byte offset, LogicalKey key, uint seq, byte slot = 0, bool down = true) =>
            new PlayerInput(new PlayerSlot(slot), new InputEvent(tick, offset, key, down, seq));

        [Fact]
        public void S01_T_I01__DirectionBeforeKAtTheSameOffset()
        {
            PlayerInput a = In(10, 50, LogicalKey.Skill, 7);
            PlayerInput b = In(10, 50, LogicalKey.Right, 8);
            var sorted = new[] { a, b };
            InputOrder.Sort(sorted);
            Assert.Equal(new[] { LogicalKey.Right, LogicalKey.Skill }, sorted.Select(x => x.Event.Key).ToArray());
            Assert.True(InputOrder.Compare(b, a) < 0);      // 순번(7 < 8)보다 분류가 먼저다

            // 처리: 세계는 정렬된 열만 받는다(K 를 먼저 처리할 길이 없다). 거절은 상태를 바꾸기 전에 끝난다
            var rig = new DetRig();
            rig.RunTo(10);
            ulong before = rig.World.ComputeStateHash();
            Assert.Throws<SimInvariantException>(() => rig.World.Step(new[] { a, b }, ReadOnlySpan<RunCommand>.Empty, rig));
            Assert.Equal(before, rig.World.ComputeStateHash());
            Assert.Equal(10U, rig.World.Tick);

            // 같은 오프셋의 방향과 K: 판정기가 방향을 먼저 받으므로 "뒤, K" 커맨드가 된다
            var stance = new DetRig();
            stance.Step(DetRig.Press(LogicalKey.Stance));
            stance.Step(DetRig.Press(LogicalKey.Skill, 50), DetRig.Press(LogicalKey.Up, 50));
            SimEvent dir = Assert.Single(stance.Of(SimEventType.CmdDirInput));
            Assert.Equal(1U, dir.Tick);
            Assert.Equal((int)ArtId.Recoil, Assert.Single(stance.Of(SimEventType.ArtCast)).A);
        }

        [Fact]
        public void S01_T_I02__OffsetBeforeKeyClass()
        {
            var e = new[] { In(10, 21, LogicalKey.Up, 2), In(10, 20, LogicalKey.Skill, 1) };
            InputOrder.Sort(e);
            Assert.Equal(new[] { LogicalKey.Skill, LogicalKey.Up }, e.Select(x => x.Event.Key).ToArray());
        }

        [Fact]
        public void S01_T_I03__TickBeforeOffset()
        {
            var e = new[] { In(11, 0, LogicalKey.Up, 2), In(10, 255, LogicalKey.Skill, 1) };
            InputOrder.Sort(e);
            Assert.Equal(new[] { LogicalKey.Skill, LogicalKey.Up }, e.Select(x => x.Event.Key).ToArray());
            Assert.Equal(new uint[] { 10, 11 }, e.Select(x => x.Event.Tick).ToArray());
        }

        [Fact]
        public void S01_T_I04__StanceBeforeDirectionSoTheDirectionIsACommand()
        {
            var e = new[] { In(5, 30, LogicalKey.Up, 1), In(5, 30, LogicalKey.Stance, 2) };
            InputOrder.Sort(e);
            Assert.Equal(new[] { LogicalKey.Stance, LogicalKey.Up }, e.Select(x => x.Event.Key).ToArray());

            // 자세 밖에서 같은 틱·같은 오프셋에 자세와 위: 자세에 먼저 들어가고 위가 커맨드(뒤)로 읽힌다
            var rig = new DetRig();
            rig.Step(DetRig.Press(LogicalKey.Up, 30), DetRig.Press(LogicalKey.Stance, 30));
            SimEvent dir = Assert.Single(rig.Of(SimEventType.CmdDirInput));
            Assert.Equal(0U, dir.Tick);
            Assert.Equal((int)Wizard.Sim.Arts.CmdDir.Back, dir.A);
            rig.Step(DetRig.Release(LogicalKey.Up), DetRig.Press(LogicalKey.Skill));
            Assert.Equal((int)ArtId.Recoil, Assert.Single(rig.Of(SimEventType.ArtCast)).A);
        }

        [Fact]
        public void S01_T_I05__RecorderKeepsTheFirstEightKeysOfATickAndCountsOneDrop()
        {
            var recorder = new InputRecorder(BotRecorder.PracticeHeader(DetKit.Db));
            var keys = new QuantizedKey[9];
            LogicalKey[] cycle = { LogicalKey.Up, LogicalKey.Left, LogicalKey.Down, LogicalKey.Right };
            for (int i = 0; i < keys.Length; i++)
            {
                keys[i] = new QuantizedKey(4, (byte)(10 * i), cycle[i % cycle.Length], i < cycle.Length);
            }
            var commandsOut = new RunCommand[1];
            var inputsOut = new PlayerInput[InputOrder.MaxEventsPerTickPerPlayer];
            for (uint t = 0; t < 4; t++)
            {
                recorder.Append(t, DetRig.P0, ReadOnlySpan<RunCommandDraft>.Empty, ReadOnlySpan<QuantizedKey>.Empty, commandsOut, out _, inputsOut, out _);
            }
            recorder.Append(4, DetRig.P0, ReadOnlySpan<RunCommandDraft>.Empty, keys, commandsOut, out _, inputsOut, out int fed);

            Assert.Equal(8, fed);
            Assert.Equal(1, recorder.DroppedKeyCount);                               // 진단 1건
            for (int i = 0; i < fed; i++)
            {
                Assert.Equal(keys[i].Offset, inputsOut[i].Event.Offset);              // 앞의 8개(오프셋 순)
                Assert.Equal((uint)(i + 1), inputsOut[i].Event.Seq);                  // 버린 키는 번호를 받지 않는다
            }

            RecordFeed feed = DetKit.Feed(recorder.Finish(5, 0).ToArray());
            Assert.Equal(8, feed.InputsAt(4).Length);
            Assert.Equal(8, feed.InputCount);
            Assert.Equal((byte)70, feed.InputsAt(4)[7].Event.Offset);
        }

        // 결투의 한 사람 한 틱 9번째 키: 코어 쪽 보증. 세계는 한 사람 9개를 받지 않고(Step 전제), 확정 기록(WZDR)은 쓰지도 읽지도 않는다.
        // 그래서 9번째는 세계에 닿기 전에 버려야 한다. 벡터 T-I06(서버 수신, 9번째부터 버린다)과 T-I13(순번)은 서버가 패킷을 받는 규칙이라
        // DuelHostCore(M2-NET-04)의 테스트가 가진다(CONTRACTS 0.3·10.6, DECISIONS-TECH T-45). 이 테스트는 벡터 이름을 쓰지 않는다:
        // 이름을 쓰면 서버 쪽 테스트가 없어도 벡터 대응 검사가 지나간다.
        [Fact]
        public void DuelCoreNeverTakesANinthKeyOfOnePlayerInATick()
        {
            DuelSetup setup = BotRecorder.DuelSetupFor(DetKit.Db, 0x6006UL);
            SimWorld world = SimWorld.CreateDuel(DetKit.Db, SimConfig.ForDuel(DetKit.Db, setup.Options), setup);
            var nine = new PlayerInput[9];
            var sixteen = new PlayerInput[16];
            for (int i = 0; i < 9; i++)
            {
                nine[i] = In(0, (byte)(i * 20), (i & 1) == 0 ? LogicalKey.Up : LogicalKey.Down, (uint)(i + 1), 0, (i & 2) == 0);
            }
            for (int i = 0; i < 16; i++)
            {
                byte slot = (byte)(i & 1);
                sixteen[i] = In(0, (byte)(i / 2 * 20), (i / 2 & 1) == 0 ? LogicalKey.Up : LogicalKey.Down, (uint)(i / 2 + 1), slot, (i / 2 & 2) == 0);
            }
            InputOrder.Sort(sixteen);
            ulong before = world.ComputeStateHash();
            Assert.Throws<SimInvariantException>(() => world.Step(nine, ReadOnlySpan<RunCommand>.Empty, new CountingSink()));
            Assert.Equal(before, world.ComputeStateHash());
            world.Step(sixteen, ReadOnlySpan<RunCommand>.Empty, new CountingSink());   // 사람마다 8개씩은 받는다
            Assert.Equal(1U, world.Tick);

            var writer = new DuelRecordWriter(setup);
            for (int i = 0; i < 8; i++)
            {
                writer.Append(in nine[i]);
            }
            Assert.ThrowsAny<Exception>(() => writer.Append(in nine[8]));

            // 봇의 몰린 틱(9~12개)도 사람마다 8개에서 끊겨 기록된다. 그 기록은 코덱을 통과한다
            var dense = new BotProfile { BurstTickPermille = 500, Keys = BotProfile.PlayKeys };
            RecordFeed feed = DetKit.Feed(BotRecorder.RecordDuel(DetKit.Db, setup, 0x9UL, 120, dense));
            int max = 0;
            for (uint t = 0; t < feed.EndTick; t++)
            {
                int[] perSlot = new int[2];
                foreach (PlayerInput p in feed.InputsAt(t))
                {
                    perSlot[p.Slot.Value]++;
                }
                max = Math.Max(max, Math.Max(perSlot[0], perSlot[1]));
            }
            Assert.Equal(InputOrder.MaxEventsPerTickPerPlayer, max);
        }

        [Fact]
        public void S01_T_I07__PressOfAHeldKeyIsIgnored()
        {
            var rig = new DetRig();
            var control = new DetRig();
            rig.Step(DetRig.Press(LogicalKey.Stance));
            control.Step(DetRig.Press(LogicalKey.Stance));
            rig.Step(DetRig.Press(LogicalKey.Up, 10));
            control.Step(DetRig.Press(LogicalKey.Up, 10));
            Assert.Single(rig.Of(SimEventType.CmdDirInput));
            KeyState keysBefore = rig.World.PlayerAt(0).Keys;

            rig.Step(DetRig.Press(LogicalKey.Up, 40));                                 // 이미 눌린 위
            control.Step();
            Assert.Single(rig.Of(SimEventType.CmdDirInput));                          // 판정기에 가지 않았다
            Assert.True(rig.World.PlayerAt(0).Keys.TryGetLastPress(LogicalKey.Up, out long t));
            Assert.True(keysBefore.TryGetLastPress(LogicalKey.Up, out long t0));
            Assert.Equal(t0, t);                                                       // 누른 시각도 그대로
            Assert.Equal(control.World.ComputeStateHash(), rig.World.ComputeStateHash());
        }

        [Fact]
        public void S01_T_I08__ReleaseOfAnUpKeyIsIgnored()
        {
            var rig = new DetRig();
            var control = new DetRig();
            rig.Step(DetRig.Press(LogicalKey.Stance));
            control.Step(DetRig.Press(LogicalKey.Stance));
            rig.Step(DetRig.Release(LogicalKey.Up, 10));
            control.Step();
            Assert.False(rig.World.PlayerAt(0).Keys.IsDown(LogicalKey.Up));
            Assert.Empty(rig.Of(SimEventType.CmdDirInput));
            Assert.Equal(control.World.ComputeStateHash(), rig.World.ComputeStateHash());
        }

        // 물리 키 → 논리 키는 클라이언트의 일이다(LogicalKeyTracker, spec 01 4.2 [임시]). 같은 논리 키에 묶인 물리 키 둘
        [Fact]
        public void S01_T_I09__TwoPhysicalKeysOnUpMakeOneLogicalPressAndOneRelease()
        {
            var tracker = new LogicalKeyTracker(KeyBindings.Default.With(LogicalKey.Up, KeyBindings.SecondarySlot, PhysicalKey.UpArrow));
            var output = new RawKeyEvent[LogicalKeyTracker.MaxEventsPerChange];
            Assert.Equal(PhysicalKey.W, KeyBindings.Default.Get(LogicalKey.Up, KeyBindings.PrimarySlot));

            Assert.Equal(1, tracker.OnPhysicalKey(PhysicalKey.W, true, 2.0, output));           // W 누름
            Assert.Equal(LogicalKey.Up, output[0].Key);
            Assert.True(output[0].Down);
            Assert.Equal(0, tracker.OnPhysicalKey(PhysicalKey.UpArrow, true, 2.1, output));     // 방향키 위 누름: 논리 이벤트 없음
            Assert.Equal(0, tracker.OnPhysicalKey(PhysicalKey.W, false, 2.2, output));          // W 뗌: 방향키 위가 아직 눌림
            Assert.True(tracker.Current.HasFlag(KeyState.Bit(LogicalKey.Up)));
            Assert.Equal(1, tracker.OnPhysicalKey(PhysicalKey.UpArrow, false, 2.3, output));    // 방향키 위를 떼야 Up 뗌
            Assert.Equal(LogicalKey.Up, output[0].Key);
            Assert.False(output[0].Down);
            Assert.Equal(KeyMask.None, tracker.Current);
        }

        [Fact]
        public void S01_T_I10__DirectionOutsideStanceIsNotACommand()
        {
            var rig = new DetRig();
            rig.Step(DetRig.Press(LogicalKey.Right));
            rig.Step(DetRig.Release(LogicalKey.Right));
            Assert.Equal(StanceMode.Normal, rig.World.PlayerAt(0).Stance);
            Assert.Equal(Dir8.E, rig.World.PlayerAt(0).Facing);                       // 자세 밖, 시야 E

            rig.Step(DetRig.Press(LogicalKey.Up));
            rig.Step(DetRig.Release(LogicalKey.Up), DetRig.Press(LogicalKey.Skill));
            Assert.Empty(rig.Of(SimEventType.CmdDirInput));
            Assert.Equal((int)ArtId.Burst, Assert.Single(rig.Of(SimEventType.ArtCast)).A);
        }

        [Fact]
        public void S01_T_I11__KeyHeldWhileEnteringStanceIsNotInput()
        {
            var rig = new DetRig();
            rig.Step(DetRig.Press(LogicalKey.Right));
            Assert.Equal(Dir8.E, rig.World.PlayerAt(0).Facing);
            rig.Step(DetRig.Press(LogicalKey.Stance));                                 // 오른쪽을 누른 채 자세
            Assert.NotEqual(StanceMode.Normal, rig.World.PlayerAt(0).Stance);
            Assert.True(rig.World.PlayerAt(0).Keys.IsDown(LogicalKey.Right));
            rig.Step(DetRig.Press(LogicalKey.Skill));
            Assert.Empty(rig.Of(SimEventType.CmdDirInput));
            Assert.Equal((int)ArtId.Burst, Assert.Single(rig.Of(SimEventType.ArtCast)).A);
        }

        [Fact]
        public void S01_T_I12__PressAndReleaseOfJInOneTickFiresOnce()
        {
            var rig = new DetRig();
            rig.RunTo(100);
            Assert.Empty(rig.Of(SimEventType.ShotFired));
            rig.Step(DetRig.Press(LogicalKey.Fire, 0), DetRig.Release(LogicalKey.Fire, 200));
            SimEvent shot = Assert.Single(rig.Of(SimEventType.ShotFired));
            Assert.Equal(100U, shot.Tick);
            rig.RunTo(160);
            Assert.Single(rig.Of(SimEventType.ShotFired));                             // 뗀 뒤로는 쏘지 않는다
        }
    }
}

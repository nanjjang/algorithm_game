using System;
using System.Linq;
using Wizard.Core;
using Wizard.Sim.Input;
using Xunit;

namespace Wizard.Sim.Tests.Replay
{
    // spec 09 19.1 "결정론과 입력" 벡터. 기대값은 명세의 표에서 가져온다.
    // 이 작업(기록·재실행)의 층에서 시험할 수 있는 부분을 시험한다. 다른 층의 몫(섬 판정 = 판정기, 출발 거절 = 서버)은 각 테스트의 주석과 결과 보고에 적었다.
    public sealed class S09DeterminismInputVectorTests
    {
        private static readonly PlayerSlot Slot0 = new PlayerSlot(0);

        private static PlayerInput[] RecordOneTick(uint tick, params QuantizedKey[] keys)
        {
            var rec = new InputRecorder(ReplayKit.Header());
            var ins = new PlayerInput[8];
            rec.Append(tick, Slot0, ReadOnlySpan<RunCommandDraft>.Empty, keys, Span<RunCommand>.Empty, out _, ins, out int n);
            return ins.Take(n).ToArray();
        }

        // 기록 → 파일 → 다시 읽은 키(순번 포함)
        private static PlayerInput[] RoundTrip(uint tick, uint endTick, params QuantizedKey[] keys)
        {
            var rec = new InputRecorder(ReplayKit.Header());
            var ins = new PlayerInput[8];
            rec.Append(tick, Slot0, ReadOnlySpan<RunCommandDraft>.Empty, keys, Span<RunCommand>.Empty, out _, ins, out _);
            byte[] file = rec.Finish(endTick, 0).ToArray();
            Assert.True(InputRecordCodec.TryRead(file, ReplayKit.Generous, out _, out _, out _));
            return InputRecorderTests.Records(file).Where(e => e.Kind == RecordKind.Input)
                .Select(e => new PlayerInput(Slot0, new InputEvent(e.Tick, e.Offset, e.Key, e.Down, e.Seq))).ToArray();
        }

        [Fact]
        public void S09_T001__SameSeedSameRecordReplayedTwiceGivesSameFinalHash()
        {
            EndlessSession s = ReplayRunnerTests.PlayRandom(1001, 1200);
            byte[] body = ReplayKit.BodyOf(s.EndAndFinish());
            InputRecordHeader header = ReplayKit.Header();
            ReplayResult a = ReplayRunner.RunCore(ReplayKit.Db, header, body, ReplayKit.Generous, 0, new HashObserver(1200));
            ReplayResult b = ReplayRunner.RunCore(ReplayKit.Db, ReplayKit.Header(), body, ReplayKit.Generous, 0, new HashObserver(1200));
            Assert.Equal(ReplayStatus.Completed, a.Status);
            Assert.Equal(ReplayStatus.Completed, b.Status);
            Assert.Equal(a.Outcome!.FinalStateHash, b.Outcome!.FinalStateHash);
            Assert.Equal(s.World.GetOutcome().FinalStateHash, a.Outcome.FinalStateHash);
        }

        [Fact]
        public void S09_T002__SameTickSameOffset_DirectionBeforeK()
        {
            // A = (tick 100, off 37, K, down), B = (tick 100, off 37, D(방향), down), seq A < B → B 가 먼저
            var a = new PlayerInput(Slot0, new InputEvent(100, 37, LogicalKey.Skill, true, 1));
            var b = new PlayerInput(Slot0, new InputEvent(100, 37, LogicalKey.Right, true, 2));
            Assert.True(InputOrder.Compare(b, a) < 0);
            var sorted = new[] { a, b };
            InputOrder.Sort(sorted);
            Assert.Equal(LogicalKey.Right, sorted[0].Event.Key);

            // 기록기는 A 를 먼저 받아도 B 에 먼저 번호를 매기고, 파일을 다시 읽어도 같다
            PlayerInput[] recorded = RecordOneTick(100, ReplayKit.Key(100, 37, LogicalKey.Skill, true), ReplayKit.Key(100, 37, LogicalKey.Right, true));
            Assert.Equal(LogicalKey.Right, recorded[0].Event.Key);
            Assert.True(recorded[0].Event.Seq < recorded[1].Event.Seq);
            PlayerInput[] reread = RoundTrip(100, 101, ReplayKit.Key(100, 37, LogicalKey.Skill, true), ReplayKit.Key(100, 37, LogicalKey.Right, true));
            Assert.Equal(new[] { LogicalKey.Right, LogicalKey.Skill }, reread.Select(p => p.Event.Key).ToArray());
            Assert.Equal(new uint[] { 1, 2 }, reread.Select(p => p.Event.Seq).ToArray());
        }

        [Fact]
        public void S09_T003__OffsetFirst_KAtOffset10BeforeWAtOffset37()
        {
            var k = new PlayerInput(Slot0, new InputEvent(100, 10, LogicalKey.Skill, true, 2));
            var w = new PlayerInput(Slot0, new InputEvent(100, 37, LogicalKey.Up, true, 1));
            Assert.True(InputOrder.Compare(k, w) < 0);
            PlayerInput[] reread = RoundTrip(100, 101, ReplayKit.Key(100, 37, LogicalKey.Up, true), ReplayKit.Key(100, 10, LogicalKey.Skill, true));
            Assert.Equal(new[] { LogicalKey.Skill, LogicalKey.Up }, reread.Select(p => p.Event.Key).ToArray());
        }

        // 섬 판정의 창 0.07초 = round(0.07 × 15360) = 1,075 입력 시각 단위(spec 01 2.1의 환산값 표). 판정 자체는 판정기(spec 03)의 몫이다.
        // 이 층의 몫: 기록과 재실행을 거쳐도 두 키의 입력 시각 차가 그대로여서 판정기가 같은 판정을 내린다
        private const long IslandWindowUnits = 1075;

        [Fact]
        public void S09_T004__DirectionAt0_KAt4Off51_GapWithinIslandWindow()
        {
            PlayerInput dir = RoundTrip(0, 1, ReplayKit.Key(0, 0, LogicalKey.Right, true))[0];
            PlayerInput k = RoundTrip(4, 5, ReplayKit.Key(4, 51, LogicalKey.Skill, true))[0];
            long gap = k.Event.Time - dir.Event.Time;
            Assert.Equal(4L * 256 + 51, gap);           // 4 + 51/256 = 4.199틱
            Assert.True(gap <= IslandWindowUnits);       // < 4.2틱 → 섬 성공
        }

        [Fact]
        public void S09_T005__DirectionAt0_KAt4Off52_GapOutsideIslandWindow()
        {
            PlayerInput dir = RoundTrip(0, 1, ReplayKit.Key(0, 0, LogicalKey.Right, true))[0];
            PlayerInput k = RoundTrip(4, 5, ReplayKit.Key(4, 52, LogicalKey.Skill, true))[0];
            long gap = k.Event.Time - dir.Event.Time;
            Assert.Equal(4L * 256 + 52, gap);           // 4.203틱
            Assert.True(gap > IslandWindowUnits);        // > 4.2틱 → 실패
        }

        [Fact]
        public void S09_T006__NineEventsInOneTick_EightRemain()
        {
            var keys = new QuantizedKey[9];
            for (int i = 0; i < 9; i++)
            {
                keys[i] = ReplayKit.Key(7, (byte)(i * 20), i % 2 == 0 ? LogicalKey.Fire : LogicalKey.Up, i % 4 < 2);
            }
            Assert.Equal(8, RecordOneTick(7, keys).Length);
            Assert.Equal(8, RoundTrip(7, 8, keys).Length);
        }

        [Fact]
        public void S09_T007__InputAtTick60Offset128_IsAbout1008Point4Ms()
        {
            PlayerInput e = RoundTrip(60, 61, ReplayKit.Key(60, 128, LogicalKey.Fire, true))[0];
            Assert.Equal(60L * 256 + 128, e.Event.Time);
            // 표시용 환산(spec 01 2.1): (tick + offset ÷ 256) × 16.667ms. 코어 판정에는 쓰지 않는다
            double ms = (e.Event.Tick + e.Event.Offset / 256.0) * 16.667;
            Assert.InRange(ms, 1008.3, 1008.5);
        }

        [Fact]
        public void S09_T008__HitstopInRecordDoesNotChangeTickCount()
        {
            // 히트스톱은 표시 계층의 일이고 기록에 없다(spec 01 2.1). 재실행의 틱 수는 기록의 end_tick 으로만 정해진다.
            // 일시정지 기록(실제 시간 수만 ms)이 섞여도 틱 수는 같다
            var s = new EndlessSession(ReplayKit.Header());
            for (int t = 0; t < 240; t++)
            {
                if (t == 60)
                {
                    s.Recorder.MarkPause(60, 30000);
                }
                s.Tick(t % 30 == 0 ? new[] { ReplayKit.Key((uint)t, 0, LogicalKey.Fire, t % 60 == 0) } : null);
            }
            byte[] file = s.EndAndFinish();
            ReplayResult r = ReplayRunner.RunCore(ReplayKit.Db, ReplayKit.Header(), ReplayKit.BodyOf(file), ReplayKit.Generous, 0, new HashObserver(240));
            Assert.Equal(ReplayStatus.Completed, r.Status);
            Assert.Equal(240U, r.TicksSimulated);
            Assert.Equal(240U, r.Outcome!.EndTick);
        }

        [Fact]
        public void S09_T009__CoreVersionInRecordHeaderIsVisibleToTheCaller()
        {
            // 출발 거절 자체는 서버(StartRun)와 결투 입장의 몫이다. 이 층의 몫: 기록 머리말의 core_version 이 바이트 그대로 읽혀
            // 호출자가 자기 코어(SimVersion.CoreVersion)와 대조할 수 있다. 한 바이트만 달라도 다른 값으로 읽힌다
            var rec = new InputRecorder(ReplayKit.Header());
            byte[] file = rec.Finish(0, 0).ToArray();
            Assert.True(InputRecordCodec.TryRead(file, ReplayKit.Generous, out InputRecordHeader? header, out _, out _));
            Assert.Equal(ReplayKit.CoreVersionHex, header!.Setup.CoreVersion);
            Assert.NotEqual(SimVersion.CoreVersion, header.Setup.CoreVersion);

            InputRecordHeader same = ReplayKit.Header();
            same.Setup.CoreVersion = SimVersion.CoreVersion;
            byte[] sameFile = new InputRecorder(same).Finish(0, 0).ToArray();
            Assert.True(InputRecordCodec.TryRead(sameFile, ReplayKit.Generous, out InputRecordHeader? sameHeader, out _, out _));
            Assert.Equal(SimVersion.CoreVersion, sameHeader!.Setup.CoreVersion);
        }
    }
}

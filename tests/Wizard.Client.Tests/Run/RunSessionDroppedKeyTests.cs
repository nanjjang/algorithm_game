using System.Linq;
using Wizard.Client.Run;
using Wizard.Sim;
using Wizard.Sim.Input;
using Xunit;

namespace Wizard.Client.Tests.Run
{
    // 한 틱의 아홉 번째 키부터 버린 뒤(spec 01 4.3)에도 세계의 키 상태가 입력원의 논리 키 상태로 돌아오는가.
    // 수락 조건 3("닫힐 때 논리 키 상태의 차이를 이벤트로 만든다", CONTRACTS 3.1)의 차이는 세계의 실제 키 상태에서 내야 한다.
    //
    // 장면(검토에서 나온 입력): 하드웨어 시각 0.32~0.48초에 20ms 간격으로 9개를 입력원이 0.52초에 한꺼번에 넘긴다(60Hz 프레임).
    // 그 프레임의 세계 틱은 31이고 9개는 모두 지나간 틱(19~28)이라 틱 31의 오프셋 0으로 옮겨진다. KeyClass 순(방향 1 < Fire 2)으로
    // 정렬되어 Fire 뗌이 아홉 번째로 버려진다. 그래서 세계는 Right|Fire, 입력원은 Right.
    public sealed class RunSessionDroppedKeyTests
    {
        private const long Sec = RunKit.UsPerSecond;
        private const long Frame = 16_667;
        private const long DeliverUs = 520_000;

        private static ScriptedSource NineKeysInOneFrame() => new ScriptedSource()
            .Add(320_000, LogicalKey.Fire, true, DeliverUs)
            .Add(340_000, LogicalKey.Up, true, DeliverUs)
            .Add(360_000, LogicalKey.Up, false, DeliverUs)
            .Add(380_000, LogicalKey.Left, true, DeliverUs)
            .Add(400_000, LogicalKey.Left, false, DeliverUs)
            .Add(420_000, LogicalKey.Down, true, DeliverUs)
            .Add(440_000, LogicalKey.Down, false, DeliverUs)
            .Add(460_000, LogicalKey.Right, true, DeliverUs)
            .Add(480_000, LogicalKey.Fire, false, DeliverUs);

        private static readonly string[] FedAtTick31 =
        {
            "in 31:0 Up down #1",
            "in 31:0 Up up #2",
            "in 31:0 Left down #3",
            "in 31:0 Left up #4",
            "in 31:0 Down down #5",
            "in 31:0 Down up #6",
            "in 31:0 Right down #7",
            "in 31:0 Fire down #8",
        };

        // 첫 프레임(0.52초 뒤의 첫 60Hz 프레임 = 프레임 32)에서 9개가 틱 31로 모이고 하나가 버려진다는 장면의 전제
        private static long DeliverFrame(RunSession session, ScriptedSource src)
        {
            long f = 0;
            for (int i = 0; f < DeliverUs; i++)
            {
                f = i * Frame;
                RunSessionTests.Step(session, src, f);
            }
            Assert.Equal(32 * Frame, f);
            Assert.Equal(9, session.RetimedLateKeyCount);
            Assert.Equal(1, session.DroppedOverflowKeyCount);
            Assert.Equal(Bit(LogicalKey.Right) | Bit(LogicalKey.Fire), session.WorldKeys);   // 24
            Assert.Equal(Bit(LogicalKey.Right), src.CurrentKeys);                           // 8
            return f;
        }

        // 버린 뒤 창이 열렸다 닫히면, 닫힐 때 세계의 키(Right|Fire)와 입력원(Right)의 차이(Fire 뗌)를 낸다
        [Fact]
        public void WindowCloseReconcilesAgainstTheWorldKeysAfterAnOverflowDrop()
        {
            RunSession session = RunKit.NewSession();
            ScriptedSource src = NineKeysInOneFrame();
            long f = DeliverFrame(session, src);

            session.SetWindowOpen(true);                    // 버린 바로 다음 프레임부터 창
            long openedAt = f + Frame;
            long closedAt = -1;
            for (long t = openedAt; t <= openedAt + 2 * Sec; t += Frame)
            {
                if (closedAt < 0 && t >= openedAt + 1 * Sec)
                {
                    Assert.Equal(Bit(LogicalKey.Right) | Bit(LogicalKey.Fire), session.WorldKeys);   // 창 동안 세계의 키는 그대로다
                    session.SetWindowOpen(false);
                    closedAt = t;
                }
                RunSessionTests.Step(session, src, t);
            }
            Assert.False(session.IsWindowOpen);
            Assert.Equal(src.CurrentKeys, session.WorldKeys);
            Assert.Equal(Bit(LogicalKey.Right), session.WorldKeys);
            Assert.Equal(0, session.DropCorrectionKeyCount);   // 창이 닫힐 때의 차이 맞추기가 맞췄다
            Assert.Equal(1, session.DroppedOverflowKeyCount);

            (uint closeTick, byte closeOffset) = RunKit.TickOf(closedAt);
            WzrcRecords rec = WzrcRecords.Read(session.Finish(RunKit.Sec(RunKit.StartUs + openedAt + 2 * Sec)).Span);
            Assert.Equal(session.WorldKeys, rec.KeysAfterInputs());
            Assert.Equal(FedAtTick31.Concat(new[] { $"in {closeTick}:{closeOffset} Fire up #9" }).ToArray(),
                         rec.Records.Select(r => r.ToString()).ToArray());
        }

        // 창이 없으면 버린 다음 프레임에 그 프레임 시각으로 차이를 낸다. 기록을 다시 돌려도 같은 해시
        [Fact]
        public void NextFrameReconcilesAfterAnOverflowDrop()
        {
            RunSession session = RunKit.NewSession();
            ScriptedSource src = NineKeysInOneFrame();
            long f = DeliverFrame(session, src);

            long next = f + Frame;
            RunSessionTests.Step(session, src, next);
            Assert.Equal(1, session.DropCorrectionKeyCount);
            for (long t = next + Frame; t <= 1 * Sec; t += Frame)
            {
                RunSessionTests.Step(session, src, t);
            }
            Assert.Equal(src.CurrentKeys, session.WorldKeys);
            Assert.Equal(Bit(LogicalKey.Right), session.WorldKeys);
            Assert.Equal(1, session.DropCorrectionKeyCount);   // 한 번 맞춘 뒤로는 내지 않는다

            session.MarkCheckpoint(1);
            ulong hash = session.ComputeStateHash();
            byte[] file = session.Finish(RunKit.Sec(RunKit.StartUs + 1 * Sec)).ToArray();
            WzrcRecords rec = WzrcRecords.Read(file);
            Assert.Equal(session.WorldKeys, rec.KeysAfterInputs());
            (uint fixTick, byte fixOffset) = RunKit.TickOf(next);
            Assert.Equal(33U, fixTick);
            Assert.Equal(FedAtTick31.Concat(new[] { $"in {fixTick}:{fixOffset} Fire up #9", $"cp {rec.Records.Last().Tick} {hash:x16}" }).ToArray(),
                         rec.Records.Select(r => r.ToString()).ToArray());

            ReplayResult r = ReplayRunner.Run(RunKit.Db, RunKit.Header(), RunKit.BodyOf(file), RunKit.Generous, upToWave: 1);
            Assert.Equal(ReplayStatus.Completed, r.Status);
            Assert.Equal(hash, r.Outcome!.FinalStateHash);
        }

        // 버린 키가 키 상태를 바꾸지 않는 것(안 눌린 키의 뗌)이면 차이가 비어 있고 아무것도 내지 않는다
        [Fact]
        public void NoCorrectionWhenTheDroppedKeyChangesNothing()
        {
            RunSession session = RunKit.NewSession();
            var src = new ScriptedSource()
                .Add(320_000, LogicalKey.Up, true, DeliverUs)
                .Add(330_000, LogicalKey.Up, false, DeliverUs)
                .Add(340_000, LogicalKey.Left, true, DeliverUs)
                .Add(350_000, LogicalKey.Left, false, DeliverUs)
                .Add(360_000, LogicalKey.Down, true, DeliverUs)
                .Add(370_000, LogicalKey.Down, false, DeliverUs)
                .Add(380_000, LogicalKey.Right, true, DeliverUs)
                .Add(390_000, LogicalKey.Right, false, DeliverUs)
                .Add(400_000, LogicalKey.Fire, false, DeliverUs);    // 눌린 적 없는 Fire 의 뗌(아홉 번째, 버려진다)
            for (long t = 0; t <= 1 * Sec; t += Frame)
            {
                RunSessionTests.Step(session, src, t);
            }
            Assert.Equal(1, session.DroppedOverflowKeyCount);
            Assert.Equal(0, session.DropCorrectionKeyCount);
            Assert.Equal(KeyMask.None, session.WorldKeys);
            Assert.Equal(KeyMask.None, src.CurrentKeys);
            WzrcRecords rec = WzrcRecords.Read(session.Finish(RunKit.Sec(RunKit.StartUs + 1 * Sec)).Span);
            Assert.Equal(8, rec.Inputs.Count);
            Assert.All(rec.Inputs, r => Assert.Equal(31U, r.Tick));
        }

        // 버린 것이 누름이면 다음 프레임에 그 누름을 다시 내고, 그 뒤의 뗌은 평소대로 들어간다
        [Fact]
        public void DroppedPressIsRestoredOnTheNextFrame()
        {
            RunSession session = RunKit.NewSession();
            var src = new ScriptedSource()
                .Add(320_000, LogicalKey.Up, true, DeliverUs)
                .Add(330_000, LogicalKey.Up, false, DeliverUs)
                .Add(340_000, LogicalKey.Left, true, DeliverUs)
                .Add(350_000, LogicalKey.Left, false, DeliverUs)
                .Add(360_000, LogicalKey.Down, true, DeliverUs)
                .Add(370_000, LogicalKey.Down, false, DeliverUs)
                .Add(380_000, LogicalKey.Right, true, DeliverUs)
                .Add(390_000, LogicalKey.Right, false, DeliverUs)
                .Add(400_000, LogicalKey.Fire, true, DeliverUs)
                .Add(600_123, LogicalKey.Fire, false);           // 1/256틱 경계는 피한다(RunKit)
            bool sawFireInWorld = false;
            for (long t = 0; t <= 1 * Sec; t += Frame)
            {
                RunSessionTests.Step(session, src, t);
                if (t == 32 * Frame)
                {
                    Assert.Equal(KeyMask.None, session.WorldKeys);        // Fire 누름이 버려졌다
                    Assert.Equal(Bit(LogicalKey.Fire), src.CurrentKeys);
                }
                sawFireInWorld |= session.WorldKeys == Bit(LogicalKey.Fire);
            }
            Assert.True(sawFireInWorld);
            Assert.Equal(1, session.DroppedOverflowKeyCount);
            Assert.Equal(1, session.DropCorrectionKeyCount);
            Assert.Equal(KeyMask.None, session.WorldKeys);
            Assert.Equal(KeyMask.None, src.CurrentKeys);
            WzrcRecords rec = WzrcRecords.Read(session.Finish(RunKit.Sec(RunKit.StartUs + 1 * Sec)).Span);
            (uint fixTick, byte fixOffset) = RunKit.TickOf(33 * Frame);
            (uint upTick, byte upOffset) = RunKit.TickOf(600_123);
            string[] lines = rec.Records.Select(r => r.ToString()).ToArray();
            Assert.Equal($"in {fixTick}:{fixOffset} Fire down #9", lines[8]);
            Assert.Equal($"in {upTick}:{upOffset} Fire up #10", lines[9]);
            Assert.Equal(10, lines.Length);
        }

        private static KeyMask Bit(LogicalKey key) => (KeyMask)(1 << (int)key);
    }
}

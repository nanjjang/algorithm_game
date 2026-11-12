using System;
using System.Linq;
using Wizard.Client.Run;
using Wizard.Core;
using Wizard.Sim;
using Wizard.Sim.Input;
using Xunit;

namespace Wizard.Client.Tests.Run
{
    // 수락 조건 3: 틱을 멈추는 것은 일시정지뿐이다(PauseMark). 창이 떠 있어도 틱은 진행하고 그동안의 조작은 발생한 틱의 RunCommand 로 남는다.
    // 창이 떠 있는 동안 키는 세계에 넣지 않고, 닫힐 때 논리 키 상태의 차이를 이벤트로 만든다.
    // 근거: CONTRACTS 3.1, ARCHITECTURE 6.1, T-13, spec 01 3.6(일시정지 중에 바뀐 키는 풀린 틱의 오프셋 0에), spec 09 3.4(PauseMark).
    public sealed class RunSessionPauseWindowTests
    {
        private const long Sec = RunKit.UsPerSecond;
        private const long Frame = 16_667;

        [Fact]
        public void PauseKeyStopsTicksUntilResumeAndRecordsThePauseMark()
        {
            RunSession session = RunKit.NewSession();
            var src = new ScriptedSource()
                .Add(100_123, LogicalKey.Up, true)
                .Add(500_123, LogicalKey.Pause, true)             // 틱 30
                .Add(600_000, LogicalKey.Pause, false)            // 여기부터 일시정지 중
                .Add(1_000_000, LogicalKey.Up, false)
                .Add(1_500_000, LogicalKey.Right, true)
                .Add(1_700_000, LogicalKey.Fire, true)
                .Add(1_800_000, LogicalKey.Fire, false)
                .Add(3_500_000, LogicalKey.Left, true);           // 풀린 뒤
            Assert.Equal(30U, RunKit.TickOf(500_123).Tick);

            long[] frames = RunKit.Regular(Frame, 0, 4 * Sec);
            long pausedAt = -1, resumedAt = -1;
            bool requested = false;
            foreach (long f in frames)
            {
                if (!requested && f >= 3 * Sec)
                {
                    session.RequestResume();
                    requested = true;
                    resumedAt = f;
                }
                RunSessionTests.Step(session, src, f);
                if (pausedAt < 0 && session.IsPaused)
                {
                    pausedAt = f;
                }
                if (session.IsPaused)
                {
                    Assert.Equal(31U, session.Tick);             // 틱 30(Pause 누름)을 돈 뒤로 멈췄다
                    Assert.Equal(31U, session.Clock.Tick);
                }
            }
            Assert.Equal(31 * Frame, pausedAt);                   // 틱 30이 끝난 뒤의 첫 프레임
            Assert.Equal(180 * Frame, resumedAt);
            Assert.False(session.IsPaused);

            // 풀린 뒤로는 실제 시간만큼 간다(멈춘 시간은 빼고)
            long expectedAfter = 31 + RunKit.TicksAt(4 * Sec - resumedAt);
            Assert.InRange((long)session.Tick, expectedAfter - 1, expectedAfter + 1);
            Assert.Equal(src.CurrentKeys, session.WorldKeys);

            long wallUs = resumedAt - pausedAt;                    // 멈춘 순간의 비율에서 이어 가므로 = 두 프레임의 사이
            Assert.Equal(1, session.PauseCount);
            Assert.InRange(session.PauseTotalMs, wallUs / 1000 - 1, wallUs / 1000);

            session.MarkCheckpoint(1);
            ulong hash = session.ComputeStateHash();
            byte[] file = session.Finish(RunKit.Sec(RunKit.StartUs + 4 * Sec)).ToArray();
            WzrcRecords rec = WzrcRecords.Read(file);
            Assert.Equal(session.WorldKeys, rec.KeysAfterInputs());
            (uint leftTick, byte leftOffset) = RunKit.TickOf(3_500_000 - wallUs);   // 풀린 뒤의 시계는 멈춘 시간만큼 밀렸다
            Assert.Equal(new[]
            {
                $"in {RunKit.TickOf(100_123).Tick}:{RunKit.TickOf(100_123).Offset} Up down #1",
                $"in 30:{RunKit.TickOf(500_123).Offset} Pause down #2",
                $"pause 31 {session.PauseTotalMs}ms",
                "in 31:0 Up up #3",                                   // 일시정지 중에 바뀐 키: 풀린 틱의 오프셋 0
                "in 31:0 Right down #4",
                "in 31:0 Pause up #5",
                $"in {leftTick}:{leftOffset} Left down #6",
                $"cp {session.Tick} {hash:x16}",
            }, rec.Records.Select(r => r.ToString()).ToArray());

            Assert.True(InputRecordCodec.TryRead(file, RunKit.Generous, out _, out InputRecordBody? body, out _));
            Assert.Equal(1, body!.PauseCount);
            Assert.Equal(session.PauseTotalMs, body.PauseTotalMs);
            ReplayResult r = ReplayRunner.Run(RunKit.Db, RunKit.Header(), RunKit.BodyOf(file), RunKit.Generous, upToWave: 1);
            Assert.Equal(ReplayStatus.Completed, r.Status);
            Assert.Equal(hash, r.Outcome!.FinalStateHash);
        }

        [Fact]
        public void FinishingWhilePausedRecordsThePauseSoFar()
        {
            RunSession session = RunKit.NewSession();
            var src = new ScriptedSource().Add(200_500, LogicalKey.Pause, true);
            foreach (long f in RunKit.Regular(Frame, 0, 2 * Sec))
            {
                RunSessionTests.Step(session, src, f);
            }
            Assert.True(session.IsPaused);
            uint tick = session.Tick;
            byte[] file = session.Finish(RunKit.Sec(RunKit.StartUs + 5 * Sec)).ToArray();
            WzrcRecords rec = WzrcRecords.Read(file);
            WzrcRecords.Rec pause = rec.Records.Single(x => x.K == WzrcRecords.Kind.Pause);
            Assert.Equal(tick, pause.Tick);
            Assert.InRange((long)pause.WallMs, 4_700L, 4_800L);
            Assert.Equal(tick, rec.EndTick);
        }

        // 창(전리품·야영 화면)이 떠 있어도 틱은 간다. 그동안의 키는 세계에 넣지 않고(Pause 키도), 닫힐 때 차이를 그 시각의 이벤트로 낸다.
        // 창에서 한 조작은 명령으로 간다(지금 세계는 어떤 명령도 받지 않으므로 기록되지 않는다: 위 RunSessionTests 참고)
        [Fact]
        public void WindowKeepsTicksRunningWithholdsKeysAndReconcilesOnClose()
        {
            RunSession session = RunKit.NewSession();
            var src = new ScriptedSource()
                .Add(100_123, LogicalKey.Up, true)
                .Add(1_200_000, LogicalKey.Fire, true)            // 창
                .Add(1_300_000, LogicalKey.Pause, true)
                .Add(1_400_000, LogicalKey.Pause, false)
                .Add(1_500_000, LogicalKey.Up, false)
                .Add(2_000_000, LogicalKey.Right, true)
                .Add(2_200_000, LogicalKey.Fire, false)
                .Add(2_900_123, LogicalKey.Down, true);           // 닫힌 뒤
            const long FrameUs = 16_000;
            long[] frames = RunKit.Regular(FrameUs, 3_210, 3 * Sec);
            long openedAt = -1, closedAt = -1, prev = 0;
            uint tickAtOpen = 0;
            foreach (long f in frames)
            {
                if (openedAt < 0 && f >= 1 * Sec)
                {
                    session.SetWindowOpen(true);
                    openedAt = f;
                    tickAtOpen = session.Tick;
                }
                if (openedAt >= 0 && f >= 1_600_000 && session.PendingCommandCount == 0 && session.RejectedCommandCount == 0)
                {
                    session.Submit(RunCommandOp.Heal);
                }
                if (closedAt < 0 && f >= 2_500_000)
                {
                    Assert.InRange((long)session.Tick, RunKit.TicksAt(prev) - 1, RunKit.TicksAt(prev) + 1);   // 창 동안에도 실제 시간만큼 갔다
                    session.SetWindowOpen(false);
                    closedAt = f;
                }
                prev = f;
                RunSessionTests.Step(session, src, f);
                Assert.False(session.IsPaused);
                if (openedAt >= 0 && closedAt < 0)
                {
                    Assert.Equal(Bit(LogicalKey.Up), session.WorldKeys);      // 창 동안 세계의 키는 그대로다
                }
            }
            Assert.True(session.Tick > tickAtOpen + 60);
            Assert.Equal(6, session.WithheldKeyCount);
            Assert.Equal(1, session.RejectedCommandCount);
            Assert.Equal(src.CurrentKeys, session.WorldKeys);

            (uint closeTick, byte closeOffset) = RunKit.TickOf(closedAt);
            (uint downTick, byte downOffset) = RunKit.TickOf(2_900_123);
            WzrcRecords rec = WzrcRecords.Read(session.Finish(RunKit.Sec(RunKit.StartUs + 3 * Sec)).Span);
            Assert.Equal(session.WorldKeys, rec.KeysAfterInputs());
            Assert.Equal(new[]
            {
                $"in {RunKit.TickOf(100_123).Tick}:{RunKit.TickOf(100_123).Offset} Up down #1",
                $"in {closeTick}:{closeOffset} Up up #2",
                $"in {closeTick}:{closeOffset} Right down #3",
                $"in {downTick}:{downOffset} Down down #4",
            }, rec.Records.Select(r => r.ToString()).ToArray());
        }

        [Fact]
        public void WindowOpenedAndClosedDuringPauseIsReconciledAtResume()
        {
            RunSession session = RunKit.NewSession();
            var src = new ScriptedSource()
                .Add(100_000, LogicalKey.Stance, true)
                .Add(300_500, LogicalKey.Pause, true)
                .Add(350_000, LogicalKey.Pause, false)
                .Add(400_000, LogicalKey.Stance, false);
            foreach (long f in RunKit.Regular(Frame, 0, 2 * Sec))
            {
                if (f >= 1 * Sec && f < 1_500_000)
                {
                    session.SetWindowOpen(true);
                }
                else if (f >= 1_500_000)
                {
                    session.SetWindowOpen(false);
                }
                if (f >= 1_800_000)
                {
                    session.RequestResume();
                }
                RunSessionTests.Step(session, src, f);
            }
            Assert.False(session.IsPaused);
            Assert.False(session.IsWindowOpen);
            Assert.Equal(KeyMask.None, session.WorldKeys);
            WzrcRecords rec = WzrcRecords.Read(session.Finish(RunKit.Sec(RunKit.StartUs + 2 * Sec)).Span);
            Assert.Equal(session.WorldKeys, rec.KeysAfterInputs());
            uint resumeTick = RunKit.TickOf(300_500).Tick + 1;
            Assert.Contains($"in {resumeTick}:0 Stance up #3", rec.Records.Select(r => r.ToString()));
            Assert.Contains($"in {resumeTick}:0 Pause up #4", rec.Records.Select(r => r.ToString()));
        }

        [Fact]
        public void ResumeWithTheWindowStillOpenReconcilesWhenTheWindowCloses()
        {
            RunSession session = RunKit.NewSession();
            var src = new ScriptedSource()
                .Add(100_000, LogicalKey.Left, true)
                .Add(300_500, LogicalKey.Pause, true)
                .Add(400_000, LogicalKey.Pause, false)
                .Add(1_600_000, LogicalKey.Left, false);
            long closeAt = -1, pausedAt = -1, resumedAt = -1;
            foreach (long f in RunKit.Regular(Frame, 0, 3 * Sec))
            {
                if (f >= 1 * Sec)
                {
                    session.SetWindowOpen(f < 2 * Sec);
                    if (f >= 2 * Sec && closeAt < 0)
                    {
                        closeAt = f;
                    }
                }
                if (f >= 1_200_000 && resumedAt < 0 && session.IsPaused)
                {
                    session.RequestResume();
                    resumedAt = f;
                }
                RunSessionTests.Step(session, src, f);
                if (pausedAt < 0 && session.IsPaused)
                {
                    pausedAt = f;
                }
            }
            Assert.True(pausedAt > 0 && resumedAt > pausedAt && closeAt > resumedAt);
            Assert.False(session.IsPaused);
            Assert.Equal(KeyMask.None, session.WorldKeys);
            WzrcRecords rec = WzrcRecords.Read(session.Finish(RunKit.Sec(RunKit.StartUs + 3 * Sec)).Span);
            Assert.Equal(session.WorldKeys, rec.KeysAfterInputs());
            (uint t, byte o) = RunKit.TickOf(closeAt - (resumedAt - pausedAt));   // 풀린 뒤의 시계는 멈춘 시간만큼 밀렸다
            string[] lines = rec.Records.Select(r => r.ToString()).ToArray();
            Assert.Contains($"in {t}:{o} Left up #3", lines);
            Assert.Contains($"in {t}:{o} Pause up #4", lines);
            Assert.DoesNotContain(lines, l => l.StartsWith("in " + (RunKit.TickOf(300_500).Tick + 1) + ":0", StringComparison.Ordinal));
        }

        [Fact]
        public void ResumeRequestWithoutPauseDoesNothing()
        {
            RunSession session = RunKit.NewSession();
            var src = new ScriptedSource();
            session.RequestResume();
            RunSessionTests.Step(session, src, 1 * Sec);
            Assert.False(session.IsPaused);
            Assert.Equal(0, session.PauseCount);
            Assert.Equal((uint)RunKit.TicksAt(1 * Sec), session.Tick);
        }

        private static KeyMask Bit(LogicalKey key) => (KeyMask)(1 << (int)key);
    }
}

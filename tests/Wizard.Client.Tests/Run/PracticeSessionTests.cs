using System;
using System.Linq;
using System.Reflection;
using Wizard.Client.Run;
using Wizard.Core;
using Wizard.Sim;
using Wizard.Sim.Input;
using Xunit;

namespace Wizard.Client.Tests.Run
{
    // ARCHITECTURE 6.1: PracticeSession 은 RunSession 과 같되 기록·제출이 없다.
    public sealed class PracticeSessionTests
    {
        private const long Sec = RunKit.UsPerSecond;
        private static PracticeSession New() => new PracticeSession(RunKit.Db, new PracticeSetup(), RunKit.Sec(RunKit.StartUs));

        [Fact]
        public void KeysDriveThePracticeWorldAtRealTime()
        {
            PracticeSession session = New();
            PracticeSession idle = New();
            var src = new ScriptedSource().Add(100_000, LogicalKey.Right, true).Add(900_000, LogicalKey.Right, false);
            Frames.Drive(session, src, RunKit.Irregular(5UL, 2 * Sec));
            Frames.Drive(idle, new ScriptedSource(), RunKit.Irregular(5UL, 2 * Sec));
            Assert.Equal(idle.Tick, session.Tick);
            Assert.InRange((long)session.Tick, RunKit.TicksAt(2 * Sec) - 1, RunKit.TicksAt(2 * Sec) + 1);
            Assert.NotEqual(idle.ComputeStateHash(), session.ComputeStateHash());                // 키가 연습 세계를 움직였다
            Assert.Equal(KeyMask.None, session.WorldKeys);
        }

        [Fact]
        public void SameInputsGiveTheSameState()
        {
            ulong Run(long[] frames)
            {
                PracticeSession s = New();
                Frames.Drive(s, ScriptedSource.Random(51UL, 5 * Sec, 200), frames);
                return s.ComputeStateHash();
            }
            ulong a = Run(RunKit.Regular(Sec / 60.0, 100, 5 * Sec));
            ulong b = Run(RunKit.Irregular(3UL, 5 * Sec));
            Assert.Equal(a, b);
        }

        [Fact]
        public void PauseWorksAsInARun()
        {
            PracticeSession session = New();
            var src = new ScriptedSource().Add(200_500, LogicalKey.Pause, true).Add(300_000, LogicalKey.Pause, false);
            Frames.Drive(session, src, RunKit.Regular(16_667, 0, 2 * Sec));
            Assert.True(session.IsPaused);
            uint t = session.Tick;
            Assert.Equal(RunKit.TickOf(200_500).Tick + 1, t);
            session.RequestResume();
            Frames.Drive(session, src, RunKit.Regular(16_667, 2 * Sec, 3 * Sec));
            Assert.False(session.IsPaused);
            Assert.InRange((long)session.Tick, t + 59L, t + 61L);
            Assert.Equal(KeyMask.None, session.WorldKeys);
        }

        [Fact]
        public void HasNoRecordOrSubmissionAndEndsForGood()
        {
            Type t = typeof(PracticeSession);
            Assert.DoesNotContain(t.GetMethods(BindingFlags.Public | BindingFlags.Instance), m => m.ReturnType == typeof(ReadOnlyMemory<byte>));
            Assert.Null(t.GetMethod("Finish"));
            Assert.Null(t.GetMethod("Snapshot"));
            Assert.Null(t.GetMethod("MarkCheckpoint"));

            PracticeSession session = New();
            Frames.Drive(session, new ScriptedSource(), new[] { 100_000L });
            session.End(RunKit.Sec(RunKit.StartUs + 100_000));
            Assert.True(session.IsHalted);
            Assert.Throws<InvalidOperationException>(() => session.Step(session.Tick));
        }
    }
}

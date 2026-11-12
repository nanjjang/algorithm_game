using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Wizard.Client.Run;
using Wizard.Core;
using Wizard.Sim;
using Wizard.Sim.Input;
using Wizard.TestKit;
using Xunit;

namespace Wizard.Client.Tests.Run
{
    // 수락 조건 2·4: RunSession 이 세계와 기록기를 묶고, 끝난 판의 기록을 ReplayRunner 로 돌리면 같은 해시가 나온다.
    // RunSession 은 SimWorld.CanApply 가 참인 명령만 기록한다. 프레임마다 Drain → Quantize → 대기 줄,
    // 틱마다 TakeForTick → InputRecorder.Append(순번) → Step 순서다(ARCHITECTURE 6.1).
    public sealed class RunSessionTests
    {
        private const long Sec = RunKit.UsPerSecond;

        // 세계에는 아직 판 끝 규칙(Waves 모듈, M6)이 없다. 그래서 "끝난 판"의 해시는 판 끝 지점에 둔 중간 지점까지 재실행해(upToWave)
        // 대조한다. 전체 재실행은 판이 끝나지 않으므로 EndedLate 이고, 그때도 기록을 끝 틱까지 다 돌렸는지 본다.
        [Theory]
        [InlineData(11UL)]
        [InlineData(12UL)]
        public void ReplayOfTheFinishedRecordGivesTheSameHash(ulong seed)
        {
            RunSession session = RunKit.NewSession();
            ScriptedSource src = ScriptedSource.Random(seed, 20 * Sec, 600);
            long[] frames = RunKit.Irregular(seed + 100, 20 * Sec);

            ulong hashAtMid = 0;
            uint tickAtMid = 0;
            bool marked = false;
            Frames.Drive(session, src, frames, f =>
            {
                if (!marked && f >= 10 * Sec)
                {
                    session.MarkCheckpoint(5);
                    hashAtMid = session.ComputeStateHash();
                    tickAtMid = session.Tick;
                    marked = true;
                }
            });
            Assert.True(marked);
            Assert.InRange((long)session.Tick, RunKit.TicksAt(20 * Sec) - 1, RunKit.TicksAt(20 * Sec) + 1);
            session.MarkCheckpoint(10);
            ulong endHash = session.ComputeStateHash();
            uint endTick = session.Tick;
            Assert.NotEqual(hashAtMid, endHash);
            RunSession idle = RunKit.NewSession();
            Frames.Drive(idle, new ScriptedSource(), frames);
            Assert.Equal(endTick, idle.Tick);
            Assert.NotEqual(idle.ComputeStateHash(), endHash);                  // 키가 세계를 바꿨다(이동)

            byte[] file = session.Finish(RunKit.Sec(RunKit.StartUs + 20 * Sec)).ToArray();
            Assert.True(InputRecordCodec.TryRead(file, RunKit.Generous, out _, out InputRecordBody? body, out RecordError error), error.ToString());
            Assert.Equal(endTick, body!.EndTick);
            Assert.Equal(endHash, body.FinalStateHash);
            Assert.Equal(src.Count + session.DropCorrectionKeyCount, body.InputCount + session.DroppedOverflowKeyCount);
            Assert.Equal(0, body.CommandCount);
            Assert.Equal(0, body.PauseCount);
            Assert.Equal(0, session.WithheldKeyCount);
            Assert.Equal(0, session.DroppedLateKeyCount);

            Assert.Equal(session.WorldKeys, WzrcRecords.Read(file).KeysAfterInputs());
            byte[] recordBody = RunKit.BodyOf(file);
            ReplayResult toEnd = ReplayRunner.Run(RunKit.Db, RunKit.Header(), recordBody, RunKit.Generous, upToWave: 10);
            Assert.Equal(ReplayStatus.Completed, toEnd.Status);
            Assert.Equal(endHash, toEnd.Outcome!.FinalStateHash);
            Assert.Equal(endTick, toEnd.Outcome.EndTick);

            ReplayResult toMid = ReplayRunner.Run(RunKit.Db, RunKit.Header(), recordBody, RunKit.Generous, upToWave: 5);
            Assert.Equal(ReplayStatus.Completed, toMid.Status);
            Assert.Equal(hashAtMid, toMid.Outcome!.FinalStateHash);
            Assert.Equal(tickAtMid, toMid.Outcome.EndTick);

            ReplayResult whole = ReplayRunner.Run(RunKit.Db, RunKit.Header(), recordBody, RunKit.Generous);
            Assert.Equal(ReplayStatus.EndedLate, whole.Status);              // 판 끝 규칙이 아직 없다(M6)
            Assert.Equal(endTick, whole.TicksSimulated);

            // 기록의 한 바이트가 바뀌면 같은 해시로 인정되지 않는다
            byte[] bad = (byte[])recordBody.Clone();
            bad[bad.Length / 2] ^= 0x01;
            Assert.Equal(ReplayStatus.RecordCorrupt, ReplayRunner.Run(RunKit.Db, RunKit.Header(), bad, RunKit.Generous, upToWave: 10).Status);
        }

        [Fact]
        public void SnapshotUpToTheLastCheckpointReplaysToItsHash()
        {
            RunSession session = RunKit.NewSession();
            ScriptedSource src = ScriptedSource.Random(21UL, 6 * Sec, 200);
            Frames.Drive(session, src, RunKit.Regular(16_667, 1_000, 3 * Sec));
            session.MarkCheckpoint(5);
            ulong hash = session.ComputeStateHash();
            uint tick = session.Tick;
            Frames.Drive(session, src, RunKit.Regular(16_667, 3 * Sec + 1_000, 6 * Sec));

            byte[] snap = session.Snapshot().ToArray();
            Assert.True(InputRecordCodec.TryRead(snap, RunKit.Generous, out _, out InputRecordBody? body, out _));
            Assert.Equal(tick, body!.EndTick);
            Assert.Equal(hash, body.FinalStateHash);
            ReplayResult r = ReplayRunner.Run(RunKit.Db, RunKit.Header(), RunKit.BodyOf(snap), RunKit.Generous, upToWave: 5);
            Assert.Equal(ReplayStatus.Completed, r.Status);
            Assert.Equal(hash, r.Outcome!.FinalStateHash);
        }

        // 같은 물리 이벤트 열이면 프레임을 어떻게 나눠도 기록 바이트와 최종 해시가 같다(두 번 돌려도 같다)
        [Fact]
        public void SameEventsGiveTheSameRecordForEveryFramePacing()
        {
            const long End = 12 * Sec;
            var schedules = new Dictionary<string, long[]>
            {
                ["60Hz"] = RunKit.Regular(Sec / 60.0, 3_210, End),
                ["144Hz"] = RunKit.Regular(Sec / 144.0, 5_001, End),
                ["30Hz"] = RunKit.Regular(Sec / 30.0, 777, End),
                ["irregular"] = RunKit.Irregular(9UL, End),
                ["irregular-again"] = RunKit.Irregular(9UL, End),
            };
            byte[]? reference = null;
            foreach (KeyValuePair<string, long[]> s in schedules)
            {
                RunSession session = RunKit.NewSession();
                Frames.Drive(session, ScriptedSource.Random(31UL, End, 500), s.Value);
                byte[] file = session.Finish(RunKit.Sec(RunKit.StartUs + End)).ToArray();
                if (reference == null)
                {
                    reference = file;
                    continue;
                }
                Assert.True(reference.AsSpan().SequenceEqual(file), s.Key + ": record bytes differ");
            }
        }

        // 순서: 프레임에서 꺼내 양자화해 줄에 넣은 키는 그 키의 틱이 돌 때(TakeForTick) 순번을 받고(Append) 세계에 들어간다(Step).
        // 이미 지나간 틱으로 양자화된 키(늦게 온 키)는 다음 틱의 오프셋 0으로 옮겨 넣는다(버리면 키 상태가 어긋난다)
        [Fact]
        public void KeysGoQueueThenTakeAppendStepAndLateKeysMoveToTheNextTick()
        {
            RunSession session = RunKit.NewSession();
            var src = new ScriptedSource()
                .Add(170_900, LogicalKey.Up, true)
                .Add(200_100, LogicalKey.Fire, true, deliverUs: 260_000);
            (uint upTick, byte upOffset) = RunKit.TickOf(170_900);
            Assert.Equal(10U, upTick);
            Assert.Equal(12U, RunKit.TickOf(200_100).Tick);

            Step(session, src, 150_000);
            Assert.Equal(9U, session.Tick);
            Assert.Equal(KeyMask.None, session.WorldKeys);

            Step(session, src, 172_000);                   // 키를 꺼내 줄에 넣었다. 틱 10은 아직 지나지 않았다
            Assert.Equal(10U, session.Tick);
            Assert.Equal(KeyMask.None, session.WorldKeys);

            Step(session, src, 190_000);                   // 틱 10이 돌았다
            Assert.Equal(11U, session.Tick);
            Assert.Equal(Bit(LogicalKey.Up), session.WorldKeys);

            Step(session, src, 230_000);
            Assert.Equal(13U, session.Tick);               // 틱 12는 J 없이 지나갔다(J는 아직 오지 않았다)
            Step(session, src, 260_000);
            Assert.Equal(1, session.RetimedLateKeyCount);
            Assert.Equal(Bit(LogicalKey.Up) | Bit(LogicalKey.Fire), session.WorldKeys);
            Assert.Equal(0, session.DroppedLateKeyCount);

            WzrcRecords rec = WzrcRecords.Read(session.Finish(RunKit.Sec(RunKit.StartUs + 260_000)).Span);
            Assert.Equal(session.WorldKeys, rec.KeysAfterInputs());
            Assert.Equal(new[]
            {
                $"in {upTick}:{upOffset} Up down #1",
                "in 13:0 Fire down #2",
            }, rec.Records.Select(r => r.ToString()).ToArray());
        }

        // 세계가 받지 않는 명령(CanApply 거짓)은 기록하지 않는다. 지금 세계에는 0단계 명령을 받는 모듈(Spell·Camp·Debug)이 없어서
        // 모든 명령이 거짓이다. 참인 명령이 기록되는 길은 그 모듈들(M3·M6)이 생긴 뒤에 시험한다(결과 보고).
        [Fact]
        public void CommandsTheWorldDoesNotAcceptAreNotRecorded()
        {
            RunSession session = RunKit.NewSession();
            var src = new ScriptedSource().Add(50_000, LogicalKey.Left, true);
            Step(session, src, 100_000);
            session.Submit(RunCommandOp.Heal);
            session.Submit(RunCommandOp.PickCard, 0);
            session.Submit(RunCommandOp.DebugSkipWave);
            Assert.Equal(3, session.PendingCommandCount);
            Step(session, src, 120_000);
            Assert.Equal(0, session.PendingCommandCount);
            Assert.Equal(3, session.RejectedCommandCount);
            Step(session, src, 1 * Sec);
            Assert.Equal((uint)RunKit.TicksAt(1 * Sec), session.Tick);

            session.MarkCheckpoint(1);
            byte[] file = session.Finish(RunKit.Sec(RunKit.StartUs + 1 * Sec)).ToArray();
            Assert.True(InputRecordCodec.TryRead(file, RunKit.Generous, out _, out InputRecordBody? body, out _));
            Assert.Equal(0, body!.CommandCount);
            Assert.False(body.HasDebugOps);
            Assert.False(body.DebugUsed);
            Assert.Equal(ReplayStatus.Completed, ReplayRunner.Run(RunKit.Db, RunKit.Header(), RunKit.BodyOf(file), RunKit.Generous, upToWave: 1).Status);
        }

        [Fact]
        public void CommandsAfterTheEndAreRejected()
        {
            RunSession session = RunKit.NewSession(new ReplayLimits(10, 10));
            var src = new ScriptedSource();
            Step(session, src, 1 * Sec);
            Assert.True(session.IsHalted);
            session.Submit(RunCommandOp.AbandonRun);
            Assert.Equal(1, session.RejectedCommandCount);
            Assert.Equal(0, session.PendingCommandCount);
        }

        // 기록의 end_tick 은 한도(SimLimitsDef 의 RunMaxTicks)를 넘지 않는다. Finish 는 같은 한도로 TryRead 를 통과한 기록만 낸다
        [Fact]
        public void StopsAtTheRecordTickLimit()
        {
            var limits = new ReplayLimits(90, 90);
            RunSession session = RunKit.NewSession(limits);
            ScriptedSource src = ScriptedSource.Random(41UL, 3 * Sec, 100);
            Frames.Drive(session, src, RunKit.Regular(16_667, 100, 3 * Sec));
            Assert.Equal(90U, session.Tick);
            Assert.True(session.IsHalted);
            Assert.False(session.Clock.NextTickDue(RunKit.Sec(RunKit.StartUs + 10 * Sec)));
            Assert.Throws<InvalidOperationException>(() => session.Step(90));

            byte[] file = session.Finish(RunKit.Sec(RunKit.StartUs + 3 * Sec)).ToArray();
            Assert.True(InputRecordCodec.TryRead(file, limits, out _, out InputRecordBody? body, out _));
            Assert.Equal(90U, body!.EndTick);
            Assert.Throws<InvalidOperationException>(() => session.Finish(RunKit.Sec(RunKit.StartUs + 3 * Sec)));
            Assert.Throws<InvalidOperationException>(() => session.MarkCheckpoint(5));
        }

        [Fact]
        public void StepMustBeTheWorldTick()
        {
            RunSession session = RunKit.NewSession();
            Assert.Throws<InvalidOperationException>(() => session.Step(1));
            session.Step(0);
            session.Clock.Advance();
            Assert.Throws<InvalidOperationException>(() => session.Step(0));
            session.Step(1);
        }

        [Fact]
        public void RejectsAHeaderForAnotherCoreOrPack()
        {
            InputRecordHeader otherCore = RunKit.Header();
            otherCore.Setup.CoreVersion = "00000000000000000000000000000000";
            Assert.Throws<ArgumentException>(() => new RunSession(RunKit.Db, otherCore, RunKit.Generous, 0.0));

            InputRecordHeader otherPack = RunKit.Header();
            otherPack.Setup.DataVersion = RunKit.Db.DataVersion + 1;
            Assert.Throws<ArgumentException>(() => new RunSession(RunKit.Db, otherPack, RunKit.Generous, 0.0));

            Assert.Throws<ArgumentOutOfRangeException>(() => new RunSession(RunKit.Db, RunKit.Header(), new ReplayLimits(0, 0), 0.0));
        }

        // CONTRACTS 3.1: 순번을 매기는 곳은 InputRecorder 와 DuelClientCore 뿐이다. 세션 폴더의 코드는 순번을 만들지 않는다
        [Fact]
        public void RunCoreFolderDoesNotNumberSequences()
        {
            string dir = TestRepo.Path("src/Wizard.Client/Run/Core");
            string[] files = Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories);
            Assert.NotEmpty(files);
            var forbidden = new Regex(@"\bSeqCounter\b|\bnew\s+InputEvent\b|\.Next\s*\(|\bDuelClientCore\b", RegexOptions.CultureInvariant);
            foreach (string file in files)
            {
                string code = string.Join("\n", File.ReadAllLines(file).Select(l =>
                {
                    int i = l.IndexOf("//", StringComparison.Ordinal);
                    return i < 0 ? l : l.Substring(0, i);
                }));
                Match m = forbidden.Match(code);
                Assert.False(m.Success, $"{Path.GetFileName(file)}: '{m.Value}'");
            }
        }

        internal static void Step(RunSession session, ScriptedSource src, long frameUs)
        {
            src.DeliverUntil(frameUs);
            session.RunFrame(src, RunKit.Sec(RunKit.StartUs + frameUs));
        }

        internal static KeyMask Bit(LogicalKey key) => (KeyMask)(1 << (int)key);
    }
}

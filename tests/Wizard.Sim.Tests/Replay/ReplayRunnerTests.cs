using System;
using System.Linq;
using Wizard.Content;
using Wizard.Core;
using Wizard.Sim.Input;
using Xunit;

namespace Wizard.Sim.Tests.Replay
{
    // CONTRACTS 3.8 ReplayRunner.Run
    public sealed class ReplayRunnerTests
    {
        private static readonly InputRecordHeader Header = ReplayKit.Header();
        private static readonly byte[] HeaderBytes = ReplayKit.HeaderBytes(Header);

        // 무작위 키(넘치는 틱 포함), 일시정지, 5웨이브마다의 중간 지점을 섞은 판 하나. endAt 틱에 판이 끝난다
        internal static EndlessSession PlayRandom(int seed, uint ticks, bool end = true)
        {
            var rng = new Random(seed);
            var s = new EndlessSession(Header);
            while (s.World.Tick < ticks)
            {
                uint t = s.World.Tick;
                if (t > 0 && t % 150 == 0)
                {
                    s.Checkpoint((int)(t / 150) * 5);
                }
                if (rng.Next(0, 50) == 0)
                {
                    s.Recorder.MarkPause(t, (uint)rng.Next(1, 60000));
                }
                int nk = rng.Next(0, 3) == 0 ? rng.Next(1, 11) : 0;
                var keys = new QuantizedKey[nk];
                for (int i = 0; i < nk; i++)
                {
                    keys[i] = ReplayKit.Key(t, (byte)rng.Next(0, 256), (LogicalKey)rng.Next(0, 9), rng.Next(0, 2) == 1);
                }
                s.Tick(keys);
            }
            return s;
        }

        private static ReplayResult Run(byte[] file, uint endAt = uint.MaxValue, ReplayLimits? limits = null, int upToWave = 0,
                                        IReplayObserver? observer = null)
        {
            return ReplayRunner.RunCore(ReplayKit.Db, Header, ReplayKit.BodyOf(file), limits ?? ReplayKit.Generous, upToWave,
                                        observer ?? new HashObserver(endAt));
        }

        [Fact]
        public void Replay_MatchesOriginalHashEveryTick_AndCompletes()
        {
            EndlessSession s = PlayRandom(7, 900);
            byte[] file = s.EndAndFinish();
            var observer = new HashObserver(900);
            ReplayResult r = Run(file, observer: observer);

            Assert.Equal(ReplayStatus.Completed, r.Status);
            Assert.Equal(900U, r.TicksSimulated);
            Assert.Equal(s.TickHashes, observer.TickHashes);
            Assert.NotNull(r.Outcome);
            Assert.Equal(900U, r.Outcome!.EndTick);
            Assert.Equal(s.World.GetOutcome().FinalStateHash, r.Outcome.FinalStateHash);
            Assert.Equal(0, s.Rejected);

            // 해시만으로는 키가 세계에 들어갔는지 알 수 없다(키를 읽는 모듈이 아직 없다). 재실행이 Step 에 넘긴 열을 원래 실행의 열과 대조한다
            Assert.True(s.Fed.Count > 100, "the random session must feed keys");
            Assert.Equal(ReplayKit.DescribeInputs(s.FedAtTick, s.Fed), ReplayKit.DescribeInputs(observer.FedAtTick, observer.Fed));
            Assert.Empty(observer.FedCommands);
        }

        [Fact]
        public void Replay_FeedsTheSameEventsWithTheSameSeq()
        {
            EndlessSession s = PlayRandom(11, 400);
            byte[] file = s.EndAndFinish();

            // 기록을 다시 읽은 열(판독기)
            var read = new System.Collections.Generic.List<PlayerInput>();
            var readTicks = new System.Collections.Generic.List<uint>();
            foreach (RecordEntry e in InputRecorderTests.Records(file))
            {
                if (e.Kind == RecordKind.Input)
                {
                    read.Add(new PlayerInput(new PlayerSlot(0), new InputEvent(e.Tick, e.Offset, e.Key, e.Down, e.Seq)));
                    readTicks.Add(e.Tick);
                }
            }
            Assert.Equal(ReplayKit.DescribeInputs(s.FedAtTick, s.Fed), ReplayKit.DescribeInputs(readTicks, read));

            // ReplayRunner 가 틱마다 Step 에 실제로 넘긴 열(그 Step 의 world.Tick 포함)
            var observer = new HashObserver(400);
            Assert.Equal(ReplayStatus.Completed, Run(file, observer: observer).Status);
            Assert.True(s.Fed.Count > 50, "the random session must feed keys");
            Assert.Equal(ReplayKit.DescribeInputs(s.FedAtTick, s.Fed), ReplayKit.DescribeInputs(observer.FedAtTick, observer.Fed));
        }

        [Fact]
        public void Replay_FeedsCommandsBeforeKeysWithTheirSeq()
        {
            // 한 틱에 명령 셋과 키 둘. 재실행이 그 틱의 Step 에 명령 셋(순번, op, A, B)과 키 둘을 원래 실행과 같은 열로 넘긴다.
            // 0단계가 받는 모듈(야영)이 아직 없어 명령은 무효이므로 그 틱의 Step 뒤에 RecordCorrupt 로 멈춘다
            var s = new EndlessSession(Header);
            for (uint t = 0; t < 30; t++)
            {
                QuantizedKey[]? keys = t % 3 == 0
                    ? new[] { ReplayKit.Key(t, 200, LogicalKey.Fire, true), ReplayKit.Key(t, 9, LogicalKey.Left, (t & 1) == 0) }
                    : null;
                RunCommandDraft[]? commands = t == 21
                    ? new[]
                    {
                        new RunCommandDraft(RunCommandOp.SetSlot, 2, 7),
                        new RunCommandDraft(RunCommandOp.Heal, 0, 0),
                        new RunCommandDraft(RunCommandOp.SwapSlots, 1, 3),
                    }
                    : null;
                s.Tick(keys, commands);
            }
            Assert.Equal(3, s.FedCommands.Count);
            byte[] file = s.EndAndFinish();

            var observer = new HashObserver(30);
            ReplayResult r = Run(file, observer: observer);
            Assert.Equal(ReplayStatus.RecordCorrupt, r.Status);
            Assert.Equal(22U, r.TicksSimulated);
            Assert.Equal(ReplayKit.DescribeCommands(s.FedCommandsAtTick, s.FedCommands),
                         ReplayKit.DescribeCommands(observer.FedCommandsAtTick, observer.FedCommands));
            // 키는 멈춘 틱(21)의 Step 까지
            var fedUpTo21 = new System.Collections.Generic.List<PlayerInput>();
            var ticksUpTo21 = new System.Collections.Generic.List<uint>();
            for (int i = 0; i < s.Fed.Count; i++)
            {
                if (s.FedAtTick[i] <= 21)
                {
                    fedUpTo21.Add(s.Fed[i]);
                    ticksUpTo21.Add(s.FedAtTick[i]);
                }
            }
            Assert.Equal(16, fedUpTo21.Count);   // 틱 0, 3, …, 21 의 여덟 틱 × 둘
            Assert.Equal(ReplayKit.DescribeInputs(ticksUpTo21, fedUpTo21), ReplayKit.DescribeInputs(observer.FedAtTick, observer.Fed));
        }

        [Fact]
        public void PublicRun_IsTheSameAsCore()
        {
            // 공개 문은 관찰자가 없다. 판 끝 규칙의 모듈이 아직 없어 세계는 끝나지 않으므로 end_tick 에서 EndedLate 다
            EndlessSession s = PlayRandom(3, 300, false);
            byte[] file = s.FinishWithoutEnding();
            ReplayResult r = ReplayRunner.Run(ReplayKit.Db, Header, ReplayKit.BodyOf(file), ReplayKit.Generous);
            Assert.Equal(ReplayStatus.EndedLate, r.Status);
            Assert.Equal(300U, r.TicksSimulated);
            Assert.Null(r.Outcome);
        }

        [Fact]
        public void Replay_EndedEarlyAndEndedLate()
        {
            EndlessSession s = PlayRandom(5, 200);
            byte[] file = s.EndAndFinish();
            // 재실행의 판이 end_tick 전에 끝난다
            Assert.Equal(ReplayStatus.EndedEarly, Run(file, endAt: 150).Status);
            // end_tick 에 닿았는데 끝나지 않는다
            Assert.Equal(ReplayStatus.EndedLate, Run(file, endAt: 201).Status);
            Assert.Equal(ReplayStatus.Completed, Run(file, endAt: 200).Status);
        }

        [Fact]
        public void Replay_InvalidCommandIsRecordCorrupt()
        {
            // 0단계가 받는 모듈(야영)이 아직 없어 모든 명령이 무효다. 무효 명령이 하나라도 있으면 RecordCorrupt
            var s = new EndlessSession(Header);
            for (int t = 0; t < 30; t++)
            {
                s.Tick(commands: t == 20 ? new[] { new RunCommandDraft(RunCommandOp.Heal, 0, 0) } : null);
            }
            Assert.Equal(1, s.Rejected);
            byte[] file = s.EndAndFinish();
            ReplayResult r = Run(file, endAt: 30);
            Assert.Equal(ReplayStatus.RecordCorrupt, r.Status);
            Assert.Equal(21U, r.TicksSimulated);
        }

        [Fact]
        public void Replay_DebugOpIsRecordCorrupt()
        {
            // 재실행 설정(ForEndless)은 AllowDebugOps 가 꺼져 있다
            byte[] file = new RawBody().Command(4, (byte)RunCommandOp.DebugSkipWave).File(HeaderBytes, 10, 0, 0x01);
            Assert.Equal(ReplayStatus.RecordCorrupt, Run(file, endAt: 10).Status);
        }

        [Fact]
        public void Replay_CheckpointHashMismatchIsRecordCorrupt()
        {
            EndlessSession s = PlayRandom(9, 500);
            byte[] file = s.EndAndFinish();
            Assert.Equal(ReplayStatus.Completed, Run(file, endAt: 500).Status);

            // 0x04 의 state_hash 만 바꾸고 다시 봉인한다(구조와 SHA 는 맞다)
            byte[] records = ReplayKit.BodyOf(file);
            records = records.Take(records.Length - 46).ToArray();
            int at = FindCheckpoint(records, 0);
            records[at + 1 + 2 + 4] ^= 0x01;
            byte[] forged = RawBody.Seal(HeaderBytes, records, 500, s.World.GetOutcome().FinalStateHash);
            Assert.True(InputRecordCodec.TryRead(forged, ReplayKit.Generous, out _, out _, out _));
            ReplayResult r = Run(forged, endAt: 500);
            Assert.Equal(ReplayStatus.RecordCorrupt, r.Status);
            Assert.Equal(150U, r.TicksSimulated);   // 첫 중간 지점(틱 150)에서 멈췄다
        }

        [Fact]
        public void Replay_FinalHashOrEndTickChangedInFooter()
        {
            // 꼬리말의 칸을 바꾸고 다시 봉인한 기록(SHA 는 맞다)은 재실행이 잡는다
            EndlessSession s = PlayRandom(13, 120);
            byte[] file = s.EndAndFinish();
            byte[] records = ReplayKit.BodyOf(file);
            records = records.Take(records.Length - 46).ToArray();
            ulong hash = s.World.GetOutcome().FinalStateHash;

            Assert.Equal(ReplayStatus.Completed, Run(RawBody.Seal(HeaderBytes, records, 120, hash), endAt: 120).Status);
            Assert.Equal(ReplayStatus.RecordCorrupt, Run(RawBody.Seal(HeaderBytes, records, 120, hash ^ 1UL), endAt: 120).Status);
            Assert.Equal(ReplayStatus.EndedEarly, Run(RawBody.Seal(HeaderBytes, records, 121, hash), endAt: 120).Status);
            // end_tick 을 줄이면 그 틱의 판 끝이 안 온다(키가 end_tick 이후면 구조 오류)
            ReplayResult shorter = Run(RawBody.Seal(HeaderBytes, records, 119, hash), endAt: 120);
            Assert.True(shorter.Status == ReplayStatus.EndedLate || shorter.Status == ReplayStatus.RecordCorrupt);
        }

        [Fact]
        public void Replay_FileHeaderOtherThanTrustedHeaderIsRecordCorrupt()
        {
            // 믿는 머리말(run.setup_canonical)과 파일의 머리말이 다르면 SHA 가 맞지 않는다: unlocked_arts 한 비트
            EndlessSession s = PlayRandom(17, 60);
            byte[] file = s.EndAndFinish();
            InputRecordHeader other = ReplayKit.Header();
            other.Setup.UnlockedArts = (ArtMask)((int)other.Setup.UnlockedArts ^ 0x0100);
            ReplayResult r = ReplayRunner.RunCore(ReplayKit.Db, other, ReplayKit.BodyOf(file), ReplayKit.Generous, 0, new HashObserver(60));
            Assert.Equal(ReplayStatus.RecordCorrupt, r.Status);
            Assert.Equal(0U, r.TicksSimulated);
        }

        [Fact]
        public void Replay_StructuralErrorsAreRecordCorrupt()
        {
            Assert.Equal(ReplayStatus.RecordCorrupt, Run(new RawBody().Input(1, 9, LogicalKey.Skill, true).Input(0, 9, LogicalKey.Up, true).File(HeaderBytes, 5)).Status);
            Assert.Equal(ReplayStatus.RecordCorrupt, Run(new RawBody().InputPacked(0, 0, 0x0C).File(HeaderBytes, 5)).Status);
            byte[] valid = new RawBody().Input(1, 9, LogicalKey.Up, true).File(HeaderBytes, 5);
            Assert.Equal(ReplayStatus.RecordCorrupt, Run(valid.Concat(new byte[] { 1 }).ToArray()).Status);
            Assert.Equal(ReplayStatus.RecordCorrupt, Run(valid.Take(valid.Length - 1).ToArray()).Status);
        }

        [Fact]
        public void Replay_EndTickOverMaxTicksIsRecordCorrupt()
        {
            byte[] file = PlayRandom(37, 2000).EndAndFinish();
            Assert.Equal(ReplayStatus.RecordCorrupt, Run(file, endAt: 2000, limits: new ReplayLimits(1999, 1_000_000)).Status);
            Assert.Equal(ReplayStatus.Completed, Run(file, endAt: 2000, limits: new ReplayLimits(2000, 1_000_000)).Status);
        }

        [Fact]
        public void Replay_StepLimitIsTimeout()
        {
            EndlessSession s = PlayRandom(19, 100);
            byte[] file = s.EndAndFinish();
            ReplayResult r = Run(file, endAt: 100, limits: new ReplayLimits(1_000_000, 99));
            Assert.Equal(ReplayStatus.Timeout, r.Status);
            Assert.Equal(99U, r.TicksSimulated);
            Assert.Equal(ReplayStatus.Completed, Run(file, endAt: 100, limits: new ReplayLimits(1_000_000, 100)).Status);
        }

        [Fact]
        public void Replay_SimInvariantExceptionIsSimFaultAndNotThrown()
        {
            EndlessSession s = PlayRandom(23, 50);
            byte[] file = s.EndAndFinish();
            ReplayResult r = ReplayRunner.RunCore(ReplayKit.Db, Header, ReplayKit.BodyOf(file), ReplayKit.Generous, 0, new ThrowAt(17, true));
            Assert.Equal(ReplayStatus.SimFault, r.Status);
            Assert.Contains("SimInvariantException", r.FaultDetail);
            Assert.Contains("at tick 18", r.FaultDetail);   // 틱 17 의 Step 뒤(world.Tick == 18)
            Assert.Null(r.Outcome);

            ReplayResult other = ReplayRunner.RunCore(ReplayKit.Db, Header, ReplayKit.BodyOf(file), ReplayKit.Generous, 0, new ThrowAt(3, false));
            Assert.Equal(ReplayStatus.SimFault, other.Status);
            Assert.Contains("InvalidOperationException", other.FaultDetail);
        }

        [Fact]
        public void Replay_BadArgumentsAreSimFaultNotThrown()
        {
            byte[] body = ReplayKit.BodyOf(new RawBody().File(HeaderBytes, 1));
            Assert.Equal(ReplayStatus.SimFault, ReplayRunner.Run(null!, Header, body, ReplayKit.Generous).Status);
            Assert.Equal(ReplayStatus.SimFault, ReplayRunner.Run(ReplayKit.Db, null!, body, ReplayKit.Generous).Status);
            var badHeader = new InputRecordHeader { Setup = new EndlessSetup() };   // CoreVersion 이 비었다: 정규 바이트가 없다
            ReplayResult r = ReplayRunner.Run(ReplayKit.Db, badHeader, body, ReplayKit.Generous);
            Assert.Equal(ReplayStatus.SimFault, r.Status);
            Assert.NotNull(r.FaultDetail);
        }

        [Fact]
        public void Replay_DataVersionOtherThanPackIsPackMissing()
        {
            InputRecordHeader other = ReplayKit.Header();
            other.Setup.DataVersion = ReplayKit.Db.DataVersion + 1;
            byte[] otherHeader = ReplayKit.HeaderBytes(other);
            byte[] file = new RawBody().Input(0, 0, LogicalKey.Up, true).File(otherHeader, 3);
            ReplayResult r = ReplayRunner.RunCore(ReplayKit.Db, other, ReplayKit.BodyOf(file), ReplayKit.Generous, 0, null);
            Assert.Equal(ReplayStatus.PackMissing, r.Status);
        }

        [Fact]
        public void Replay_UpToWaveStopsAtThatCheckpoint()
        {
            EndlessSession s = PlayRandom(29, 700);
            byte[] snapshotAt10;
            {
                // 원정 정리: 기기가 올린 마지막 중간 지점까지의 기록(Snapshot)
                snapshotAt10 = PlayRandom(29, 301).Recorder.Snapshot().ToArray();   // 틱 300 의 중간 지점 = 웨이브 10
            }
            byte[] file = s.EndAndFinish();

            ReplayResult full = Run(file, endAt: 700, upToWave: 10);
            Assert.Equal(ReplayStatus.Completed, full.Status);
            Assert.Equal(300U, full.TicksSimulated);
            // 그 지점까지의 결과: 판은 끝나지 않았고(EndKind = None) 끝 틱과 해시는 그 지점의 것이다(CONTRACTS 3.8)
            Assert.NotNull(full.Outcome);
            Assert.Equal(Wizard.Sim.Waves.RunEndKind.None, full.Outcome!.EndKind);
            Assert.Equal(300U, full.Outcome.EndTick);
            Assert.Equal(s.TickHashes[299], full.Outcome.FinalStateHash);   // 300번째 Step 뒤 = world.Tick == 300 의 해시
            Assert.Equal(Header.Setup.StartWave, full.Outcome.StartWave);

            ReplayResult snap = Run(snapshotAt10, upToWave: 10);
            Assert.Equal(ReplayStatus.Completed, snap.Status);
            Assert.Equal(300U, snap.TicksSimulated);
            Assert.Equal(full.Outcome.FinalStateHash, snap.Outcome!.FinalStateHash);

            Assert.Equal(ReplayStatus.RecordCorrupt, Run(file, endAt: 700, upToWave: 7).Status);
        }

        [Fact]
        public void Replay_UpToWave_CheckpointHashMismatchAtTheStopTickIsRecordCorrupt()
        {
            // CONTRACTS 3.8: upToWave 는 그 지점까지의 0x04 를 대조한다. 멈춘 틱(웨이브 10, 틱 300)의 0x04 자체도 대조 대상이다
            EndlessSession s = PlayRandom(29, 700);
            byte[] file = s.EndAndFinish();
            ulong finalHash = s.World.GetOutcome().FinalStateHash;
            byte[] records = ReplayKit.BodyOf(file);
            records = records.Take(records.Length - 46).ToArray();
            int at = FindCheckpoint(records, 1);
            records[at + 1 + 2 + 4] ^= 0x01;
            byte[] forged = RawBody.Seal(HeaderBytes, records, 700, finalHash);
            Assert.True(InputRecordCodec.TryRead(forged, ReplayKit.Generous, out _, out InputRecordBody? body, out _));
            Assert.Equal(10, body!.Checkpoints[1].Wave);
            Assert.Equal(300U, body.Checkpoints[1].Tick);
            Assert.NotEqual(s.TickHashes[299], body.Checkpoints[1].StateHash);

            ReplayResult r = Run(forged, endAt: 700, upToWave: 10);
            Assert.Equal(ReplayStatus.RecordCorrupt, r.Status);
            Assert.Equal(300U, r.TicksSimulated);
            Assert.Null(r.Outcome);

            // 기기가 올린 원정 기록(Snapshot): 웨이브 10 의 0x04 가 마지막 레코드이고 그 틱이 end_tick 이다
            byte[] snap = PlayRandom(29, 301).Recorder.Snapshot().ToArray();
            Assert.Equal(ReplayStatus.Completed, Run(snap, upToWave: 10).Status);
            Assert.True(InputRecordCodec.TryRead(snap, ReplayKit.Generous, out _, out InputRecordBody? snapBody, out _));
            Assert.Equal(300U, snapBody!.EndTick);
            Assert.Equal(2, snapBody.Checkpoints.Length);
            Assert.Equal(300U, snapBody.Checkpoints[1].Tick);
            byte[] snapRecords = ReplayKit.BodyOf(snap);
            snapRecords = snapRecords.Take(snapRecords.Length - 46).ToArray();
            snapRecords[FindCheckpoint(snapRecords, 1) + 1 + 2 + 4] ^= 0x80;
            byte[] snapForged = RawBody.Seal(HeaderBytes, snapRecords, snapBody.EndTick, snapBody.FinalStateHash, (byte)(snapBody.DebugUsed ? 1 : 0));
            Assert.True(InputRecordCodec.TryRead(snapForged, ReplayKit.Generous, out _, out _, out _));
            ReplayResult rs = Run(snapForged, upToWave: 10);
            Assert.Equal(ReplayStatus.RecordCorrupt, rs.Status);
            Assert.Equal(300U, rs.TicksSimulated);
        }

        [Fact]
        public void Replay_CheckpointAtEndTickHashMismatchIsRecordCorrupt()
        {
            // tick == end_tick 의 0x04. 그 틱에는 Step 이 없으므로 고리 뒤에서 대조한다.
            // 판 끝 표시는 end_tick - 1 의 Step 이 세우므로 그 틱의 해시에 들어 있다(원래 실행도 판을 끝낸 뒤 중간 지점을 남긴다)
            EndlessSession s = PlayRandom(43, 400);
            s.World.MarkOver();
            s.Checkpoint(14);
            byte[] file = s.EndAndFinish();
            ulong finalHash = s.World.GetOutcome().FinalStateHash;
            Assert.True(InputRecordCodec.TryRead(file, ReplayKit.Generous, out _, out InputRecordBody? body, out _));
            Assert.Equal(3, body!.Checkpoints.Length);          // 틱 150, 300, 400
            Assert.Equal(400U, body.Checkpoints[2].Tick);
            Assert.Equal(400U, body.EndTick);
            Assert.Equal(ReplayStatus.Completed, Run(file, endAt: 400).Status);

            byte[] records = ReplayKit.BodyOf(file);
            records = records.Take(records.Length - 46).ToArray();
            records[FindCheckpoint(records, 2) + 1 + 2 + 4 + 7] ^= 0x01;   // 해시의 마지막 바이트
            byte[] forged = RawBody.Seal(HeaderBytes, records, 400, finalHash);
            Assert.True(InputRecordCodec.TryRead(forged, ReplayKit.Generous, out _, out _, out _));
            ReplayResult r = Run(forged, endAt: 400);
            Assert.Equal(ReplayStatus.RecordCorrupt, r.Status);
            Assert.Equal(400U, r.TicksSimulated);
            Assert.Null(r.Outcome);
        }

        [Fact]
        public void Replay_IsDeterministic_TwoRunsAndSaveRestoreGiveSameHashes()
        {
            EndlessSession s = PlayRandom(31, 600);
            byte[] file = s.EndAndFinish();
            var a = new HashObserver(600);
            var b = new HashObserver(600);
            Assert.Equal(ReplayStatus.Completed, Run(file, observer: a).Status);
            Assert.Equal(ReplayStatus.Completed, Run(file, observer: b).Status);
            Assert.Equal(a.TickHashes, b.TickHashes);

            // 재실행 도중 저장 → 진행 → 복원 → 다시 진행해도 같은 해시
            var saver = new SaveRestoreObserver(250, 600);
            Assert.Equal(ReplayStatus.Completed, Run(file, observer: saver).Status);
            Assert.Equal(a.TickHashes, saver.TickHashes);
            Assert.True(saver.Checked);
        }

        // 본문(꼬리말 앞까지)에서 nth 번째 0x04 레코드의 자리
        private static int FindCheckpoint(byte[] records, int nth)
        {
            byte[] withEnd = records.Concat(new byte[] { 0xFF }).ToArray();
            int index = 0;
            for (int pos = 0; withEnd[pos] != 0xFF; pos += RecordLength(withEnd, pos))
            {
                if (withEnd[pos] == 0x04)
                {
                    if (index == nth)
                    {
                        return pos;
                    }
                    index++;
                }
            }
            throw new InvalidOperationException("no checkpoint");
        }

        private static int RecordLength(byte[] b, int pos)
        {
            int VarLen(int p)
            {
                int n = 1;
                while ((b[p + n - 1] & 0x80) != 0)
                {
                    n++;
                }
                return n;
            }
            switch (b[pos])
            {
                case 0x01: return 1 + VarLen(pos + 1) + 2;
                case 0x02: return 1 + VarLen(pos + 1) + 9;
                case 0x03: return 1 + VarLen(pos + 1) + 4;
                case 0x04: return 1 + 2 + 4 + 8;
                default: throw new InvalidOperationException("tag");
            }
        }

        private sealed class ThrowAt : IReplayObserver
        {
            private readonly uint _tick;
            private readonly bool _invariant;

            public ThrowAt(uint tick, bool invariant)
            {
                _tick = tick;
                _invariant = invariant;
            }

            public void BeforeStep(SimWorld world, ReadOnlySpan<PlayerInput> inputs, ReadOnlySpan<RunCommand> commands)
            {
            }

            public void AfterStep(SimWorld world)
            {
                if (world.Tick == _tick + 1)
                {
                    if (_invariant)
                    {
                        throw new SimInvariantException("test fault");
                    }
                    throw new InvalidOperationException("test defect");
                }
            }
        }

        private sealed class SaveRestoreObserver : IReplayObserver
        {
            private readonly uint _saveAt;
            private readonly uint _endAt;
            private SimSnapshot? _snap;
            private ulong _hashAtSave;

            public SaveRestoreObserver(uint saveAt, uint endAt)
            {
                _saveAt = saveAt;
                _endAt = endAt;
            }

            public System.Collections.Generic.List<ulong> TickHashes { get; } = new System.Collections.Generic.List<ulong>();

            public bool Checked { get; private set; }

            public void BeforeStep(SimWorld world, ReadOnlySpan<PlayerInput> inputs, ReadOnlySpan<RunCommand> commands)
            {
            }

            public void AfterStep(SimWorld world)
            {
                if (world.Tick == _saveAt && _snap == null)
                {
                    _snap = world.CreateSnapshotBuffer();
                    world.SaveSnapshot(_snap);
                    _hashAtSave = world.ComputeStateHash();
                    // 빈 입력으로 몇 틱 진행해 상태를 흩뜨린 뒤 복원한다
                    var sink = new Runtime.RecordingSink();
                    for (int i = 0; i < 5; i++)
                    {
                        world.Step(ReadOnlySpan<PlayerInput>.Empty, ReadOnlySpan<RunCommand>.Empty, sink);
                    }
                    world.LoadSnapshot(_snap);
                    Checked = world.ComputeStateHash() == _hashAtSave && world.Tick == _saveAt;
                }
                TickHashes.Add(world.ComputeStateHash());
                if (world.Tick == _endAt)
                {
                    world.MarkOver();
                }
            }
        }
    }
}

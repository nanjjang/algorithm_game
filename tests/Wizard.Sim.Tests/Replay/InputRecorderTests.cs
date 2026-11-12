using System;
using System.Collections.Generic;
using Wizard.Core;
using Wizard.Sim.Input;
using Xunit;

namespace Wizard.Sim.Tests.Replay
{
    // CONTRACTS 3.1 "순번을 매기는 곳", 3.8 InputRecorder
    public sealed class InputRecorderTests
    {
        private static readonly PlayerSlot Slot0 = new PlayerSlot(0);

        private static (RunCommand[] cmds, int nc, PlayerInput[] ins, int ni) Append(InputRecorder rec, uint tick,
            RunCommandDraft[] commands, QuantizedKey[] keys)
        {
            var cmds = new RunCommand[commands.Length];
            var ins = new PlayerInput[InputOrder.MaxEventsPerTickPerPlayer];
            rec.Append(tick, Slot0, commands, keys, cmds, out int nc, ins, out int ni);
            return (cmds, nc, ins, ni);
        }

        [Fact]
        public void Append_NumbersCommandsFirstThenKeysByOffsetAndClass()
        {
            var rec = new InputRecorder(ReplayKit.Header());
            var commands = new[] { new RunCommandDraft(RunCommandOp.Heal, 0, 0), new RunCommandDraft(RunCommandOp.SetSlot, 2, 7) };
            var keys = new[]
            {
                ReplayKit.Key(5, 40, LogicalKey.Skill, true),     // 오프셋 40, 분류 3
                ReplayKit.Key(5, 10, LogicalKey.Fire, true),      // 오프셋 10, 분류 2
                ReplayKit.Key(5, 40, LogicalKey.Left, true),      // 오프셋 40, 분류 1 → K보다 먼저
                ReplayKit.Key(5, 10, LogicalKey.Stance, true),    // 오프셋 10, 분류 0 → J보다 먼저
            };
            var (cmds, nc, ins, ni) = Append(rec, 5, commands, keys);

            Assert.Equal(2, nc);
            Assert.Equal(1U, cmds[0].Seq);
            Assert.Equal(2U, cmds[1].Seq);
            Assert.Equal(RunCommandOp.Heal, cmds[0].Op);
            Assert.Equal(5U, cmds[1].Tick);
            Assert.Equal(7, cmds[1].B);

            Assert.Equal(4, ni);
            Assert.Equal(new[] { LogicalKey.Stance, LogicalKey.Fire, LogicalKey.Left, LogicalKey.Skill },
                         new[] { ins[0].Event.Key, ins[1].Event.Key, ins[2].Event.Key, ins[3].Event.Key });
            Assert.Equal(new uint[] { 3, 4, 5, 6 }, new[] { ins[0].Event.Seq, ins[1].Event.Seq, ins[2].Event.Seq, ins[3].Event.Seq });
            for (int i = 1; i < ni; i++)
            {
                Assert.True(InputOrder.Compare(ins[i - 1], ins[i]) < 0);
            }

            // 다음 틱은 이어서 매긴다
            var (_, _, ins2, ni2) = Append(rec, 6, Array.Empty<RunCommandDraft>(), new[] { ReplayKit.Key(6, 0, LogicalKey.Up, false) });
            Assert.Equal(1, ni2);
            Assert.Equal(7U, ins2[0].Event.Seq);
        }

        [Fact]
        public void Append_KeepsInputOrderForEqualOffsetAndClass()
        {
            // 같은 (오프셋, 분류)는 받은 순서를 지킨다(안정 정렬)
            var rec = new InputRecorder(ReplayKit.Header());
            var keys = new[]
            {
                ReplayKit.Key(0, 3, LogicalKey.Right, true),
                ReplayKit.Key(0, 3, LogicalKey.Up, true),
                ReplayKit.Key(0, 3, LogicalKey.Right, false),
            };
            var (_, _, ins, ni) = Append(rec, 0, Array.Empty<RunCommandDraft>(), keys);
            Assert.Equal(3, ni);
            Assert.Equal(LogicalKey.Right, ins[0].Event.Key);
            Assert.True(ins[0].Event.Down);
            Assert.Equal(LogicalKey.Up, ins[1].Event.Key);
            Assert.Equal(LogicalKey.Right, ins[2].Event.Key);
            Assert.False(ins[2].Event.Down);
        }

        [Fact]
        public void Append_DropsNinthKeyWithoutNumber()
        {
            var rec = new InputRecorder(ReplayKit.Header());
            var keys = new QuantizedKey[10];
            for (int i = 0; i < keys.Length; i++)
            {
                keys[i] = ReplayKit.Key(3, (byte)(i * 10), (i & 1) == 0 ? LogicalKey.Up : LogicalKey.Down, true);
            }
            var (_, _, ins, ni) = Append(rec, 3, Array.Empty<RunCommandDraft>(), keys);
            Assert.Equal(8, ni);
            Assert.Equal(8U, ins[7].Event.Seq);
            Assert.Equal((byte)70, ins[7].Event.Offset);
            Assert.Equal(2, rec.DroppedKeyCount);

            // 버린 키는 번호를 쓰지 않았다: 다음 키는 9번
            var (_, _, ins2, _) = Append(rec, 4, Array.Empty<RunCommandDraft>(), new[] { ReplayKit.Key(4, 0, LogicalKey.Fire, true) });
            Assert.Equal(9U, ins2[0].Event.Seq);

            // 기록에도 8개만 있다
            byte[] file = rec.Finish(5, 0).ToArray();
            List<RecordEntry> records = Records(file);
            Assert.Equal(9, records.Count);
        }

        [Fact]
        public void RereadSeqEqualsRuntimeSeq()
        {
            // 무작위 입력 600틱: 명령, 넘치는 키, 일시정지, 중간 지점을 섞는다
            var rng = new Random(20261112);
            var rec = new InputRecorder(ReplayKit.Header());
            var runtime = new List<(RecordKind kind, uint tick, uint seq, byte offset, LogicalKey key, bool down, RunCommandOp op)>();
            var cmds = new RunCommand[4];
            var ins = new PlayerInput[8];
            for (uint t = 0; t < 600; t++)
            {
                if (t % 97 == 50)
                {
                    rec.MarkPause(t, (uint)rng.Next(1, 100000));
                }
                if (t % 120 == 0 && t > 0)
                {
                    rec.MarkCheckpoint((int)(t / 120) * 5, t, (ulong)rng.NextInt64());
                }
                int nk = rng.Next(0, 4) == 0 ? rng.Next(0, 12) : 0;
                var keys = new QuantizedKey[nk];
                for (int i = 0; i < nk; i++)
                {
                    keys[i] = ReplayKit.Key(t, (byte)rng.Next(0, 256), (LogicalKey)rng.Next(0, 9), rng.Next(0, 2) == 1);
                }
                int ncmd = rng.Next(0, 20) == 0 ? rng.Next(1, 4) : 0;
                var drafts = new RunCommandDraft[ncmd];
                for (int i = 0; i < ncmd; i++)
                {
                    drafts[i] = new RunCommandDraft((RunCommandOp)rng.Next(1, 13), rng.Next(), rng.Next());
                }
                rec.Append(t, Slot0, drafts, keys, cmds, out int nc, ins, out int ni);
                for (int i = 0; i < nc; i++)
                {
                    runtime.Add((RecordKind.Command, cmds[i].Tick, cmds[i].Seq, 0, 0, false, cmds[i].Op));
                }
                for (int i = 0; i < ni; i++)
                {
                    InputEvent e = ins[i].Event;
                    runtime.Add((RecordKind.Input, e.Tick, e.Seq, e.Offset, e.Key, e.Down, 0));
                }
            }
            byte[] file = rec.Finish(600, 0).ToArray();

            Assert.True(InputRecordCodec.TryRead(file, ReplayKit.Generous, out _, out _, out RecordError error), error.ToString());
            var reread = new List<(RecordKind, uint, uint, byte, LogicalKey, bool, RunCommandOp)>();
            foreach (RecordEntry r in Records(file))
            {
                if (r.Kind == RecordKind.Input)
                {
                    reread.Add((RecordKind.Input, r.Tick, r.Seq, r.Offset, r.Key, r.Down, 0));
                }
                else if (r.Kind == RecordKind.Command)
                {
                    reread.Add((RecordKind.Command, r.Tick, r.Seq, 0, 0, false, r.Op));
                }
            }
            Assert.True(runtime.Count > 100);
            Assert.Equal(runtime, reread);
        }

        [Fact]
        public void Append_RejectsMisuseWithoutRecording()
        {
            var rec = new InputRecorder(ReplayKit.Header());
            var cmds = new RunCommand[2];
            var ins = new PlayerInput[8];
            rec.Append(10, Slot0, ReadOnlySpan<RunCommandDraft>.Empty, ReadOnlySpan<QuantizedKey>.Empty, cmds, out _, ins, out _);

            Assert.Throws<InvalidOperationException>(() =>
                rec.Append(10, Slot0, Array.Empty<RunCommandDraft>(), Array.Empty<QuantizedKey>(), cmds, out _, ins, out _));
            Assert.Throws<InvalidOperationException>(() =>
                rec.Append(9, Slot0, Array.Empty<RunCommandDraft>(), Array.Empty<QuantizedKey>(), cmds, out _, ins, out _));
            Assert.Throws<ArgumentException>(() =>
                rec.Append(11, new PlayerSlot(1), Array.Empty<RunCommandDraft>(), Array.Empty<QuantizedKey>(), cmds, out _, ins, out _));
            Assert.Throws<ArgumentException>(() =>
                rec.Append(11, Slot0, Array.Empty<RunCommandDraft>(), new[] { ReplayKit.Key(12, 0, LogicalKey.Up, true) }, cmds, out _, ins, out _));
            Assert.Throws<ArgumentException>(() =>
                rec.Append(11, Slot0, Array.Empty<RunCommandDraft>(), new[] { ReplayKit.Key(11, 0, (LogicalKey)9, true) }, cmds, out _, ins, out _));
            Assert.Throws<ArgumentException>(() =>
                rec.Append(11, Slot0, new[] { new RunCommandDraft((RunCommandOp)99, 0, 0) }, Array.Empty<QuantizedKey>(), cmds, out _, ins, out _));
            Assert.Throws<ArgumentException>(() =>
                rec.Append(11, Slot0, new RunCommandDraft[3], Array.Empty<QuantizedKey>(), cmds, out _, ins, out _));
            Assert.Throws<InvalidOperationException>(() => rec.MarkCheckpoint(5, 10, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => rec.MarkCheckpoint(70000, 20, 0));
            Assert.Throws<InvalidOperationException>(() => rec.Snapshot());

            // 실패한 호출은 아무것도 남기지 않았다: 다음 틱이 순번 1부터 받는다
            rec.Append(11, Slot0, Array.Empty<RunCommandDraft>(), new[] { ReplayKit.Key(11, 0, LogicalKey.Up, true) }, cmds, out _, ins, out int ni);
            Assert.Equal(1, ni);
            Assert.Equal(1U, ins[0].Event.Seq);

            rec.Finish(12, 0);
            Assert.Throws<InvalidOperationException>(() => rec.Finish(13, 0));
            Assert.Throws<InvalidOperationException>(() =>
                rec.Append(20, Slot0, Array.Empty<RunCommandDraft>(), Array.Empty<QuantizedKey>(), cmds, out _, ins, out _));
        }

        [Fact]
        public void Finish_SetsDebugUsedOnlyWhenDebugOpRecorded()
        {
            var plain = new InputRecorder(ReplayKit.Header());
            Append(plain, 0, new[] { new RunCommandDraft(RunCommandOp.Heal, 0, 0) }, Array.Empty<QuantizedKey>());
            Assert.True(InputRecordCodec.TryRead(plain.Finish(1, 0).Span, ReplayKit.MaxTicks(10), out _, out InputRecordBody? f0, out _));
            Assert.False(f0!.DebugUsed);
            Assert.False(f0.HasDebugOps);

            var debug = new InputRecorder(ReplayKit.Header());
            Append(debug, 0, Array.Empty<RunCommandDraft>(), Array.Empty<QuantizedKey>());
            debug.MarkCheckpoint(5, 1, 0x77);
            Append(debug, 1, new[] { new RunCommandDraft(RunCommandOp.DebugGrant, 1, 100) }, Array.Empty<QuantizedKey>());
            // 디버그 op 앞의 중간 지점까지의 파일은 꺼진 채다
            Assert.True(InputRecordCodec.TryRead(debug.Snapshot().Span, ReplayKit.MaxTicks(10), out _, out InputRecordBody? fs, out _));
            Assert.False(fs!.DebugUsed);
            Assert.False(fs.HasDebugOps);
            Assert.Equal(1U, fs.EndTick);
            Assert.Equal(0x77UL, fs.FinalStateHash);
            Assert.True(InputRecordCodec.TryRead(debug.Finish(2, 0).Span, ReplayKit.MaxTicks(10), out _, out InputRecordBody? f1, out _));
            Assert.True(f1!.DebugUsed);
            Assert.True(f1.HasDebugOps);
        }

        [Fact]
        public void Snapshot_EndsAtLastCheckpoint()
        {
            var rec = new InputRecorder(ReplayKit.Header());
            var ins = new PlayerInput[8];
            var cmds = new RunCommand[1];
            rec.Append(0, Slot0, Array.Empty<RunCommandDraft>(), new[] { ReplayKit.Key(0, 1, LogicalKey.Up, true) }, cmds, out _, ins, out _);
            rec.MarkCheckpoint(5, 1, 0xABCDUL);
            rec.Append(1, Slot0, Array.Empty<RunCommandDraft>(), new[] { ReplayKit.Key(1, 1, LogicalKey.Up, false) }, cmds, out _, ins, out _);
            rec.Append(2, Slot0, Array.Empty<RunCommandDraft>(), new[] { ReplayKit.Key(2, 1, LogicalKey.Up, true) }, cmds, out _, ins, out _);

            byte[] snap = rec.Snapshot().ToArray();
            List<RecordEntry> records = Records(snap);
            Assert.Equal(2, records.Count);
            Assert.Equal(RecordKind.Input, records[0].Kind);
            Assert.Equal(RecordKind.Checkpoint, records[1].Kind);
            Assert.Equal((ushort)5, records[1].Wave);
            Assert.Equal(0xABCDUL, records[1].StateHash);

            // 스냅숏 뒤에도 계속 기록할 수 있고, 끝 파일은 전부를 담는다
            Assert.Equal(4, Records(rec.Finish(3, 0).ToArray()).Count);
        }

        // 파일의 레코드(검사를 통과한 것)
        internal static List<RecordEntry> Records(byte[] file)
        {
            Assert.True(EndlessSetup.TryReadCanonicalPrefix(file, out _, out int length));
            var list = new List<RecordEntry>();
            Collect(file.AsSpan(length), list);
            return list;
        }

        private static void Collect(ReadOnlySpan<byte> body, List<RecordEntry> list)
        {
            var reader = new WzrcBodyReader(body);
            while (reader.Next(out RecordEntry e, out _))
            {
                list.Add(e);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Wizard.Content;
using Wizard.Core;
using Wizard.Sim.Duel;
using Wizard.Sim.Input;
using Wizard.Sim.Tests.Runtime;
using Xunit;

namespace Wizard.Sim.Tests.Replay
{
    // CONTRACTS 3.8·11.1, DECISIONS-TECH T-48: WZDR(결투 확정 기록) 코덱, DuelRecordWriter, ReplayRunner.RunDuel.
    // 머리말 = DuelSetup 의 정규 바이트, 그 뒤에 input_delay 두 칸, 0x05 레코드의 열, 꼬리말
    public sealed class DuelRecordTests
    {
        private static DuelSetup Setup(ulong seed = 0xD0E1UL) => new DuelSetup
        {
            MatchId = new Guid("0f1e2d3c-4b5a-6978-8796-a5b4c3d2e1f0"),
            Seed = seed,
            CoreVersion = ReplayKit.CoreVersionHex,
            DataVersion = ReplayKit.Db.DataVersion,
            Options = new DuelRoomOptions
            {
                Rounds = 3,
                RoundSeconds = 90,
                HpMultPermille = 1000,
                EnhanceCap = 13,
                AllowMutations = true,
                FloorColor = TimeOfDay.Day,
            },
            Players = new[]
            {
                new DuelPlayerSetup
                {
                    Slot = new PlayerSlot(0),
                    AppliedWand = new WandSnapshot { Character = CharacterId.Char1, Stage = 4, Core = (BlockId)1, Links = new[] { (BlockId)2 } },
                    ActualStage = 6,
                    MaxHpPoints = 300,
                    UnlockedArts = (ArtMask)0x0003,
                },
                new DuelPlayerSetup
                {
                    Slot = new PlayerSlot(1),
                    AppliedWand = new WandSnapshot { Character = CharacterId.Char4, Stage = 4, Mutations = new[] { (MutationId)2 }, Core = (BlockId)3 },
                    ActualStage = 4,
                    MaxHpPoints = 280,
                    UnlockedArts = (ArtMask)0x0010,
                },
            },
        };

        private static byte[] SetupBytes(DuelSetup? setup = null)
        {
            var w = new ByteWriter();
            (setup ?? Setup()).WriteCanonical(w);
            return w.ToArray();
        }

        private static readonly byte[] HeaderBytes = SetupBytes();

        // fed 를 주면 원래 실행이 틱마다 Step 에 넘긴 확정 입력을 (그 Step 의 world.Tick 과 함께) 남긴다
        private static (byte[] file, List<ulong> hashes, ulong finalHash) PlayDuel(int seed, uint ticks, List<string>? fed = null)
        {
            var rng = new Random(seed);
            DuelSetup setup = Setup();
            SimWorld world = SimWorld.CreateDuel(ReplayKit.Db, SimConfig.ForDuel(ReplayKit.Db, setup.Options), setup);
            var writer = new DuelRecordWriter(setup);
            writer.SetInputDelay(new PlayerSlot(0), 2);
            writer.SetInputDelay(new PlayerSlot(1), 3);
            var seqs = new SeqCounter[2];
            var hashes = new List<ulong>();
            var sink = new RecordingSink();
            var buffer = new PlayerInput[16];
            for (uint t = 0; t < ticks; t++)
            {
                int n = 0;
                for (byte p = 0; p < 2; p++)
                {
                    int k = rng.Next(0, 3) == 0 ? rng.Next(1, 9) : 0;
                    var raw = new List<QuantizedKey>();
                    for (int i = 0; i < k; i++)
                    {
                        raw.Add(new QuantizedKey(t, (byte)rng.Next(0, 256), (LogicalKey)rng.Next(0, 8), rng.Next(0, 2) == 1));
                    }
                    // DuelClientCore 처럼 (offset, KeyClass) 순으로 놓은 뒤 순번을 매긴다(OrderBy 는 안정 정렬)
                    foreach (QuantizedKey q in raw.OrderBy(q => q.Offset).ThenBy(q => InputOrder.KeyClass(q.Key)))
                    {
                        if (rng.Next(0, 10) == 0)
                        {
                            seqs[p].Next();   // 서버가 유실로 소비한 순번(건너뜀)
                        }
                        buffer[n++] = new PlayerInput(new PlayerSlot(p), new InputEvent(t, q.Offset, q.Key, q.Down, seqs[p].Next()));
                    }
                }
                InputOrder.Sort(buffer.AsSpan(0, n));
                for (int i = 0; i < n; i++)
                {
                    writer.Append(buffer[i]);
                    fed?.Add(ReplayKit.Describe(world.Tick, buffer[i]));
                }
                sink.Clear();
                world.Step(buffer.AsSpan(0, n), ReadOnlySpan<RunCommand>.Empty, sink);
                hashes.Add(world.ComputeStateHash());
            }
            world.MarkOver();
            ulong final = world.GetDuelOutcome().FinalStateHash;
            return (writer.Finish(world.Tick, final).ToArray(), hashes, final);
        }

        private static RecordError Read(byte[] file, uint maxTicks = uint.MaxValue)
        {
            bool ok = DuelRecordCodec.TryRead(file, ReplayKit.MaxTicks(maxTicks), out DuelSetup? setup, out DuelRecordBody? body, out RecordError e);
            Assert.Equal(ok, e == RecordError.None);
            Assert.Equal(ok, setup != null && body != null);
            return e;
        }

        // 손으로 봉인한 WZDR: 머리말 ‖ input_delay(2, 2) ‖ 레코드 ‖ 꼬리말
        private static byte[] Seal(ByteWriter records, uint endTick, ulong hash = 0, byte flags = 0)
        {
            byte[] prefix = HeaderBytes.Concat(new byte[] { 2, 2 }).ToArray();
            return RawBody.Seal(prefix, records.ToArray(), endTick, hash, flags);
        }

        private static void Rec(ByteWriter w, ulong delta, byte slot, byte offset, LogicalKey key, bool down, ulong seq)
        {
            w.U8(0x05);
            w.VarUInt(delta);
            w.U8(slot);
            w.U8(offset);
            w.U8((byte)((int)key | (down ? 0x10 : 0)));
            w.VarUInt(seq);
        }

        [Fact]
        public void RunDuel_ReplaysTwoPlayerConfirmedRecordWithSameHashes()
        {
            var fed = new List<string>();
            var (file, hashes, final) = PlayDuel(41, 600, fed);
            var observer = new HashObserver(600);
            DuelReplayResult r = ReplayRunner.RunDuelCore(ReplayKit.Db, file, ReplayKit.Generous, observer);
            Assert.Equal(ReplayStatus.Completed, r.Status);
            Assert.Equal(600U, r.TicksSimulated);
            Assert.Equal(hashes, observer.TickHashes);
            Assert.Equal(final, r.Outcome!.FinalStateHash);
            Assert.Equal(600U, r.Outcome.EndTick);

            // 해시만으로는 입력이 세계에 들어갔는지 알 수 없다(키를 읽는 모듈이 아직 없다).
            // 재실행이 틱마다 Step 에 넘긴 두 사람의 확정 입력(슬롯, 오프셋, 키, 순번, 그 Step 의 틱)이 원래 실행의 열과 같다
            Assert.True(fed.Count > 300, "the random duel must feed inputs");
            Assert.Contains(fed, d => d.Contains("|0:"));
            Assert.Contains(fed, d => d.Contains("|1:"));
            Assert.Equal(fed, ReplayKit.DescribeInputs(observer.FedAtTick, observer.Fed));
            Assert.Empty(observer.FedCommands);

            // 두 번 돌려도 같다
            var again = new HashObserver(600);
            Assert.Equal(ReplayStatus.Completed, ReplayRunner.RunDuelCore(ReplayKit.Db, file, ReplayKit.Generous, again).Status);
            Assert.Equal(hashes, again.TickHashes);
        }

        [Fact]
        public void PublicRunDuel_ReadsTheSetupFromTheFile()
        {
            // 공개 문은 관찰자가 없다. 경기 끝 규칙의 모듈이 아직 없어 세계는 끝나지 않으므로 end_tick 에서 EndedLate 다.
            // 머리말을 읽어 2인 세계를 만들고 확정 입력을 끝까지 넣었다는 뜻이다
            var (file, _, _) = PlayDuel(47, 120);
            DuelReplayResult r = ReplayRunner.RunDuel(ReplayKit.Db, file, ReplayKit.Generous);
            Assert.Equal(ReplayStatus.EndedLate, r.Status);
            Assert.Equal(120U, r.TicksSimulated);
            Assert.Null(r.Outcome);
        }

        [Fact]
        public void RunDuel_StatusesFollowTheSameRulesAsRun()
        {
            var (file, _, final) = PlayDuel(53, 200);
            Assert.Equal(ReplayStatus.EndedEarly, ReplayRunner.RunDuelCore(ReplayKit.Db, file, ReplayKit.Generous, new HashObserver(150)).Status);
            Assert.Equal(ReplayStatus.EndedLate, ReplayRunner.RunDuelCore(ReplayKit.Db, file, ReplayKit.Generous, new HashObserver(201)).Status);
            Assert.Equal(ReplayStatus.RecordCorrupt,
                ReplayRunner.RunDuelCore(ReplayKit.Db, file, new ReplayLimits(199, 1_000_000), new HashObserver(200)).Status);
            DuelReplayResult timeout = ReplayRunner.RunDuelCore(ReplayKit.Db, file, new ReplayLimits(1_000_000, 199), new HashObserver(200));
            Assert.Equal(ReplayStatus.Timeout, timeout.Status);
            Assert.Equal(199U, timeout.TicksSimulated);

            // 꼬리말의 final_state_hash 가 재실행의 해시와 다르다(다시 봉인해 SHA 는 맞다)
            int prefix = HeaderBytes.Length + 2;
            byte[] records = file.Skip(prefix).Take(file.Length - prefix - 46).ToArray();
            byte[] head = file.Take(prefix).ToArray();
            Assert.Equal(ReplayStatus.Completed,
                ReplayRunner.RunDuelCore(ReplayKit.Db, RawBody.Seal(head, records, 200, final), ReplayKit.Generous, new HashObserver(200)).Status);
            Assert.Equal(ReplayStatus.RecordCorrupt,
                ReplayRunner.RunDuelCore(ReplayKit.Db, RawBody.Seal(head, records, 200, final ^ 1UL), ReplayKit.Generous, new HashObserver(200)).Status);

            // 예외는 밖으로 나오지 않는다
            DuelReplayResult fault = ReplayRunner.RunDuelCore(ReplayKit.Db, file, ReplayKit.Generous, new Thrower(17));
            Assert.Equal(ReplayStatus.SimFault, fault.Status);
            Assert.Contains("SimInvariantException", fault.FaultDetail);
            Assert.Contains("at tick 18", fault.FaultDetail);
            Assert.Null(fault.Outcome);
            Assert.Equal(ReplayStatus.SimFault, ReplayRunner.RunDuel(null!, file, ReplayKit.Generous).Status);
        }

        [Fact]
        public void RunDuel_DataVersionOtherThanPackIsPackMissing()
        {
            DuelSetup other = Setup();
            other.DataVersion = ReplayKit.Db.DataVersion + 1;
            byte[] file = new DuelRecordWriter(other).Finish(5, 0).ToArray();
            Assert.Equal(RecordError.None, Read(file));
            Assert.Equal(ReplayStatus.PackMissing, ReplayRunner.RunDuel(ReplayKit.Db, file, ReplayKit.Generous).Status);
        }

        [Fact]
        public void Wzdr_LayoutIsSetupCanonicalThenInputDelayThenRecordsThenFooter()
        {
            DuelSetup setup = Setup();
            var writer = new DuelRecordWriter(setup);
            writer.SetInputDelay(new PlayerSlot(1), 4);
            writer.SetInputDelay(new PlayerSlot(0), 9);
            writer.SetInputDelay(new PlayerSlot(0), 2);     // 마지막으로 준 값이 실린다
            writer.Append(new PlayerInput(new PlayerSlot(1), new InputEvent(3, 200, LogicalKey.Right, true, 7)));
            writer.Append(new PlayerInput(new PlayerSlot(0), new InputEvent(3, 200, LogicalKey.Skill, true, 1)));
            writer.Append(new PlayerInput(new PlayerSlot(0), new InputEvent(130, 0, LogicalKey.Skill, false, 300)));
            byte[] file = writer.Finish(131, 0x0102030405060708UL).ToArray();

            var expected = new ByteWriter();
            expected.Bytes(HeaderBytes);                    // 머리말 = DuelSetup 의 정규 바이트
            expected.U8(2); expected.U8(4);                 // input_delay u8 × 2(슬롯 순)
            // 0x05: varuint Δtick, u8 slot, u8 offset, u8 key_down, varuint seq
            expected.U8(0x05); expected.U8(3); expected.U8(1); expected.U8(200); expected.U8(0x13); expected.U8(7);
            expected.U8(0x05); expected.U8(0); expected.U8(0); expected.U8(200); expected.U8(0x15); expected.U8(1);
            expected.U8(0x05); expected.U8(127); expected.U8(0); expected.U8(0); expected.U8(0x05); expected.U8(0xAC); expected.U8(0x02);
            // 꼬리말: 0xFF end_tick u32 final_state_hash u64 flags u8 record_sha256 = 그 앞의 모든 바이트의 SHA-256
            expected.U8(0xFF); expected.U32(131); expected.U64(0x0102030405060708UL); expected.U8(0);
            expected.Bytes(SHA256.HashData(expected.ToArray()));
            Assert.Equal(expected.ToArray(), file);

            Assert.True(DuelRecordCodec.TryRead(file, ReplayKit.Generous, out DuelSetup? read, out DuelRecordBody? body, out RecordError error));
            Assert.Equal(RecordError.None, error);
            Assert.Equal(HeaderBytes, SetupBytes(read));
            Assert.Equal(HeaderBytes.Length, body!.HeaderLength);
            Assert.Equal(2, body.InputDelayTicks0);
            Assert.Equal(4, body.InputDelayTicks1);
            Assert.Equal(131U, body.EndTick);
            Assert.Equal(0x0102030405060708UL, body.FinalStateHash);
            Assert.Equal(3, body.InputCount);
        }

        [Fact]
        public void Wzdr_RejectsAnyChangedBitTruncationAndTrailingBytes()
        {
            var (file, _, _) = PlayDuel(43, 50);
            Assert.Equal(RecordError.None, Read(file));

            // 머리말, input_delay, 본문, 꼬리말의 칸, SHA 어디든 한 비트만 바뀌어도 거절한다
            for (int i = 0; i < file.Length; i++)
            {
                for (int bit = 0; bit < 8; bit++)
                {
                    byte[] changed = (byte[])file.Clone();
                    changed[i] ^= (byte)(1 << bit);
                    Assert.NotEqual(RecordError.None, Read(changed));
                }
            }
            // input_delay 두 칸은 구조 검사에 걸리지 않으므로 SHA 가 잡는다
            for (int i = HeaderBytes.Length; i < HeaderBytes.Length + 2; i++)
            {
                byte[] changed = (byte[])file.Clone();
                changed[i] ^= 0x40;
                Assert.Equal(RecordError.ShaMismatch, Read(changed));
            }
            for (int n = 0; n < file.Length; n++)
            {
                Assert.NotEqual(RecordError.None, Read(file.Take(n).ToArray()));
            }
            Assert.Equal(RecordError.TrailingBytes, Read(file.Concat(new byte[] { 0 }).ToArray()));
        }

        [Fact]
        public void Wzdr_RejectsBadMagicVersionAndHeader()
        {
            var (file, _, _) = PlayDuel(59, 10);
            byte[] magic = (byte[])file.Clone();
            magic[3] = (byte)'C';                                   // .wzrc 의 magic
            Assert.Equal(RecordError.BadMagic, Read(magic));
            byte[] version = (byte[])file.Clone();
            version[4] = 2;
            Assert.Equal(RecordError.BadVersion, Read(version));
            Assert.Equal(RecordError.Truncated, Read(Array.Empty<byte>()));
            Assert.Equal(RecordError.BadHeader, Read(file.Take(40).ToArray()));          // 머리말 도중에 끝남
            Assert.Equal(RecordError.Truncated, Read(file.Take(HeaderBytes.Length + 1).ToArray()));   // input_delay 가 한 칸뿐

            // .wzrc 는 WZDR 이 아니다
            byte[] wzrc = new RawBody().File(ReplayKit.HeaderBytes(ReplayKit.Header()), 1);
            Assert.Equal(RecordError.BadMagic, Read(wzrc));
            Assert.Equal(ReplayStatus.RecordCorrupt, ReplayRunner.RunDuel(ReplayKit.Db, wzrc, ReplayKit.Generous).Status);
        }

        [Fact]
        public void Wzdr_RejectsStructuralErrors()
        {
            var w = new ByteWriter();
            Rec(w, 0, 0, 5, LogicalKey.Up, true, 1);
            Rec(w, 0, 1, 5, LogicalKey.Up, true, 1);
            Assert.Equal(RecordError.None, Read(Seal(w, 1)));

            // 슬롯 2(결투는 두 사람)
            w = new ByteWriter();
            Rec(w, 0, 2, 5, LogicalKey.Up, true, 1);
            Assert.Equal(RecordError.BadSlot, Read(Seal(w, 1)));
            // 범위 밖 키
            w = new ByteWriter();
            w.U8(0x05); w.U8(0); w.U8(0); w.U8(0); w.U8(0x0A); w.U8(1);
            Assert.Equal(RecordError.BadKey, Read(Seal(w, 1)));
            // 순번 0, 순번 되풀이, uint 를 넘는 순번
            w = new ByteWriter();
            Rec(w, 0, 0, 5, LogicalKey.Up, true, 0);
            Assert.Equal(RecordError.BadSeq, Read(Seal(w, 1)));
            w = new ByteWriter();
            Rec(w, 0, 0, 5, LogicalKey.Up, true, 3);
            Rec(w, 1, 0, 5, LogicalKey.Up, false, 3);
            Assert.Equal(RecordError.BadSeq, Read(Seal(w, 2)));
            w = new ByteWriter();
            Rec(w, 0, 0, 5, LogicalKey.Up, true, 1UL << 32);
            Assert.Equal(RecordError.BadSeq, Read(Seal(w, 1)));
            // 같은 틱 안의 InputOrder 역행: 같은 오프셋에서 슬롯 1 뒤의 슬롯 0
            w = new ByteWriter();
            Rec(w, 0, 1, 5, LogicalKey.Up, true, 1);
            Rec(w, 0, 0, 5, LogicalKey.Up, true, 1);
            Assert.Equal(RecordError.KeyOrder, Read(Seal(w, 1)));
            // 한 사람 9개(다른 사람 8개는 된다)
            w = new ByteWriter();
            for (int i = 0; i < 8; i++)
            {
                Rec(w, 0, 0, (byte)(i * 2), LogicalKey.Fire, (i & 1) == 0, (ulong)(i + 1));
                Rec(w, 0, 1, (byte)(i * 2), LogicalKey.Fire, (i & 1) == 0, (ulong)(i + 1));
            }
            Assert.Equal(RecordError.None, Read(Seal(w, 1)));
            Rec(w, 0, 0, 100, LogicalKey.Fire, true, 9);
            Assert.Equal(RecordError.TooManyKeys, Read(Seal(w, 1)));
            // .wzrc 의 꼬리표는 WZDR 에 없다
            w = new ByteWriter();
            w.U8(0x01); w.U8(0); w.U8(0); w.U8(0);
            Assert.Equal(RecordError.UnknownTag, Read(Seal(w, 1)));
            // Δtick 의 넘침
            w = new ByteWriter();
            Rec(w, uint.MaxValue, 0, 5, LogicalKey.Up, true, 1);
            Rec(w, 1, 0, 5, LogicalKey.Up, false, 2);
            Assert.Equal(RecordError.TickOverflow, Read(Seal(w, uint.MaxValue)));
            // end_tick 과 상한
            w = new ByteWriter();
            Rec(w, 7, 0, 5, LogicalKey.Up, true, 1);
            Assert.Equal(RecordError.TickOverflow, Read(Seal(w, 7)));
            Assert.Equal(RecordError.EndTickOverLimit, Read(Seal(w, 8), 7));
            Assert.Equal(RecordError.None, Read(Seal(w, 8), 8));
            // 결투에는 디버그 op 이 없다: 꼬리말 flags 는 0 뿐이다
            for (int bit = 0; bit < 8; bit++)
            {
                Assert.Equal(RecordError.BadFlags, Read(Seal(w, 8, 0, (byte)(1 << bit))));
            }
        }

        [Fact]
        public void Writer_RejectsMisuseWithoutRecording()
        {
            Assert.Throws<ArgumentNullException>(() => new DuelRecordWriter(null!));
            Assert.Throws<InvalidOperationException>(() => new DuelRecordWriter(WorldKit.Duel(1)));   // CoreVersion 이 비었다: 정규 바이트가 없다

            var writer = new DuelRecordWriter(Setup());
            PlayerInput In(byte slot, uint tick, byte offset, LogicalKey key, uint seq) =>
                new PlayerInput(new PlayerSlot(slot), new InputEvent(tick, offset, key, true, seq));

            writer.Append(In(0, 10, 50, LogicalKey.Fire, 5));
            Assert.Throws<ArgumentException>(() => writer.Append(In(2, 11, 0, LogicalKey.Up, 1)));              // 슬롯
            Assert.Throws<ArgumentException>(() => writer.Append(In(1, 11, 0, (LogicalKey)9, 1)));              // 키
            Assert.Throws<InvalidOperationException>(() => writer.Append(In(1, 11, 0, LogicalKey.Up, 0)));      // 순번 0
            Assert.Throws<InvalidOperationException>(() => writer.Append(In(0, 11, 0, LogicalKey.Up, 5)));      // 순번이 늘지 않음
            Assert.Throws<InvalidOperationException>(() => writer.Append(In(1, 9, 0, LogicalKey.Up, 1)));       // 틱 역행
            Assert.Throws<InvalidOperationException>(() => writer.Append(In(1, 10, 49, LogicalKey.Up, 1)));     // 틱 안의 순서 역행
            Assert.Throws<ArgumentException>(() => writer.SetInputDelay(new PlayerSlot(2), 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => writer.SetInputDelay(new PlayerSlot(0), 256));
            Assert.Throws<ArgumentOutOfRangeException>(() => writer.SetInputDelay(new PlayerSlot(0), -1));
            Assert.Throws<InvalidOperationException>(() => writer.Finish(10, 0));                               // 처리되지 않을 틱의 입력

            // 한 사람이 한 틱에 9개째
            for (uint i = 0; i < 8; i++)
            {
                writer.Append(In(1, 20, (byte)i, LogicalKey.Fire, i + 1));
            }
            Assert.Throws<InvalidOperationException>(() => writer.Append(In(1, 20, 200, LogicalKey.Fire, 9)));
            writer.Append(In(0, 20, 200, LogicalKey.Fire, 6));                                                  // 다른 사람은 된다

            // 실패한 호출은 아무것도 남기지 않았다: 읽는 쪽이 받는 파일이고 레코드는 열 개다
            byte[] file = writer.Finish(21, 0).ToArray();
            Assert.True(DuelRecordCodec.TryRead(file, ReplayKit.Generous, out _, out DuelRecordBody? body, out RecordError error), error.ToString());
            Assert.Equal(10, body!.InputCount);
            Assert.Equal(0, body.InputDelayTicks0);
            Assert.Equal(0, body.InputDelayTicks1);

            Assert.Throws<InvalidOperationException>(() => writer.Finish(22, 0));
            Assert.Throws<InvalidOperationException>(() => writer.Append(In(0, 30, 0, LogicalKey.Up, 100)));
            Assert.Throws<InvalidOperationException>(() => writer.SetInputDelay(new PlayerSlot(0), 2));
        }

        [Fact]
        public void RunDuel_NoExceptionOnRandomBytes()
        {
            // 넷 가지 꼴: 순수 무작위, 바른 머리말 + 무작위 꼬리, 바른 파일을 바꾸거나 자름, 무작위 레코드를 바르게 봉인(SHA 통과 뒤의 검사)
            var rng = new Random(77);
            var (valid, _, _) = PlayDuel(61, 20);
            for (int i = 0; i < 40000; i++)
            {
                byte[] input;
                switch (i & 3)
                {
                    case 0:
                        input = new byte[rng.Next(0, 160)];
                        rng.NextBytes(input);
                        break;
                    case 1:
                        {
                            var tail = new byte[rng.Next(0, 80)];
                            rng.NextBytes(tail);
                            input = HeaderBytes.Concat(tail).ToArray();
                            break;
                        }
                    case 2:
                        input = (byte[])valid.Clone();
                        if (rng.Next(0, 2) == 0)
                        {
                            input[rng.Next(0, input.Length)] = (byte)rng.Next(0, 256);
                        }
                        else
                        {
                            input = input.Take(rng.Next(0, input.Length)).ToArray();
                        }
                        break;
                    default:
                        {
                            var w = new ByteWriter();
                            int count = rng.Next(0, 6);
                            for (int k = 0; k < count; k++)
                            {
                                Rec(w, (ulong)rng.Next(0, 3), (byte)rng.Next(0, 3), (byte)rng.Next(0, 256), (LogicalKey)rng.Next(0, 10),
                                    rng.Next(0, 2) == 1, (ulong)rng.Next(0, 12));
                            }
                            input = Seal(w, (uint)rng.Next(0, 12), (ulong)rng.NextInt64(), (byte)rng.Next(0, 2));
                            break;
                        }
                }
                DuelReplayResult r;
                try
                {
                    r = ReplayRunner.RunDuel(ReplayKit.Db, input, ReplayKit.Generous);
                }
                catch (Exception e)
                {
                    throw new Xunit.Sdk.XunitException("RunDuel threw on case " + i + ": " + Convert.ToHexString(input) + " " + e);
                }
                // 경기를 끝내는 모듈이 아직 없으므로 공개 문은 Completed 를 내지 못한다. SimFault 도 나오면 안 된다(검사를 통과한 기록은 Step 의 전제를 지킨다)
                Assert.True(r.Status == ReplayStatus.RecordCorrupt || r.Status == ReplayStatus.EndedLate, r.Status + " " + r.FaultDetail);
            }
        }

        private sealed class Thrower : IReplayObserver
        {
            private readonly uint _tick;

            public Thrower(uint tick)
            {
                _tick = tick;
            }

            public void BeforeStep(SimWorld world, ReadOnlySpan<PlayerInput> inputs, ReadOnlySpan<RunCommand> commands)
            {
            }

            public void AfterStep(SimWorld world)
            {
                if (world.Tick == _tick + 1)
                {
                    throw new SimInvariantException("test fault");
                }
            }
        }
    }
}

using System;
using System.Linq;
using System.Security.Cryptography;
using Wizard.Core;
using Wizard.Sim.Input;
using Xunit;

namespace Wizard.Sim.Tests.Replay
{
    // CONTRACTS 3.8 InputRecordCodec, 11.1 .wzrc 형식
    public sealed class InputRecordCodecTests
    {
        private static readonly byte[] Header = ReplayKit.HeaderBytes(ReplayKit.Header());

        private static RecordError Read(byte[] file, uint maxTicks = uint.MaxValue)
        {
            bool ok = InputRecordCodec.TryRead(file, ReplayKit.MaxTicks(maxTicks), out InputRecordHeader? h, out InputRecordBody? b, out RecordError e);
            Assert.Equal(ok, e == RecordError.None);
            Assert.Equal(ok, h != null && b != null);
            return e;
        }

        private static byte[] SmallFile()
        {
            var rec = new InputRecorder(ReplayKit.Header());
            var cmds = new RunCommand[1];
            var ins = new PlayerInput[8];
            rec.Append(3, new PlayerSlot(0), new[] { new RunCommandDraft(RunCommandOp.SetSlot, 2, -1) },
                       new[] { ReplayKit.Key(3, 200, LogicalKey.Skill, true), ReplayKit.Key(3, 200, LogicalKey.Right, true) }, cmds, out _, ins, out _);
            rec.MarkPause(130, 4321);
            rec.MarkCheckpoint(5, 140, 0x1122334455667788UL);
            rec.Append(140, new PlayerSlot(0), Array.Empty<RunCommandDraft>(), new[] { ReplayKit.Key(140, 0, LogicalKey.Skill, false) }, cmds, out _, ins, out _);
            return rec.Finish(141, 0x0102030405060708UL).ToArray();
        }

        [Fact]
        public void File_HeaderIsEndlessSetupCanonicalBytes_UuidInNetworkOrder()
        {
            byte[] file = SmallFile();
            var w = new ByteWriter();
            ReplayKit.Setup().WriteCanonical(w);
            byte[] canonical = w.ToArray();
            Assert.Equal(canonical, file.Take(canonical.Length).ToArray());

            // magic(4) format_version(2) core_version(16) data_version(4) 다음이 run_id 16바이트: 문자열 hex 순서(RFC 4122 네트워크 순서)
            byte[] runId = file.Skip(4 + 2 + 16 + 4).Take(16).ToArray();
            Assert.Equal(new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88, 0x99, 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF }, runId);

            Assert.True(InputRecordCodec.TryRead(file, ReplayKit.Generous, out InputRecordHeader? header, out InputRecordBody? body, out RecordError error));
            Assert.Equal(RecordError.None, error);
            Assert.NotNull(body);
            Assert.Equal(EndlessSetup.CanonicalFormatVersion, header!.FormatVersion);
            Assert.Equal(ReplayKit.Setup().RunId, header.Setup.RunId);
            Assert.Equal(ReplayKit.CoreVersionHex, header.Setup.CoreVersion);
            Assert.Equal(canonical, ReplayKit.HeaderBytes(header));
        }

        [Fact]
        public void File_BodyAndFooterBytesMatchContract()
        {
            byte[] file = SmallFile();
            byte[] body = file.Skip(Header.Length).ToArray();
            var expected = new ByteWriter();
            // 틱 3: 명령이 먼저(Δtick 3), 그다음 키는 (offset, KeyClass) 순: Right(방향) → Skill(K)
            expected.U8(0x02); expected.U8(3); expected.U8((byte)RunCommandOp.SetSlot); expected.I32(2); expected.I32(-1);
            expected.U8(0x01); expected.U8(0); expected.U8(200); expected.U8(0x13);
            expected.U8(0x01); expected.U8(0); expected.U8(200); expected.U8(0x15);
            // 일시정지 틱 130: Δtick 127
            expected.U8(0x03); expected.U8(127); expected.U32(4321);
            // 중간 지점: 절대 틱
            expected.U8(0x04); expected.U16(5); expected.U32(140); expected.U64(0x1122334455667788UL);
            // 틱 140 의 K 뗌: Δtick 0
            expected.U8(0x01); expected.U8(0); expected.U8(0); expected.U8(0x05);
            byte[] records = expected.ToArray();
            Assert.Equal(records, body.Take(records.Length).ToArray());

            // 꼬리말: 0xFF end_tick u32 final_state_hash u64 flags u8 record_sha256[32] = 그 앞의 모든 바이트의 SHA-256(T-49)
            var footer = new ByteWriter();
            footer.U8(0xFF); footer.U32(141); footer.U64(0x0102030405060708UL); footer.U8(0);
            byte[] fields = footer.ToArray();
            footer.Bytes(SHA256.HashData(Header.Concat(records).Concat(fields).ToArray()));
            Assert.Equal(footer.ToArray(), body.Skip(records.Length).ToArray());
            Assert.Equal(46, footer.Length);
        }

        // CONTRACTS 3.8 InputRecordBody: 서버의 1차 검사가 쓰는 칸
        [Fact]
        public void TryRead_FillsBodyFromRecordsAndFooter()
        {
            byte[] file = SmallFile();
            Assert.True(InputRecordCodec.TryRead(file, ReplayKit.Generous, out _, out InputRecordBody? body, out _));
            Assert.Equal(Header.Length, body!.HeaderLength);
            Assert.Equal(141U, body.EndTick);
            Assert.Equal(0x0102030405060708UL, body.FinalStateHash);
            Assert.False(body.DebugUsed);
            Assert.False(body.HasDebugOps);
            Assert.Equal(1, body.CommandCount);
            Assert.Equal(3, body.InputCount);
            Assert.Equal(1, body.PauseCount);
            Assert.Equal(4321L, body.PauseTotalMs);
            Assert.Single(body.Checkpoints);
            Assert.Equal(5, body.Checkpoints[0].Wave);
            Assert.Equal(140U, body.Checkpoints[0].Tick);
            Assert.Equal(0x1122334455667788UL, body.Checkpoints[0].StateHash);

            // 머리말을 뗀 나머지가 ReplayRunner.Run 의 recordBody 다
            Assert.Equal(ReplayKit.BodyOf(file), file.Skip(body.HeaderLength).ToArray());
        }

        [Fact]
        public void TryRead_BodySumsPausesKeepsCheckpointOrderAndSeesDebugOps()
        {
            byte[] file = new RawBody()
                .Pause(1, uint.MaxValue).Pause(0, uint.MaxValue).Pause(3, 7)
                .Checkpoint(5, 10, 0xA1).Checkpoint(10, 10, 0xA2).Checkpoint(15, 30, 0xA3)
                .Command(0, (byte)RunCommandOp.DebugGrant, 1, 2).Command(1, (byte)RunCommandOp.Heal)
                .File(Header, 40, 0x99, 0);
            Assert.True(InputRecordCodec.TryRead(file, ReplayKit.Generous, out _, out InputRecordBody? body, out _));
            Assert.Equal(3, body!.PauseCount);
            Assert.Equal(2L * uint.MaxValue + 7L, body.PauseTotalMs);
            Assert.Equal(new[] { 5, 10, 15 }, body.Checkpoints.Select(c => c.Wave).ToArray());
            Assert.Equal(new uint[] { 10, 10, 30 }, body.Checkpoints.Select(c => c.Tick).ToArray());
            Assert.Equal(new ulong[] { 0xA1, 0xA2, 0xA3 }, body.Checkpoints.Select(c => c.StateHash).ToArray());
            Assert.Equal(2, body.CommandCount);
            Assert.Equal(0, body.InputCount);
            // 꼬리말의 debug_used 는 꺼져 있어도 본문의 디버그 op 은 보인다(서버는 플래그와 무관하게 거절한다, 11.1절)
            Assert.False(body.DebugUsed);
            Assert.True(body.HasDebugOps);
            Assert.Equal(0x99UL, body.FinalStateHash);

            byte[] flagged = new RawBody().Command(0, (byte)RunCommandOp.Heal).File(Header, 1, 0, 0x01);
            Assert.True(InputRecordCodec.TryRead(flagged, ReplayKit.Generous, out _, out InputRecordBody? flaggedBody, out _));
            Assert.True(flaggedBody!.DebugUsed);
            Assert.False(flaggedBody.HasDebugOps);
            Assert.Empty(flaggedBody.Checkpoints);
        }

        [Fact]
        public void TryRead_RejectsEveryTruncation()
        {
            byte[] file = SmallFile();
            for (int n = 0; n < file.Length; n++)
            {
                Assert.NotEqual(RecordError.None, Read(file.Take(n).ToArray()));
            }
            Assert.Equal(RecordError.None, Read(file));
        }

        // 수락 조건 "바뀐 바이트": 머리말, 본문, 꼬리말의 칸(end_tick·final_state_hash·flags), record_sha256 어디든 한 비트만 바뀌어도 거절한다(T-49)
        [Fact]
        public void TryRead_RejectsAnyChangedBitAnywhereInTheFile()
        {
            byte[] file = SmallFile();
            for (int i = 0; i < file.Length; i++)
            {
                for (int bit = 0; bit < 8; bit++)
                {
                    byte[] changed = (byte[])file.Clone();
                    changed[i] ^= (byte)(1 << bit);
                    Assert.NotEqual(RecordError.None, Read(changed));
                }
            }
        }

        [Fact]
        public void TryRead_ChangedFooterFieldsAreShaMismatch()
        {
            byte[] file = SmallFile();
            int footerAt = file.Length - 46;   // 0xFF 의 자리
            for (int i = footerAt + 1; i <= footerAt + 13; i++)   // end_tick(4), final_state_hash(8), flags(1)
            {
                byte[] changed = (byte[])file.Clone();
                changed[i] ^= 0x01;
                Assert.Equal(RecordError.ShaMismatch, Read(changed));
            }
        }

        [Fact]
        public void TryRead_FooterFlagsBeyondDebugUsedAreRejected()
        {
            byte[] records = new RawBody().Input(0, 0, LogicalKey.Up, true).Records;
            Assert.Equal(RecordError.None, Read(RawBody.Seal(Header, records, 1, 0, 0x01)));
            for (int bit = 1; bit < 8; bit++)
            {
                Assert.Equal(RecordError.BadFlags, Read(RawBody.Seal(Header, records, 1, 0, (byte)(1 << bit))));
            }
        }

        [Fact]
        public void TryRead_RejectsTrailingBytesAfterFooter()
        {
            byte[] file = SmallFile();
            Assert.Equal(RecordError.TrailingBytes, Read(file.Concat(new byte[] { 0 }).ToArray()));
            Assert.Equal(RecordError.TrailingBytes, Read(file.Concat(file).ToArray()));
        }

        [Fact]
        public void TryRead_RejectsBadMagicAndVersion()
        {
            byte[] file = SmallFile();
            byte[] magic = (byte[])file.Clone();
            magic[3] = (byte)'D';
            Assert.Equal(RecordError.BadMagic, Read(magic));
            byte[] version = (byte[])file.Clone();
            version[4] = 2;
            Assert.Equal(RecordError.BadVersion, Read(version));
            Assert.Equal(RecordError.Truncated, Read(Array.Empty<byte>()));
        }

        // magic 과 format_version 은 맞는데 머리말이 정규 바이트가 아니다(T-46: 한 설정에 바이트 열 하나)
        [Fact]
        public void TryRead_RejectsNonCanonicalHeaderAsBadHeader()
        {
            byte[] file = SmallFile();
            int kindAt = 4 + 2 + 16 + 4 + 16 + 8;   // magic, format_version, core_version, data_version, run_id, seed 다음이 kind
            byte[] kind = (byte[])file.Clone();
            kind[kindAt] = 0x7F;                    // RunKind 에 없는 값
            Assert.Equal(RecordError.BadHeader, Read(kind));
            byte[] flag = (byte[])file.Clone();
            flag[kindAt + 1 + 2] = 2;               // phantom_first 는 0·1 만
            Assert.Equal(RecordError.BadHeader, Read(flag));
            Assert.Equal(RecordError.BadHeader, Read(file.Take(kindAt).ToArray()));   // 머리말 도중에 끝남
        }

        [Fact]
        public void TryRead_RejectsDeltaTickOverflow()
        {
            Assert.Equal(RecordError.None, Read(new RawBody().Input(uint.MaxValue - 1, 0, LogicalKey.Up, true).File(Header, uint.MaxValue)));
            // 누적이 uint 를 넘는다
            Assert.Equal(RecordError.TickOverflow, Read(new RawBody().Input(uint.MaxValue, 0, LogicalKey.Up, true)
                                                                      .Input(1, 0, LogicalKey.Up, false).File(Header, uint.MaxValue)));
            // Δtick 하나가 uint 를 넘는다
            Assert.Equal(RecordError.TickOverflow, Read(new RawBody().Command(1UL << 32, 1).File(Header, uint.MaxValue)));
            Assert.Equal(RecordError.TickOverflow, Read(new RawBody().Pause(ulong.MaxValue, 1).File(Header, uint.MaxValue)));
            // LEB128 이 64비트를 넘는다
            Assert.Equal(RecordError.TickOverflow, Read(new RawBody().Bytes(0x01, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x7F, 0, 0)
                                                                      .File(Header, uint.MaxValue)));
        }

        [Fact]
        public void TryRead_RejectsRecordsAtOrBeyondEndTick()
        {
            // 명령·키는 end_tick 보다 앞의 틱이어야 한다(그 틱은 처리되지 않는다)
            Assert.Equal(RecordError.None, Read(new RawBody().Input(9, 0, LogicalKey.Up, true).File(Header, 10)));
            Assert.Equal(RecordError.TickOverflow, Read(new RawBody().Input(10, 0, LogicalKey.Up, true).File(Header, 10)));
            Assert.Equal(RecordError.TickOverflow, Read(new RawBody().Command(10, 1).File(Header, 10)));
            // 일시정지·중간 지점은 end_tick 까지
            Assert.Equal(RecordError.None, Read(new RawBody().Pause(10, 5).Checkpoint(5, 10, 0).File(Header, 10)));
            Assert.Equal(RecordError.TickOverflow, Read(new RawBody().Checkpoint(5, 11, 0).File(Header, 10)));
        }

        [Fact]
        public void TryRead_RejectsBackwardsTicks()
        {
            Assert.Equal(RecordError.TickBackwards, Read(new RawBody().Input(50, 0, LogicalKey.Up, true).Checkpoint(5, 49, 0).File(Header, 100)));
            // 중간 지점의 해시는 그 틱의 Step 전의 것: 이미 기록된 그 틱의 입력 뒤에 올 수 없다
            Assert.Equal(RecordError.TickBackwards, Read(new RawBody().Input(50, 0, LogicalKey.Up, true).Checkpoint(5, 50, 0).File(Header, 100)));
            Assert.Equal(RecordError.None, Read(new RawBody().Pause(50, 1).Checkpoint(5, 50, 0).Input(0, 0, LogicalKey.Up, true).File(Header, 100)));
        }

        [Fact]
        public void TryRead_RejectsNineKeysInOneTick()
        {
            var eight = new RawBody().Input(7, 0, LogicalKey.Up, true);
            for (int i = 1; i < 8; i++)
            {
                eight.Input(0, (byte)i, LogicalKey.Up, (i & 1) == 0);
            }
            Assert.Equal(RecordError.None, Read(eight.File(Header, 8)));
            eight.Input(0, 9, LogicalKey.Up, false);
            Assert.Equal(RecordError.TooManyKeys, Read(eight.File(Header, 8)));

            // 틱이 바뀌면 다시 센다. 일시정지·중간 지점은 키가 아니다
            var spread = new RawBody();
            for (int t = 0; t < 3; t++)
            {
                spread.Pause(t == 0 ? 0UL : 1UL, 1);
                for (int i = 0; i < 8; i++)
                {
                    spread.Input(0, (byte)i, LogicalKey.Fire, (i & 1) == 0);
                }
            }
            Assert.Equal(RecordError.None, Read(spread.File(Header, 3)));
        }

        [Fact]
        public void TryRead_RejectsKeyOrderRegressionInsideTick()
        {
            // 오프셋 역행
            Assert.Equal(RecordError.KeyOrder, Read(new RawBody().Input(1, 10, LogicalKey.Up, true).Input(0, 9, LogicalKey.Up, false).File(Header, 5)));
            // 같은 오프셋에서 K(3) 뒤의 방향(1)
            Assert.Equal(RecordError.KeyOrder, Read(new RawBody().Input(1, 10, LogicalKey.Skill, true).Input(0, 10, LogicalKey.Left, true).File(Header, 5)));
            // 같은 (offset, KeyClass)는 순번이 가른다
            Assert.Equal(RecordError.None, Read(new RawBody().Input(1, 10, LogicalKey.Left, true).Input(0, 10, LogicalKey.Up, true).File(Header, 5)));
            // 다음 틱이면 오프셋이 작아도 된다
            Assert.Equal(RecordError.None, Read(new RawBody().Input(1, 10, LogicalKey.Skill, true).Input(1, 0, LogicalKey.Up, true).File(Header, 5)));
        }

        [Fact]
        public void TryRead_RejectsCommandAfterKeyInSameTick()
        {
            Assert.Equal(RecordError.CommandAfterKey, Read(new RawBody().Input(2, 0, LogicalKey.Up, true).Command(0, 6).File(Header, 5)));
            Assert.Equal(RecordError.None, Read(new RawBody().Command(2, 6).Command(0, 8).Input(0, 0, LogicalKey.Up, true).Command(1, 6).File(Header, 5)));
        }

        [Theory]
        [InlineData(0x09)]
        [InlineData(0x0F)]
        [InlineData(0x20)]
        [InlineData(0x40)]
        [InlineData(0x80)]
        [InlineData(0x19)]
        public void TryRead_RejectsKeyOutsideLogicalKey(byte packed)
        {
            Assert.Equal(RecordError.BadKey, Read(new RawBody().InputPacked(0, 0, packed).File(Header, 1)));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(13)]
        [InlineData(100)]
        [InlineData(199)]
        [InlineData(204)]
        [InlineData(255)]
        public void TryRead_RejectsOpOutsideRunCommandOp(byte op)
        {
            Assert.Equal(RecordError.BadOp, Read(new RawBody().Command(0, op).File(Header, 1)));
        }

        [Fact]
        public void TryRead_AcceptsEveryDefinedOp()
        {
            foreach (RunCommandOp op in Enum.GetValues(typeof(RunCommandOp)))
            {
                Assert.Equal(RecordError.None, Read(new RawBody().Command(0, (byte)op).File(Header, 1)));
            }
        }

        [Theory]
        [InlineData(0x00)]
        [InlineData(0x05)]   // 0x05 DuelInput 은 WZDR 에만 있다
        [InlineData(0x06)]
        [InlineData(0xFE)]
        public void TryRead_RejectsUnknownTag(byte tag)
        {
            Assert.Equal(RecordError.UnknownTag, Read(new RawBody().Bytes(tag, 0, 0, 0).File(Header, 1)));
        }

        [Fact]
        public void TryRead_RejectsEndTickOverLimit()
        {
            byte[] file = new RawBody().Input(0, 0, LogicalKey.Up, true).File(Header, 1001);
            Assert.Equal(RecordError.None, Read(file, 1001));
            Assert.Equal(RecordError.EndTickOverLimit, Read(file, 1000));
        }

        [Fact]
        public void TryRead_NoExceptionOnMillionRandomInputs()
        {
            // 무작위 바이트 100만 개(CONTRACTS 3.8 수락 조건): 넷 가지 꼴을 25만 개씩.
            // (1) 순수 무작위 (2) 바른 머리말 + 무작위 본문 (3) 바른 파일의 바이트를 바꾸거나 자르거나 늘림 (4) 무작위 레코드를 바르게 봉인(SHA 통과 뒤의 검사)
            var rng = new Random(0x0BADF00D);
            byte[] valid = SmallFile();
            int accepted = 0;
            for (int i = 0; i < 1_000_000; i++)
            {
                byte[] input;
                switch (i & 3)
                {
                    case 0:
                        input = new byte[rng.Next(0, 96)];
                        rng.NextBytes(input);
                        break;
                    case 1:
                        {
                            var tail = new byte[rng.Next(0, 80)];
                            rng.NextBytes(tail);
                            input = Header.Concat(tail).ToArray();
                            break;
                        }
                    case 2:
                        input = Mutate(rng, valid);
                        break;
                    default:
                        input = RandomSealed(rng);
                        break;
                }
                bool ok;
                try
                {
                    ok = InputRecordCodec.TryRead(input, ReplayKit.Generous, out _, out _, out _);
                }
                catch (Exception e)
                {
                    throw new Xunit.Sdk.XunitException("TryRead threw on case " + i + ": " + Convert.ToHexString(input) + " " + e);
                }
                if (ok)
                {
                    accepted++;
                }
            }
            Assert.True(accepted > 0);   // (4)의 일부는 구조까지 통과한다(뒤의 검사를 실제로 거쳤다)
        }

        private static byte[] Mutate(Random rng, byte[] valid)
        {
            byte[] m = (byte[])valid.Clone();
            switch (rng.Next(0, 3))
            {
                case 0:
                    int n = rng.Next(1, 5);
                    for (int k = 0; k < n; k++)
                    {
                        m[rng.Next(0, m.Length)] = (byte)rng.Next(0, 256);
                    }
                    return m;
                case 1:
                    return m.Take(rng.Next(0, m.Length)).ToArray();
                default:
                    var extra = new byte[rng.Next(1, 8)];
                    rng.NextBytes(extra);
                    return m.Concat(extra).ToArray();
            }
        }

        private static byte[] RandomSealed(Random rng)
        {
            var b = new RawBody();
            int count = rng.Next(0, 12);
            for (int k = 0; k < count; k++)
            {
                switch (rng.Next(0, 6))
                {
                    case 0:
                        b.InputPacked((ulong)rng.Next(0, 3), (byte)rng.Next(0, 256), (byte)rng.Next(0, 24));
                        break;
                    case 1:
                        b.Command((ulong)rng.Next(0, 3), (byte)rng.Next(0, 16), rng.Next(), rng.Next());
                        break;
                    case 2:
                        b.Pause((ulong)rng.Next(0, 3), (uint)rng.Next());
                        break;
                    case 3:
                        b.Checkpoint((ushort)rng.Next(0, 20), (uint)rng.Next(0, 40), (ulong)rng.NextInt64());
                        break;
                    case 4:
                        b.Input((ulong)(rng.Next(0, 2) == 0 ? 0 : uint.MaxValue), (byte)rng.Next(0, 4), (LogicalKey)rng.Next(0, 9), true);
                        break;
                    default:
                        var junk = new byte[rng.Next(1, 6)];
                        rng.NextBytes(junk);
                        b.Bytes(junk);
                        break;
                }
            }
            return b.File(Header, (uint)rng.Next(0, 60), (ulong)rng.NextInt64(), (byte)rng.Next(0, 3));
        }
    }
}

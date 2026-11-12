using System;
using System.Collections.Generic;
using Wizard.Core;
using Wizard.Protocol.Duel;
using Wizard.Sim.Input;
using Xunit;

namespace Wizard.Protocol.Tests.Duel
{
    // 잘린 패킷, 남는 바이트, 모르는 종류, 범위를 넘는 값은 예외 없이 거절한다. 실패한 버퍼는 비운 상태로 남는다.
    public sealed class DuelCodecRejectTests
    {
        public static TheoryData<string> AllBodies() => DuelBodyFixtureTests.AllBodies();

        [Theory]
        [MemberData(nameof(AllBodies))]
        public void EveryProperPrefixIsTruncated(string name)
        {
            PacketFixtures.Body body = PacketFixtures.Bodies[name];
            var rt = new BodyRoundTrip();
            for (int len = 0; len < body.Bytes.Length; len++)
            {
                Assert.Equal(DuelCodecError.Truncated, rt.Read((DuelPacketKind)body.Kind, body.Bytes.AsSpan(0, len)));
            }
        }

        [Theory]
        [MemberData(nameof(AllBodies))]
        public void TrailingByteIsRejected(string name)
        {
            PacketFixtures.Body body = PacketFixtures.Bodies[name];
            var longer = new byte[body.Bytes.Length + 1];
            body.Bytes.CopyTo(longer, 0);
            Assert.Equal(DuelCodecError.TrailingBytes, new BodyRoundTrip().Read((DuelPacketKind)body.Kind, longer));
        }

        [Fact]
        public void FailedReadLeavesBufferCleared()
        {
            var ci = new ClientInputPacket();
            Assert.Equal(DuelCodecError.None, ci.TryRead(PacketFixtures.BodyBytes("client_input.events")));
            byte[] bad = PacketFixtures.BodyBytes("client_input.events");
            Assert.Equal(DuelCodecError.Truncated, ci.TryRead(bad.AsSpan(0, bad.Length - 1)));
            Assert.Equal(0, ci.EventCount);
            Assert.Equal(0U, ci.SealedThroughTick);

            var st = new ServerTickPacket();
            Assert.Equal(DuelCodecError.None, st.TryRead(PacketFixtures.BodyBytes("server_tick.full")));
            byte[] b2 = PacketFixtures.BodyBytes("server_tick.full");
            Assert.Equal(DuelCodecError.Truncated, st.TryRead(b2.AsSpan(0, b2.Length - 1)));
            Assert.Equal(0, st.ConfirmedCount);
            Assert.Equal(0, st.DroppedCount);
            Assert.False(st.HasHash);
        }

        [Fact]
        public void BodyOverMaxIsTooLarge()
        {
            var big = new byte[DuelWire.MaxBodyBytes + 1];
            Assert.Equal(DuelCodecError.TooLarge, new ClientInputPacket().TryRead(big));
            Assert.Equal(DuelCodecError.TooLarge, new ServerTickPacket().TryRead(big));
        }

        // ---- ClientInput 의 범위

        [Fact]
        public void ClientInputEventCountOver64()
        {
            byte[] b = PacketFixtures.BodyBytes("client_input.empty");
            b[22] = 65;
            Assert.Equal(DuelCodecError.OutOfRange, new ClientInputPacket().TryRead(b));
            b[22] = 255;
            Assert.Equal(DuelCodecError.OutOfRange, new ClientInputPacket().TryRead(b));
        }

        [Theory]
        [InlineData(0x08)]   // Pause: 결투에 없다
        [InlineData(0x18)]
        [InlineData(0x09)]
        [InlineData(0x0f)]
        [InlineData(0x20)]   // 윗 3비트
        [InlineData(0x30)]
        [InlineData(0x80)]
        public void ClientInputBadKeyByte(byte packed)
        {
            byte[] b = PacketFixtures.BodyBytes("client_input.events");
            b[23 + 5] = packed;                     // e0 의 key_down
            Assert.Equal(DuelCodecError.OutOfRange, new ClientInputPacket().TryRead(b));
            byte[] c = PacketFixtures.BodyBytes("client_input.events");
            c[23 + 10 + 2] = packed;                // e1 의 key_down
            Assert.Equal(DuelCodecError.OutOfRange, new ClientInputPacket().TryRead(c));
        }

        [Fact]
        public void ClientInputAllDuelKeysAccepted()
        {
            for (int k = 0; k <= (int)LogicalKey.Reverse; k++)
            {
                foreach (byte down in new byte[] { 0, DuelWire.KeyDownBit })
                {
                    byte[] b = PacketFixtures.BodyBytes("client_input.events");
                    b[23 + 5] = (byte)(k | down);
                    var p = new ClientInputPacket();
                    Assert.Equal(DuelCodecError.None, p.TryRead(b));
                    Assert.Equal((LogicalKey)k, p.Events[0].Key);
                    Assert.Equal(down != 0, p.Events[0].Down);
                }
            }
        }

        [Fact]
        public void ClientInputTickAndSeqOverflow()
        {
            // e0 tick = u32 최대, e1 Δtick 1 → 넘침
            var p = new ClientInputPacket();
            p.TryAddEvent(new InputEvent(uint.MaxValue, 0, LogicalKey.Up, true, 1));
            byte[] head = Write(p);
            byte[] overTick = Concat(head, new byte[] { 0x01, 0x00, 0x10, 0x01 });
            overTick[22] = 2;
            Assert.Equal(DuelCodecError.OutOfRange, new ClientInputPacket().TryRead(overTick));

            var q = new ClientInputPacket();
            q.TryAddEvent(new InputEvent(5, 0, LogicalKey.Up, true, uint.MaxValue));
            byte[] overSeq = Concat(Write(q), new byte[] { 0x00, 0x01, 0x10, 0x01 });
            overSeq[22] = 2;
            Assert.Equal(DuelCodecError.OutOfRange, new ClientInputPacket().TryRead(overSeq));
        }

        [Theory]
        [InlineData(new byte[] { 0x80, 0x00 }, DuelCodecError.NonCanonical)]               // 0 의 긴 꼴
        [InlineData(new byte[] { 0x81, 0x80, 0x00 }, DuelCodecError.NonCanonical)]         // 1 의 긴 꼴
        [InlineData(new byte[] { 0xff, 0xff, 0xff, 0xff, 0x10 }, DuelCodecError.OutOfRange)]   // 2^32 이상
        [InlineData(new byte[] { 0xff, 0xff, 0xff, 0xff, 0x8f, 0x00 }, DuelCodecError.OutOfRange)]   // 6바이트
        [InlineData(new byte[] { 0x80 }, DuelCodecError.Truncated)]
        public void ClientInputVarUIntForms(byte[] dTick, DuelCodecError expected)
        {
            var p = new ClientInputPacket();
            p.TryAddEvent(new InputEvent(5, 0, LogicalKey.Up, true, 1));
            byte[] b = Concat(Write(p), dTick);
            if (expected != DuelCodecError.Truncated)
            {
                b = Concat(b, new byte[] { 0x00, 0x00, 0x01 });   // offset, key_down, Δseq
            }
            b[22] = 2;
            Assert.Equal(expected, new ClientInputPacket().TryRead(b));
        }

        [Fact]
        public void ClientInputLargestCanonicalVarUIntsAccepted()
        {
            var p = new ClientInputPacket();
            p.TryAddEvent(new InputEvent(0, 0, LogicalKey.Up, true, 0));
            byte[] b = Concat(Write(p), new byte[] { 0xff, 0xff, 0xff, 0xff, 0x0f, 0x00, 0x00, 0xff, 0xff, 0xff, 0xff, 0x0f });
            b[22] = 2;
            var r = new ClientInputPacket();
            Assert.Equal(DuelCodecError.None, r.TryRead(b));
            Assert.Equal(uint.MaxValue, r.Events[1].Tick);
            Assert.Equal(uint.MaxValue, r.Events[1].Seq);
        }

        // 10.6 (0)의 순서 위반은 형식이 맞으므로 코덱이 넘겨 준다(호스트가 몰수 계수로 센다)
        [Fact]
        public void ClientInputOrderViolationsAreDecodedForTheHost()
        {
            var p = new ClientInputPacket { BaseSeq = 20 };
            p.TryAddEvent(new InputEvent(50, 200, LogicalKey.Up, true, 10));    // base_seq ≥ 첫 순번
            p.TryAddEvent(new InputEvent(50, 100, LogicalKey.Left, true, 10));  // 같은 틱 오프셋 역행, Δseq 0
            byte[] b = Write(p);
            var r = new ClientInputPacket();
            Assert.Equal(DuelCodecError.None, r.TryRead(b));
            Assert.Equal(20U, r.BaseSeq);
            Assert.Equal(10U, r.Events[1].Seq);
            Assert.Equal(100, r.Events[1].Offset);
            Assert.Equal(b, Write(r));
        }

        [Fact]
        public void ClientInputWriterRejectsUnrepresentableAndBadKeys()
        {
            var back = new ClientInputPacket();
            back.TryAddEvent(new InputEvent(50, 0, LogicalKey.Up, true, 10));
            back.TryAddEvent(new InputEvent(49, 0, LogicalKey.Up, false, 11));
            AssertWriteFails(back, DuelCodecError.Unrepresentable);

            var seqBack = new ClientInputPacket();
            seqBack.TryAddEvent(new InputEvent(50, 0, LogicalKey.Up, true, 10));
            seqBack.TryAddEvent(new InputEvent(50, 1, LogicalKey.Up, false, 9));
            AssertWriteFails(seqBack, DuelCodecError.Unrepresentable);

            var pause = new ClientInputPacket();
            pause.TryAddEvent(new InputEvent(50, 0, LogicalKey.Pause, true, 10));
            AssertWriteFails(pause, DuelCodecError.OutOfRange);

            var full = new ClientInputPacket();
            for (int i = 0; i < DuelWire.MaxClientEvents; i++)
            {
                Assert.True(full.TryAddEvent(new InputEvent((uint)i, 0, LogicalKey.Fire, (i & 1) == 0, (uint)i + 1)));
            }
            Assert.False(full.TryAddEvent(new InputEvent(100, 0, LogicalKey.Fire, true, 100)));
            byte[] b = Write(full);
            var r = new ClientInputPacket();
            Assert.Equal(DuelCodecError.None, r.TryRead(b));
            Assert.Equal(DuelWire.MaxClientEvents, r.EventCount);
        }

        // ---- ServerTick 의 범위

        [Theory]
        [InlineData(0x04)]
        [InlineData(0x08)]
        [InlineData(0x80)]
        [InlineData(0x07)]
        public void ServerTickUnknownFlags(byte flags)
        {
            byte[] b = PacketFixtures.BodyBytes("server_tick.empty");
            b[10] = flags;
            Assert.Equal(DuelCodecError.OutOfRange, new ServerTickPacket().TryRead(b));
        }

        [Fact]
        public void ServerTickConfirmedCountOver32()
        {
            byte[] b = PacketFixtures.BodyBytes("server_tick.empty");
            b[15] = 33;
            Assert.Equal(DuelCodecError.OutOfRange, new ServerTickPacket().TryRead(b));
        }

        [Fact]
        public void ServerTickConfirmedTickOverflow()
        {
            byte[] b = PacketFixtures.BodyBytes("server_tick.empty");
            b[11] = 0xff; b[12] = 0xff; b[13] = 0xff; b[14] = 0xff;     // confirmed_from_tick = u32 최대
            byte[] one = Concat(b, new byte[] { 0, 0 });
            one[15] = 1;
            Assert.Equal(DuelCodecError.None, new ServerTickPacket().TryRead(one));  // 마지막 틱이 u32 최대: 된다
            byte[] two = Concat(b, new byte[] { 0, 0, 0, 0 });
            two[15] = 2;
            Assert.Equal(DuelCodecError.OutOfRange, new ServerTickPacket().TryRead(two));
        }

        [Fact]
        public void ServerTickPerSlotCountOver8()
        {
            byte[] full = PacketFixtures.BodyBytes("server_tick.full");
            byte[] n0 = (byte[])full.Clone();
            n0[16] = 9;
            Assert.Equal(DuelCodecError.OutOfRange, new ServerTickPacket().TryRead(n0));
            byte[] n1 = (byte[])full.Clone();
            n1[17] = 9;
            Assert.Equal(DuelCodecError.OutOfRange, new ServerTickPacket().TryRead(n1));
        }

        [Fact]
        public void ServerTickEventSeqZeroAndBadKey()
        {
            byte[] full = PacketFixtures.BodyBytes("server_tick.full");
            byte[] seq0 = (byte[])full.Clone();
            seq0[20] = 0x00;                         // 슬롯 0 이벤트의 seq 5 → 0
            Assert.Equal(DuelCodecError.OutOfRange, new ServerTickPacket().TryRead(seq0));
            byte[] key = (byte[])full.Clone();
            key[19] = 0x18;                          // Pause 누름
            Assert.Equal(DuelCodecError.OutOfRange, new ServerTickPacket().TryRead(key));
            byte[] overlong = (byte[])full.Clone();
            overlong[20] = 0x85;                     // seq 5 를 85 00 으로: 긴 꼴 → 뒤가 밀리지만 먼저 긴 꼴로 걸린다
            byte[] withZero = new byte[overlong.Length + 1];
            Array.Copy(overlong, 0, withZero, 0, 21);
            withZero[21] = 0x00;
            Array.Copy(overlong, 21, withZero, 22, overlong.Length - 21);
            Assert.Equal(DuelCodecError.NonCanonical, new ServerTickPacket().TryRead(withZero));
        }

        [Theory]
        [InlineData((byte)0)]
        [InlineData((byte)6)]
        [InlineData((byte)255)]
        public void ServerTickDroppedReasonOutOfRange(byte reason)
        {
            byte[] b = PacketFixtures.BodyBytes("server_tick.dropped_only");
            b[21] = reason;
            Assert.Equal(DuelCodecError.OutOfRange, new ServerTickPacket().TryRead(b));
        }

        [Fact]
        public void ServerTickAllDropReasonsAccepted()
        {
            for (byte reason = 1; reason <= 5; reason++)
            {
                byte[] b = PacketFixtures.BodyBytes("server_tick.dropped_only");
                b[21] = reason;
                b[17] = 0x01; b[18] = 0; b[19] = 0; b[20] = 0;   // seq 1
                var p = new ServerTickPacket();
                Assert.Equal(DuelCodecError.None, p.TryRead(b));
                Assert.Equal((DuelDropReason)reason, p.Dropped[0].Reason);
            }
        }

        [Fact]
        public void ServerTickDroppedSeqZeroCountZeroAndRunOverflow()
        {
            byte[] b = PacketFixtures.BodyBytes("server_tick.dropped_only");
            byte[] seq0 = (byte[])b.Clone();
            seq0[17] = 0; seq0[18] = 0; seq0[19] = 0; seq0[20] = 0;
            Assert.Equal(DuelCodecError.OutOfRange, new ServerTickPacket().TryRead(seq0));

            byte[] run = (byte[])b.Clone();
            run[22] = 0x01;                                   // seq u32 최대 + run 1 → 넘침
            Assert.Equal(DuelCodecError.OutOfRange, new ServerTickPacket().TryRead(run));

            byte[] count0 = new byte[17];
            Array.Copy(b, count0, 16);
            count0[16] = 0;                                   // flags&2 인데 dropped_count 0: 같은 바이트로 다시 쓸 수 없다
            Assert.Equal(DuelCodecError.OutOfRange, new ServerTickPacket().TryRead(count0));
        }

        [Fact]
        public void ServerTickWriterLimits()
        {
            var p = new ServerTickPacket { ConfirmedFromTick = 10 };
            var nine = new InputEvent[9];
            for (int i = 0; i < nine.Length; i++)
            {
                nine[i] = new InputEvent(10, (byte)i, LogicalKey.Fire, (i & 1) == 0, (uint)i + 1);
            }
            Assert.Equal(DuelCodecError.OutOfRange, p.TryAddConfirmedTick(nine, ReadOnlySpan<InputEvent>.Empty));
            Assert.Equal(DuelCodecError.OutOfRange, p.TryAddConfirmedTick(ReadOnlySpan<InputEvent>.Empty, nine));
            Assert.Equal(0, p.ConfirmedCount);
            // 틱이 맞지 않는 이벤트, 순번 0, Pause
            Assert.Equal(DuelCodecError.OutOfRange, p.TryAddConfirmedTick(new[] { new InputEvent(11, 0, LogicalKey.Up, true, 1) }, default));
            Assert.Equal(DuelCodecError.OutOfRange, p.TryAddConfirmedTick(new[] { new InputEvent(10, 0, LogicalKey.Up, true, 0) }, default));
            Assert.Equal(DuelCodecError.OutOfRange, p.TryAddConfirmedTick(new[] { new InputEvent(10, 0, LogicalKey.Pause, true, 1) }, default));
            for (int t = 0; t < DuelWire.MaxConfirmedTicks; t++)
            {
                Assert.Equal(DuelCodecError.None, p.TryAddConfirmedTick(default, default));
            }
            Assert.Equal(DuelCodecError.OutOfRange, p.TryAddConfirmedTick(default, default));
            Assert.Equal(DuelCodecError.OutOfRange, p.TryAddDropped(new DroppedRun(0, DuelDropReason.Late, 0)));
            Assert.Equal(DuelCodecError.OutOfRange, p.TryAddDropped(new DroppedRun(1, (DuelDropReason)6, 0)));
            Assert.Equal(DuelCodecError.OutOfRange, p.TryAddDropped(new DroppedRun(uint.MaxValue, DuelDropReason.Late, 1)));
            for (int i = 0; i < ServerTickPacket.MaxDropped; i++)
            {
                Assert.Equal(DuelCodecError.None, p.TryAddDropped(new DroppedRun((uint)i + 1, DuelDropReason.Lost, 0)));
            }
            Assert.Equal(DuelCodecError.OutOfRange, p.TryAddDropped(new DroppedRun(1000, DuelDropReason.Lost, 0)));
        }

        // 1,200바이트 상한: 확정 32틱 × 2슬롯 × 8개 같은 큰 묶음은 쓰지 않는다(쓰는 쪽이 나눠 보낸다)
        [Fact]
        public void ServerTickOverPacketLimitIsTooLargeAndWritesNothing()
        {
            var p = new ServerTickPacket { ConfirmedFromTick = 1000 };
            uint seq = 1u << 28;                     // varuint 5바이트
            var s = new InputEvent[DuelWire.MaxEventsPerTickPerSlot];
            for (int t = 0; t < DuelWire.MaxConfirmedTicks; t++)
            {
                for (int i = 0; i < s.Length; i++)
                {
                    s[i] = new InputEvent(1000U + (uint)t, (byte)i, LogicalKey.Fire, (i & 1) == 0, seq++);
                }
                Assert.Equal(DuelCodecError.None, p.TryAddConfirmedTick(s, s));
            }
            Assert.Equal(DuelCodecError.TooLarge, p.Measure(out _));
            var w = new ByteWriter();
            Assert.Equal(DuelCodecError.TooLarge, p.TryWrite(w));
            Assert.Equal(0, w.Length);
        }

        [Fact]
        public void ServerTickBodyExactlyAtLimitIsAccepted()
        {
            // 본문 상한 1,171: 고정 16 + dropped(1 + 7 × 164) = 1,165 는 되고, 165개(1,172)는 넘는다
            var p = new ServerTickPacket();
            for (int i = 0; i < 164; i++)
            {
                p.TryAddDropped(new DroppedRun((uint)i + 1, DuelDropReason.Late, 0));
            }
            Assert.Equal(DuelCodecError.None, p.Measure(out int size));
            Assert.Equal(16 + 1 + 7 * 164, size);
            p.TryAddDropped(new DroppedRun(500, DuelDropReason.Late, 0));
            Assert.Equal(DuelCodecError.TooLarge, p.Measure(out _));
        }

        // ---- 시계와 인사

        [Theory]
        [InlineData((byte)2)]
        [InlineData((byte)255)]
        public void HelloSlotOutOfRange(byte slot)
        {
            byte[] b = PacketFixtures.BodyBytes("hello");
            b[0] = slot;
            Assert.Equal(DuelCodecError.OutOfRange, HelloPacket.TryRead(b, out _));
            Assert.Equal(DuelCodecError.OutOfRange, new HelloPacket(new PlayerSlot(slot), 1).TryWrite(new ByteWriter()));
        }

        private static void AssertWriteFails(ClientInputPacket p, DuelCodecError expected)
        {
            var w = new ByteWriter();
            Assert.Equal(expected, p.Measure(out _));
            Assert.Equal(expected, p.TryWrite(w));
            Assert.Equal(0, w.Length);
        }

        private static byte[] Write(ClientInputPacket p)
        {
            var w = new ByteWriter();
            Assert.Equal(DuelCodecError.None, p.TryWrite(w));
            return w.ToArray();
        }

        private static byte[] Concat(byte[] a, byte[] b)
        {
            var r = new List<byte>(a);
            r.AddRange(b);
            return r.ToArray();
        }
    }
}

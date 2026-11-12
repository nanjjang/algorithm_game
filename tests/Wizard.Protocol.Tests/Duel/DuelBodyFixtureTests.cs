using System;
using Wizard.Core;
using Wizard.Protocol.Duel;
using Wizard.Sim.Input;
using Xunit;

namespace Wizard.Protocol.Tests.Duel
{
    // CONTRACTS 10.2~10.4. 필드 값(여기)으로 쓴 바이트 = 픽스처(손으로 옮긴 바이트), 픽스처를 읽은 필드 = 여기의 값,
    // 쓰기 → 읽기 → 쓰기가 바이트 단위로 같다.
    public sealed class DuelBodyFixtureTests
    {
        public static TheoryData<string> AllBodies()
        {
            var d = new TheoryData<string>();
            foreach (string n in PacketFixtures.Bodies.Keys)
            {
                d.Add(n);
            }
            return d;
        }

        [Fact]
        public void EveryPacketKindHasABodyFixture()
        {
            foreach (DuelPacketKind kind in Enum.GetValues(typeof(DuelPacketKind)))
            {
                bool found = false;
                foreach (PacketFixtures.Body b in PacketFixtures.Bodies.Values)
                {
                    found |= b.Kind == (int)kind;
                }
                Assert.True(found, "no body fixture for " + kind);
            }
            Assert.Equal(7, Enum.GetValues(typeof(DuelPacketKind)).Length);
        }

        [Theory]
        [MemberData(nameof(AllBodies))]
        public void WriteReadWriteIsByteIdentical(string name)
        {
            PacketFixtures.Body body = PacketFixtures.Bodies[name];
            var rt = new BodyRoundTrip();
            Assert.Equal(DuelCodecError.None, rt.Read((DuelPacketKind)body.Kind, body.Bytes));
            byte[] once = rt.Rewritten;
            Assert.Equal(body.Bytes, once);
            Assert.Equal(DuelCodecError.None, rt.Read((DuelPacketKind)body.Kind, once));
            Assert.Equal(once, rt.Rewritten);
        }

        // ---- ClientInput (10.2)

        [Fact]
        public void ClientInputEmpty()
        {
            var p = new ClientInputPacket
            {
                SealedThroughTick = 100, AckConfirmedTick = 96, ClientSendTimeMs = 1667, EchoServerTimeMs = 1650, EchoHoldMs = 5, BaseSeq = 42,
            };
            AssertWrites("client_input.empty", p);
            var r = new ClientInputPacket();
            Assert.Equal(DuelCodecError.None, r.TryRead(PacketFixtures.BodyBytes("client_input.empty")));
            AssertSame(p, r);
            Assert.Equal(ClientInputPacket.FixedBytes, PacketFixtures.BodyBytes("client_input.empty").Length);
            // 입력이 없을 때 패킷 전체는 52바이트(10.2)
            Assert.Equal(52, DuelPacketFrame.SealedLength(ClientInputPacket.FixedBytes));
        }

        [Fact]
        public void ClientInputEvents()
        {
            var p = new ClientInputPacket
            {
                SealedThroughTick = 100, AckConfirmedTick = 95, ClientSendTimeMs = 1700, EchoServerTimeMs = 1690, EchoHoldMs = 3, BaseSeq = 9,
            };
            Assert.True(p.TryAddEvent(new InputEvent(93, 128, LogicalKey.Up, true, 10)));
            Assert.True(p.TryAddEvent(new InputEvent(93, 192, LogicalKey.Skill, true, 11)));
            Assert.True(p.TryAddEvent(new InputEvent(100, 16, LogicalKey.Up, false, 13)));
            Assert.True(p.TryAddEvent(new InputEvent(101, 255, LogicalKey.Reverse, true, 14)));
            AssertWrites("client_input.events", p);
            var r = new ClientInputPacket();
            Assert.Equal(DuelCodecError.None, r.TryRead(PacketFixtures.BodyBytes("client_input.events")));
            AssertSame(p, r);
        }

        [Fact]
        public void ClientInputWideValues()
        {
            var p = new ClientInputPacket
            {
                SealedThroughTick = uint.MaxValue, AckConfirmedTick = 0, ClientSendTimeMs = uint.MaxValue, EchoServerTimeMs = 0,
                EchoHoldMs = ushort.MaxValue, BaseSeq = 0,
            };
            Assert.True(p.TryAddEvent(new InputEvent(1000, 0, LogicalKey.Stance, true, 1)));
            Assert.True(p.TryAddEvent(new InputEvent(1300, 1, LogicalKey.Fire, false, 201)));
            Assert.True(p.TryAddEvent(new InputEvent(uint.MaxValue, 2, LogicalKey.Right, true, uint.MaxValue)));
            AssertWrites("client_input.wide", p);
            var r = new ClientInputPacket();
            Assert.Equal(DuelCodecError.None, r.TryRead(PacketFixtures.BodyBytes("client_input.wide")));
            AssertSame(p, r);
        }

        // ---- ServerTick (10.3)

        [Fact]
        public void ServerTickEmpty()
        {
            var p = new ServerTickPacket { ServerTimeMs = 2000, EchoClientSendTimeMs = 1990, MarginMsEstimate = -3, ConfirmedFromTick = 120 };
            AssertWrites("server_tick.empty", p);
            var r = new ServerTickPacket();
            Assert.Equal(DuelCodecError.None, r.TryRead(PacketFixtures.BodyBytes("server_tick.empty")));
            AssertSame(p, r);
            Assert.Equal(0, r.Flags);
        }

        [Fact]
        public void ServerTickFull()
        {
            ServerTickPacket p = Full();
            AssertWrites("server_tick.full", p);
            var r = new ServerTickPacket();
            Assert.Equal(DuelCodecError.None, r.TryRead(PacketFixtures.BodyBytes("server_tick.full")));
            AssertSame(p, r);
            Assert.Equal(3, r.Flags);
            Assert.Equal(89U, r.SlotEvents(0, 1)[1].Tick);       // 틱은 순서로 안다
            Assert.Equal(131U, r.SlotEvents(0, 1)[1].Seq);
            Assert.Equal(0, r.SlotEvents(1, 0).Length);
            Assert.Equal(306U, r.Dropped[1].LastSeq);
            Assert.Equal(DuelDropReason.Lost, r.Dropped[1].Reason);
        }

        [Fact]
        public void ServerTickDroppedOnly()
        {
            var p = new ServerTickPacket { ServerTimeMs = 1 };
            Assert.Equal(DuelCodecError.None, p.TryAddDropped(new DroppedRun(uint.MaxValue, DuelDropReason.Early, 0)));
            AssertWrites("server_tick.dropped_only", p);
            var r = new ServerTickPacket();
            Assert.Equal(DuelCodecError.None, r.TryRead(PacketFixtures.BodyBytes("server_tick.dropped_only")));
            AssertSame(p, r);
            Assert.Equal(DuelWire.ServerTickFlagDropped, r.Flags);
        }

        // ---- 시계와 인사 (10.4)

        [Fact]
        public void SyncRequestFirstAndEcho()
        {
            AssertWrites("sync_request.first", w => new SyncRequestPacket(0x0102030405UL, 0, 250).WriteTo(w));
            AssertWrites("sync_request.echo", w => new SyncRequestPacket(1000000UL, 7, 1200).WriteTo(w));
            Assert.Equal(DuelCodecError.None, SyncRequestPacket.TryRead(PacketFixtures.BodyBytes("sync_request.echo"), out SyncRequestPacket q));
            Assert.Equal(1000000UL, q.T0Us);
            Assert.Equal(7U, q.EchoServerSeq);
            Assert.Equal(1200U, q.EchoHoldUs);
            Assert.Equal(DuelCodecError.None, SyncRequestPacket.TryRead(PacketFixtures.BodyBytes("sync_request.first"), out q));
            Assert.Equal(0U, q.EchoServerSeq);
            Assert.Equal(250U, q.EchoHoldUs);
        }

        [Fact]
        public void SyncReply()
        {
            AssertWrites("sync_reply", w => new SyncReplyPacket(1000000UL, 0x500000001UL, 8).WriteTo(w));
            Assert.Equal(DuelCodecError.None, SyncReplyPacket.TryRead(PacketFixtures.BodyBytes("sync_reply"), out SyncReplyPacket r));
            Assert.Equal(1000000UL, r.T0Us);
            Assert.Equal(0x500000001UL, r.ServerTimeUs);
            Assert.Equal(8U, r.ServerSeq);
        }

        [Fact]
        public void Hello()
        {
            AssertWrites("hello", w => Assert.Equal(DuelCodecError.None, new HelloPacket(new PlayerSlot(1), 20261112).TryWrite(w)));
            Assert.Equal(DuelCodecError.None, HelloPacket.TryRead(PacketFixtures.BodyBytes("hello"), out HelloPacket h));
            Assert.Equal(new PlayerSlot(1), h.Slot);
            Assert.Equal(20261112U, h.ClientBuild);
        }

        [Fact]
        public void HelloAck()
        {
            AssertWrites("hello_ack", w => new HelloAckPacket(180, 1500000UL, 3, 2, 74).WriteTo(w));
            Assert.Equal(DuelCodecError.None, HelloAckPacket.TryRead(PacketFixtures.BodyBytes("hello_ack"), out HelloAckPacket a));
            Assert.Equal(180U, a.StartTick);
            Assert.Equal(1500000UL, a.MatchEpochServerUs);
            Assert.Equal(3, a.InputDelaySelf);
            Assert.Equal(2, a.InputDelayOpponent);
            Assert.Equal(74, a.RttMsMeasured);
        }

        [Fact]
        public void Bye()
        {
            AssertWrites("bye", w => new ByePacket(1).WriteTo(w));
            Assert.Equal(DuelCodecError.None, ByePacket.TryRead(PacketFixtures.BodyBytes("bye"), out ByePacket b));
            Assert.Equal(1, b.Reason);
        }

        internal static ServerTickPacket Full()
        {
            var p = new ServerTickPacket
            {
                ServerTimeMs = 74565, EchoClientSendTimeMs = 74550, MarginMsEstimate = 21, ConfirmedFromTick = 89,
                HasHash = true, HashTick = 90, StateHash = 0x0123456789abcdefUL,
            };
            InputEvent[] s0 = { new InputEvent(89, 64, LogicalKey.Fire, true, 5) };
            InputEvent[] s1 = { new InputEvent(89, 0, LogicalKey.Up, true, 130), new InputEvent(89, 0, LogicalKey.Skill, true, 131) };
            Assert.Equal(DuelCodecError.None, p.TryAddConfirmedTick(s0, s1));
            Assert.Equal(DuelCodecError.None, p.TryAddConfirmedTick(ReadOnlySpan<InputEvent>.Empty, ReadOnlySpan<InputEvent>.Empty));
            Assert.Equal(DuelCodecError.None, p.TryAddDropped(new DroppedRun(6, DuelDropReason.Late, 0)));
            Assert.Equal(DuelCodecError.None, p.TryAddDropped(new DroppedRun(7, DuelDropReason.Lost, 299)));
            return p;
        }

        private static void AssertWrites(string fixture, ClientInputPacket p)
        {
            var w = new ByteWriter();
            Assert.Equal(DuelCodecError.None, p.Measure(out int size));
            Assert.Equal(DuelCodecError.None, p.TryWrite(w));
            Assert.Equal(size, w.Length);
            Assert.Equal(PacketFixtures.BodyBytes(fixture), w.ToArray());
        }

        private static void AssertWrites(string fixture, ServerTickPacket p)
        {
            var w = new ByteWriter();
            Assert.Equal(DuelCodecError.None, p.Measure(out int size));
            Assert.Equal(DuelCodecError.None, p.TryWrite(w));
            Assert.Equal(size, w.Length);
            Assert.Equal(PacketFixtures.BodyBytes(fixture), w.ToArray());
        }

        private static void AssertWrites(string fixture, Action<ByteWriter> write)
        {
            var w = new ByteWriter();
            write(w);
            Assert.Equal(PacketFixtures.BodyBytes(fixture), w.ToArray());
        }

        internal static void AssertSame(ClientInputPacket expected, ClientInputPacket actual)
        {
            Assert.Equal(expected.SealedThroughTick, actual.SealedThroughTick);
            Assert.Equal(expected.AckConfirmedTick, actual.AckConfirmedTick);
            Assert.Equal(expected.ClientSendTimeMs, actual.ClientSendTimeMs);
            Assert.Equal(expected.EchoServerTimeMs, actual.EchoServerTimeMs);
            Assert.Equal(expected.EchoHoldMs, actual.EchoHoldMs);
            Assert.Equal(expected.BaseSeq, actual.BaseSeq);
            AssertEvents(expected.Events, actual.Events);
        }

        internal static void AssertSame(ServerTickPacket expected, ServerTickPacket actual)
        {
            Assert.Equal(expected.ServerTimeMs, actual.ServerTimeMs);
            Assert.Equal(expected.EchoClientSendTimeMs, actual.EchoClientSendTimeMs);
            Assert.Equal(expected.MarginMsEstimate, actual.MarginMsEstimate);
            Assert.Equal(expected.ConfirmedFromTick, actual.ConfirmedFromTick);
            Assert.Equal(expected.ConfirmedCount, actual.ConfirmedCount);
            for (int t = 0; t < expected.ConfirmedCount; t++)
            {
                AssertEvents(expected.SlotEvents(t, 0), actual.SlotEvents(t, 0));
                AssertEvents(expected.SlotEvents(t, 1), actual.SlotEvents(t, 1));
            }
            Assert.Equal(expected.HasHash, actual.HasHash);
            Assert.Equal(expected.HashTick, actual.HashTick);
            Assert.Equal(expected.StateHash, actual.StateHash);
            Assert.Equal(expected.DroppedCount, actual.DroppedCount);
            for (int i = 0; i < expected.DroppedCount; i++)
            {
                Assert.Equal(expected.Dropped[i].Seq, actual.Dropped[i].Seq);
                Assert.Equal(expected.Dropped[i].Reason, actual.Dropped[i].Reason);
                Assert.Equal(expected.Dropped[i].Run, actual.Dropped[i].Run);
            }
        }

        private static void AssertEvents(ReadOnlySpan<InputEvent> expected, ReadOnlySpan<InputEvent> actual)
        {
            Assert.Equal(expected.Length, actual.Length);
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.Equal(expected[i].Tick, actual[i].Tick);
                Assert.Equal(expected[i].Offset, actual[i].Offset);
                Assert.Equal(expected[i].Key, actual[i].Key);
                Assert.Equal(expected[i].Down, actual[i].Down);
                Assert.Equal(expected[i].Seq, actual[i].Seq);
            }
        }
    }
}

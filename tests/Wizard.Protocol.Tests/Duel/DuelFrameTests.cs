using System;
using System.Security.Cryptography;
using System.Text;
using Wizard.Protocol.Duel;
using Xunit;

namespace Wizard.Protocol.Tests.Duel
{
    // CONTRACTS 10.1 겉 형식: 머리 13바이트(= AAD), nonce, 1,200바이트 상한, 태그가 틀리면 열지 않는다.
    public sealed class DuelFrameTests
    {
        private readonly BclChaCha20Poly1305 _aead = new BclChaCha20Poly1305();

        public static TheoryData<string> AllFrames()
        {
            var d = new TheoryData<string>();
            foreach (string n in PacketFixtures.Frames.Keys)
            {
                d.Add(n);
            }
            return d;
        }

        // 시험 암호가 정말 ChaCha20-Poly1305 인지 RFC 8439 2.8.2 로 먼저 확인한다(픽스처를 만든 Node 쪽도 같은 벡터를 확인했다)
        [Fact]
        public void TestCipherMatchesRfc8439Section282()
        {
            Assert.True(ChaCha20Poly1305.IsSupported, "ChaCha20Poly1305 is not supported on this machine");
            byte[] key = PacketFixtures.Hex("808182838485868788898a8b8c8d8e8f909192939495969798999a9b9c9d9e9f");
            byte[] nonce = PacketFixtures.Hex("070000004041424344454647");
            byte[] aad = PacketFixtures.Hex("50515253c0c1c2c3c4c5c6c7");
            byte[] pt = Encoding.ASCII.GetBytes(
                "Ladies and Gentlemen of the class of '99: If I could offer you only one tip for the future, sunscreen would be it.");
            var outBuf = new byte[pt.Length + 16];
            _aead.Seal(key, nonce, aad, pt, outBuf);
            Assert.Equal(PacketFixtures.Hex("d31a8d34648e60db7b86afbc53ef7ec2"), outBuf.AsSpan(0, 16).ToArray());
            Assert.Equal(PacketFixtures.Hex("1ae10b594f09e26a7e902ecbd0600691"), outBuf.AsSpan(pt.Length, 16).ToArray());
        }

        [Fact]
        public void EveryFrameFixtureUsesItsHeaderAsNonceSource()
        {
            foreach (PacketFixtures.Frame f in PacketFixtures.Frames.Values)
            {
                Assert.Equal(DuelCodecError.None, DuelPacketFrame.TryPeekHeader(f.Packet, out DuelPacketHeader h));
                var nonce = new byte[DuelWire.NonceBytes];
                DuelPacketFrame.BuildNonce((DuelDirection)f.Direction, h.PacketNumber, nonce);
                Assert.Equal(f.Nonce, nonce);
                Assert.Equal(PacketFixtures.Bodies[f.BodyName].Kind, (int)h.Kind);
            }
        }

        [Theory]
        [MemberData(nameof(AllFrames))]
        public void SealMatchesFixtureAndOpenRecoversBody(string name)
        {
            PacketFixtures.Frame f = PacketFixtures.Frames[name];
            byte[] body = PacketFixtures.BodyBytes(f.BodyName);
            Assert.Equal(DuelCodecError.None, DuelPacketFrame.TryPeekHeader(f.Packet, out DuelPacketHeader h));

            var packet = new byte[DuelWire.MaxPacketBytes];
            int n = DuelPacketFrame.Seal(_aead, f.Key, (DuelDirection)f.Direction, h, body, packet, out DuelCodecError e);
            Assert.Equal(DuelCodecError.None, e);
            Assert.Equal(f.Packet, packet.AsSpan(0, n).ToArray());

            var opened = new byte[DuelWire.MaxBodyBytes];
            Assert.True(DuelPacketFrame.TryOpen(_aead, f.Key, (DuelDirection)f.Direction, f.Packet, opened, out DuelPacketHeader oh,
                out int len, out e));
            Assert.Equal(DuelCodecError.None, e);
            Assert.Equal(body, opened.AsSpan(0, len).ToArray());
            Assert.Equal(h.Kind, oh.Kind);
            Assert.Equal(h.TokenId, oh.TokenId);
            Assert.Equal(h.PacketNumber, oh.PacketNumber);

            // 다시 봉인해도 같은 바이트(쓰기 → 읽기 → 쓰기)
            int n2 = DuelPacketFrame.Seal(_aead, f.Key, (DuelDirection)f.Direction, oh, opened.AsSpan(0, len), packet, out e);
            Assert.Equal(f.Packet, packet.AsSpan(0, n2).ToArray());
        }

        [Fact]
        public void HeaderFieldsOfFixtures()
        {
            Assert.Equal(DuelCodecError.None, DuelPacketFrame.TryPeekHeader(PacketFixtures.Frames["client_input.empty"].Packet, out DuelPacketHeader a));
            Assert.Equal(DuelPacketKind.ClientInput, a.Kind);
            Assert.Equal(0x0a0b0c0dU, a.TokenId);
            Assert.Equal(1UL, a.PacketNumber);
            Assert.Equal(0x11, a.VerType);
            Assert.Equal(52, PacketFixtures.Frames["client_input.empty"].Packet.Length);
            Assert.Equal(DuelCodecError.None, DuelPacketFrame.TryPeekHeader(PacketFixtures.Frames["bye.max_number"].Packet, out DuelPacketHeader b));
            Assert.Equal(DuelPacketKind.Bye, b.Kind);
            Assert.Equal(ulong.MaxValue, b.PacketNumber);
        }

        // 태그가 틀리거나, 머리(AAD)·본문·방향·키가 다르면 열리지 않는다
        [Fact]
        public void TamperedPacketsDoNotOpen()
        {
            PacketFixtures.Frame f = PacketFixtures.Frames["server_tick.full"];
            var body = new byte[DuelWire.MaxBodyBytes];
            for (int i = 0; i < f.Packet.Length; i++)
            {
                byte[] p = (byte[])f.Packet.Clone();
                p[i] ^= 0x01;
                bool ok = DuelPacketFrame.TryOpen(_aead, f.Key, (DuelDirection)f.Direction, p, body, out _, out _, out DuelCodecError e);
                Assert.False(ok, "flipped byte " + i);
                Assert.NotEqual(DuelCodecError.None, e);
            }
            Assert.False(DuelPacketFrame.TryOpen(_aead, f.Key, DuelDirection.ClientToServer, f.Packet, body, out _, out _, out DuelCodecError wrongDir));
            Assert.Equal(DuelCodecError.Unauthentic, wrongDir);
            byte[] otherKey = PacketFixtures.Frames["client_input.empty"].Key;
            Assert.False(DuelPacketFrame.TryOpen(_aead, otherKey, (DuelDirection)f.Direction, f.Packet, body, out _, out _, out DuelCodecError wrongKey));
            Assert.Equal(DuelCodecError.Unauthentic, wrongKey);
        }

        [Fact]
        public void SizeLimits()
        {
            byte[] key = PacketFixtures.Frames["client_input.empty"].Key;
            var h = new DuelPacketHeader(DuelPacketKind.Bye, 7, 1);
            var big = new byte[DuelWire.MaxPacketBytes + 64];

            // 본문 1,171바이트 = 패킷 1,200바이트까지 된다
            Assert.Equal(1171, DuelWire.MaxBodyBytes);
            int n = DuelPacketFrame.Seal(_aead, key, DuelDirection.ClientToServer, h, new byte[DuelWire.MaxBodyBytes], big, out DuelCodecError e);
            Assert.Equal(DuelCodecError.None, e);
            Assert.Equal(DuelWire.MaxPacketBytes, n);
            var body = new byte[DuelWire.MaxPacketBytes];
            Assert.True(DuelPacketFrame.TryOpen(_aead, key, DuelDirection.ClientToServer, big.AsSpan(0, n), body, out _, out int len, out e));
            Assert.Equal(DuelWire.MaxBodyBytes, len);

            // 하나 더 크면 봉인도 열기도 거절한다(packet 은 건드리지 않는다)
            var untouched = new byte[DuelWire.MaxPacketBytes + 64];
            Assert.Equal(0, DuelPacketFrame.Seal(_aead, key, DuelDirection.ClientToServer, h, new byte[DuelWire.MaxBodyBytes + 1], untouched, out e));
            Assert.Equal(DuelCodecError.TooLarge, e);
            Assert.All(untouched, b => Assert.Equal(0, b));
            Assert.Equal(DuelCodecError.TooLarge, DuelPacketFrame.TryPeekHeader(new byte[DuelWire.MaxPacketBytes + 1], out _));
            Assert.False(DuelPacketFrame.TryOpen(_aead, key, DuelDirection.ClientToServer, big.AsSpan(0, DuelWire.MaxPacketBytes + 1), body, out _, out _, out e));
            Assert.Equal(DuelCodecError.TooLarge, e);

            // 받는 버퍼가 작아도, 내보낼 버퍼가 작아도 거절
            Assert.False(DuelPacketFrame.TryOpen(_aead, key, DuelDirection.ClientToServer, big.AsSpan(0, n), new byte[10], out _, out _, out e));
            Assert.Equal(DuelCodecError.TooLarge, e);
            Assert.Equal(0, DuelPacketFrame.Seal(_aead, key, DuelDirection.ClientToServer, h, new byte[1], new byte[29], out e));
            Assert.Equal(DuelCodecError.TooLarge, e);

            // 머리와 태그(29바이트)보다 짧으면 잘린 것
            Assert.Equal(DuelCodecError.Truncated, DuelPacketFrame.TryPeekHeader(new byte[DuelWire.OverheadBytes - 1], out _));
        }

        [Fact]
        public void HeaderRejectsVersionKindNumberAndKey()
        {
            byte[] key = PacketFixtures.Frames["client_input.empty"].Key;
            byte[] p = (byte[])PacketFixtures.Frames["client_input.empty"].Packet.Clone();
            var body = new byte[DuelWire.MaxBodyBytes];

            byte[] v2 = (byte[])p.Clone();
            v2[0] = 0x21;
            Assert.Equal(DuelCodecError.BadVersion, DuelPacketFrame.TryPeekHeader(v2, out _));
            byte[] v0 = (byte[])p.Clone();
            v0[0] = 0x01;
            Assert.Equal(DuelCodecError.BadVersion, DuelPacketFrame.TryPeekHeader(v0, out _));
            foreach (byte kind in new byte[] { 0x10, 0x18, 0x1f })
            {
                byte[] k = (byte[])p.Clone();
                k[0] = kind;
                Assert.Equal(DuelCodecError.UnknownKind, DuelPacketFrame.TryPeekHeader(k, out _));
            }
            byte[] zero = (byte[])p.Clone();
            zero.AsSpan(5, 8).Clear();
            Assert.Equal(DuelCodecError.OutOfRange, DuelPacketFrame.TryPeekHeader(zero, out _));

            Assert.False(DuelPacketFrame.TryOpen(_aead, key.AsSpan(0, 31), DuelDirection.ClientToServer, p, body, out _, out _, out DuelCodecError e));
            Assert.Equal(DuelCodecError.BadKey, e);
            var outBuf = new byte[64];
            Assert.Equal(0, DuelPacketFrame.Seal(_aead, key.AsSpan(0, 16), DuelDirection.ClientToServer, new DuelPacketHeader(DuelPacketKind.Bye, 1, 1),
                new byte[1], outBuf, out e));
            Assert.Equal(DuelCodecError.BadKey, e);
            Assert.Equal(0, DuelPacketFrame.Seal(_aead, key, DuelDirection.ClientToServer, new DuelPacketHeader(DuelPacketKind.Bye, 1, 0),
                new byte[1], outBuf, out e));
            Assert.Equal(DuelCodecError.OutOfRange, e);
            Assert.Equal(0, DuelPacketFrame.Seal(_aead, key, DuelDirection.ClientToServer, new DuelPacketHeader((DuelPacketKind)8, 1, 1),
                new byte[1], outBuf, out e));
            Assert.Equal(DuelCodecError.UnknownKind, e);
        }

        [Fact]
        public void NonceLayout()
        {
            var n = new byte[12];
            DuelPacketFrame.BuildNonce(DuelDirection.ServerToClient, 0x0807060504030201UL, n);
            Assert.Equal(PacketFixtures.Hex("01 000000 0102030405060708"), n);
            DuelPacketFrame.BuildNonce(DuelDirection.ClientToServer, 1UL, n);
            Assert.Equal(PacketFixtures.Hex("00 000000 0100000000000000"), n);
        }
    }
}

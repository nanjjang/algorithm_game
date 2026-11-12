using System;

namespace Wizard.Protocol.Duel
{
    // CONTRACTS 10.1. 겉 형식: 머리 13바이트 ‖ ciphertext(본문) ‖ tag 16바이트. 한 패킷은 1,200바이트를 넘지 않는다.
    // 암호: ChaCha20-Poly1305(IAeadCipher, M2-NET-02). nonce = 방향 1바이트 ‖ 0x00 0x00 0x00 ‖ packet_number 8바이트 LE. AAD = 머리 13바이트.
    // 받는 쪽 순서(10.1): TryPeekHeader → token_id 로 키를 찾는다(모르면 버린다, 응답 없음) → DuelReplayWindow.IsFresh
    //   → TryOpen(태그가 틀리면 버린다) → DuelReplayWindow.Mark → 응답 주소 갱신. 태그를 통과하기 전에는 아무 상태도 바꾸지 않는다.
    public static class DuelPacketFrame
    {
        public static void BuildNonce(DuelDirection direction, ulong packetNumber, Span<byte> nonce12)
        {
            nonce12[0] = (byte)direction;
            nonce12[1] = 0;
            nonce12[2] = 0;
            nonce12[3] = 0;
            for (int i = 0; i < 8; i++)
            {
                nonce12[4 + i] = (byte)(packetNumber >> (8 * i));
            }
        }

        public static int SealedLength(int bodyLength) => DuelWire.OverheadBytes + bodyLength;

        // 본문을 봉인해 packet 에 쓴다. 돌려주는 값은 쓴 길이. 실패하면 0이고 error 에 이유가 있다(packet 은 건드리지 않는다)
        public static int Seal(IAeadCipher cipher, ReadOnlySpan<byte> key, DuelDirection direction, in DuelPacketHeader header,
                               ReadOnlySpan<byte> body, Span<byte> packet, out DuelCodecError error)
        {
            error = Check(key, header, body.Length, packet.Length);
            if (error != DuelCodecError.None)
            {
                return 0;
            }
            int total = SealedLength(body.Length);
            Span<byte> nonce = stackalloc byte[DuelWire.NonceBytes];
            BuildNonce(direction, header.PacketNumber, nonce);
            header.WriteTo(packet.Slice(0, DuelWire.HeaderBytes));
            cipher.Seal(key, nonce, packet.Slice(0, DuelWire.HeaderBytes), body, packet.Slice(DuelWire.HeaderBytes, total - DuelWire.HeaderBytes));
            return total;
        }

        // 머리만 읽는다(열지 않는다). 길이가 겉 형식의 범위(29~1,200)인지도 본다
        public static DuelCodecError TryPeekHeader(ReadOnlySpan<byte> packet, out DuelPacketHeader header)
        {
            header = default;
            if (packet.Length > DuelWire.MaxPacketBytes)
            {
                return DuelCodecError.TooLarge;
            }
            if (packet.Length < DuelWire.OverheadBytes)
            {
                return DuelCodecError.Truncated;
            }
            return DuelPacketHeader.TryRead(packet, out header);
        }

        // 머리를 확인하고 태그를 검증해 본문을 body 에 연다. body 는 packet.Length − 29 이상이어야 한다.
        // 실패하면 false. 태그가 틀리면 Unauthentic(본문을 읽지 않는다)
        public static bool TryOpen(IAeadCipher cipher, ReadOnlySpan<byte> key, DuelDirection direction, ReadOnlySpan<byte> packet,
                                   Span<byte> body, out DuelPacketHeader header, out int bodyLength, out DuelCodecError error)
        {
            bodyLength = 0;
            error = TryPeekHeader(packet, out header);
            if (error != DuelCodecError.None)
            {
                return false;
            }
            if (key.Length != DuelWire.KeyBytes)
            {
                error = DuelCodecError.BadKey;
                return false;
            }
            int n = packet.Length - DuelWire.OverheadBytes;
            if (body.Length < n)
            {
                error = DuelCodecError.TooLarge;
                return false;
            }
            Span<byte> nonce = stackalloc byte[DuelWire.NonceBytes];
            BuildNonce(direction, header.PacketNumber, nonce);
            if (!cipher.Open(key, nonce, packet.Slice(0, DuelWire.HeaderBytes), packet.Slice(DuelWire.HeaderBytes), body.Slice(0, n)))
            {
                error = DuelCodecError.Unauthentic;
                return false;
            }
            bodyLength = n;
            return true;
        }

        private static DuelCodecError Check(ReadOnlySpan<byte> key, in DuelPacketHeader header, int bodyLength, int capacity)
        {
            if (key.Length != DuelWire.KeyBytes)
            {
                return DuelCodecError.BadKey;
            }
            DuelCodecError e = header.Validate();
            if (e != DuelCodecError.None)
            {
                return e;
            }
            if (bodyLength > DuelWire.MaxBodyBytes)
            {
                return DuelCodecError.TooLarge;
            }
            return capacity < SealedLength(bodyLength) ? DuelCodecError.TooLarge : DuelCodecError.None;
        }
    }
}

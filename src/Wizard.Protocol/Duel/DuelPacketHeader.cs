using System;
using Wizard.Core;

namespace Wizard.Protocol.Duel
{
    // CONTRACTS 10.1 겉 형식의 앞 13바이트(= AAD).
    // 0 ver_type(윗 4비트 판 1, 아랫 4비트 종류) · 1 token_id u32 · 5 packet_number u64(보내는 쪽마다 1부터)
    public readonly struct DuelPacketHeader
    {
        public readonly DuelPacketKind Kind;
        public readonly uint TokenId;
        public readonly ulong PacketNumber;

        public DuelPacketHeader(DuelPacketKind kind, uint tokenId, ulong packetNumber)
        {
            Kind = kind;
            TokenId = tokenId;
            PacketNumber = packetNumber;
        }

        public byte VerType => (byte)((DuelWire.ProtocolVersion << 4) | (int)Kind);

        public static bool IsKnownKind(DuelPacketKind kind) => kind >= DuelPacketKind.ClientInput && kind <= DuelPacketKind.Bye;

        // 판, 종류, 번호(0 은 없다)를 확인한다. 쓰기와 읽기가 같은 검사를 쓴다
        public DuelCodecError Validate()
        {
            if (!IsKnownKind(Kind))
            {
                return DuelCodecError.UnknownKind;
            }
            return PacketNumber == 0UL ? DuelCodecError.OutOfRange : DuelCodecError.None;
        }

        // 앞 13바이트만 읽는다. 뒤의 길이는 보지 않는다(DuelPacketFrame 이 본다)
        public static DuelCodecError TryRead(ReadOnlySpan<byte> packet, out DuelPacketHeader header)
        {
            header = default;
            var r = new ByteReader(packet);
            if (!r.TryU8(out byte verType) || !r.TryU32(out uint token) || !r.TryU64(out ulong number))
            {
                return DuelCodecError.Truncated;
            }
            if ((verType >> 4) != DuelWire.ProtocolVersion)
            {
                return DuelCodecError.BadVersion;
            }
            var h = new DuelPacketHeader((DuelPacketKind)(verType & 0x0F), token, number);
            DuelCodecError e = h.Validate();
            if (e != DuelCodecError.None)
            {
                return e;
            }
            header = h;
            return DuelCodecError.None;
        }

        // 13바이트를 그대로 쓴다. 시프트로만 쓴다(ByteWriter 와 같은 바이트)
        public void WriteTo(Span<byte> destination)
        {
            destination[0] = VerType;
            for (int i = 0; i < 4; i++)
            {
                destination[1 + i] = (byte)(TokenId >> (8 * i));
            }
            for (int i = 0; i < 8; i++)
            {
                destination[5 + i] = (byte)(PacketNumber >> (8 * i));
            }
        }
    }
}

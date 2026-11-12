using System;
using Wizard.Core;

namespace Wizard.Protocol.Duel
{
    // CONTRACTS 10.4. 양쪽. 1바이트. reason 의 값은 계약이 아직 정하지 않았다(결과 보고의 계약 변경 요청).
    // 그때까지 코덱은 바이트를 그대로 옮기고 범위를 따지지 않는다.
    public readonly struct ByePacket
    {
        public const int BodyBytes = 1;

        public readonly byte Reason;

        public ByePacket(byte reason)
        {
            Reason = reason;
        }

        public void WriteTo(ByteWriter w) => w.U8(Reason);

        public static DuelCodecError TryRead(ReadOnlySpan<byte> body, out ByePacket packet)
        {
            packet = default;
            if (body.Length > BodyBytes)
            {
                return DuelCodecError.TrailingBytes;
            }
            var r = new ByteReader(body);
            if (!r.TryU8(out byte reason))
            {
                return DuelCodecError.Truncated;
            }
            packet = new ByePacket(reason);
            return DuelCodecError.None;
        }
    }
}

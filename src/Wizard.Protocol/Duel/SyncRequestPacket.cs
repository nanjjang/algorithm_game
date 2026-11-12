using System;
using Wizard.Core;

namespace Wizard.Protocol.Duel
{
    // CONTRACTS 10.4. 클라 → 서버. 16바이트.
    // t0_us: 클라 단조 시계. echo_server_seq: 직전 SyncReply 의 server_seq(처음이면 0). echo_hold_us: 그 답을 받고 이것을 보낼 때까지 든 시간.
    // 서버는 echo 두 칸으로 자기 왕복 표본을 만든다(10.5. 클라이언트가 잰 왕복은 싣지 않는다)
    public readonly struct SyncRequestPacket
    {
        public const int BodyBytes = 16;

        public readonly ulong T0Us;
        public readonly uint EchoServerSeq;
        public readonly uint EchoHoldUs;

        public SyncRequestPacket(ulong t0Us, uint echoServerSeq, uint echoHoldUs)
        {
            T0Us = t0Us;
            EchoServerSeq = echoServerSeq;
            EchoHoldUs = echoHoldUs;
        }

        public void WriteTo(ByteWriter w)
        {
            w.U64(T0Us);
            w.U32(EchoServerSeq);
            w.U32(EchoHoldUs);
        }

        public static DuelCodecError TryRead(ReadOnlySpan<byte> body, out SyncRequestPacket packet)
        {
            packet = default;
            if (body.Length > BodyBytes)
            {
                return DuelCodecError.TrailingBytes;
            }
            var r = new ByteReader(body);
            if (!r.TryU64(out ulong t0) || !r.TryU32(out uint seq) || !r.TryU32(out uint hold))
            {
                return DuelCodecError.Truncated;
            }
            packet = new SyncRequestPacket(t0, seq, hold);
            return DuelCodecError.None;
        }
    }
}

using System;
using Wizard.Core;

namespace Wizard.Protocol.Duel
{
    // CONTRACTS 10.4. 서버 → 클라. 20바이트.
    // t0_us: 받은 SyncRequest 의 값 그대로. server_time_us: 서버 시각. server_seq: 서버가 매긴 번호(보낸 시각을 서버가 기억한다)
    public readonly struct SyncReplyPacket
    {
        public const int BodyBytes = 20;

        public readonly ulong T0Us;
        public readonly ulong ServerTimeUs;
        public readonly uint ServerSeq;

        public SyncReplyPacket(ulong t0Us, ulong serverTimeUs, uint serverSeq)
        {
            T0Us = t0Us;
            ServerTimeUs = serverTimeUs;
            ServerSeq = serverSeq;
        }

        public void WriteTo(ByteWriter w)
        {
            w.U64(T0Us);
            w.U64(ServerTimeUs);
            w.U32(ServerSeq);
        }

        public static DuelCodecError TryRead(ReadOnlySpan<byte> body, out SyncReplyPacket packet)
        {
            packet = default;
            if (body.Length > BodyBytes)
            {
                return DuelCodecError.TrailingBytes;
            }
            var r = new ByteReader(body);
            if (!r.TryU64(out ulong t0) || !r.TryU64(out ulong ts) || !r.TryU32(out uint seq))
            {
                return DuelCodecError.Truncated;
            }
            packet = new SyncReplyPacket(t0, ts, seq);
            return DuelCodecError.None;
        }
    }
}

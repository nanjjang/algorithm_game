using System;
using Wizard.Core;
using Wizard.Protocol.Duel;

namespace Wizard.Protocol.Tests.Duel
{
    // 종류별 본문 코덱을 한 입구로 묶는다(시험용). 읽기에 성공하면 다시 쓴 바이트를 돌려준다.
    internal sealed class BodyRoundTrip
    {
        private readonly ClientInputPacket _ci = new ClientInputPacket();
        private readonly ServerTickPacket _st = new ServerTickPacket();
        private readonly ByteWriter _w = new ByteWriter(DuelWire.MaxPacketBytes);

        public ClientInputPacket ClientInput => _ci;

        public ServerTickPacket ServerTick => _st;

        public DuelCodecError Read(DuelPacketKind kind, ReadOnlySpan<byte> body)
        {
            _w.Reset();
            DuelCodecError e;
            switch (kind)
            {
                case DuelPacketKind.ClientInput:
                    e = _ci.TryRead(body);
                    return e == DuelCodecError.None ? _ci.TryWrite(_w) : e;
                case DuelPacketKind.ServerTick:
                    e = _st.TryRead(body);
                    return e == DuelCodecError.None ? _st.TryWrite(_w) : e;
                case DuelPacketKind.SyncRequest:
                    e = SyncRequestPacket.TryRead(body, out SyncRequestPacket sq);
                    if (e == DuelCodecError.None)
                    {
                        sq.WriteTo(_w);
                    }
                    return e;
                case DuelPacketKind.SyncReply:
                    e = SyncReplyPacket.TryRead(body, out SyncReplyPacket sr);
                    if (e == DuelCodecError.None)
                    {
                        sr.WriteTo(_w);
                    }
                    return e;
                case DuelPacketKind.Hello:
                    e = HelloPacket.TryRead(body, out HelloPacket h);
                    return e == DuelCodecError.None ? h.TryWrite(_w) : e;
                case DuelPacketKind.HelloAck:
                    e = HelloAckPacket.TryRead(body, out HelloAckPacket ha);
                    if (e == DuelCodecError.None)
                    {
                        ha.WriteTo(_w);
                    }
                    return e;
                case DuelPacketKind.Bye:
                    e = ByePacket.TryRead(body, out ByePacket b);
                    if (e == DuelCodecError.None)
                    {
                        b.WriteTo(_w);
                    }
                    return e;
                default:
                    return DuelCodecError.UnknownKind;
            }
        }

        public byte[] Rewritten => _w.ToArray();

        public ReadOnlySpan<byte> RewrittenSpan => _w.Written;
    }
}

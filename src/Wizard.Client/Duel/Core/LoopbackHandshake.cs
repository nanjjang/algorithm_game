using System;
using Wizard.Content.Rules;
using Wizard.Core;
using Wizard.Netcode.Clock;
using Wizard.Netcode.Duel;
using Wizard.Protocol;
using Wizard.Protocol.Duel;

namespace Wizard.Client.Duel
{
    // 기기 안 호스트의 연결 절차 한 사람 몫(CONTRACTS 10.4·10.5): SyncRequest → SyncReply, Hello → HelloAck.
    // DuelHostCore 는 Hello·SyncRequest 의 태그만 보고 내용을 보지 않는다(M2-NET-04). 그래서 서버 쪽 인사는 wizard-duel 의 루프가 하고,
    // 기기 안에서는 LoopbackHost 가 이 클래스로 한다. 바이트는 서버 경기와 같다.
    //
    // 받는 쪽: 자기 재전송 창(C→S)으로 연다. 같은 키의 ClientInput 은 DuelHostCore 의 창이 연다(클라이언트의 번호는 한 자리에서 나오므로
    // 두 창이 같은 번호를 받는 일은 없다. 빈 번호는 허용이다, T-62 3번).
    // 왕복은 서버가 잰다(10.5): SyncRequest 의 echo_server_seq 로 내가 SyncReply 를 보낸 시각을 찾고, 왕복 = 도착 − 보낸 시각 − echo_hold_us.
    //
    // 보내는 쪽(S→C)의 번호. 같은 키의 S→C 번호는 DuelHostCore 가 1부터 센다(HostPeer.NextOutPacketNumber)인데 그 자리를 밖에서 받을 문이
    // 계약에 없다. 같은 (키, 방향, 번호)를 두 번 봉인하지 않게 인사 답의 번호는 번호 공간의 위쪽 절반(2^62부터)을 쓴다 [임시].
    // DuelHostCore 가 한 경기에 2^62 개를 보내는 일은 없다. 계약 변경 요청으로 올린다(결과 보고: S→C 번호의 단일 자리).
    internal sealed class LoopbackHandshake
    {
        // [임시] 위 머리 주석. 번호 공간을 가르는 구조 상수(기획 수치가 아니다)
        public const ulong FirstReplyPacketNumber = 1UL << 62;

        public readonly PlayerSlot Slot;
        public readonly uint TokenId;
        private readonly byte[] _key;
        private readonly long[] _replySentUs;      // server_seq 로 찾는 보낸 시각(최근 ClockSyncSamples 개)
        private readonly RttSampleWindow _rtt;
        private readonly byte[] _body = new byte[DuelWire.MaxBodyBytes];
        private readonly ByteWriter _writer = new ByteWriter(DuelWire.MaxBodyBytes);
        private DuelReplayWindow _in;
        private ulong _nextOut = FirstReplyPacketNumber;
        private uint _serverSeq;

        public LoopbackHandshake(PlayerSlot slot, in DuelPeerKey key, NetRuleSet rules)
        {
            Slot = slot;
            TokenId = key.TokenId;
            _key = key.Key.ToArray();
            _rtt = new RttSampleWindow(rules);
            _replySentUs = new long[rules.ClockSyncSamples];
        }

        public bool HelloSeen { get; private set; }

        public uint ClientBuild { get; private set; }

        // 서버가 잰 왕복(최근 표본의 최솟값, ms 올림). 표본이 없으면 −1
        public int MeasuredRttMs => _rtt.MinRttMs;

        public int SyncRepliesSent => (int)_serverSeq;

        // 이 사람의 인사 패킷(SyncRequest, Hello)을 연다. 열었으면 kind 를 돌려주고, 아니면(모르는 종류, 재전송, 태그 틀림, 형식 틀림) false
        public bool TryOpen(IAeadCipher cipher, ReadOnlySpan<byte> packet, out DuelPacketKind kind, out int bodyLength)
        {
            kind = default;
            bodyLength = 0;
            if (DuelPacketFrame.TryPeekHeader(packet, out DuelPacketHeader header) != DuelCodecError.None || header.TokenId != TokenId
                || (header.Kind != DuelPacketKind.SyncRequest && header.Kind != DuelPacketKind.Hello) || !_in.IsFresh(header.PacketNumber))
            {
                return false;
            }
            if (!DuelPacketFrame.TryOpen(cipher, _key, DuelDirection.ClientToServer, packet, _body, out _, out bodyLength, out _))
            {
                return false;
            }
            _in.Mark(header.PacketNumber);
            kind = header.Kind;
            return true;
        }

        // 열린 SyncRequest 에 SyncReply 를 짓는다. arrivalUs = 그 패킷이 닿은 서버 시각, nowUs = 답을 보내는 서버 시각(server_time_us)
        public int ReplyToSync(IAeadCipher cipher, int bodyLength, long arrivalUs, long nowUs, Span<byte> reply)
        {
            if (SyncRequestPacket.TryRead(new ReadOnlySpan<byte>(_body, 0, bodyLength), out SyncRequestPacket req) != DuelCodecError.None)
            {
                return 0;
            }
            uint echo = req.EchoServerSeq;
            if (echo != 0U && echo <= _serverSeq && _serverSeq - echo < (uint)_replySentUs.Length)
            {
                _rtt.Offer(arrivalUs, _replySentUs[(int)(echo % (uint)_replySentUs.Length)], req.EchoHoldUs);
            }
            _serverSeq++;
            _replySentUs[(int)(_serverSeq % (uint)_replySentUs.Length)] = nowUs;
            _writer.Reset();
            new SyncReplyPacket(req.T0Us, unchecked((ulong)nowUs), _serverSeq).WriteTo(_writer);
            return Seal(cipher, DuelPacketKind.SyncReply, reply);
        }

        // 열린 Hello 를 적어 둔다. 자리가 이 사람의 것이 아니면 false(그 Hello 는 무시한다)
        public bool AcceptHello(int bodyLength)
        {
            if (HelloPacket.TryRead(new ReadOnlySpan<byte>(_body, 0, bodyLength), out HelloPacket hello) != DuelCodecError.None
                || hello.Slot.Value != Slot.Value)
            {
                return false;
            }
            HelloSeen = true;
            ClientBuild = hello.ClientBuild;
            return true;
        }

        public int WriteHelloAck(IAeadCipher cipher, in HelloAckPacket ack, Span<byte> reply)
        {
            _writer.Reset();
            ack.WriteTo(_writer);
            return Seal(cipher, DuelPacketKind.HelloAck, reply);
        }

        private int Seal(IAeadCipher cipher, DuelPacketKind kind, Span<byte> reply)
        {
            var header = new DuelPacketHeader(kind, TokenId, _nextOut);
            int n = DuelPacketFrame.Seal(cipher, _key, DuelDirection.ServerToClient, in header, _writer.Written, reply, out _);
            _nextOut++;   // 실패해도 번호는 다시 쓰지 않는다
            return n;
        }
    }
}

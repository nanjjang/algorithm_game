using System;
using Wizard.Core;
using Wizard.Netcode.Clock;
using Wizard.Netcode.Duel;
using Wizard.Protocol;
using Wizard.Protocol.Duel;

namespace Wizard.Client.Duel
{
    // 결투 연결 절차의 클라이언트 쪽(CONTRACTS 10.4·10.5, spec 10 14.4, ARCHITECTURE 6.4): SyncRequest/SyncReply 를 ClockSyncSamples(데이터 값)번
    // 주고받아 시계 오프셋을 정하고(ClockSync.Estimate, 왕복이 가장 짧은 표본) → Hello → HelloAck(서버가 정한 D, 경기 시계의 기준점).
    // 소켓도 시계도 모른다: 시각(클라이언트 단조 시계 µs)과 패킷은 인자로만 받는다.
    //
    // 키와 번호(T-62). 키는 DuelSession 이 DuelClientCore 와 같이 쓰는 DuelClientKeys 하나이고, 보내는 번호는 keys.SendCounter.Take()에서만
    // 받는다. 그래서 같은 키로 보내는 SyncRequest·Hello·ClientInput 의 (키, packet_number)는 서로 다르다(10.1).
    // 받는 쪽: SyncReply 와 HelloAck 만 이 클래스의 재전송 창(S→C)으로 연다. ServerTick 은 DuelClientCore 가 자기 창으로 연다
    // (종류는 AAD 에 들어 있어 바꿔 칠 수 없으므로 한 번호는 한 창만 지난다, T-62 4번).
    //
    // 다시 보내기: 답이 retryUs 안에 오지 않으면 같은 내용을 새 번호로 다시 보낸다(같은 번호로 다시 봉인하지 않는다).
    // retryUs 는 명세에 없는 값이라 부르는 쪽이 준다(DuelSessionOptions.HandshakeRetryUs).
    public sealed class DuelConnector
    {
        private readonly DuelClientKeys _keys;
        private readonly IAeadCipher _cipher;
        private readonly PlayerSlot _me;
        private readonly uint _clientBuild;
        private readonly long _retryUs;
        private readonly ClockSample[] _samples;
        private readonly long[] _outstandingT0;      // 답을 기다리는 SyncRequest 의 t0(다시 보낸 것 포함). 0 = 빈 칸이 아니라 쓰지 않은 칸이다
        private readonly bool[] _outstandingUsed;
        private readonly ByteWriter _writer = new ByteWriter(DuelWire.MaxBodyBytes);
        private readonly byte[] _body = new byte[DuelWire.MaxBodyBytes];
        private DuelReplayWindow _in;

        private int _sampleCount;
        private int _outstandingNext;
        private bool _awaiting;
        private long _lastSentUs;
        private uint _lastServerSeq;
        private long _lastReplyUs;
        private ClockOffset _offset;
        private HelloAckPacket _ack;

        public DuelConnector(DuelClientKeys keys, IAeadCipher cipher, PlayerSlot me, uint clientBuild, int syncSamples, long retryUs)
        {
            if (!keys.IsValid)
            {
                throw new ArgumentException("missing duel key (default DuelClientKeys)", nameof(keys));
            }
            if (me.Value >= DuelWire.DuelSlots)
            {
                throw new ArgumentOutOfRangeException(nameof(me), "a duel has slots 0 and 1");
            }
            if (syncSamples < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(syncSamples));
            }
            if (retryUs < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(retryUs));
            }
            _keys = keys;
            _cipher = cipher ?? throw new ArgumentNullException(nameof(cipher));
            _me = me;
            _clientBuild = clientBuild;
            _retryUs = retryUs;
            _samples = new ClockSample[syncSamples];
            _outstandingT0 = new long[syncSamples];
            _outstandingUsed = new bool[syncSamples];
        }

        public DuelConnectState State { get; private set; }

        public int SamplesCollected => _sampleCount;

        // Ready 이후의 값. 서버 시각 = 클라이언트 시각 + Offset.OffsetUs
        public ClockOffset Offset => _offset;

        public int InputDelayTicks => _ack.InputDelaySelf;

        public int OpponentInputDelayTicks => _ack.InputDelayOpponent;

        public long MatchEpochServerUs => unchecked((long)_ack.MatchEpochServerUs);

        public uint StartTick => _ack.StartTick;

        // 서버가 잰 왕복(표시와 기록용, 10.4). 65,535 = 서버가 표본을 갖지 못했다
        public int ServerMeasuredRttMs => _ack.RttMsMeasured;

        // 지금 보낼 인사 패킷 하나. 없으면 0. buffer 가 모자라면 0 이고 번호를 쓰지 않는다
        public int Poll(Span<byte> buffer, long nowUs)
        {
            if (State == DuelConnectState.Ready)
            {
                return 0;
            }
            if (_awaiting && nowUs - _lastSentUs < _retryUs)
            {
                return 0;
            }
            _writer.Reset();
            DuelPacketKind kind;
            if (State == DuelConnectState.Syncing)
            {
                kind = DuelPacketKind.SyncRequest;
                uint hold = 0U;
                if (_lastServerSeq != 0U)
                {
                    long h = nowUs - _lastReplyUs;
                    hold = h <= 0L ? 0U : h >= uint.MaxValue ? uint.MaxValue : (uint)h;
                }
                new SyncRequestPacket(unchecked((ulong)nowUs), _lastServerSeq, hold).WriteTo(_writer);
            }
            else
            {
                kind = DuelPacketKind.Hello;
                if (new HelloPacket(_me, _clientBuild).TryWrite(_writer) != DuelCodecError.None)
                {
                    return 0;
                }
            }
            if (buffer.Length < DuelPacketFrame.SealedLength(_writer.Written.Length))
            {
                return 0;   // 번호를 받기 전에 본다(T-62 3번)
            }
            var header = new DuelPacketHeader(kind, _keys.TokenId, _keys.SendCounter.Take());
            int n = DuelPacketFrame.Seal(_cipher, _keys.Key, DuelDirection.ClientToServer, in header, _writer.Written, buffer, out DuelCodecError error);
            if (error != DuelCodecError.None)
            {
                return 0;   // 받은 번호는 버린다(빈 번호는 받는 쪽 창이 탓하지 않는다)
            }
            if (kind == DuelPacketKind.SyncRequest)
            {
                _outstandingT0[_outstandingNext] = nowUs;
                _outstandingUsed[_outstandingNext] = true;
                _outstandingNext = (_outstandingNext + 1) % _outstandingT0.Length;
            }
            _awaiting = true;
            _lastSentUs = nowUs;
            return n;
        }

        // SyncReply 나 HelloAck 를 연다. 열어서 썼으면 true. 다른 종류(ServerTick 포함), 다른 사람의 token_id, 재전송, 태그 틀림이면 false
        public bool OnPacket(ReadOnlySpan<byte> packet, long nowUs)
        {
            if (DuelPacketFrame.TryPeekHeader(packet, out DuelPacketHeader header) != DuelCodecError.None
                || (header.Kind != DuelPacketKind.SyncReply && header.Kind != DuelPacketKind.HelloAck)
                || header.TokenId != _keys.TokenId || !_in.IsFresh(header.PacketNumber))
            {
                return false;
            }
            if (!DuelPacketFrame.TryOpen(_cipher, _keys.Key, DuelDirection.ServerToClient, packet, _body, out _, out int bodyLength, out _))
            {
                return false;
            }
            _in.Mark(header.PacketNumber);
            var body = new ReadOnlySpan<byte>(_body, 0, bodyLength);
            if (header.Kind == DuelPacketKind.SyncReply)
            {
                if (SyncReplyPacket.TryRead(body, out SyncReplyPacket reply) == DuelCodecError.None)
                {
                    OnSyncReply(in reply, nowUs);
                }
                return true;
            }
            if (HelloAckPacket.TryRead(body, out HelloAckPacket ack) == DuelCodecError.None && State != DuelConnectState.Syncing)
            {
                if (State == DuelConnectState.Greeting)
                {
                    _ack = ack;
                    State = DuelConnectState.Ready;
                    _awaiting = false;
                }
                // Ready 뒤에 다시 온 HelloAck(앞의 Hello 를 다시 보냈다)는 같은 값이다. 처음 받은 값을 지킨다
            }
            return true;
        }

        private void OnSyncReply(in SyncReplyPacket reply, long nowUs)
        {
            _lastServerSeq = reply.ServerSeq;
            _lastReplyUs = nowUs;
            if (State != DuelConnectState.Syncing)
            {
                return;
            }
            long t0 = unchecked((long)reply.T0Us);
            int slot = -1;
            for (int i = 0; i < _outstandingT0.Length; i++)
            {
                if (_outstandingUsed[i] && _outstandingT0[i] == t0)
                {
                    slot = i;
                    break;
                }
            }
            if (slot < 0)
            {
                return;   // 내가 보낸 적 없는(또는 이미 쓴) t0. 표본으로 쓰지 않는다
            }
            _outstandingUsed[slot] = false;
            _samples[_sampleCount++] = new ClockSample(t0, unchecked((long)reply.ServerTimeUs), nowUs);
            _awaiting = false;
            if (_sampleCount < _samples.Length)
            {
                return;
            }
            _offset = ClockSync.Estimate(_samples);
            if (!_offset.IsValid)
            {
                _sampleCount = 0;   // 쓸 표본이 없었다(시계가 거꾸로 갔다). 처음부터 다시 잰다
                return;
            }
            State = DuelConnectState.Greeting;
        }
    }
}

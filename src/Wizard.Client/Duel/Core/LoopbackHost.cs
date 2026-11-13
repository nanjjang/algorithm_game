using System;
using Wizard.Content;
using Wizard.Content.Rules;
using Wizard.Core;
using Wizard.Netcode.Clock;
using Wizard.Netcode.Duel;
using Wizard.Protocol;
using Wizard.Protocol.Duel;
using Wizard.Sim;

namespace Wizard.Client.Duel
{
    // ARCHITECTURE 6.4 "한 기기 2인과 봇 연습은 LoopbackTransport 로 기기 안의 DuelHostCore 에 붙는다. 서버 경기와 코드 경로가 같다",
    // spec 10 14.10·19의 4, CONTRACTS 6.3. 기기 안의 서버 하나: DuelHostCore + 연결 절차의 답(LoopbackHandshake) + 사람마다 두 방향의 줄.
    //
    // 시계. 이 호스트의 서버 시계는 기기의 단조 시계(µs)다. SyncReply.server_time_us 와 HelloAck.match_epoch_server_us 는 그 시계의 값이고,
    // DuelHostCore 에는 경기 시계(틱 0 = 0µs, 서버 시각 − match_epoch)를 준다.
    //
    // 한 번의 Pump(now): 닿은 패킷(닿는 시각 ≤ now)을 닿은 순서로 연다 → (경기가 정해졌으면) Advance(now − epoch) → 두 사람의 보낼 패킷을 다 꺼내
    // 내려가는 줄에 넣는다. DuelHostCore 의 계약(수신 버퍼를 다 비운 뒤 Advance, OnPacket 의 시각 = 기기에 닿은 시각)을 그대로 지킨다.
    // OnPacket 의 시각은 Pump 를 부른 때가 아니라 그 패킷이 닿은 시각(보낸 때의 호스트 시각 + 올라가는 지연)이다(wizard-duel 의 커널 수신 시각과 같다).
    //
    // 인위 지연(디버그 값): 사람마다 올라가는(C→S)·내려가는(S→C) 편도 µs. 기본 0.
    //
    // 스레드 하나에서만 쓴다(두 세션과 호스트가 같은 프레임 루프에서 돈다).
    public sealed class LoopbackHost
    {
        private const int Slots = DuelWire.DuelSlots;

        private readonly DuelHostCore _core;
        private readonly IAeadCipher _cipher;
        private readonly NetRuleSet _rules;
        private readonly LoopbackHostOptions _options;
        private readonly LoopbackHandshake[] _handshakes;
        private readonly LoopbackPacketQueue[] _up = { new LoopbackPacketQueue(), new LoopbackPacketQueue() };
        private readonly LoopbackPacketQueue[] _down = { new LoopbackPacketQueue(), new LoopbackPacketQueue() };
        private readonly long[] _upUs = new long[Slots];
        private readonly long[] _downUs = new long[Slots];
        private readonly LoopbackTransport?[] _transports = new LoopbackTransport?[Slots];
        private readonly int[] _inputDelay = new int[Slots];
        private readonly byte[] _packet = new byte[DuelWire.MaxPacketBytes];
        private readonly byte[] _reply = new byte[DuelWire.MaxPacketBytes];

        private long _nowUs;
        private bool _hasNow;
        private bool _started;
        private long _matchEpochUs;
        private ILoopbackTap? _tap;

        public LoopbackHost(ContentDb db, DuelSetup setup, DuelHostKeys keys, IAeadCipher cipher, LoopbackHostOptions options)
        {
            if (db == null)
            {
                throw new ArgumentNullException(nameof(db));
            }
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _options.Validate();
            _cipher = cipher ?? throw new ArgumentNullException(nameof(cipher));
            _rules = db.Net;
            _core = new DuelHostCore(db, setup, keys, cipher);   // 키 검사(빈 키, 같은 token_id)는 여기서 한다
            _handshakes = new[]
            {
                new LoopbackHandshake(new PlayerSlot(0), keys.Slot0, _rules),
                new LoopbackHandshake(new PlayerSlot(1), keys.Slot1, _rules),
            };
        }

        public DuelHostCore Core => _core;

        // 두 사람의 Hello 가 다 와서 경기 시계가 정해졌다
        public bool IsStarted => _started;

        // 경기 시계의 틱 0 이 시작하는 서버(기기) 시각(µs). IsStarted 전에는 0
        public long MatchEpochUs => _matchEpochUs;

        // 마지막 Pump 의 시각(µs). 보낸 패킷의 닿는 시각을 여기서 잰다
        public long NowUs => _nowUs;

        public void SetTap(ILoopbackTap? tap) => _tap = tap;

        // 이 사람의 D(HelloAck 로 내려 준 값). IsStarted 전에는 0
        public int InputDelayTicks(PlayerSlot slot) => _inputDelay[Index(slot)];

        // 연결 절차의 왕복 표본으로 잰 이 사람의 왕복(ms, 올림). 표본이 없으면 −1. D 를 정하는 값이다(경기 중의 값은 Core.MeasuredRttMs)
        public int HandshakeRttMs(PlayerSlot slot) => _handshakes[Index(slot)].MeasuredRttMs;

        // 사람마다 하나. 두 번 부르면 InvalidOperationException(한 자리에 전송 둘이 붙으면 패킷이 갈린다)
        public LoopbackTransport Connect(PlayerSlot slot)
        {
            int i = Index(slot);
            if (_transports[i] != null)
            {
                throw new InvalidOperationException("this slot already has a loopback transport");
            }
            var t = new LoopbackTransport(this, i);
            _transports[i] = t;
            return t;
        }

        // 인위 지연(디버그 값). 편도 µs, 0 이상. 바꾼 뒤에 보내는 패킷부터 적용된다
        public void SetLatency(PlayerSlot slot, long upUs, long downUs)
        {
            if (upUs < 0 || downUs < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(upUs), "latency must not be negative");
            }
            int i = Index(slot);
            _upUs[i] = upUs;
            _downUs[i] = downUs;
        }

        // 시각을 앞으로 옮기고(되돌아가지 않는다) 닿은 것을 처리한다. 프레임마다 세션들보다 먼저, 그리고 세션들 뒤에 한 번 더 불러도 된다
        public void Pump(long nowUs)
        {
            if (!_hasNow || nowUs > _nowUs)
            {
                _nowUs = nowUs;
                _hasNow = true;
            }
            for (int s = 0; s < Slots; s++)
            {
                DeliverUp(s);
            }
            if (!_started)
            {
                return;
            }
            long matchUs = _nowUs - _matchEpochUs;
            _tap?.OnHostAdvance(matchUs);
            _core.Advance(matchUs);
            for (int s = 0; s < Slots; s++)
            {
                var slot = new PlayerSlot((byte)s);
                int n;
                while ((n = _core.PollOutgoing(slot, _packet, out ulong to)) > 0)
                {
                    var p = new ReadOnlySpan<byte>(_packet, 0, n);
                    _tap?.OnHostSend(slot, p, to);
                    int target = AddressSlot(to);
                    if (target >= 0)
                    {
                        _down[target].Enqueue(p, _nowUs + _downUs[target]);
                    }
                }
            }
        }

        // ── 전송이 부른다

        internal void FromClient(int slot, ReadOnlySpan<byte> packet)
        {
            _up[slot].Enqueue(packet, _nowUs + _upUs[slot]);
        }

        internal int ToClient(int slot, Span<byte> buffer)
        {
            while (true)
            {
                int n = _down[slot].TryDequeue(_nowUs, buffer, out _);
                if (n >= 0)
                {
                    return n;
                }
                // 버퍼가 모자라 받지 못한 패킷은 버려졌다(UDP 와 같다). 다음 것을 본다
            }
        }

        internal void Detach(int slot) => _transports[slot] = null;

        // ── 내부

        private void DeliverUp(int slot)
        {
            int n;
            while ((n = _up[slot].TryDequeue(_nowUs, _packet, out long arrivalUs)) != 0)
            {
                if (n < 0)
                {
                    continue;
                }
                var p = new ReadOnlySpan<byte>(_packet, 0, n);
                if (DuelPacketFrame.TryPeekHeader(p, out DuelPacketHeader header) != DuelCodecError.None)
                {
                    continue;   // 겉 형식이 아니다. 열어 보지 않고 버린다(10.1)
                }
                if (header.Kind == DuelPacketKind.SyncRequest || header.Kind == DuelPacketKind.Hello)
                {
                    OnHandshake(slot, p, arrivalUs);
                    continue;
                }
                if (!_started)
                {
                    continue;   // 경기 시계가 없다. 인사 전의 경기 패킷은 버린다(정직한 클라이언트는 HelloAck 전에 보내지 않는다)
                }
                long receiveMatchUs = arrivalUs - _matchEpochUs;
                ulong from = AddressOf(slot);
                _tap?.OnHostPacket(p, receiveMatchUs, from);
                _core.OnPacket(p, receiveMatchUs, from);
            }
        }

        // 연결 절차의 답. 패킷의 token_id 가 이 줄의 사람 것이 아니면 버린다(DuelHostCore 와 같이 다른 사람의 키로 열지 않는다)
        private void OnHandshake(int slot, ReadOnlySpan<byte> packet, long arrivalUs)
        {
            LoopbackHandshake h = _handshakes[slot];
            if (!h.TryOpen(_cipher, packet, out DuelPacketKind kind, out int bodyLength))
            {
                return;
            }
            if (kind == DuelPacketKind.SyncRequest)
            {
                int n = h.ReplyToSync(_cipher, bodyLength, arrivalUs, _nowUs, _reply);
                if (n > 0)
                {
                    _down[slot].Enqueue(new ReadOnlySpan<byte>(_reply, 0, n), _nowUs + _downUs[slot]);
                }
                return;
            }
            if (!h.AcceptHello(bodyLength))
            {
                return;
            }
            if (!_started)
            {
                if (!_handshakes[0].HelloSeen || !_handshakes[1].HelloSeen)
                {
                    return;   // 상대를 기다린다. 클라이언트는 HelloAck 가 올 때까지 Hello 를 다시 보낸다
                }
                Start();
                for (int s = 0; s < Slots; s++)
                {
                    SendHelloAck(s);
                }
                return;
            }
            SendHelloAck(slot);   // 다시 온 Hello(앞의 HelloAck 가 사라졌다): 같은 값을 다시 준다
        }

        // D 는 서버가 자기 표본으로 정한다(10.5). 시뮬레이션에는 넣지 않고 호스트의 이른 시각 검사와 WZDR 머리말에만 간다
        private void Start()
        {
            for (int s = 0; s < Slots; s++)
            {
                int d = ClockSync.ChooseInputDelay(_handshakes[s].MeasuredRttMs, _rules);
                _inputDelay[s] = d;
                _core.SetInputDelay(new PlayerSlot((byte)s), d);
            }
            _matchEpochUs = _nowUs + _options.StartLeadUs;
            _started = true;
        }

        private void SendHelloAck(int slot)
        {
            int rtt = _handshakes[slot].MeasuredRttMs;
            ushort rttField = rtt < 0 || rtt > ushort.MaxValue ? ushort.MaxValue : (ushort)rtt;   // [임시] 모르면 65,535
            var ack = new HelloAckPacket(0U, unchecked((ulong)_matchEpochUs), (byte)_inputDelay[slot], (byte)_inputDelay[1 - slot], rttField);
            int n = _handshakes[slot].WriteHelloAck(_cipher, in ack, _reply);
            if (n > 0)
            {
                _down[slot].Enqueue(new ReadOnlySpan<byte>(_reply, 0, n), _nowUs + _downUs[slot]);
            }
        }

        // 기기 안의 응답 주소: 슬롯 + 1(0 은 "주소 없음"과 헷갈리지 않게). DuelHostCore 에는 불투명한 값이다
        private static ulong AddressOf(int slot) => (ulong)slot + 1UL;

        private static int AddressSlot(ulong address) => address >= 1UL && address <= Slots ? (int)(address - 1UL) : -1;

        private static int Index(PlayerSlot slot)
        {
            if (slot.Value >= Slots)
            {
                throw new ArgumentOutOfRangeException(nameof(slot), "duel slots are 0 and 1");
            }
            return slot.Value;
        }
    }
}

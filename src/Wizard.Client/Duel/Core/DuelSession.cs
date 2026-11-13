using System;
using Wizard.Content;
using Wizard.Core;
using Wizard.Netcode.Clock;
using Wizard.Netcode.Duel;
using Wizard.Protocol;
using Wizard.Protocol.Duel;
using Wizard.Sim;
using Wizard.Sim.Events;
using Wizard.Sim.Input;

namespace Wizard.Client.Duel
{
    // ARCHITECTURE 6.1·6.4, CONTRACTS 6.3·6.4, spec 10 14.10·19. 결투 한 사람의 세션: 연결 절차(DuelConnector) + 넷코드 코어(DuelClientCore)
    // + 입력 시계(DuelInputClock) + 전송(IDuelTransport). 전송이 Loopback 이든 UDP 든 웹소켓이든 이 클래스는 같다.
    //
    // 프레임마다(FixedTickDriver):
    //   OnInput(source, now):  Drain → Quantize(입력 시계, D) → OnLocalInput(순번은 코어가 매긴다) → 곧바로 PollOutgoing → Send.
    //                          키 이벤트는 틱 루프를 기다리지 않는다(ARCHITECTURE 6.1). 결투 중에는 프레임과 따로 더 자주 불러도 된다.
    //   Step(source, now):     로컬 입력(OnInput) → 받은 패킷 전부(ServerTick 은 코어, 나머지는 연결 절차) → Advance(입력 시계의 틱)
    //                          → 그 틱의 패킷 Send → 표시 이벤트 꺼내기(Events). 예측·되감기와 따라잡기는 코어 안에서 한다.
    //
    // 시각. now(초)는 RawKeyEvent 의 하드웨어 시각과 같은 단조 시계다. 코어와 연결 절차에는 그 시각의 µs(내림)를 준다:
    // OnPacket(닿은 시각)과 PollOutgoing(보내는 시각)이 같은 시계를 본다(T-62 6번).
    //
    // 키와 번호(T-62). duel.setup 하나에 DuelClientKeys 를 한 번만 만들어 이 세션 하나에 준다. 연결 절차와 코어가 같은 값을 받으므로
    // 번호는 그 안의 SendCounter 한 자리에서만 나온다.
    //
    // 연결 중과 틱 0 전에 꺼낸 키는 세계에 넣지 않는다(입력 시계가 없거나, 경기 시계가 아직 0 전이라 서버가 이른 시각으로 버린다).
    // 그 키들로 셈한 논리 키 상태는 기억해 두었다가, 입력 시계가 틱 0 에 든 뒤 처음 부르는 OnInput 에서 "눌림"으로 낸다
    // (마지막으로 버린 키의 시각에. 틱 0 전의 시각은 양자화기가 틱 0 + D, 오프셋 0 으로 묶는다).
    // 그 뒤로 어긋남은 코어의 보정 이벤트(물리 키 상태 = IInputSource.CurrentKeys)가 맞춘다.
    public sealed class DuelSession
    {
        private const double MicrosPerSecond = 1000000.0;   // 단위 환산
        private const int RawChunk = 32;                    // 자료구조 크기
        private const int EventChunk = 64;                  // 자료구조 크기

        private readonly DuelClientCore _core;
        private readonly DuelConnector _connector;
        private readonly DuelInputClock _clock;
        private readonly IDuelTransport _transport;
        private readonly int _resyncTicks;
        private readonly InputQuantizer _quantizer = new InputQuantizer();
        private readonly RawKeyEvent[] _raw = new RawKeyEvent[RawChunk];
        private readonly QuantizedKey[] _quantized = new QuantizedKey[RawChunk];
        private readonly byte[] _packet = new byte[DuelWire.MaxPacketBytes];
        private readonly ClockSample[] _samples;
        private readonly PresentationEvent[] _chunk = new PresentationEvent[EventChunk];
        private PresentationEvent[] _events = new PresentationEvent[EventChunk];
        private int _eventCount;

        private int _connectingMask;           // 연결 중·틱 0 전에 넣지 않은 키로 셈한 논리 키 상태
        private double _connectingLastSeconds = double.NegativeInfinity;
        private bool _startKeysPending;
        private long _nextResyncTick;
        private DuelAdvance _lastAdvance;

        public DuelSession(ContentDb db, DuelSetup setup, PlayerSlot me, DuelClientKeys keys, IAeadCipher cipher, IDuelTransport transport,
                           DuelSessionOptions options)
        {
            if (db == null)
            {
                throw new ArgumentNullException(nameof(db));
            }
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }
            options.Validate();
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _core = new DuelClientCore(db, setup, me, keys, cipher);
            _connector = new DuelConnector(keys, cipher, me, options.ClientBuild, db.Net.ClockSyncSamples, options.HandshakeRetryUs);
            _clock = new DuelInputClock(db.Net);
            _resyncTicks = db.Net.ClockResyncTicks;
            _samples = new ClockSample[db.Net.ClockSyncSamples];
            Me = me;
        }

        public PlayerSlot Me { get; }

        public DuelSessionPhase Phase { get; private set; }

        public DuelTransportKind TransportKind => _transport.Kind;

        public DuelConnector Connector => _connector;

        public DuelInputClock Clock => _clock;

        // 예측 세계(표시 계층이 읽는다)
        public ISimView View => _core.View;

        // 마지막 Step 에서 나온 표시 이벤트(Predicted, Confirmed, Retracted). 다음 Step 이 지운다
        public ReadOnlySpan<PresentationEvent> Events => new ReadOnlySpan<PresentationEvent>(_events, 0, _eventCount);

        public DuelAdvance LastAdvance => _lastAdvance;

        // HelloAck 로 받은 D. 연결 중에는 0
        public int InputDelayTicks => Phase == DuelSessionPhase.Running ? _connector.InputDelayTicks : 0;

        public int MarginMsEstimate => _core.MarginMsEstimate;

        public bool NeedsFullState => _core.NeedsFullState;

        public int HashMismatchCount => _core.HashMismatchCount;

        public bool HashMismatchFlagged => _core.HashMismatchFlagged;

        public ulong ConfirmedHash(uint tick) => _core.ConfirmedHash(tick);

        // 입력 시계의 지금 틱. 연결 중이거나 틱 0 전이면 음수
        public long InputClockTick(double nowSeconds) => Phase == DuelSessionPhase.Running ? _clock.TickAt(nowSeconds) : -1;

        // 상태 전체(duel.full_state. 기기 안 경기는 LoopbackHost.Core.TryWriteFullState). tick = 그 상태의 SimWorld.Tick
        public void OnFullState(uint tick, ReadOnlySpan<byte> state) => _core.OnFullState(tick, state);

        // 프레임마다 Drain 직후. 키 이벤트를 양자화해 순번을 매기고 곧바로 보낸다
        public void OnInput(IInputSource source, double nowSeconds)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }
            long nowUs = ToMicros(nowSeconds);
            bool running = Phase == DuelSessionPhase.Running;
            // 틱 0 전(카운트다운 시작 전의 대기)에는 키를 세계에 넣지 않는다. 그때 보낸 이벤트는 서버가 "이른 시각"으로 버린다(10.6):
            // 유효 틱은 0 + D 로 묶이는데 닿는 시각은 경기 시계의 음수다
            bool live = running && _clock.TickAt(nowSeconds) >= 0;
            if (live && _startKeysPending)
            {
                EmitStartKeys(source.CurrentKeys);
            }
            while (true)
            {
                int n = source.Drain(_raw);
                if (n < 0 || n > _raw.Length)
                {
                    throw new InvalidOperationException("IInputSource.Drain returned a count outside the buffer");
                }
                if (n > 0)
                {
                    if (live)
                    {
                        InputClock clock = _clock.InputClock;
                        int m = _quantizer.Quantize(new ReadOnlySpan<RawKeyEvent>(_raw, 0, n), in clock, _connector.InputDelayTicks, _quantized);
                        _core.OnLocalInput(new ReadOnlySpan<QuantizedKey>(_quantized, 0, m), source.CurrentKeys);
                    }
                    else
                    {
                        RememberWhileConnecting(new ReadOnlySpan<RawKeyEvent>(_raw, 0, n));
                    }
                }
                if (n < _raw.Length)
                {
                    break;
                }
            }
            if (running)
            {
                SendGamePacket(nowUs);
            }
        }

        // 프레임 하나: 로컬 입력 → 패킷 → Advance → 이벤트 꺼내기
        public void Step(IInputSource source, double nowSeconds)
        {
            OnInput(source, nowSeconds);
            long nowUs = ToMicros(nowSeconds);
            _eventCount = 0;
            ReceiveAll(nowUs);
            if (Phase != DuelSessionPhase.Running)
            {
                int n = _connector.Poll(_packet, nowUs);
                if (n > 0)
                {
                    _transport.Send(new ReadOnlySpan<byte>(_packet, 0, n));
                }
                return;
            }
            long tick = _clock.TickAt(nowSeconds);
            if (tick >= 0)
            {
                Resync(nowSeconds, tick);
                _lastAdvance = _core.Advance(tick > uint.MaxValue ? uint.MaxValue : (uint)tick);
            }
            SendGamePacket(nowUs);
            DrainEvents();
        }

        // ── 내부

        private void ReceiveAll(long nowUs)
        {
            int n;
            while ((n = _transport.Receive(_packet)) > 0)
            {
                var p = new ReadOnlySpan<byte>(_packet, 0, n);
                if (DuelPacketFrame.TryPeekHeader(p, out DuelPacketHeader header) != DuelCodecError.None)
                {
                    continue;
                }
                if (header.Kind == DuelPacketKind.ServerTick)
                {
                    if (Phase == DuelSessionPhase.Running)
                    {
                        _core.OnPacket(p, nowUs);
                    }
                    continue;
                }
                if (_connector.OnPacket(p, nowUs) && Phase == DuelSessionPhase.Connecting && _connector.State == DuelConnectState.Ready)
                {
                    BeginRunning();
                }
            }
        }

        private void BeginRunning()
        {
            _core.SetInputDelay(_connector.InputDelayTicks);
            ClockOffset offset = _connector.Offset;
            _clock.Start(_connector.MatchEpochServerUs, in offset);
            _nextResyncTick = _resyncTicks;
            _startKeysPending = true;
            Phase = DuelSessionPhase.Running;
        }

        private void SendGamePacket(long nowUs)
        {
            int n = _core.PollOutgoing(_packet, nowUs);
            if (n > 0)
            {
                _transport.Send(new ReadOnlySpan<byte>(_packet, 0, n));
            }
        }

        // 경기 중 시계 다시 재기(spec 10 14.4, CONTRACTS 10.4): 표본은 늘 모으고 ClockResyncTicks 마다 틱 길이를 맞춘다
        private void Resync(double nowSeconds, long tick)
        {
            int n;
            while ((n = _core.DrainClockSamples(_samples)) > 0)
            {
                for (int i = 0; i < n; i++)
                {
                    _clock.AddMatchSample(in _samples[i]);
                }
                if (n < _samples.Length)
                {
                    break;
                }
            }
            if (tick >= _nextResyncTick && _clock.Resync(nowSeconds, out _))
            {
                _nextResyncTick = tick + _resyncTicks;
            }
        }

        private void DrainEvents()
        {
            int n;
            while ((n = _core.DrainEvents(_chunk)) > 0)
            {
                if (_eventCount + n > _events.Length)
                {
                    Array.Resize(ref _events, Math.Max(_events.Length * 2, _eventCount + n));
                }
                Array.Copy(_chunk, 0, _events, _eventCount, n);
                _eventCount += n;
                if (n < _chunk.Length)
                {
                    break;
                }
            }
        }

        private void RememberWhileConnecting(ReadOnlySpan<RawKeyEvent> raw)
        {
            for (int i = 0; i < raw.Length; i++)
            {
                int bit = 1 << (int)raw[i].Key;
                _connectingMask = raw[i].Down ? _connectingMask | bit : _connectingMask & ~bit;
                if (raw[i].TimeSeconds > _connectingLastSeconds)
                {
                    _connectingLastSeconds = raw[i].TimeSeconds;
                }
            }
        }

        // 연결 중·틱 0 전에 눌린 채 남은 키를 틱 0 에 든 뒤 처음으로 낸다. 지금 떼어져 있는 키는 내지 않는다(물리 키 상태와 맞춘다)
        private void EmitStartKeys(KeyMask physicalNow)
        {
            _startKeysPending = false;
            int held = _connectingMask & (int)physicalNow;
            _connectingMask = 0;
            if (held == 0)
            {
                return;
            }
            int n = 0;
            for (int k = 0; k < KeyState.KeyCount; k++)
            {
                if ((held & (1 << k)) != 0)
                {
                    _raw[n++] = new RawKeyEvent((LogicalKey)k, true, _connectingLastSeconds);
                }
            }
            InputClock clock = _clock.InputClock;
            int m = _quantizer.Quantize(new ReadOnlySpan<RawKeyEvent>(_raw, 0, n), in clock, _connector.InputDelayTicks, _quantized);
            _core.OnLocalInput(new ReadOnlySpan<QuantizedKey>(_quantized, 0, m), physicalNow);
        }

        private static long ToMicros(double seconds) => (long)Math.Floor(seconds * MicrosPerSecond);
    }
}

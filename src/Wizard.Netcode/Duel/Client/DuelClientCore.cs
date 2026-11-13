using System;
using Wizard.Content;
using Wizard.Content.Rules;
using Wizard.Core;
using Wizard.Netcode.Clock;
using Wizard.Protocol;
using Wizard.Protocol.Duel;
using Wizard.Sim;
using Wizard.Sim.Events;
using Wizard.Sim.Input;

namespace Wizard.Netcode.Duel
{
    // CONTRACTS 6.4, 10.2, 10.3, spec 10 14.6·14.7·14.9, online 4.11-6·4.11-7. 결투 클라이언트의 넷코드 코어.
    // 소켓도 시계도 모른다(T-19): 시각과 패킷은 인자로만 받는다. 같은 호출 열이면 같은 출력 열이다.
    //
    // 세계 둘.
    //   확정 세계: 서버가 확정한 입력만으로 나아간다. 해시는 여기서 계산한다(확정 틱마다 남겨 서버 해시와 견준다).
    //   예측 세계: 확정 세계의 스냅숏에서 출발해 (내 입력 + 상대의 "직전 키 상태 유지" = 새 이벤트 없음)으로 입력 시계까지 간다.
    //   확정 열이 예측 세계가 그 틱에 넣은 입력과 다르면(내 이벤트가 버려진 것도 다름이다) 다음 Advance 에서 확정 세계를
    //   예측 세계에 복사하고(SaveSnapshot → LoadSnapshot, 스냅숏 버퍼 1개) 예측 세계가 가 있던 틱까지 다시 돌린다.
    //
    // 시계 둘.
    //   입력 시계(Advance 의 인자): 멈추지 않는다. 키 이벤트의 유효 틱과 패킷의 sealed_through_tick(= n + D − 1)의 기준.
    //   예측 틱: 예측 세계가 틱 p 를 처리할 수 있는 것은 p − (마지막 확정 틱) ≤ RollbackLimitTicks 일 때뿐이다(spec 10 14.7의 3).
    //   한도에 걸리면 Advance 는 Stalled 를 돌려주고 예측을 멈춘다. 전체 상태를 청하지 않는다. 그동안에도 키 이벤트는 입력 시계로
    //   찍혀 곧바로 나간다. 멈춤이 풀리면 한 번의 Advance 에서 CatchupTicksPerAdvance 까지 여러 틱을 돌려 따라잡는다.
    //
    // 틱의 표기. DuelAdvance.PredictedTick·ConfirmedTick 은 그 세계의 SimWorld.Tick(다음에 처리할 틱 = 처리한 틱 수)이다.
    //   "마지막 확정 틱"은 ConfirmedTick − 1 이다. ServerTick 의 확정 틱 T 와 ConfirmedHash(T)의 T 는 처리한 틱이다
    //   (서버의 sim.Step(T) 뒤의 해시, spec 10 14.6).
    //
    // 순번은 여기서 매긴다(CONTRACTS 3.1). OnLocalInput 의 QuantizedKey.Tick 은 이미 유효 틱이다(InputQuantizer 가 D 를 더한다, 6.5절).
    // 한 틱의 아홉 번째 키부터는 번호를 매기기 전에 버린다. 시각은 직전 이벤트보다 앞서지 않게 올린다(한 패킷 안의 역행은
    // 패킷 거부이고 몰수 계수다, 10.6 (0). 입력 시계의 ±0.5% 보정으로 양자화 결과가 살짝 뒤로 갈 수 있다).
    // 번호 전에 버린 키(시각을 올려 찬 틱에 걸린 키 포함)도 버려진 내 입력이다: 같은 호출 끝에서 보정 이벤트로 맞춘다.
    //
    // 버려진 내 입력. 내 이벤트 가운데 유효 틱이 확정됐는데 확정 열에 없는 것은 버려진 것이다(ServerTick 의 dropped 목록은 이유일 뿐).
    // 그때 (확정 세계의 내 KeyMask + 아직 확정 안 된 내 이벤트) 와 물리 KeyMask 의 차이를 새 순번의 보정 이벤트로 낸다.
    // 보정 이벤트의 시각은 입력 시계의 지금 틱 + D 이고 직전 내 이벤트보다 늦게 올린다. 버려진 원래 시각을 되살리지 않는다.
    //
    // 전체 상태는 둘일 때만 청한다(NeedsFullState): 서버 해시가 내 확정 해시와 다를 때, 서버가 보낸 확정의 첫 틱이 내 확정 틱보다
    // FullStateGapTicks 넘게 앞설 때. 청하는 동안 확정 세계는 멈춘다(틀린 세계에서 Confirmed 를 내지 않는다).
    //
    // 키와 패킷 번호(T-62). 키는 DuelClientKeys 로 받고, 보내는 패킷의 번호는 그 안의 SendCounter 에서만 받는다. 같은 키로 Hello 와
    // SyncRequest 를 보내는 연결 절차(DuelSession)가 같은 자리에서 번호를 받으므로 같은 (키, nonce)가 두 번 쓰이지 않는다(10.1).
    // 받는 쪽은 ServerTick 만 연다. 다른 종류(HelloAck, SyncReply)는 건드리지 않고 돌려보낸다(연결 절차가 자기 창으로 연다).
    //
    // 시각(T-62). OnPacket 과 PollOutgoing 의 clientTimeUs 는 같은 클라이언트 단조 시계다. 여기서는 두 값의 차(echo_hold_ms)와
    // 보낸 시각의 표기(client_send_time_ms), 경기 중 시계 표본(DrainClockSamples)에만 쓴다. 틱과 예측은 이 시각을 보지 않는다
    // (입력 시계의 틱은 Advance 의 인자다).
    //
    // 해시 불일치의 표시(T-62 8번, spec 10 2.8 hash_mismatch_flag·14.7). 한도는 데이터(NetRuleSet.HashMismatchFlagOver)이고
    // HashMismatchCount 가 그 값을 넘으면(한도 + 1번째) HashMismatchFlagged 가 선다. 확정 공백으로 청한 전체 상태는 세지 않는다.
    public sealed class DuelClientCore
    {
        private const int DuelPlayers = 2;
        private const int DuelKeyBits = (1 << ((int)LogicalKey.Reverse + 1)) - 1;   // 결투 키 Up~Reverse. Pause 는 결투에 없다(CONTRACTS 3.1)
        private const long UsPerMs = 1000L;                   // 단위 환산 상수
        private const int InitialOwnEvents = 256;             // 자료구조 크기

        private readonly NetRuleSet _net;
        private readonly PlayerSlot _me;
        private readonly IAeadCipher _cipher;
        private readonly DuelClientKeys _keys;
        private readonly SimWorld _confirmed;
        private readonly SimWorld _predicted;
        private readonly SimSnapshot _snapshot;
        private readonly int _rollbackLimit;
        private readonly int _catchupMax;
        private readonly int _gapLimit;
        private readonly int _hashMismatchFlagOver;

        // 입력
        private int _inputDelay;
        private uint _inputClock;
        private bool _clockStarted;
        private SeqCounter _seq;
        private KeyMask _physical;
        private bool _physicalKnown;
        private readonly OwnEventLog _own = new OwnEventLog(InitialOwnEvents);
        private uint _sealedThrough;
        private bool _needCorrection;

        // 재접속(DuelResume): 전체 상태가 오기 전에는 서버가 아는 키 상태를 확정 키 상태로 본다(CONTRACTS 10.1)
        private bool _resumeMaskActive;
        private KeyMask _resumeMask;
        private uint _resumeNextTick;

        // 확정과 예측
        private bool _needsFullState;
        private int _hashMismatchCount;
        private bool _mispredicted;
        private uint _confirmedEmitFrom;                      // 이 틱보다 이른 확정 틱은 이미 Confirmed 를 냈다
        private readonly PredictedInputLog _predictedInputs;
        private readonly ConfirmedHashRing _hashes = new ConfirmedHashRing();
        private bool _hasServerHash;
        private uint _serverHashTick;
        private ulong _serverHash;
        private bool _hasComparedHash;
        private uint _lastComparedHashTick;
        private readonly PresentationLedger _ledger;
        private readonly StepEventBuffer _sink = new StepEventBuffer();
        private readonly PlayerInput[] _stepInputs = new PlayerInput[InputOrder.MaxEventsPerTickPerPlayer * DuelPlayers];

        // 통신
        private bool _packetDue;
        private DuelReplayWindow _replay;
        private bool _hasEcho;
        private uint _echoServerTimeMs;
        private long _echoReceiveTimeUs;
        private short _marginMs;
        private readonly ClockSampleLog _clockSamples;
        private readonly ClientInputPacket _outPacket = new ClientInputPacket();
        private readonly ServerTickPacket _inPacket = new ServerTickPacket();
        private readonly ByteWriter _body = new ByteWriter(DuelWire.MaxBodyBytes);
        private readonly byte[] _opened = new byte[DuelWire.MaxBodyBytes];

        public DuelClientCore(ContentDb db, DuelSetup setup, PlayerSlot me, DuelClientKeys keys, IAeadCipher cipher, DuelResume? resume = null)
        {
            if (db == null)
            {
                throw new ArgumentNullException(nameof(db));
            }
            if (setup == null)
            {
                throw new ArgumentNullException(nameof(setup));
            }
            if (me.Value >= DuelPlayers)
            {
                throw new ArgumentOutOfRangeException(nameof(me), "a duel has slots 0 and 1");
            }
            if (!keys.IsValid)
            {
                throw new ArgumentException("missing duel key (default DuelClientKeys)", nameof(keys));
            }
            _cipher = cipher ?? throw new ArgumentNullException(nameof(cipher));
            _keys = keys;
            _me = me;
            _net = db.Net;
            _rollbackLimit = _net.RollbackLimitTicks;
            _catchupMax = _net.CatchupTicksPerAdvance;
            _gapLimit = _net.FullStateGapTicks;
            _hashMismatchFlagOver = _net.HashMismatchFlagOver;
            _clockSamples = new ClockSampleLog(_net.ClockSyncSamples);

            SimConfig config = SimConfig.ForDuel(db, setup.Options);
            _confirmed = SimWorld.CreateDuel(db, config, setup);
            _predicted = SimWorld.CreateDuel(db, config, setup);
            _snapshot = _confirmed.CreateSnapshotBuffer();
            // 기록이 있는 틱은 [확정 틱, 확정 틱 + 한도) 안이다. 한 칸 더 둔다
            _predictedInputs = new PredictedInputLog(_rollbackLimit + 1, _stepInputs.Length);
            _ledger = new PresentationLedger(_rollbackLimit + 1);

            if (resume != null)
            {
                _seq.Last = resume.NextSeq > 0U ? resume.NextSeq - 1U : 0U;
                _resumeMaskActive = true;
                _resumeMask = (KeyMask)resume.KeyState;
                _resumeNextTick = resume.ConfirmedTick + 1U;
                _needCorrection = true;
            }
        }

        // HelloAck 로 받은 D(10.4). 시뮬레이션은 D 를 모른다. 유효 틱과 봉인(n + D − 1), 보정 이벤트의 시각에만 쓴다
        public void SetInputDelay(int delayTicks)
        {
            if (delayTicks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(delayTicks));
            }
            _inputDelay = delayTicks;
        }

        // 순번을 매겨 내 이벤트 열에 넣고, 곧바로 PollOutgoing 에 그 이벤트가 든 패킷이 생긴다(다음 틱을 기다리지 않는다, 10.2).
        public void OnLocalInput(ReadOnlySpan<QuantizedKey> keys, KeyMask physicalKeysNow)
        {
            _physical = physicalKeysNow;
            _physicalKnown = true;
            for (int i = 0; i < keys.Length; i++)
            {
                QuantizedKey k = keys[i];
                if (!DuelWire.IsDuelKey(k.Key))
                {
                    continue;
                }
                long time = k.Time;
                long last = _own.LastTime;
                if (time < last)
                {
                    time = last;
                }
                AddOwnEvent(time, k.Key, k.Down);
            }
            if (_needCorrection)
            {
                EmitCorrections();
            }
        }

        public void OnPacket(ReadOnlySpan<byte> packet, long clientTimeUs)
        {
            if (DuelPacketFrame.TryPeekHeader(packet, out DuelPacketHeader header) != DuelCodecError.None
                || header.Kind != DuelPacketKind.ServerTick || !_replay.IsFresh(header.PacketNumber))
            {
                return;
            }
            if (!DuelPacketFrame.TryOpen(_cipher, _keys.Key, DuelDirection.ServerToClient, packet, _opened, out _, out int bodyLength, out _))
            {
                return;
            }
            _replay.Mark(header.PacketNumber);
            if (_inPacket.TryRead(new ReadOnlySpan<byte>(_opened, 0, bodyLength)) != DuelCodecError.None)
            {
                return;
            }
            _hasEcho = true;
            _echoServerTimeMs = _inPacket.ServerTimeMs;
            _echoReceiveTimeUs = clientTimeUs;
            _marginMs = _inPacket.MarginMsEstimate;
            _clockSamples.OnServerTick(_inPacket.EchoClientSendTimeMs, _inPacket.ServerTimeMs, clientTimeUs);

            ApplyConfirmedTicks();
            if (_inPacket.HasHash)
            {
                OnServerHash(_inPacket.HashTick, _inPacket.StateHash);
            }
            if (_needCorrection)
            {
                EmitCorrections();
            }
        }

        // 서버가 보낸 상태 전체(해시 불일치·확정 공백 복구, 재접속). tick = 그 상태의 SimWorld.Tick(다음에 처리할 틱).
        // 상태를 먼저 예측 세계에 읽어 확인한 뒤 확정 세계로 옮긴다: 깨진 상태가 확정 세계를 망가뜨리지 않는다.
        public void OnFullState(uint tick, ReadOnlySpan<byte> state)
        {
            _mispredicted = true;   // 예측 세계는 어느 쪽이든 확정 세계에서 다시 만든다
            try
            {
                _predicted.ReadState(state);
            }
            catch (SimInvariantException)
            {
                return;
            }
            if (_predicted.Tick != tick)
            {
                return;
            }
            _predicted.SaveSnapshot(_snapshot);
            _confirmed.LoadSnapshot(_snapshot);

            _needsFullState = false;
            _hasServerHash = false;
            _hashes.Clear();
            if (tick > 0U)
            {
                _hashes.Put(tick - 1U, _confirmed.ComputeStateHash());
            }
            if (_confirmedEmitFrom < tick)
            {
                _confirmedEmitFrom = tick;
            }
            _predictedInputs.Clear();
            _ledger.ForgetBefore(tick);
            for (int i = 0; i < _own.Count; i++)
            {
                if (_own.StateAt(i) == OwnEventState.Pending && _own.At(i).Tick < tick)
                {
                    _own.SetState(i, OwnEventState.Superseded);
                }
            }
            _resumeMaskActive = false;
            _needCorrection = true;
            EmitCorrections();
        }

        // 예측을 입력 시계까지 끌고 간다. 입력 시계가 새 틱에 들어서면 그 틱의 패킷이 생긴다(10.2 "매 틱 1개")
        public DuelAdvance Advance(uint inputClockTick)
        {
            if (!_clockStarted || inputClockTick > _inputClock)
            {
                _inputClock = inputClockTick;
                _clockStarted = true;
                _packetDue = true;
            }
            UpdateSealed();

            int depth = 0;
            if (_mispredicted || _predicted.Tick < _confirmed.Tick)
            {
                // 예측 세계가 가 있던 곳까지 다시 돌린다. 한도를 넘는 곳(받지 않은 전체 상태를 읽다 만 세계)까지는 가지 않는다
                long limit = (long)_confirmed.Tick + _rollbackLimit;
                uint reach = _predicted.Tick < limit ? _predicted.Tick : (uint)limit;
                _confirmed.SaveSnapshot(_snapshot);
                _predicted.LoadSnapshot(_snapshot);
                _mispredicted = false;
                while (_predicted.Tick < reach && !_predicted.IsOver)
                {
                    StepPredicted();
                    depth++;
                }
            }

            int simulated = 0;
            bool stalled = false;
            while (_predicted.Tick <= _inputClock && !_predicted.IsOver)
            {
                if ((long)_predicted.Tick >= (long)_confirmed.Tick + _rollbackLimit)
                {
                    stalled = true;
                    break;
                }
                if (simulated >= _catchupMax)
                {
                    break;
                }
                StepPredicted();
                simulated++;
            }
            _own.Prune(Math.Min(_confirmed.Tick, ClientInputWindow.FirstTick(_sealedThrough)));
            return new DuelAdvance(stalled, simulated, depth, _predicted.Tick, _confirmed.Tick);
        }

        // 보낼 ClientInput 하나. 없으면 0. buffer 가 모자라면 0 이고 패킷은 다음 호출로 남는다(패킷 번호를 쓰지 않는다).
        // clientTimeUs = 이 패킷을 내보내는 시각(OnPacket 과 같은 시계). 서버의 왕복 표본은 도착 − 서버가 보낸 시각 − echo_hold 이므로
        // (10.5) hold 를 실제보다 크게 적으면 왕복이 작게 잡혀 D 가 모자라게 정해진다. 그래서 틱으로 어림하지 않고 두 시각의 차를 내림한다
        public int PollOutgoing(Span<byte> buffer, long clientTimeUs)
        {
            if (!_packetDue)
            {
                return 0;
            }
            UpdateSealed();
            _outPacket.Clear();
            _outPacket.SealedThroughTick = _sealedThrough;
            // 아직 하나도 받지 못했어도 0 이다(틱 0만 받은 것과 같은 값). 서버는 0 이면 틱 0부터 싣는다(T-62)
            _outPacket.AckConfirmedTick = _confirmed.Tick > 0U ? _confirmed.Tick - 1U : 0U;
            _outPacket.ClientSendTimeMs = unchecked((uint)(clientTimeUs / UsPerMs));   // u32 ms. 넘치면 아랫 32비트(받는 쪽은 그대로 돌려줄 뿐이다)
            if (_hasEcho)
            {
                _outPacket.EchoServerTimeMs = _echoServerTimeMs;
                long heldMs = (clientTimeUs - _echoReceiveTimeUs) / UsPerMs;
                _outPacket.EchoHoldMs = heldMs <= 0L ? (ushort)0 : heldMs > ushort.MaxValue ? ushort.MaxValue : (ushort)heldMs;
            }
            ClientInputWindow.Fill(_outPacket, _own.Events, _seq.Last);
            _body.Reset();
            if (_outPacket.TryWrite(_body) != DuelCodecError.None)
            {
                return 0;
            }
            if (buffer.Length < DuelPacketFrame.SealedLength(_body.Written.Length))
            {
                return 0;                                    // 번호를 받기 전에 본다. 모자란 버퍼로 번호를 태우지 않는다
            }
            var header = new DuelPacketHeader(DuelPacketKind.ClientInput, _keys.TokenId, _keys.SendCounter.Take());
            int n = DuelPacketFrame.Seal(_cipher, _keys.Key, DuelDirection.ClientToServer, header, _body.Written, buffer, out DuelCodecError error);
            if (error != DuelCodecError.None)
            {
                return 0;                                    // 받은 번호는 버린다. 다시 쓰지 않는다(빈 번호는 받는 쪽 창이 탓하지 않는다)
            }
            _packetDue = false;
            _clockSamples.OnSent(_outPacket.ClientSendTimeMs, clientTimeUs);   // 실제로 나간 패킷의 보낸 시각만 기억한다
            return n;
        }

        public ISimView View => _predicted.View;

        // 확정 틱 tick 을 처리한 뒤의 확정 세계 해시. 남아 있지 않은 틱(오래됐거나 아직 오지 않았다)이면 0
        public ulong ConfirmedHash(uint tick)
        {
            if (_confirmed.Tick > 0U && tick == _confirmed.Tick - 1U)
            {
                return _confirmed.ComputeStateHash();
            }
            return _hashes.TryGet(tick, out ulong hash) ? hash : 0UL;
        }

        public int DrainEvents(Span<PresentationEvent> buffer) => _ledger.Drain(buffer);

        public int MarginMsEstimate => _marginMs;

        public bool NeedsFullState => _needsFullState;

        // 이 경기에서 서버 해시와 어긋난 횟수(확정 공백으로 청한 전체 상태는 세지 않는다). spec 10 14.7 "3번 넘게 어긋난 클라이언트는
        // 기록해 표시한다"의 셈이다
        public int HashMismatchCount => _hashMismatchCount;

        // HashMismatchCount > NetRuleSet.HashMismatchFlagOver(spec 10 2.8 hash_mismatch_flag, T-62 8번). 한도는 팩에서 온다
        public bool HashMismatchFlagged => _hashMismatchCount > _hashMismatchFlagOver;

        // 경기 중 시계 표본(CONTRACTS 10.4, T-62 9번). 받은 ServerTick 하나에 많아야 하나. 가장 오래된 것부터 buffer 에 옮기고
        // 옮긴 수를 돌려준다. ServerUs 는 경기 시계(HelloAck.match_epoch_server_us 부터 센 서버 시각)의 µs 이고, 서버 시계로 옮기는 것
        // (+ match_epoch_server_us)은 입력 시계를 모는 쪽이 한다. 꺼내지 않고 ClockSyncSamples 개를 넘으면 가장 오래된 것부터 버린다
        public int DrainClockSamples(Span<ClockSample> buffer) => _clockSamples.Drain(buffer);

        internal uint LastAssignedSeq => _seq.Last;

        internal uint SealedThroughTick => _sealedThrough;

        // ── 내부

        private void AddOwnEvent(long time, LogicalKey key, bool down)
        {
            uint tick = (uint)(time / SimTime.OffsetsPerTick);
            if (_own.LiveCountAt(tick) >= InputOrder.MaxEventsPerTickPerPlayer)
            {
                // 아홉 번째부터는 번호를 매기기 전에 버린다(CONTRACTS 3.1). 버린 키만큼 서버가 볼 키 상태와 물리 키 상태가 어긋날 수
                // 있으니 보정을 표시한다(6.4 "버려진 내 입력", 버림 사유에 수량 포함). 표시하지 않으면 사용자가 그 키를 다시 누르고
                // 뗄 때까지 어긋난 채 남는다. EmitCorrections 는 찬 틱이면 다음 틱 처음으로 올린다
                _needCorrection = true;
                return;
            }
            var e = new InputEvent(tick, (byte)(time % SimTime.OffsetsPerTick), key, down, _seq.Next());
            _own.Append(e);
            _packetDue = true;
            if (tick < _confirmed.Tick)
            {
                // 이미 확정된 틱이다. 서버는 봉인된 틱의 이벤트를 늦음으로 버린다(10.6). 확정 열이 정본이므로 여기서 버림으로 본다
                _own.SetState(_own.Count - 1, OwnEventState.Dropped);
                _needCorrection = true;
            }
            else if (tick < _predicted.Tick)
            {
                _mispredicted = true;                        // 이미 예측한 틱에 끼어든다. 다음 Advance 에서 다시 돌린다
            }
        }

        // 확정 키 상태(확정 세계의 내 KeyMask, 재접속 직후에는 서버가 아는 키 상태) + 아직 확정 안 된 내 이벤트 vs 물리 키 상태
        private void EmitCorrections()
        {
            if (!_physicalKnown || _needsFullState)
            {
                return;                                      // 물리 키를 모르거나 확정 세계가 틀렸다. 알게 된 뒤에 맞춘다
            }
            int diff = ((int)ProjectedServerKeys() ^ (int)_physical) & DuelKeyBits;
            _needCorrection = false;
            if (diff == 0)
            {
                return;
            }
            long time = ((long)_inputClock + _inputDelay) * SimTime.OffsetsPerTick;
            long last = _own.LastTime;
            if (time <= last)
            {
                time = last + 1L;
            }
            // 같은 시각 안에서는 키 분류 순(자세·반전, 방향, J, K)으로 번호를 매긴다: 순번 순 = (틱, 오프셋, 키 분류) 순
            for (int keyClass = 0; keyClass <= InputOrder.KeyClass(LogicalKey.Skill); keyClass++)
            {
                for (int k = 0; k <= (int)LogicalKey.Reverse; k++)
                {
                    var key = (LogicalKey)k;
                    if ((diff & (1 << k)) == 0 || InputOrder.KeyClass(key) != keyClass)
                    {
                        continue;
                    }
                    uint tick = (uint)(time / SimTime.OffsetsPerTick);
                    if (_own.LiveCountAt(tick) >= InputOrder.MaxEventsPerTickPerPlayer)
                    {
                        time = ((long)tick + 1L) * SimTime.OffsetsPerTick;   // 그 틱이 찼다. 다음 틱 처음으로 올린다
                    }
                    AddOwnEvent(time, key, ((int)_physical & (1 << k)) != 0);
                }
            }
        }

        private KeyMask ProjectedServerKeys()
        {
            int mask;
            uint from;
            if (_resumeMaskActive && _confirmed.Tick < _resumeNextTick)
            {
                mask = (int)_resumeMask;
                from = _resumeNextTick;
            }
            else
            {
                mask = (int)_confirmed.View.Player(_me).Keys;
                from = _confirmed.Tick;
            }
            for (int i = 0; i < _own.Count; i++)
            {
                InputEvent e = _own.At(i);
                if (_own.StateAt(i) != OwnEventState.Pending || e.Tick < from)
                {
                    continue;
                }
                int bit = 1 << (int)e.Key;
                mask = e.Down ? mask | bit : mask & ~bit;    // 눌린 키의 누름, 안 눌린 키의 뗌은 바꾸지 않는다(KeyState 와 같다)
            }
            return (KeyMask)mask;
        }

        private void ApplyConfirmedTicks()
        {
            uint from = _inPacket.ConfirmedFromTick;
            for (int i = 0; i < _inPacket.ConfirmedCount; i++)
            {
                if (_needsFullState || _confirmed.IsOver)
                {
                    return;
                }
                uint tick = from + (uint)i;
                uint next = _confirmed.Tick;
                if (tick < next)
                {
                    continue;                                // 이미 받은 틱(ack 가 닿기 전에 다시 온 것)
                }
                if (tick > next)
                {
                    // 확정 공백. 서버는 ack 다음 틱부터 다시 보내므로 대개 다음 패킷이 메운다. 한도를 넘으면 전체 상태를 청한다
                    if ((long)tick - next > _gapLimit)
                    {
                        _needsFullState = true;
                    }
                    return;
                }
                ApplyConfirmedTick(tick, _inPacket.SlotEvents(i, 0), _inPacket.SlotEvents(i, 1));
            }
        }

        private void ApplyConfirmedTick(uint tick, ReadOnlySpan<InputEvent> slot0, ReadOnlySpan<InputEvent> slot1)
        {
            int n = 0;
            for (int i = 0; i < slot0.Length; i++)
            {
                _stepInputs[n++] = new PlayerInput(new PlayerSlot(0), slot0[i]);
            }
            for (int i = 0; i < slot1.Length; i++)
            {
                _stepInputs[n++] = new PlayerInput(new PlayerSlot(1), slot1[i]);
            }
            var inputs = new Span<PlayerInput>(_stepInputs, 0, n);
            InputOrder.Sort(inputs);

            ResolveOwn(tick, _me.Value == 0 ? slot0 : slot1);
            if (tick < _predicted.Tick && !_predictedInputs.Matches(tick, inputs))
            {
                _mispredicted = true;
            }
            _predictedInputs.Forget(tick);

            _sink.Begin();
            _confirmed.Step(inputs, ReadOnlySpan<RunCommand>.Empty, _sink);
            _ledger.OnConfirmedTick(tick, _sink.Events, tick >= _confirmedEmitFrom);
            if (tick >= _confirmedEmitFrom)
            {
                _confirmedEmitFrom = tick + 1U;
            }
            ulong hash = _confirmed.ComputeStateHash();
            _hashes.Put(tick, hash);
            if (_hasServerHash && _serverHashTick == tick)
            {
                _hasServerHash = false;
                CompareHash(tick, hash, _serverHash);
            }
            if (_resumeMaskActive && _confirmed.Tick >= _resumeNextTick)
            {
                _resumeMaskActive = false;
            }
        }

        // 그 틱의 내 이벤트를 확정 열과 맞춘다. 같은 순번에 같은 (시각, 키, 누름)이면 확정, 아니면 버림.
        // 확정 열에 내가 매기지 않은 순번(서버가 끊김 때 만든 뗌, 10.1)이 있으면 순번 공간을 그 뒤로 옮긴다
        private void ResolveOwn(uint tick, ReadOnlySpan<InputEvent> mine)
        {
            for (int k = 0; k < mine.Length; k++)
            {
                if (mine[k].Seq > _seq.Last)
                {
                    _seq.Last = mine[k].Seq;
                }
            }
            _own.RangeAt(tick, out int first, out int length);
            for (int i = first; i < first + length; i++)
            {
                if (_own.StateAt(i) != OwnEventState.Pending)
                {
                    continue;
                }
                InputEvent e = _own.At(i);
                bool found = false;
                for (int k = 0; k < mine.Length; k++)
                {
                    InputEvent c = mine[k];
                    if (c.Seq == e.Seq && c.Offset == e.Offset && c.Key == e.Key && c.Down == e.Down)
                    {
                        found = true;
                        break;
                    }
                }
                _own.SetState(i, found ? OwnEventState.Confirmed : OwnEventState.Dropped);
                if (!found)
                {
                    _needCorrection = true;
                }
            }
        }

        private void OnServerHash(uint tick, ulong serverHash)
        {
            if (_hasComparedHash && _lastComparedHashTick == tick)
            {
                return;                                      // 같은 틱의 해시가 다시 왔다. 한 번만 센다
            }
            if (tick < _confirmed.Tick)
            {
                if (_hashes.TryGet(tick, out ulong mine))
                {
                    CompareHash(tick, mine, serverHash);
                }
                return;
            }
            _hasServerHash = true;                           // 아직 그 틱에 닿지 않았다. 지날 때 견준다
            _serverHashTick = tick;
            _serverHash = serverHash;
        }

        private void CompareHash(uint tick, ulong mine, ulong serverHash)
        {
            _hasComparedHash = true;
            _lastComparedHashTick = tick;
            if (mine != serverHash)
            {
                _hashMismatchCount++;
                _needsFullState = true;
            }
        }

        private void StepPredicted()
        {
            uint tick = _predicted.Tick;
            int n = _own.CollectPending(tick, _me, _stepInputs);
            var inputs = new Span<PlayerInput>(_stepInputs, 0, n);
            InputOrder.Sort(inputs);
            _predictedInputs.Record(tick, inputs);
            _sink.Begin();
            _predicted.Step(inputs, ReadOnlySpan<RunCommand>.Empty, _sink);
            _ledger.OnPredictedTick(tick, _sink.Events);
        }

        // sealed_through_tick = n + D − 1(10.2). 줄지 않는다
        private void UpdateSealed()
        {
            long s = (long)_inputClock + _inputDelay - 1L;
            if (s > _sealedThrough)
            {
                _sealedThrough = (uint)s;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using Wizard.Content;
using Wizard.Content.Rules;
using Wizard.Core;
using Wizard.Protocol;
using Wizard.Protocol.Duel;
using Wizard.Sim;
using Wizard.Sim.Events;
using Wizard.Sim.Input;

namespace Wizard.Netcode.Duel
{
    // CONTRACTS 6.4·10절, spec 10 14.6·14.8, DECISIONS-TECH T-19·T-45·T-48. 결투의 서버 쪽: 봉인 → 검증 → 정렬 → SimWorld.Step → 확정,
    // 확정 입력과 30틱마다의 상태 해시를 ServerTick 으로 내보내고, 확정 열을 WZDR 로 남긴다.
    //
    // 소켓도 시계도 모른다. 시각은 인자로만 받는다(경기 시계 µs, 틱 0 = 0µs. HostTime). 같은 패킷 열과 같은 시각 열이면 같은 출력 열이 나온다.
    // 서버 프로세스(wizard-duel), Loopback(한 기기 2인), 테스트가 같은 클래스를 쓴다.
    //
    // 부르는 순서(호출자의 계약): 수신 버퍼를 다 비우며 OnPacket(패킷, 기기에 닿은 시각, 주소)을 부르고 → Advance(지금) → PollOutgoing.
    // OnPacket 의 시각은 큐에서 꺼낸 시각이 아니라 도착 시각이다. 그래서 서버의 GC·루프 지연은 늦음 판정에 섞이지 않는다.
    // 다만 봉인은 Advance 에서도 일어나므로(T + 1틱이 지나면 닫힌다), 받은 패킷을 OnPacket 에 넘기기 전에 Advance 를 부르면 그 사이의 틱이
    // 먼저 닫혀 그 패킷의 이벤트가 늦음이 된다. 버퍼를 다 비운 뒤에 Advance 를 부르는 것이 그것을 막는다.
    //
    // 계약 타입(DECISIONS-TECH T-62·T-64): 생성자는 DuelHostKeys(슬롯 0·1 의 DuelPeerKey)를 받고, StatsFor 는 DuelNetStats 를,
    // Status 는 DuelHostStatus(진행, 끝)를 돌려준다. Rekey·관전과 Status 의 나머지 칸(끝의 결과, 몰수, 끊김 타이머)은 수명주기·관전 작업
    // (M14-NET-01)의 몫이라 계약 꼴의 자리만 있다. Hello·Bye·SyncRequest 는 태그를 검증하고(재전송 창, 응답 주소) 내용은 보지 않는다.
    //
    // 확정 열의 순번(T-64): 확정 열은 InputOrder 순(틱, 오프셋, 키 분류, 슬롯, 순번)이고 순번은 받은 값 그대로다. 그래서 한 사람의 순번이
    // 확정 열에서 늘어나기만 하지는 않는다: 같은 (틱, 오프셋)의 K(순번 10)·앞(순번 11)은 앞 → K 로 확정되고(spec 10 TV-N20, 14.8 "틱 안 순서"),
    // 패킷 사이에서 시각을 되돌린 클라이언트의 이벤트는 뒤 틱에 더 작은 순번으로 실린다(10.6 은 한 패킷 안의 역행만 거절한다. 그런 이벤트도
    // 봉인·늦음·이른 시각 검사를 그대로 지나므로 얻는 것이 없다). ServerTick 과 WZDR 은 둘 다 그 열을 그대로 싣는다.
    public sealed class DuelHostCore
    {
        private const int Slots = DuelWire.DuelSlots;
        private const int MaxPerTick = InputOrder.MaxEventsPerTickPerPlayer;

        // spec 10 2.8 state_hash_interval [제안] "30틱마다". NetRuleSet 에 아직 칸이 없어 여기 둔다. 데이터로 옮기는 작업이 CCR-2 다
        // (DECISIONS-TECH T-64: NetRuleSet.StateHashIntervalTicks, data/rules/net.json 의 state_hash_interval_ticks). 그 작업이 이 상수를 지운다.
        internal const int StateHashIntervalTicks = 30;

        // spec 10 2.8 future_claim_tolerance [제안] "1틱"(= 256 입력 단위). 이른 시각 검사의 여유. CONTRACTS 10.6 의 "+ 256".
        // 이것도 CCR-2 가 NetRuleSet.FutureClaimToleranceOffsets 로 옮긴다(T-64)
        private const long FutureToleranceUnits = SimTime.OffsetsPerTick;

        // 자료구조 크기: 한 ServerTick 에 싣는 dropped 항목의 상한(64 × 7바이트). 본문 1,171바이트 안에 확정 틱 자리를 남긴다
        private const int MaxDropsPerPacket = 64;

        private readonly SimWorld _world;
        private readonly DuelSetup _setup;
        private readonly IAeadCipher _cipher;
        private readonly HostPeer[] _peers;
        private readonly ConfirmedTickRing _confirmed = new ConfirmedTickRing();
        private readonly List<PlayerInput> _record = new List<PlayerInput>();   // 확정 열 전부(WZDR). 확정 순서 = InputOrder 순
        private readonly PlayerInput[] _stepInputs = new PlayerInput[Slots * MaxPerTick];
        private readonly HostSink _sink = new HostSink();
        private readonly ClientInputPacket _in = new ClientInputPacket();
        private readonly ServerTickPacket _out = new ServerTickPacket();
        private readonly byte[] _body = new byte[DuelWire.MaxBodyBytes];
        private readonly ByteWriter _bodyWriter = new ByteWriter(DuelWire.MaxBodyBytes);

        private long _nowUs;                // Advance 가 받은 가장 늦은 시각
        private bool _advanced;
        private long _lastTicksStarted;
        private ReadOnlyMemory<byte> _recordCache;
        private uint _recordCacheTick;
        private int _recordCacheCount = -1;

        // keys = 슬롯 0·1 의 키(T-62·T-64). 채우지 않은 값(default)과 같은 token_id 는 ArgumentException 이다(빈 키로 조용히 돌지 않는다).
        // setup 은 경기의 시작 조건이고 봉투 값(CoreVersion, DataVersion)이 채워져 있어야 한다
        // (WZDR 머리말 = setup 의 정규 바이트. 담기지 않으면 여기서 InvalidOperationException, T-48)
        public DuelHostCore(ContentDb db, DuelSetup setup, DuelHostKeys keys, IAeadCipher cipher)
        {
            if (db == null)
            {
                throw new ArgumentNullException(nameof(db));
            }
            if (setup == null)
            {
                throw new ArgumentNullException(nameof(setup));
            }
            if (cipher == null)
            {
                throw new ArgumentNullException(nameof(cipher));
            }
            // default(DuelHostKeys)는 DuelHostKeys 의 생성자를 지나지 않았다. 여기서 한 번 더 본다
            if (!keys.Slot0.IsValid || !keys.Slot1.IsValid)
            {
                throw new ArgumentException("both duel keys must be set", nameof(keys));
            }
            if (keys.Slot0.TokenId == keys.Slot1.TokenId)
            {
                throw new ArgumentException("the two players must have different token_id", nameof(keys));
            }
            _ = new DuelRecordWriter(setup);   // 머리말을 쓸 수 있는 설정인지 지금 본다(경기 끝에 알게 되지 않게)
            NetRuleSet rules = db.Net;
            _setup = setup;
            _cipher = cipher;
            _world = SimWorld.CreateDuel(db, SimConfig.ForDuel(db, setup.Options), setup);
            _peers = new[]
            {
                new HostPeer(new PlayerSlot(0), keys.Slot0, rules),
                new HostPeer(new PlayerSlot(1), keys.Slot1, rules),
            };
        }

        // ── 받기

        // CONTRACTS 10.1 받는 쪽 순서: 머리 → token_id 로 사람 찾기(모르면 버린다, 열지 않는다, 답하지 않는다) → 재전송 창 → 태그 → 창 표시·응답 주소.
        // 태그를 통과하기 전에는 아무 상태도 바꾸지 않는다(통계 칸만 센다). 예외를 던지지 않는다(시각이 범위 밖일 때만 호출자의 결함으로 던진다)
        public void OnPacket(ReadOnlySpan<byte> packet, long receiveTimeUs, ulong fromAddress)
        {
            HostTime.Require(receiveTimeUs, nameof(receiveTimeUs));
            if (DuelPacketFrame.TryPeekHeader(packet, out DuelPacketHeader header) != DuelCodecError.None)
            {
                return;
            }
            HostPeer? peer = FindPeer(header.TokenId);
            if (peer == null || !IsClientKind(header.Kind))
            {
                return;   // 모르는 token_id, 또는 서버가 보내는 종류: 열어 보지 않는다
            }
            if (!peer.Window.IsFresh(header.PacketNumber))
            {
                peer.Stats.PacketsReplayed++;
                return;
            }
            if (!DuelPacketFrame.TryOpen(_cipher, peer.Key, DuelDirection.ClientToServer, packet, _body, out _, out int bodyLength, out _))
            {
                peer.Stats.PacketsUnauthentic++;
                return;
            }
            peer.Window.Mark(header.PacketNumber);
            peer.Address = fromAddress;
            peer.HasAddress = true;
            peer.Stats.PacketsVerified++;
            if (header.Kind == DuelPacketKind.ClientInput)
            {
                OnClientInput(peer, new ReadOnlySpan<byte>(_body, 0, bodyLength), receiveTimeUs);
            }
        }

        // CONTRACTS 10.6 의 순서 그대로(정본)
        private void OnClientInput(HostPeer p, ReadOnlySpan<byte> body, long receiveUs)
        {
            if (_in.TryRead(body) != DuelCodecError.None)
            {
                // 태그는 맞는데 10.2 형식으로 읽히지 않는다: 정직한 클라이언트의 부호기는 만들지 않는 바이트다(전송 중 깨짐은 태그가 잡는다).
                // 한 패킷 안의 순번 역행(spec 10 TV-N16, spec 01 T-I13 의 "7 다음 5")은 형식(Δseq 는 부호 없는 varuint)으로 적으면
                // 32비트를 넘어 도는 Δ 가 되어 여기로 온다. 그래서 (0)과 같이 이 패킷만 버리고 순서 위반으로 센다. 알릴 순번은 읽지 못했다
                p.Stats.PacketsMalformed++;
                p.Stats.OrderViolations++;
                return;
            }
            ReadOnlySpan<InputEvent> events = _in.Events;

            // (0) 패킷 구조: 한 패킷 안에서 seq 가 늘지 않거나 (tick, offset)이 역행하거나 base_seq ≥ 첫 seq 이면 조작이다.
            //     이 패킷만 버린다. 봉인도, 에코·ack 도 쓰지 않는다. 몰수는 이 수만 센다
            if (!IsWellOrdered(_in.BaseSeq, events))
            {
                p.Stats.OrderViolations++;
                uint notice = RejectedNoticeSeq(p.LastSeq, events);
                if (notice != 0U)
                {
                    p.Drops.AddRange(notice, notice, DuelDropReason.Order);
                }
                return;
            }

            OfferEcho(p, receiveUs);
            p.EchoClientSendTimeMs = _in.ClientSendTimeMs;

            // (1) 순번 맞추기: 창 밖으로 밀려난 이벤트는 영영 오지 않는다. 유실로 소비한다
            if (_in.BaseSeq > p.LastSeq)
            {
                MarkLost(p, p.LastSeq + 1U, _in.BaseSeq);
                p.LastSeq = _in.BaseSeq;
            }
            for (int i = 0; i < events.Length; i++)
            {
                InputEvent e = events[i];
                if (e.Seq <= p.LastSeq)
                {
                    continue;   // 겹쳐 보낸 것. 조용히 건너뛴다
                }
                if (e.Seq > p.LastSeq + 1U)
                {
                    MarkLost(p, p.LastSeq + 1U, e.Seq - 1U);
                }
                p.LastSeq = e.Seq;   // 아래에서 무엇이 되든 이 순번은 소비됐다
                Judge(p, in e, receiveUs);
            }

            // (2) 봉인
            if (_in.SealedThroughTick > p.SealedThrough)
            {
                p.SealedThrough = _in.SealedThroughTick;
            }

            // ack: 클라이언트가 받은 마지막 확정 틱. 0 은 "아직 없음"과 "틱 0 까지"가 같은 값이라 틱 0 을 한 번 더 보내는 쪽으로 읽는다
            long next = _in.AckConfirmedTick == 0U ? 0L : (long)_in.AckConfirmedTick + 1;
            if (next > _world.Tick)
            {
                next = _world.Tick;   // 받지도 않은 틱을 받았다고 하면 확정한 데까지만
            }
            if (next > p.NextUnackedTick)
            {
                p.NextUnackedTick = (uint)next;
            }

            // 마감 여유 추정(HUD, CONTRACTS 6.5 의 (D + 1 − f)틱 − 편도 − 전송 대기): sealed = n + D − 1 이므로 지금 누른 키의 마감은 틱 sealed + 2 의 시작
            p.MarginMs = HostTime.ToMarginMillis(HostTime.MicrosUntilTickStart((long)_in.SealedThroughTick + 2, receiveUs));
        }

        // 순번을 소비한 이벤트 하나의 판정(10.6 (1)의 for 몸통)
        private void Judge(HostPeer p, in InputEvent e, long receiveUs)
        {
            if (p.IsSealed(e.Tick) || HostTime.AtOrAfterTickEnd(receiveUs, e.Tick))
            {
                p.Stats.EventsLate++;
                p.Drops.AddRange(e.Seq, e.Seq, DuelDropReason.Late);
                return;   // 다른 틱으로 옮기지 않는다
            }
            long press = ((long)e.Tick - p.InputDelayTicks) * SimTime.OffsetsPerTick + e.Offset;
            long oneWay = p.Rtt.OneWayUs;
            if (oneWay < 0)
            {
                oneWay = 0;   // 표본이 없으면 편도를 0 으로 본다(가장 너그러운 쪽. 정직한 클라이언트를 버리지 않는다)
            }
            if (press > HostTime.InputTimeAt(receiveUs - oneWay) + FutureToleranceUnits || !PendingInputRing.Fits(e.Tick, _world.Tick))
            {
                // 두 번째 조건: 확정할 다음 틱에서 4.3초 넘게 앞선 틱. Advance 를 그만큼 부르지 않았을 때만 닿는다(정직한 이벤트는 이른 시각 검사 안이다)
                p.Stats.EventsEarly++;
                p.Drops.AddRange(e.Seq, e.Seq, DuelDropReason.Early);
                return;
            }
            int bit = 1 << (int)e.Key;
            bool held = (p.KeyDown & bit) != 0;
            if (e.Down == held)
            {
                p.Stats.EventsKeyStateIgnored++;   // 키 상태 모순. 무시(알리지 않는다)
                return;
            }
            if (p.Pending.Count(e.Tick) >= MaxPerTick)
            {
                p.Stats.EventsQuantity++;
                p.Drops.AddRange(e.Seq, e.Seq, DuelDropReason.Quantity);
                return;
            }
            p.Pending.Add(in e);
            p.KeyDown = (ushort)(p.KeyDown ^ bit);
            p.Stats.EventsAccepted++;
            long margin = HostTime.MicrosUntilTickStart((long)e.Tick + 1, receiveUs);
            if (p.Stats.ArrivalMarginCount == 0 || margin < p.Stats.ArrivalMarginMinUs)
            {
                p.Stats.ArrivalMarginMinUs = margin;
            }
            p.Stats.ArrivalMarginCount++;
            p.Stats.ArrivalMarginSumUs += margin;
        }

        private static bool IsWellOrdered(uint baseSeq, ReadOnlySpan<InputEvent> events)
        {
            if (events.Length == 0)
            {
                return true;
            }
            if (baseSeq >= events[0].Seq)
            {
                return false;
            }
            for (int i = 1; i < events.Length; i++)
            {
                if (events[i].Seq <= events[i - 1].Seq || events[i].Time < events[i - 1].Time)
                {
                    return false;
                }
            }
            return true;
        }

        // 버린 패킷(reason 4)을 알릴 순번: 아직 소비하지 않은 첫 순번(거부한 패킷은 순번을 소비하지 않는다. 이미 받은 순번을 "버렸다"고 알리지 않는다).
        // 새 순번이 없으면 첫 이벤트의 순번. 순번 0 은 알릴 수 없다(0 을 돌려준다)
        private static uint RejectedNoticeSeq(uint lastSeq, ReadOnlySpan<InputEvent> events)
        {
            for (int i = 0; i < events.Length; i++)
            {
                if (events[i].Seq > lastSeq)
                {
                    return events[i].Seq;
                }
            }
            return events.Length > 0 ? events[0].Seq : 0U;
        }

        private static void MarkLost(HostPeer p, uint first, uint last)
        {
            p.Stats.SeqsLost += (long)last - first + 1;
            p.Drops.AddRange(first, last, DuelDropReason.Lost);
        }

        // 서버의 왕복 표본(10.5): 도착 − 서버가 보낸 시각(에코) − hold. 서버가 보낸 적 없는 시각(보낸 최댓값보다 큰 에코)은 받지 않는다
        private void OfferEcho(HostPeer p, long receiveUs)
        {
            uint echo = _in.EchoServerTimeMs;
            if (echo == 0U || !p.SentServerTime || echo > p.LastSentServerTimeMs)
            {
                return;   // 0 = 아직 받은 서버 패킷 없음(10.2)
            }
            p.Rtt.Offer(receiveUs, HostTime.MillisToMicros(echo), HostTime.MillisToMicros(_in.EchoHoldMs));
        }

        // ── 진행

        // 봉인 → 정렬 → SimWorld.Step → 확정. 서버 시각이 T + 1틱에 닿은 틱 T 는 받은 것까지로 닫힌다("변화 없음"이면 빈 목록).
        // 두 사람이 다 봉인한 틱만 확정한다. 시각이 앞 호출보다 이르면 앞 시각을 쓴다(되돌아가지 않는다)
        public void Advance(long serverTimeUs)
        {
            HostTime.Require(serverTimeUs, nameof(serverTimeUs));
            if (!_advanced || serverTimeUs > _nowUs)
            {
                _nowUs = serverTimeUs;
            }
            long started = HostTime.TicksStartedBy(_nowUs);
            bool newTick = !_advanced || started != _lastTicksStarted;
            _advanced = true;
            _lastTicksStarted = started;

            long sealBy = started - 1;
            for (int s = 0; s < Slots; s++)
            {
                HostPeer p = _peers[s];
                if (sealBy > p.SealedThrough)
                {
                    p.Stats.TicksSealedByDeadline += sealBy - p.SealedThrough;
                    p.SealedThrough = sealBy;
                }
            }

            uint before = _world.Tick;
            ConfirmReady();
            bool confirmed = _world.Tick != before;
            for (int s = 0; s < Slots; s++)
            {
                HostPeer p = _peers[s];
                if (newTick || confirmed || p.Drops.Count > 0 || p.HashPending)
                {
                    p.SendDue = true;
                }
            }
        }

        private void ConfirmReady()
        {
            while (!_world.IsOver)
            {
                uint t = _world.Tick;
                if (!_peers[0].IsSealed(t) || !_peers[1].IsSealed(t))
                {
                    return;
                }
                int n = 0;
                for (int s = 0; s < Slots; s++)
                {
                    HostPeer p = _peers[s];
                    n += p.Pending.Take(t, p.Slot, new Span<PlayerInput>(_stepInputs, n, MaxPerTick));
                }
                var inputs = new Span<PlayerInput>(_stepInputs, 0, n);
                InputOrder.Sort(inputs);   // spec 10 14.2 정렬(T-12): (틱, 오프셋, 키 분류, 슬롯, 순번)
                _world.Step(inputs, ReadOnlySpan<RunCommand>.Empty, _sink);
                for (int i = 0; i < n; i++)
                {
                    _record.Add(inputs[i]);
                }
                _confirmed.Store(t, inputs);
                if (t % StateHashIntervalTicks == 0U)
                {
                    // 해시의 틱 T = 틱 T 를 Step 한 뒤의 상태(spec 10 14.6 의사코드: Step(next) 뒤 next % 30 == 0 이면 방송)
                    ulong hash = _world.ComputeStateHash();
                    for (int s = 0; s < Slots; s++)
                    {
                        _peers[s].HashPending = true;
                        _peers[s].HashTick = t;
                        _peers[s].HashValue = hash;
                    }
                }
            }
        }

        // ── 보내기

        // 보낼 패킷 하나를 buffer 에 쓴다. 없으면 0. toAddress = 그 사람의 태그 검증을 통과한 마지막 패킷의 출발 주소(그런 패킷이 없으면 보내지 않는다).
        // ServerTick 은 Advance 마다 많아야 하나: ack 받지 못한 첫 확정 틱부터 32틱, 해시(그 틱이 이 패킷까지로 클라이언트에 닿을 때), 버린 이벤트 알림.
        // 확정 틱 T 는 두 사람이 다 봉인한 뒤에만 생기므로, B 의 틱 T 입력은 A 의 틱 T 가 봉인된 뒤에만 A 에게 실린다(미리 보기 막기, 14.8).
        // buffer 가 모자라면 0 이고 아무것도 소비하지 않는다
        public int PollOutgoing(PlayerSlot to, Span<byte> buffer, out ulong toAddress)
        {
            HostPeer p = Peer(to);
            toAddress = 0UL;
            if (!p.HasAddress || !p.SendDue)
            {
                return 0;
            }
            _out.Clear();
            _out.ServerTimeMs = HostTime.ToWireMillis(_nowUs);
            _out.EchoClientSendTimeMs = p.EchoClientSendTimeMs;
            _out.MarginMsEstimate = p.MarginMs;

            int size = ServerTickPacket.FixedBytes;
            if (p.HashPending)
            {
                size += ServerTickPacket.HashBytes;   // 자리를 먼저 잡는다. 실을지는 틱을 채운 뒤 정한다
            }
            int drops = Math.Min(p.Drops.Count, MaxDropsPerPacket);
            for (int i = 0; i < drops; i++)
            {
                _out.TryAddDropped(p.Drops[i]);
            }
            if (drops > 0)
            {
                size += 1 + DroppedRun.WireBytes * drops;
            }

            uint from = p.NextUnackedTick;
            if (from < _confirmed.OldestTick)
            {
                from = _confirmed.OldestTick;   // 더 뒤처졌다: 남은 가장 오래된 틱부터(클라이언트는 공백으로 전체 상태를 청한다)
            }
            _out.ConfirmedFromTick = from;
            for (uint t = from; t < _confirmed.EndTick && _out.ConfirmedCount < DuelWire.MaxConfirmedTicks; t++)
            {
                ReadOnlySpan<InputEvent> e0 = _confirmed.Events(t, 0);
                ReadOnlySpan<InputEvent> e1 = _confirmed.Events(t, 1);
                int add = 2 + EventsBytes(e0) + EventsBytes(e1);
                if (size + add > DuelWire.MaxBodyBytes)
                {
                    break;
                }
                if (_out.TryAddConfirmedTick(e0, e1) != DuelCodecError.None)
                {
                    break;
                }
                size += add;
            }
            bool withHash = p.HashPending && p.HashTick < (long)from + _out.ConfirmedCount;
            if (withHash)
            {
                _out.HasHash = true;
                _out.HashTick = p.HashTick;
                _out.StateHash = p.HashValue;
            }

            _bodyWriter.Reset();
            if (_out.TryWrite(_bodyWriter) != DuelCodecError.None)
            {
                return 0;
            }
            var header = new DuelPacketHeader(DuelPacketKind.ServerTick, p.TokenId, p.NextOutPacketNumber);
            int n = DuelPacketFrame.Seal(_cipher, p.Key, DuelDirection.ServerToClient, in header, _bodyWriter.Written, buffer, out _);
            if (n == 0)
            {
                return 0;
            }
            p.NextOutPacketNumber++;
            p.Drops.RemoveFirst(drops);
            if (withHash)
            {
                p.HashPending = false;
            }
            p.SendDue = false;
            p.SentServerTime = true;
            if (_out.ServerTimeMs > p.LastSentServerTimeMs)
            {
                p.LastSentServerTimeMs = _out.ServerTimeMs;
            }
            toAddress = p.Address;
            return n;
        }

        private static int EventsBytes(ReadOnlySpan<InputEvent> events)
        {
            int size = 0;
            for (int i = 0; i < events.Length; i++)
            {
                size += 2 + VarUIntSize(events[i].Seq);
            }
            return size;
        }

        // ByteWriter.VarUInt 의 길이(7비트씩, LEB128). ServerTick 의 크기를 쓰기 전에 센다
        private static int VarUIntSize(uint v)
        {
            const int bitsPerByte = 7;
            int n = 1;
            while ((v >>= bitsPerByte) != 0U)
            {
                n++;
            }
            return n;
        }

        // ── 설정과 조회

        // 서버가 잰 왕복으로 정한 D(ClockSync.ChooseInputDelay). 이른 시각 검사에 쓰고 WZDR 의 input_delay 칸에 실린다. 시뮬레이션에는 들어가지 않는다
        public void SetInputDelay(PlayerSlot slot, int delayTicks)
        {
            if (delayTicks < 0 || delayTicks > byte.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(delayTicks));
            }
            Peer(slot).InputDelayTicks = delayTicks;
        }

        // 서버가 직접 쌓은 표본(최근 NetRuleSet.ClockSyncSamples 개)의 최솟값(ms, 올림). 표본이 없으면 −1
        public int MeasuredRttMs(PlayerSlot slot) => Peer(slot).Rtt.MinRttMs;

        // 마지막 확정 틱(아직 없으면 0), 다음 순번, 서버가 아는 키 상태(받아들인 이벤트로 본 것)
        public DuelResume ResumeInfo(PlayerSlot slot)
        {
            HostPeer p = Peer(slot);
            return new DuelResume
            {
                ConfirmedTick = _world.Tick == 0U ? 0U : _world.Tick - 1U,
                NextSeq = p.LastSeq + 1U,
                KeyState = p.KeyDown,
            };
        }

        // 확정 세계의 상태 전체(SimWorld.WriteState). tick = 그 상태가 다음에 처리할 틱
        public bool TryWriteFullState(ByteWriter target, out uint tick)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }
            tick = _world.Tick;
            _world.WriteState(target);
            return true;
        }

        // ── 수명주기·관전의 자리(CONTRACTS 6.4). 끊김·몰수·재접속과 관전 묶음은 M14-NET-01(Lifecycle/, Spectate/)이 채운다.
        //    그때까지는 아무것도 하지 않는 값을 돌려준다. 다만 Rekey 를 조용히 무시하면 옛 키로 계속 보내게 되므로(nonce 재사용의 길) 던진다

        // 진행과 끝(T-64): 다음에 확정할 틱(= 호스트 세계의 SimWorld.Tick)과 세계의 경기 끝.
        // 끝의 결과, 몰수(누가), 끊김 타이머의 칸은 M14-NET-01 이 DuelHostStatus 에 더한다
        public DuelHostStatus Status => new DuelHostStatus(_world.Tick, _world.IsOver);

        // 재접속: 새 token_id, 새 키, epoch + 1(10.1). 키 바꾸기는 끊김 타이머·순번 잇기·전체 상태와 한 묶음이라 M14-NET-01 이 채운다
        public void Rekey(PlayerSlot slot, DuelPeerKey key)
        {
            Peer(slot);
            throw new NotSupportedException("DuelHostCore.Rekey: reconnect is not implemented yet (M14-NET-01)");
        }

        // 120틱 전 시점의 상태(관전 시작용). 아직 보관하지 않으므로 늘 false(M14-NET-01)
        public bool TryWriteSpectatorStart(ByteWriter target, out uint tick)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }
            tick = 0U;
            return false;
        }

        // 120틱 늦은 확정 입력. 아직 없으므로 늘 0(M14-NET-01)
        public int PollSpectatorChunk(Span<byte> buffer) => 0;

        // 그 사람의 통계(경기 처음부터 센 값의 사본, spec 10 14.8). duel_match.net_stats 의 원천이다
        public DuelNetStats StatsFor(PlayerSlot slot)
        {
            HostPeer p = Peer(slot);
            DuelNetStats s = p.Stats;
            s.NoticesOverflowed = p.Drops.Overflowed;
            return s;
        }

        // WZDR(CONTRACTS 11.1, T-48). 지금까지의 확정 열을 DuelRecordWriter 로 쓴다: end_tick = 확정한 틱 수(세계의 Tick),
        // final_state_hash = 그때의 상태 해시, input_delay = SetInputDelay 로 받은 마지막 값. ReplayRunner.RunDuel 로 돌리면 같은 해시가 나온다.
        // 경기 중에 불러도 된다(그 시점까지의 기록). 확정이 늘지 않았으면 같은 바이트를 돌려준다
        public ReadOnlyMemory<byte> ConfirmedInputRecord()
        {
            if (_recordCacheCount == _record.Count && _recordCacheTick == _world.Tick)
            {
                return _recordCache;
            }
            var writer = new DuelRecordWriter(_setup);
            for (int s = 0; s < Slots; s++)
            {
                writer.SetInputDelay(_peers[s].Slot, _peers[s].InputDelayTicks);
            }
            for (int i = 0; i < _record.Count; i++)
            {
                writer.Append(_record[i]);
            }
            _recordCache = writer.Finish(_world.Tick, _world.ComputeStateHash());
            _recordCacheTick = _world.Tick;
            _recordCacheCount = _record.Count;
            return _recordCache;
        }

        // ── 내부

        private HostPeer Peer(PlayerSlot slot)
        {
            if (slot.Value >= Slots)
            {
                throw new ArgumentOutOfRangeException(nameof(slot), "duel slots are 0 and 1");
            }
            return _peers[slot.Value];
        }

        private HostPeer? FindPeer(uint tokenId)
        {
            for (int s = 0; s < Slots; s++)
            {
                if (_peers[s].TokenId == tokenId)
                {
                    return _peers[s];
                }
            }
            return null;
        }

        // 클라이언트가 보내는 종류(10.1): ClientInput, SyncRequest, Hello, Bye
        private static bool IsClientKind(DuelPacketKind kind) =>
            kind == DuelPacketKind.ClientInput || kind == DuelPacketKind.SyncRequest
            || kind == DuelPacketKind.Hello || kind == DuelPacketKind.Bye;

        // 호스트는 표시 이벤트를 쓰지 않는다(소리·연출은 클라이언트의 확정 세계가 낸다, 6.4절)
        private sealed class HostSink : ISimEventSink
        {
            public void Emit(in SimEvent e)
            {
            }
        }
    }
}

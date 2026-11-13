using System;
using System.Collections.Generic;
using Wizard.Core;
using Wizard.Netcode.Duel;
using Wizard.Netcode.Tests.Duel.Host;
using Wizard.Protocol.Duel;
using Wizard.Sim;
using Wizard.Sim.Input;
using Xunit;

namespace Wizard.Netcode.Tests.NetSim
{
    // 호스트 1(DuelHostCore) + 클라이언트 2(DuelClientCore, 슬롯 0·1)를 메모리 안에서 잇는 시험 경기.
    // 시각은 경기 시계 µs 하나다(틱 0 = 0µs). 클라이언트의 시계는 서버와 맞춰져 있다고 본다(시계 맞추기는 ClockSync 의 시험이 따로 잰다):
    // 입력 시계 틱 n 은 서버의 틱 n 과 같은 µs 에 시작한다.
    //
    // 일정(같은 시각이면 이 순서): 호스트 수신 → 클라이언트 수신 → 호스트 틱 → 클라이언트 틱 → 키 누름.
    //   호스트: 패킷이 닿으면 같은 시각에 닿은 것을 다 OnPacket 한 뒤 Advance(그 시각) → 두 사람에게 PollOutgoing(빌 때까지).
    //           틱이 시작할 때마다(T + 1틱이 지나면 봉인) 한 번 더 Advance. 서버 루프·GC 지연은 없다(늦음 판정은 도착 시각으로 한다, CONTRACTS 6.4).
    //   클라이언트: 틱 n 의 시작에 Advance(n) → 표시 이벤트를 비운다 → PollOutgoing. 키는 틱 안의 정한 위치에 OnLocalInput 하고 곧바로
    //           PollOutgoing 한다(키 이벤트가 생기면 즉시 한 패킷, 10.2). 패킷이 닿으면 OnPacket → (보정 이벤트가 생겼으면) PollOutgoing.
    // 링크는 방향·슬롯마다 따로이고(LinkProfile) 씨앗에서 갈라 낸 난수를 쓴다. 같은 씨앗이면 같은 바이트 열이다.
    internal sealed class NetSimMatch
    {
        private const int Slots = 2;
        private const int OffsetsPerTick = 256;           // 단위 환산(입력 시각 단위 = 1/256틱)
        private const int KindHostRecv = 0;
        private const int KindClientRecv = 1;
        private const int KindHostTick = 2;
        private const int KindClientTick = 3;
        private const int KindPress = 4;

        public readonly DuelHostCore Host;
        public readonly DuelClientCore[] Clients = new DuelClientCore[Slots];
        public readonly ClientTrace[] Trace = { new ClientTrace(), new ClientTrace() };
        public readonly NetLink[] Up = new NetLink[Slots];
        public readonly NetLink[] Down = new NetLink[Slots];
        public readonly List<byte[]> HostOutgoing = new List<byte[]>();
        public readonly long[] HostDeliveries = new long[Slots];                                  // 호스트에 닿은 패킷(중복 포함)
        public readonly HashSet<byte[]>[] HostDistinct = { new HashSet<byte[]>(ReferenceEqualityComparer.Instance), new HashSet<byte[]>(ReferenceEqualityComparer.Instance) };

        // 틱 n 에 누를 키(틱 안 위치, 키). null 이면 누르지 않는다. 같은 틱의 키는 위치가 달라야 한다(같으면 키 분류 순으로 넣는다)
        public Func<int, uint, NetRng, List<(byte offset, LogicalKey key)>?> Keys = (slot, n, rng) => null;

        private readonly Dictionary<byte[], uint> _sealedThroughOf = new Dictionary<byte[], uint>(ReferenceEqualityComparer.Instance);
        private readonly SortedSet<Ev> _queue = new SortedSet<Ev>(new EvOrder());
        private readonly NetRng[] _keyRng = new NetRng[Slots];
        private readonly long[] _arrivals = new long[2];
        private readonly byte[] _buf = new byte[DuelWire.MaxPacketBytes];
        private readonly byte[] _body = new byte[DuelWire.MaxBodyBytes];
        private readonly ClientInputPacket _decode = new ClientInputPacket();
        private readonly ServerTickPacket _serverTick = new ServerTickPacket();
        private readonly TestCipher _peekCipher = new TestCipher();
        private readonly ulong _worldSeed;
        private long _order;
        private uint _n;

        public NetSimMatch(ulong seed, LinkProfile[] up, LinkProfile[] down, int[]? delayOverride = null, ulong worldSeed = 0xD0E1_5EEDUL)
        {
            _worldSeed = worldSeed;
            Host = new DuelHostCore(HostKit.Db, HostKit.Setup(worldSeed), HostKit.Keys(), new TestCipher());
            for (int s = 0; s < Slots; s++)
            {
                Up[s] = new NetLink(up[s], NetRng.Derive(seed, 1UL, (ulong)s));
                Down[s] = new NetLink(down[s], NetRng.Derive(seed, 2UL, (ulong)s));
                _keyRng[s] = new NetRng(NetRng.Derive(seed, 3UL, (ulong)s));
                int rttMs = (int)((up[s].BaseUs + down[s].BaseUs) / 1000L);
                int d = delayOverride != null ? delayOverride[s] : Wizard.Netcode.Clock.ClockSync.ChooseInputDelay(rttMs, HostKit.Db.Net);
                Trace[s].Delay = d;
                var keys = new DuelClientKeys(s == 0 ? HostKit.Token0 : HostKit.Token1, 1U, HostKit.Key(s));
                Clients[s] = new DuelClientCore(HostKit.Db, HostKit.Setup(worldSeed), new PlayerSlot((byte)s), keys, new TestCipher());
                Clients[s].SetInputDelay(d);
                Host.SetInputDelay(new PlayerSlot((byte)s), d);
            }
        }

        public static NetSimMatch Symmetric(ulong seed, int rttMs, int jitterMs, int lossPpm, int[]? delayOverride = null)
        {
            long oneWay = rttMs * 1000L / 2;
            long jitter = jitterMs * 1000L;
            var up = new[] { LinkProfile.OneWay(oneWay, jitter, lossPpm), LinkProfile.OneWay(oneWay, jitter, lossPpm) };
            var down = new[] { LinkProfile.OneWay(oneWay, jitter, lossPpm), LinkProfile.OneWay(oneWay, jitter, lossPpm) };
            return new NetSimMatch(seed, up, down, delayOverride);
        }

        public uint InputClock => _n;

        public ulong WorldSeed => _worldSeed;

        // 무작위 키: 틱마다 1/ratePerTick 의 확률로 1~2개의 키를 누르거나 뗀다(Pause 제외). 위치는 틱 안 균등
        public static List<(byte, LogicalKey)>? RandomKeys(NetRng rng, int ratePerTick)
        {
            if (rng.Below(ratePerTick) != 0)
            {
                rng.Below(2);
                rng.Below(OffsetsPerTick);
                rng.Below(OffsetsPerTick);
                rng.Below((int)LogicalKey.Pause);
                rng.Below((int)LogicalKey.Pause);
                return null;
            }
            int count = 1 + (int)rng.Below(2);
            var a = ((byte)rng.Below(OffsetsPerTick), (LogicalKey)rng.Below((int)LogicalKey.Pause));
            var b = ((byte)rng.Below(OffsetsPerTick), (LogicalKey)rng.Below((int)LogicalKey.Pause));
            var list = new List<(byte, LogicalKey)> { a };
            if (count == 2 && b.Item2 != a.Item2 && b.Item1 != a.Item1)
            {
                list.Add(b);
            }
            list.Sort((x, y) => x.Item1.CompareTo(y.Item1));
            return list;
        }

        public void Run(uint ticks)
        {
            for (uint i = 0; i < ticks; i++)
            {
                RunTick();
            }
        }

        public void RunTick()
        {
            uint n = _n;
            long start = HostKit.TickStartUs(n);
            long end = HostKit.TickStartUs(n + 1);
            Push(new Ev { TimeUs = start, Kind = KindHostTick });
            for (int s = 0; s < Slots; s++)
            {
                Push(new Ev { TimeUs = start, Kind = KindClientTick, Slot = s, Tick = n });
                List<(byte offset, LogicalKey key)>? keys = Keys(s, n, _keyRng[s]);
                if (keys == null)
                {
                    continue;
                }
                foreach ((byte offset, LogicalKey key) in keys)
                {
                    long at = Math.Max(start, HostKit.AtTicks(n, offset, OffsetsPerTick));
                    Push(new Ev { TimeUs = at, Kind = KindPress, Slot = s, Tick = n, Offset = offset, Key = key });
                }
            }
            while (_queue.Count > 0 && _queue.Min!.TimeUs < end)
            {
                Ev e = _queue.Min!;
                _queue.Remove(e);
                Handle(e);
            }
            _n++;
        }

        private void Handle(Ev e)
        {
            switch (e.Kind)
            {
                case KindHostRecv:
                    HostDeliveries[e.Slot]++;
                    HostDistinct[e.Slot].Add(e.Bytes);
                    Trace[e.Slot].SealArrivals.Add((e.TimeUs, _sealedThroughOf[e.Bytes]));
                    Host.OnPacket(e.Bytes, e.TimeUs, e.Slot == 0 ? HostKit.Addr0 : HostKit.Addr1);
                    if (_queue.Count == 0 || _queue.Min!.Kind != KindHostRecv || _queue.Min.TimeUs != e.TimeUs)
                    {
                        AdvanceHost(e.TimeUs);   // 같은 시각에 닿은 것을 다 넣은 뒤(호출자의 계약)
                    }
                    break;
                case KindClientRecv:
                    Clients[e.Slot].OnPacket(e.Bytes, e.TimeUs);
                    CaptureConfirmed(e.Slot, e.TimeUs);
                    SendFromClient(e.Slot, e.TimeUs);
                    break;
                case KindHostTick:
                    AdvanceHost(e.TimeUs);
                    break;
                case KindClientTick:
                {
                    DuelAdvance a = Clients[e.Slot].Advance(e.Tick);
                    Trace[e.Slot].Advances.Add((e.Tick, a));
                    DrainEvents(e.Slot);
                    SendFromClient(e.Slot, e.TimeUs);
                    break;
                }
                case KindPress:
                {
                    ClientTrace t = Trace[e.Slot];
                    int bit = 1 << (int)e.Key;
                    bool down = ((int)t.Physical & bit) == 0;
                    t.Physical = (KeyMask)((int)t.Physical ^ bit);
                    var q = new QuantizedKey(e.Tick + (uint)t.Delay, e.Offset, e.Key, down);
                    t.Pressed.Add((e.TimeUs, q));
                    Clients[e.Slot].OnLocalInput(new[] { q }, t.Physical);
                    SendFromClient(e.Slot, e.TimeUs);
                    break;
                }
            }
        }

        private void AdvanceHost(long nowUs)
        {
            Host.Advance(nowUs);
            for (int s = 0; s < Slots; s++)
            {
                int len;
                while ((len = Host.PollOutgoing(new PlayerSlot((byte)s), _buf, out ulong to)) > 0)
                {
                    Assert.Equal(s == 0 ? HostKit.Addr0 : HostKit.Addr1, to);
                    byte[] bytes = _buf.AsSpan(0, len).ToArray();
                    HostOutgoing.Add(bytes);
                    CollectDropNotices(s, bytes);
                    int k = Down[s].Transmit(nowUs, _arrivals);
                    for (int i = 0; i < k; i++)
                    {
                        Push(new Ev { TimeUs = _arrivals[i], Kind = KindClientRecv, Slot = s, Bytes = bytes });
                    }
                }
            }
        }

        // 호스트가 낸 ServerTick 의 dropped 를 그대로 모은다(링크에서 사라져도 호스트가 알린 것은 알린 것이다)
        private void CollectDropNotices(int s, byte[] packet)
        {
            Assert.True(DuelPacketFrame.TryOpen(_peekCipher, HostKit.Key(s), DuelDirection.ServerToClient, packet, _body, out _, out int len, out _));
            Assert.Equal(DuelCodecError.None, _serverTick.TryRead(_body.AsSpan(0, len)));
            foreach (DroppedRun r in _serverTick.Dropped)
            {
                for (uint q = r.Seq; q <= r.LastSeq; q++)
                {
                    Trace[s].DropNotices[q] = r.Reason;
                }
            }
        }

        private void SendFromClient(int s, long nowUs)
        {
            int len;
            while ((len = Clients[s].PollOutgoing(_buf, nowUs)) > 0)
            {
                byte[] bytes = _buf.AsSpan(0, len).ToArray();
                Trace[s].PacketsSent++;
                Decode(s, bytes);
                int k = Up[s].Transmit(nowUs, _arrivals);
                for (int i = 0; i < k; i++)
                {
                    Push(new Ev { TimeUs = _arrivals[i], Kind = KindHostRecv, Slot = s, Bytes = bytes });
                }
            }
        }

        // 내보낸 ClientInput 을 열어 내 이벤트를 순번으로 모은다(같은 순번은 늘 같은 이벤트여야 한다)
        private void Decode(int s, byte[] packet)
        {
            Assert.True(DuelPacketFrame.TryOpen(_peekCipher, HostKit.Key(s), DuelDirection.ClientToServer, packet, _body,
                                                out _, out int len, out DuelCodecError err), err.ToString());
            Assert.Equal(DuelCodecError.None, _decode.TryRead(_body.AsSpan(0, len)));
            _sealedThroughOf[packet] = _decode.SealedThroughTick;
            foreach (InputEvent ev in _decode.Events)
            {
                if (Trace[s].SentBySeq.TryGetValue(ev.Seq, out InputEvent seen))
                {
                    Assert.Equal(seen, ev);
                }
                else
                {
                    Trace[s].SentBySeq.Add(ev.Seq, ev);
                }
            }
        }

        private void CaptureConfirmed(int s, long nowUs)
        {
            ClientTrace t = Trace[s];
            if (Clients[s].NeedsFullState)
            {
                t.EverNeedsFullState = true;
            }
            while (true)
            {
                ulong h = Clients[s].ConfirmedHash((uint)t.ConfirmedHashes.Count);
                if (h == 0UL)
                {
                    return;
                }
                t.ConfirmedHashes.Add(h);
                t.ConfirmedAtUs.Add(nowUs);
            }
        }

        private void DrainEvents(int s)
        {
            var buffer = new PresentationEvent[64];
            int k;
            while ((k = Clients[s].DrainEvents(buffer)) > 0)
            {
                for (int i = 0; i < k; i++)
                {
                    Trace[s].Events.Add(buffer[i]);
                }
            }
        }

        private void Push(Ev e)
        {
            e.Order = _order++;
            _queue.Add(e);
        }

        // ── 끝난 뒤 읽기

        // 호스트의 확정 열(WZDR)을 다시 돌린 틱마다의 해시(색인 = 틱)와 최종 해시
        public (List<ulong> perTick, ulong final, Wzdr.File file) HostReference()
        {
            Wzdr.File file = Wzdr.Read(Host.ConfirmedInputRecord());
            var (afterStep, final) = Wzdr.Replay(file);
            Assert.Equal(file.Body.FinalStateHash, final);
            return (afterStep, final, file);
        }

        private sealed class Ev
        {
            public long TimeUs;
            public int Kind;
            public long Order;
            public int Slot;
            public uint Tick;
            public byte Offset;
            public LogicalKey Key;
            public byte[] Bytes = Array.Empty<byte>();
        }

        private sealed class EvOrder : IComparer<Ev>
        {
            public int Compare(Ev? a, Ev? b)
            {
                int c = a!.TimeUs.CompareTo(b!.TimeUs);
                if (c != 0)
                {
                    return c;
                }
                c = a.Kind.CompareTo(b.Kind);
                return c != 0 ? c : a.Order.CompareTo(b.Order);
            }
        }
    }
}

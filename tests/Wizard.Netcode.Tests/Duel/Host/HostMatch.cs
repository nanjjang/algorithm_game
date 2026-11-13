using System;
using System.Collections.Generic;
using System.Linq;
using Wizard.Core;
using Wizard.Netcode.Duel;
using Wizard.Protocol.Duel;
using Wizard.Sim;
using Wizard.Sim.Input;
using Xunit;

namespace Wizard.Netcode.Tests.Duel.Host
{
    // 정직한 두 클라이언트와 호스트를 틱마다 돌리는 시험 경기(CONTRACTS 6.4·6.5·10.2 의 클라이언트 쪽 규칙을 흉내 낸다).
    //   입력 시계 n 에서: 키 이벤트(유효 틱 n + D) → 순번 → 패킷(sealed = n + D − 1, 창 = sealed − 7 이상, base_seq 는 ClientInputWindow)
    //   패킷은 (n 틱의 시작 + 편도)에 서버에 닿는다. 서버는 틱마다 (n 틱의 시작 + 1ms)에 그때까지 닿은 패킷을 다 넣고 → Advance → 두 사람에게 Poll.
    // 같은 씨앗이면 같은 패킷 열과 같은 시각 열이다.
    internal sealed class HostMatch
    {
        private const long ServerLagUs = 1000;

        public readonly DuelHostCore Host;
        public readonly int[] Delay = { 4, 4 };
        public readonly long[] OneWayUs = { 8000, 8000 };
        public Func<int, uint, bool> DropPacket = (slot, n) => false;            // 그 사람의 입력 시계 n 의 패킷을 잃는다
        public Func<int, uint, List<(byte offset, LogicalKey key)>?> Keys = (slot, n) => null;  // 그 틱에 누르거나 뗄 키. null 이면 무작위

        public readonly List<InputEvent>[] Sent = { new List<InputEvent>(), new List<InputEvent>() };   // 낸 이벤트(순번 순)
        public readonly List<ServerView>[] Views = { new List<ServerView>(), new List<ServerView>() };
        public readonly List<byte[]> Outgoing = new List<byte[]>();                                       // 호스트가 낸 바이트 그대로(슬롯 순)

        private readonly Random _rng;
        private readonly FakeClient[] _clients = { new FakeClient(0), new FakeClient(1) };
        private readonly ushort[] _physical = new ushort[2];
        private readonly uint[] _seq = new uint[2];
        private readonly long[] _nextConfirmed = new long[2];
        private readonly uint[] _echoMs = new uint[2];
        private readonly long[] _echoAtUs = new long[2];
        private readonly List<(long at, int order, int slot, byte[] bytes)> _inFlight = new List<(long, int, int, byte[])>();
        private readonly ClientInputPacket _scratch = new ClientInputPacket();
        private int _order;
        private uint _n;

        public HostMatch(int seed, ulong worldSeed = 0xD0E1_5EEDUL)
        {
            _rng = new Random(seed);
            Host = HostKit.NewHost(null, worldSeed);
        }

        public void Start()
        {
            Host.SetInputDelay(HostKit.S0, Delay[0]);
            Host.SetInputDelay(HostKit.S1, Delay[1]);
        }

        public uint InputClock => _n;

        public void Run(uint ticks)
        {
            for (uint i = 0; i < ticks; i++)
            {
                StepOnce();
            }
        }

        public void StepOnce()
        {
            uint n = _n;
            long tickStart = HostKit.TickStartUs(n);
            for (int s = 0; s < 2; s++)
            {
                ClientTick(s, n, tickStart);
            }
            long now = tickStart + ServerLagUs;
            Deliver(now);
            Host.Advance(now);
            for (int s = 0; s < 2; s++)
            {
                var buf = new byte[DuelWire.MaxPacketBytes];
                int len = Host.PollOutgoing(new PlayerSlot((byte)s), buf, out _);
                if (len == 0)
                {
                    continue;
                }
                Outgoing.Add(buf.AsSpan(0, len).ToArray());
                ServerView v = Open(s, buf.AsSpan(0, len).ToArray());
                Views[s].Add(v);
                if (v.Ticks.Count > 0 && v.ConfirmedFrom <= _nextConfirmed[s])
                {
                    _nextConfirmed[s] = Math.Max(_nextConfirmed[s], v.ConfirmedFrom + v.Ticks.Count);
                }
                _echoMs[s] = v.ServerTimeMs;
                _echoAtUs[s] = now;
            }
            _n++;
        }

        private void ClientTick(int s, uint n, long tickStart)
        {
            uint eff = n + (uint)Delay[s];
            List<(byte offset, LogicalKey key)> keys = Keys(s, n) ?? RandomKeys();
            foreach ((byte offset, LogicalKey key) in keys.OrderBy(k => k.offset).ThenBy(k => InputOrder.KeyClass(k.key)))
            {
                int bit = 1 << (int)key;
                bool down = (_physical[s] & bit) == 0;
                _physical[s] = (ushort)(_physical[s] ^ bit);
                Sent[s].Add(new InputEvent(eff, offset, key, down, ++_seq[s]));
            }
            _scratch.Clear();
            _scratch.SealedThroughTick = eff - 1U;
            ClientInputWindow.Fill(_scratch, Sent[s].ToArray(), _seq[s]);
            uint ack = _nextConfirmed[s] == 0 ? 0U : (uint)(_nextConfirmed[s] - 1);
            ushort hold = _echoMs[s] == 0 ? (ushort)0 : (ushort)((tickStart - _echoAtUs[s]) / 1000);
            byte[] bytes = _clients[s].Build(_scratch.SealedThroughTick, _scratch.Events.ToArray(), _scratch.BaseSeq, 0, ack, _echoMs[s], hold);
            if (!DropPacket(s, n))
            {
                _inFlight.Add((tickStart + OneWayUs[s], _order++, s, bytes));
            }
        }

        private List<(byte, LogicalKey)> RandomKeys()
        {
            var list = new List<(byte, LogicalKey)>();
            if (_rng.Next(0, 3) != 0)
            {
                return list;
            }
            int k = _rng.Next(1, 3);
            var used = new HashSet<LogicalKey>();
            for (int i = 0; i < k; i++)
            {
                var key = (LogicalKey)_rng.Next(0, 8);
                if (used.Add(key))
                {
                    list.Add(((byte)_rng.Next(0, 256), key));
                }
            }
            return list;
        }

        private void Deliver(long now)
        {
            var due = _inFlight.Where(p => p.at <= now).OrderBy(p => p.at).ThenBy(p => p.order).ToList();
            foreach (var p in due)
            {
                Host.OnPacket(p.bytes, p.at, p.slot == 0 ? HostKit.Addr0 : HostKit.Addr1);
                _inFlight.Remove(p);
            }
        }

        private static ServerView Open(int slot, byte[] packet)
        {
            var body = new byte[DuelWire.MaxBodyBytes];
            Assert.True(DuelPacketFrame.TryOpen(new TestCipher(), HostKit.Key(slot), DuelDirection.ServerToClient, packet, body,
                                                out DuelPacketHeader header, out int len, out _));
            var st = new ServerTickPacket();
            Assert.Equal(DuelCodecError.None, st.TryRead(body.AsSpan(0, len)));
            var v = new ServerView
            {
                PacketNumber = header.PacketNumber,
                ServerTimeMs = st.ServerTimeMs,
                MarginMs = st.MarginMsEstimate,
                ConfirmedFrom = st.ConfirmedFromTick,
                HasHash = st.HasHash,
                HashTick = st.HashTick,
                Hash = st.StateHash,
            };
            for (int i = 0; i < st.ConfirmedCount; i++)
            {
                v.Ticks.Add(new[] { st.SlotEvents(i, 0).ToArray(), st.SlotEvents(i, 1).ToArray() });
            }
            v.Dropped.AddRange(st.Dropped.ToArray());
            return v;
        }
    }
}

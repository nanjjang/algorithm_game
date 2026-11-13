using System;
using System.Collections.Generic;
using System.Linq;
using Wizard.Client.Duel;
using Wizard.Core;
using Wizard.Netcode.Duel;
using Wizard.Protocol;
using Wizard.Protocol.Duel;
using Wizard.Sim.Input;
using Xunit;

namespace Wizard.Client.Tests.Duel
{
    // M2-NET-07 수락 조건: LoopbackTransport 가 기기 안의 DuelHostCore 에 붙고 서버 경기와 같은 바이트가 오간다, 한 기기 2인(키 배치 둘),
    // 인위 지연, DuelSession 의 순서, T-62(번호의 단일 자리, 같은 시계, 받는 쪽의 갈림, 경기 중 시계 다시 재기).
    public sealed class LoopbackDuelTests
    {
        private const double TickBaseSeconds = 1.0 / 60;

        // ── 연결 절차와 인위 지연

        [Fact]
        public void Loopback_HandshakeReachesRunning_AndBothSidesAgreeOnDelay()
        {
            var rig = new DuelRig();
            rig.Connect();
            Assert.True(rig.Host.IsStarted);
            for (int s = 0; s < 2; s++)
            {
                DuelSession session = rig.Sessions[s];
                Assert.Equal(DuelTransportKind.Loopback, session.TransportKind);
                Assert.Equal(DuelSessionPhase.Running, session.Phase);
                Assert.Equal(DuelConnectState.Ready, session.Connector.State);
                Assert.Equal(DuelKit.Db.Net.ClockSyncSamples, session.Connector.SamplesCollected);
                Assert.Equal(rig.Host.InputDelayTicks(new PlayerSlot((byte)s)), session.InputDelayTicks);
                Assert.Equal(2, session.InputDelayTicks);   // 지연 0: 서버가 잰 왕복은 한 프레임(16.7ms) 안쪽 → 60ms 이하 → 2틱(spec 10 14.5)
                Assert.Equal(rig.Host.MatchEpochUs, session.Connector.MatchEpochServerUs);
                Assert.Equal(rig.Host.InputDelayTicks(new PlayerSlot((byte)(1 - s))), session.Connector.OpponentInputDelayTicks);
            }
        }

        // 서버가 잰 왕복 = 올라감 + 내려감 + (클라이언트가 프레임에서 받기까지의 기다림, 한 프레임 단위). D 는 그 값으로 서버가 고른다(10.5)
        [Theory]
        [InlineData(0L, 0L, 2)]
        [InlineData(20_000L, 20_000L, 2)]   // 20 + 33.3 = 53.3ms → 2
        [InlineData(35_000L, 35_000L, 3)]   // 35 + 50.0 = 85.0ms → 3
        [InlineData(60_000L, 60_000L, 4)]   // 60 + 66.7 = 126.7ms → 4(120ms 초과도 4)
        public void ArtificialLatency_ServerMeasuresRtt_AndChoosesDelay(long upUs, long downUs, int expectedDelay)
        {
            var rig = new DuelRig();
            rig.Host.SetLatency(DuelKit.S0, upUs, downUs);
            rig.Host.SetLatency(DuelKit.S1, upUs, downUs);
            rig.Connect();
            long frames = (downUs + DuelKit.FrameUs - 1) / DuelKit.FrameUs;
            long expectedRttUs = upUs + Math.Max(1, frames) * DuelKit.FrameUs;
            for (int s = 0; s < 2; s++)
            {
                int rtt = rig.Host.HandshakeRttMs(new PlayerSlot((byte)s));
                Assert.InRange(rtt, (upUs + downUs) / 1000, expectedRttUs / 1000 + 1);
                Assert.Equal(expectedDelay, rig.Sessions[s].InputDelayTicks);
                Assert.Equal(rtt, rig.Sessions[s].Connector.ServerMeasuredRttMs);
            }
        }

        [Fact]
        public void ArtificialLatency_HonestInputsAreConfirmedAtPressTickPlusDelay()
        {
            var rig = new DuelRig();
            rig.Host.SetLatency(DuelKit.S0, 30_000, 30_000);
            rig.Host.SetLatency(DuelKit.S1, 30_000, 30_000);
            long t = rig.Connect();
            long start = rig.MatchTickUs(1);
            var expected = new List<(LogicalKey Key, bool Down, long Tick)>();
            var presses = new (long At, PhysicalKey Key, LogicalKey Logical, bool Down)[]
            {
                (start + 100_123, PhysicalKey.W, LogicalKey.Up, true),
                (start + 250_400, PhysicalKey.J, LogicalKey.Fire, true),
                (start + 260_001, PhysicalKey.J, LogicalKey.Fire, false),
                (start + 400_777, PhysicalKey.W, LogicalKey.Up, false),
            };
            int next = 0;
            rig.Run(t, start + 1_500_000, now =>
            {
                while (next < presses.Length && presses[next].At <= now)
                {
                    var p = presses[next++];
                    DuelSession s0 = rig.Sessions[0];
                    expected.Add((p.Logical, p.Down, s0.Clock.TickAt(DuelKit.Sec(DuelKit.StartUs + p.At)) + s0.InputDelayTicks));
                    rig.Press(p.At, p.Key, p.Down);
                }
            });
            Assert.Equal(3, rig.Sessions[0].InputDelayTicks);
            List<InputEvent> confirmed = rig.Tap.EventsOf(0);
            Assert.Equal(expected.Count, confirmed.Count);
            for (int i = 0; i < expected.Count; i++)
            {
                Assert.Equal(expected[i].Key, confirmed[i].Key);
                Assert.Equal(expected[i].Down, confirmed[i].Down);
                Assert.Equal(expected[i].Tick, (long)confirmed[i].Tick);
            }
            DuelNetStats stats = rig.Host.Core.StatsFor(DuelKit.S0);
            Assert.Equal(0, stats.EventsLate);
            Assert.Equal(0, stats.EventsEarly);
            Assert.Equal(0, stats.OrderViolations);
            Assert.Equal(expected.Count, stats.EventsAccepted);
        }

        // ── T-62

        // 연결 절차(SyncRequest, Hello)와 코어(ClientInput)가 같은 키의 SendCounter 한 자리에서 번호를 받는다. 다시 보내기가 생기는 지연에서도
        // 한 경기의 모든 (키, packet_number)가 서로 다르다
        [Fact]
        public void T62_AllClientPacketsOfAMatch_HaveDistinctKeyAndPacketNumber()
        {
            var rig = new DuelRig();
            rig.Host.SetLatency(DuelKit.S0, 60_000, 60_000);    // 왕복(≈127ms)이 다시 보내기 간격(100ms)보다 길다 → 다시 보낸다
            rig.Host.SetLatency(DuelKit.S1, 10_000, 10_000);
            long t = rig.Connect();
            long start = rig.MatchTickUs(1);
            rig.Run(t, start + 2_000_000, now =>
            {
                if ((now / 37_000) % 5 == 0)
                {
                    rig.Press(now, PhysicalKey.D, (now / 37_000) % 2 == 0);
                    rig.Press(now, PhysicalKey.RightArrow, (now / 37_000) % 2 == 1);
                }
            });

            var seen = new HashSet<(uint Token, ulong Number)>();
            for (int s = 0; s < 2; s++)
            {
                var kinds = new Dictionary<DuelPacketKind, int>();
                ulong last = 0;
                foreach (byte[] p in rig.Transports[s].Sent)
                {
                    DuelPacketHeader h = DuelKit.Header(p);
                    Assert.Equal(rig.Keys[s].TokenId, h.TokenId);
                    Assert.True(seen.Add((h.TokenId, h.PacketNumber)), $"slot {s}: packet number {h.PacketNumber} was sealed twice");
                    Assert.True(h.PacketNumber > last, "the packet numbers of one key only go up");
                    last = h.PacketNumber;
                    kinds[h.Kind] = kinds.TryGetValue(h.Kind, out int c) ? c + 1 : 1;
                }
                Assert.True(kinds[DuelPacketKind.SyncRequest] >= DuelKit.Db.Net.ClockSyncSamples);
                Assert.True(kinds[DuelPacketKind.Hello] >= 1);
                Assert.True(kinds[DuelPacketKind.ClientInput] > 60);
                // 번호는 키 하나의 SendCounter 에서만 나왔다: 마지막으로 내준 번호 = 보낸 패킷의 수(태운 번호 없음)
                Assert.Equal((ulong)rig.Transports[s].Sent.Count, rig.Keys[s].SendCounter.Taken);
                Assert.Equal(rig.Keys[s].SendCounter.Taken, last);
            }
            // 지연을 건 쪽은 답이 늦어 인사를 다시 보냈다
            int syncs0 = rig.Transports[0].Sent.Count(p => DuelKit.Header(p).Kind == DuelPacketKind.SyncRequest);
            Assert.True(syncs0 > DuelKit.Db.Net.ClockSyncSamples, $"expected handshake retransmissions, saw {syncs0} SyncRequests");
        }

        // 받는 쪽의 갈림: ServerTick 은 코어가, SyncReply·HelloAck 는 연결 절차가 자기 창으로 연다. 연결 절차는 ServerTick 을 열지 않고,
        // 이미 받은 인사 답을 다시 받지 않는다
        [Fact]
        public void T62_ConnectorOpensOnlyHandshakeReplies_ServerTickGoesToCore()
        {
            var rig = new DuelRig();
            long t = rig.Connect();
            rig.Run(t, rig.MatchTickUs(40));
            RecordingTransport tr = rig.Transports[0];
            byte[] serverTick = tr.Received.First(p => DuelKit.Header(p).Kind == DuelPacketKind.ServerTick);
            byte[] syncReply = tr.Received.First(p => DuelKit.Header(p).Kind == DuelPacketKind.SyncReply);
            byte[] helloAck = tr.Received.First(p => DuelKit.Header(p).Kind == DuelPacketKind.HelloAck);
            DuelConnector c = rig.Sessions[0].Connector;
            Assert.False(c.OnPacket(serverTick, 1));
            Assert.False(c.OnPacket(syncReply, 1));   // 재전송 창
            Assert.False(c.OnPacket(helloAck, 1));
            Assert.True(rig.Sessions[0].LastAdvance.ConfirmedTick > 30U);   // 코어는 ServerTick 으로 확정을 받았다
            // 다른 사람의 인사 답(다른 token_id)은 열지 않는다
            byte[] otherAck = rig.Transports[1].Received.First(p => DuelKit.Header(p).Kind == DuelPacketKind.HelloAck);
            Assert.False(c.OnPacket(otherAck, 1));
        }

        // OnPacket(닿은 시각)과 PollOutgoing(보내는 시각)에 같은 시계를 준다: 서버가 ClientInput 의 echo_hold_ms 로 잰 경기 중 왕복이
        // 실제 지연(올라감 + 내려감 + 받기까지의 프레임 기다림)과 맞는다. hold 를 틱으로 어림하면 한 틱 가까이 작게 잡힌다(T-62 5번)
        [Theory]
        [InlineData(20_000L, 20_000L)]
        [InlineData(45_000L, 10_000L)]
        public void T62_SameClockForReceiveAndSend_ServerRttMatchesTheLink(long upUs, long downUs)
        {
            var rig = new DuelRig();
            rig.Host.SetLatency(DuelKit.S0, upUs, downUs);
            rig.Host.SetLatency(DuelKit.S1, upUs, downUs);
            long t = rig.Connect();
            rig.Run(t, rig.MatchTickUs(120));
            long frames = Math.Max(1, (downUs + DuelKit.FrameUs - 1) / DuelKit.FrameUs);
            for (int s = 0; s < 2; s++)
            {
                int rtt = rig.Host.Core.MeasuredRttMs(new PlayerSlot((byte)s));
                Assert.InRange(rtt, (upUs + downUs) / 1000, (upUs + frames * DuelKit.FrameUs) / 1000 + 2);
            }
        }

        // 경기 중 시계 다시 재기는 DrainClockSamples 의 표본으로 한다(ServerUs 는 경기 시계의 µs). 서버 시계가 0.2% 빠르면 입력 시계가
        // 틱을 0.5% 안에서만 줄여 따라간다. 틱을 건너뛰지 않는다(TV-N31 의 범위)
        [Theory]
        [InlineData(2_000L)]
        [InlineData(-3_000L)]
        public void ClockSamples_SlewInputClockTowardServerMatchClock(long driftPpm)
        {
            var rig = new DuelRig { ServerDriftPpm = driftPpm };
            long t = rig.Connect();
            int maxPpm = DuelKit.Db.Net.ClockSlewMaxPpm;
            double min = TickBaseSeconds * (1_000_000 - maxPpm) / 1_000_000 - 1e-12;
            double max = TickBaseSeconds * (1_000_000 + maxPpm) / 1_000_000 + 1e-12;
            long lastTick = long.MinValue;
            long end = rig.MatchTickUs(60 * 20);
            rig.Run(t, end, now =>
            {
                DuelSession s0 = rig.Sessions[0];
                Assert.InRange(s0.Clock.TickSeconds, min, max);
                long tick = s0.InputClockTick(DuelKit.Sec(DuelKit.StartUs + now));
                Assert.True(tick >= lastTick, "the input clock never goes back");
                lastTick = tick;
            });
            double nowSec = DuelKit.Sec(DuelKit.StartUs + end);
            long serverMatchUs = rig.ServerUs(end) - rig.Host.MatchEpochUs;
            long serverTick = serverMatchUs * 60 / DuelKit.UsPerSecond;
            for (int s = 0; s < 2; s++)
            {
                DuelSession session = rig.Sessions[s];
                // 보정이 없으면 20초에 0.2% = 40ms(2.4틱)가 벌어진다
                Assert.InRange(session.InputClockTick(nowSec), serverTick - 1, serverTick + 1);
                Assert.True(Math.Sign(session.Clock.AdjustPpm) == -Math.Sign(driftPpm), $"adjust {session.Clock.AdjustPpm}ppm for drift {driftPpm}ppm");
                Assert.InRange(session.Clock.AdjustPpm, -maxPpm, maxPpm);
            }
            Assert.Equal(0, rig.Host.Core.StatsFor(DuelKit.S0).EventsLate);
        }

        [Fact]
        public void ClockSamples_NoDrift_KeepsTickLengthNearBase()
        {
            var rig = new DuelRig();
            long t = rig.Connect();
            rig.Run(t, rig.MatchTickUs(60 * 5));
            for (int s = 0; s < 2; s++)
            {
                Assert.InRange(rig.Sessions[s].Clock.AdjustPpm, -1_100, 1_100);   // 표본의 ms 내림(≤ 1ms)만큼
            }
        }

        // ── DuelSession 의 순서

        // 프레임마다 Drain → Quantize → OnLocalInput → 곧바로 Send. Step 을 부르지 않아도 키 이벤트가 든 패킷이 나간다
        [Fact]
        public void OnInput_SendsTheKeyEventAtOnce_WithoutWaitingForTheTickLoop()
        {
            var rig = new DuelRig();
            long t = rig.Connect();
            t = rig.Run(t, rig.MatchTickUs(20));
            long press = rig.MatchTickUs(21) + 7_000;   // 틱 한가운데
            rig.Host.Pump(rig.ServerUs(press));
            int before = rig.Transports[0].Sent.Count;
            rig.Press(press, PhysicalKey.J, true);
            DuelSession s0 = rig.Sessions[0];
            double now = DuelKit.Sec(DuelKit.StartUs + press);
            s0.OnInput(rig.Keyboard.Player(0), now);
            Assert.Equal(before + 1, rig.Transports[0].Sent.Count);
            byte[] packet = rig.Transports[0].Sent[before];
            Assert.Equal(DuelPacketKind.ClientInput, DuelKit.Header(packet).Kind);
            var ci = new ClientInputPacket();
            Assert.Equal(DuelCodecError.None, ci.TryRead(DuelKit.Open(packet, 0, DuelDirection.ClientToServer)));
            InputEvent last = ci.Events[ci.EventCount - 1];
            Assert.Equal(LogicalKey.Fire, last.Key);
            Assert.True(last.Down);
            Assert.Equal(s0.Clock.TickAt(now) + s0.InputDelayTicks, (long)last.Tick);
        }

        // Step: 로컬 입력(곧바로 보냄) → 받은 패킷 전부 → Advance → 그 틱의 패킷 → 표시 이벤트
        [Fact]
        public void Step_RunsLocalInput_Packets_Advance_Events_InThatOrder()
        {
            var rig = new DuelRig();
            // 지연 0 에서는 확정이 예측보다 먼저 그 틱에 닿는다(봉인이 n + D − 1 이라 예측 세계가 n + D 에 닿기 전에 확정된다).
            // 예측이 먼저 나오는 것을 보려고 지연을 건다
            rig.Host.SetLatency(DuelKit.S0, 30_000, 30_000);
            rig.Host.SetLatency(DuelKit.S1, 30_000, 30_000);
            long t = rig.Connect();
            t = rig.Run(t, rig.MatchTickUs(30));
            long at = rig.MatchTickUs(31) + 1_000;     // 새 틱에 막 들어선 시각
            rig.Host.Pump(rig.ServerUs(at));
            rig.Press(at - 500, PhysicalKey.LeftShift, true);
            RecordingTransport tr = rig.Transports[0];
            tr.Ops.Clear();
            uint predictedBefore = rig.Sessions[0].LastAdvance.PredictedTick;
            rig.Sessions[0].Step(rig.Keyboard.Player(0), DuelKit.Sec(DuelKit.StartUs + at));
            List<string> ops = tr.Ops;
            Assert.Equal("send:ClientInput", ops[0]);                            // 키 이벤트는 받기·Advance 를 기다리지 않는다
            int firstRecv = ops.FindIndex(o => o.StartsWith("recv:", StringComparison.Ordinal));
            int lastRecv = ops.FindLastIndex(o => o.StartsWith("recv:", StringComparison.Ordinal));
            Assert.Equal(1, firstRecv);
            Assert.Equal("recv:none", ops[lastRecv]);                            // 받은 버퍼를 다 비웠다
            Assert.Contains("recv:ServerTick", ops);
            Assert.Equal(lastRecv + 2, ops.Count);
            Assert.Equal("send:ClientInput", ops[ops.Count - 1]);              // Advance 가 연 새 틱의 패킷
            Assert.True(rig.Sessions[0].LastAdvance.PredictedTick > predictedBefore);

            // 표시 이벤트: 자세에 든 것이 예측 세계에서 먼저(Predicted), 확정 세계가 그 틱을 지나면 Confirmed 로 나온다(CONTRACTS 6.4)
            var seen = new List<EventPhase>();
            void Collect()
            {
                foreach (PresentationEvent e in rig.Sessions[0].Events.ToArray())
                {
                    if (e.Event.Type == Wizard.Sim.Events.SimEventType.StanceEntered && e.Event.Slot.Value == 0)
                    {
                        seen.Add(e.Phase);
                    }
                }
            }
            Collect();
            rig.Run(at + DuelKit.FrameUs, at + 500_000, _ => Collect());
            Collect();
            Assert.Equal(new[] { EventPhase.Predicted, EventPhase.Confirmed }, seen);
        }

        // ── 서버 경기와 같은 바이트

        // 기기 안의 호스트가 DuelHostCore 와 주고받은 호출 열을 따로 만든 DuelHostCore(wizard-duel 이 쓰는 것과 같은 클래스)에 그대로 주면
        // 바이트 단위로 같은 패킷이 나온다. 올라간 패킷은 10.2 형식으로 읽고 다시 써도 같은 바이트다
        [Fact]
        public void LoopbackBytes_AreServerBytes_StandaloneHostReproducesThem()
        {
            var rig = new DuelRig(seed: 0xB17E_5000UL);
            long t = rig.Connect();
            long start = rig.MatchTickUs(1);
            rig.Run(t, start + 2_000_000, now =>
            {
                long k = now / 50_000;
                if (now % 50_000 < DuelKit.FrameUs)
                {
                    rig.Press(now, k % 3 == 0 ? PhysicalKey.J : PhysicalKey.A, k % 2 == 0);
                    rig.Press(now, k % 3 == 1 ? PhysicalKey.Z : PhysicalKey.UpArrow, k % 2 == 1);
                }
            });

            var host = new DuelHostCore(DuelKit.Db, DuelKit.Setup(0xB17E_5000UL), DuelKit.HostKeys(), DuelKit.Cipher());
            host.SetInputDelay(DuelKit.S0, rig.Host.InputDelayTicks(DuelKit.S0));
            host.SetInputDelay(DuelKit.S1, rig.Host.InputDelayTicks(DuelKit.S1));
            var buffer = new byte[DuelWire.MaxPacketBytes];
            int sends = 0, packets = 0;
            foreach (var call in rig.Tap.Calls)
            {
                switch (call.Op)
                {
                    case HostTap.OpPacket:
                        packets++;
                        var ci = new ClientInputPacket();
                        byte[] body = DuelKit.Open(call.Packet, (int)call.Address - 1, DuelDirection.ClientToServer);
                        Assert.Equal(DuelCodecError.None, ci.TryRead(body));
                        var w = new ByteWriter();
                        Assert.Equal(DuelCodecError.None, ci.TryWrite(w));
                        Assert.Equal(body, w.ToArray());
                        host.OnPacket(call.Packet, call.TimeUs, call.Address);
                        break;
                    case HostTap.OpAdvance:
                        host.Advance(call.TimeUs);
                        break;
                    default:
                        int n = host.PollOutgoing(new PlayerSlot(call.Slot), buffer, out ulong to);
                        Assert.Equal(call.Address, to);
                        Assert.Equal(call.Packet, buffer.AsSpan(0, n).ToArray());
                        sends++;
                        break;
                }
            }
            Assert.True(packets > 200 && sends > 200);
            Assert.Equal(rig.Host.Core.ConfirmedInputRecord().ToArray(), host.ConfirmedInputRecord().ToArray());
            Assert.True(rig.Tap.EventsOf(0).Count > 10 && rig.Tap.EventsOf(1).Count > 10);
        }

        // ── 한 기기 2인

        // 키보드 하나, 키 배치 둘(사람 0 = WASD·J·K·Shift·Space, 사람 1 = 방향키·Z·X·C·V). 한 사람의 키는 그 사람의 슬롯으로만 간다
        [Fact]
        public void OneKeyboard_TwoLayouts_EachPlayersKeysReachOnlyTheirSlot()
        {
            LocalDuel duel = DuelKit.NewLocal();
            var tap = new HostTap();
            duel.Host.SetTap(tap);
            long t = DuelKit.Connect(duel);
            long start = DuelKit.MatchTickUs(duel, 1);
            var script = new (long At, PhysicalKey Key, bool Down)[]
            {
                (start + 50_000, PhysicalKey.W, true),
                (start + 80_000, PhysicalKey.UpArrow, true),
                (start + 120_000, PhysicalKey.J, true),
                (start + 121_000, PhysicalKey.Z, true),
                (start + 200_000, PhysicalKey.J, false),
                (start + 200_000, PhysicalKey.Z, false),
                (start + 300_000, PhysicalKey.W, false),
                (start + 330_000, PhysicalKey.UpArrow, false),
            };
            var expected = new[] { new List<(LogicalKey, bool, long)>(), new List<(LogicalKey, bool, long)>() };
            int next = 0;
            DuelKit.Run(duel, t, start + 800_000, now =>
            {
                while (next < script.Length && script[next].At <= now)
                {
                    var e = script[next++];
                    int slot = e.Key == PhysicalKey.W || e.Key == PhysicalKey.J ? 0 : 1;
                    LogicalKey logical = e.Key == PhysicalKey.W || e.Key == PhysicalKey.UpArrow ? LogicalKey.Up : LogicalKey.Fire;
                    DuelSession s = duel.Session(new PlayerSlot((byte)slot));
                    double sec = DuelKit.Sec(DuelKit.StartUs + e.At);
                    expected[slot].Add((logical, e.Down, s.Clock.TickAt(sec) + s.InputDelayTicks));
                    duel.Keyboard.OnPhysicalKey(e.Key, e.Down, sec);
                }
            });
            for (int slot = 0; slot < 2; slot++)
            {
                List<InputEvent> got = tap.EventsOf(slot);
                Assert.Equal(expected[slot].Count, got.Count);
                for (int i = 0; i < got.Count; i++)
                {
                    Assert.Equal(expected[slot][i], (got[i].Key, got[i].Down, (long)got[i].Tick));
                }
                Assert.Equal(0, duel.Host.Core.StatsFor(new PlayerSlot((byte)slot)).EventsLate);
                Assert.Equal(0, duel.Session(new PlayerSlot((byte)slot)).HashMismatchCount);
                Assert.False(duel.Session(new PlayerSlot((byte)slot)).NeedsFullState);
            }
        }

        // 같은 키 열이면 같은 확정 기록이고, 두 클라이언트의 확정 세계는 서버 해시와 어긋나지 않는다
        [Fact]
        public void LocalDuel_IsDeterministic_AndClientsMatchServerHashes()
        {
            byte[] Play()
            {
                LocalDuel duel = DuelKit.NewLocal(0x0DE7_0001UL);
                long t = DuelKit.Connect(duel);
                long start = DuelKit.MatchTickUs(duel, 1);
                DuelKit.Run(duel, t, start + 3_000_000, now =>
                {
                    long k = now / 70_001;
                    if (now % 70_001 < DuelKit.FrameUs)
                    {
                        double sec = DuelKit.Sec(DuelKit.StartUs + now);
                        duel.Keyboard.OnPhysicalKey(k % 2 == 0 ? PhysicalKey.D : PhysicalKey.J, k % 4 < 2, sec);
                        duel.Keyboard.OnPhysicalKey(k % 3 == 0 ? PhysicalKey.LeftArrow : PhysicalKey.Z, k % 4 >= 2, sec);
                    }
                });
                for (int s = 0; s < 2; s++)
                {
                    DuelSession session = duel.Session(new PlayerSlot((byte)s));
                    Assert.Equal(0, session.HashMismatchCount);
                    Assert.True(session.LastAdvance.ConfirmedTick > 150U);
                }
                return duel.Host.Core.ConfirmedInputRecord().ToArray();
            }
            Assert.Equal(Play(), Play());
        }

        // 연결 중에 눌러 둔 키는 입력 시계가 생긴 뒤 처음으로 나간다(틱 0 + D, 오프셋 0). 연결 중에 눌렀다 뗀 키는 나가지 않는다
        [Fact]
        public void KeyHeldWhileConnecting_IsSentAtMatchStart()
        {
            var rig = new DuelRig();
            rig.Press(0, PhysicalKey.W, true);
            rig.Press(0, PhysicalKey.J, true);
            rig.Press(1_000, PhysicalKey.J, false);
            long t = rig.Connect();
            rig.Run(t, rig.MatchTickUs(20));
            List<InputEvent> got = rig.Tap.EventsOf(0);
            Assert.Single(got);
            Assert.Equal(LogicalKey.Up, got[0].Key);
            Assert.True(got[0].Down);
            Assert.Equal((uint)rig.Sessions[0].InputDelayTicks, got[0].Tick);
            Assert.Equal(0, got[0].Offset);
        }

        // ── 전송의 계약

        [Fact]
        public void LoopbackTransport_Contract()
        {
            var host = new LoopbackHost(DuelKit.Db, DuelKit.Setup(), DuelKit.HostKeys(), DuelKit.Cipher(), DuelKit.HostOptions());
            LoopbackTransport a = host.Connect(DuelKit.S0);
            Assert.Equal(DuelTransportKind.Loopback, a.Kind);
            Assert.Throws<InvalidOperationException>(() => host.Connect(DuelKit.S0));
            Assert.Throws<ArgumentOutOfRangeException>(() => host.Connect(new PlayerSlot(2)));
            Assert.Throws<ArgumentOutOfRangeException>(() => host.SetLatency(DuelKit.S0, -1, 0));
            var buffer = new byte[DuelWire.MaxPacketBytes];
            Assert.Equal(0, a.Receive(buffer));     // 막지 않는다
            a.Dispose();
            a.Send(new byte[40]);                   // 닫힌 뒤에는 아무것도 하지 않는다
            Assert.Equal(0, a.Receive(buffer));
            LoopbackTransport again = host.Connect(DuelKit.S0);
            Assert.Equal(DuelTransportKind.Loopback, again.Kind);
        }

        [Fact]
        public void Session_RejectsMissingKeysAndBadOptions()
        {
            var host = new LoopbackHost(DuelKit.Db, DuelKit.Setup(), DuelKit.HostKeys(), DuelKit.Cipher(), DuelKit.HostOptions());
            LoopbackTransport tr = host.Connect(DuelKit.S0);
            Assert.Throws<ArgumentException>(() =>
                new DuelSession(DuelKit.Db, DuelKit.Setup(), DuelKit.S0, default(DuelClientKeys), DuelKit.Cipher(), tr, DuelKit.SessionOptions()));
            var keys = new DuelClientKeys(DuelKit.Token0, 1U, DuelKit.Key(0));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new DuelSession(DuelKit.Db, DuelKit.Setup(), DuelKit.S0, keys, DuelKit.Cipher(), tr, new DuelSessionOptions { HandshakeRetryUs = 0 }));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new LoopbackHost(DuelKit.Db, DuelKit.Setup(), DuelKit.HostKeys(), DuelKit.Cipher(), new LoopbackHostOptions { StartLeadUs = -1 }));
        }
    }
}

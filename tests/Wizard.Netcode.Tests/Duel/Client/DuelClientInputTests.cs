using System;
using System.Collections.Generic;
using Wizard.Core;
using Wizard.Netcode.Duel;
using Wizard.Protocol.Duel;
using Wizard.Sim.Input;
using Xunit;

namespace Wizard.Netcode.Tests.Duel.Client
{
    // M2-NET-05 수락 조건: 순번은 여기서 매긴다, OnLocalInput 직후 PollOutgoing 에 패킷, 버려진 내 이벤트의 보정(CONTRACTS 6.4, 10.2)
    public sealed class DuelClientInputTests
    {
        private const int AllDuelKeys = (1 << ((int)LogicalKey.Reverse + 1)) - 1;

        private static KeyMask Bit(LogicalKey k) => (KeyMask)(1 << (int)k);

        [Fact]
        public void LocalInput_GetsNextSeqAndIsInTheVeryNextPacket()
        {
            var rig = new ClientRig(me: 0, inputDelay: 2);
            rig.Client.Advance(10);
            Assert.NotNull(rig.Outgoing());                          // 틱 10 의 패킷
            Assert.Null(rig.Outgoing());                             // 새 틱도 새 키도 없으면 없다

            rig.Client.OnLocalInput(new[] { ClientRig.Q(12, LogicalKey.Fire, true, 37) }, Bit(LogicalKey.Fire));
            ClientInputPacket? p = rig.Outgoing();                   // 다음 틱을 기다리지 않는다
            Assert.NotNull(p);
            Assert.Equal(1, p!.EventCount);
            InputEvent e = p.Events[0];
            Assert.Equal(12u, e.Tick);                               // QuantizedKey.Tick 은 이미 유효 틱(누른 틱 10 + D 2)
            Assert.Equal((byte)37, e.Offset);
            Assert.Equal(LogicalKey.Fire, e.Key);
            Assert.True(e.Down);
            Assert.Equal(1u, e.Seq);                                 // 경기 처음 1부터
            Assert.Equal(0u, p.BaseSeq);
            Assert.Equal(11u, p.SealedThroughTick);                  // n + D − 1

            rig.Client.OnLocalInput(new[] { ClientRig.Q(12, LogicalKey.Fire, false, 90) }, KeyMask.None);
            ClientInputPacket? q = rig.Outgoing();
            Assert.NotNull(q);
            Assert.Equal(2, q!.EventCount);                          // 최근 8틱을 겹쳐 싣는다
            Assert.Equal(2u, q.Events[1].Seq);
            Assert.Equal(2u, rig.Client.LastAssignedSeq);
        }

        [Fact]
        public void NinthKeyOfATick_IsDroppedBeforeNumbering_AndPauseIsIgnored()
        {
            var rig = new ClientRig(me: 0);
            rig.Client.Advance(0);
            var keys = new List<QuantizedKey>();
            for (int i = 0; i < 9; i++)
            {
                keys.Add(ClientRig.Q(5, (i & 1) == 0 ? LogicalKey.Up : LogicalKey.Down, true, (byte)(i * 10)));
            }
            keys.Add(ClientRig.Q(5, LogicalKey.Pause, true, 200));
            // 물리 키 상태는 누른 그대로(Up, Down). 버린 아홉 번째 키(Up 누름)는 상태를 바꾸지 않으니 보정할 것이 없다
            rig.Client.OnLocalInput(keys.ToArray(), Bit(LogicalKey.Up) | Bit(LogicalKey.Down));
            Assert.Equal(8u, rig.Client.LastAssignedSeq);
            ClientInputPacket? p = rig.Outgoing();
            Assert.Equal(8, p!.EventCount);
            for (int i = 0; i < 8; i++)
            {
                Assert.Equal((uint)(i + 1), p.Events[i].Seq);
            }
        }

        // 번호 전에 버린 아홉 번째 키가 뗌이면 서버가 볼 키 상태(Fire 눌림)와 물리 키 상태(Fire 뗌)가 어긋난다.
        // 같은 호출에서 새 순번의 보정 이벤트를 내고, 찬 틱 5 를 넘겨 틱 6 처음에 둔다(CONTRACTS 3.1, 6.4)
        [Fact]
        public void NinthKeyDroppedBeforeNumbering_IsCorrected_AndServerKeysMatchPhysical()
        {
            const int d = 2;
            var rig = new ClientRig(me: 0, inputDelay: d);
            for (uint n = 0; n <= 3; n++)
            {
                rig.Client.Advance(n);
            }
            while (rig.Outgoing() != null)
            {
            }
            var keys = new List<QuantizedKey>();
            for (int k = 0; k <= (int)LogicalKey.Reverse; k++)
            {
                keys.Add(ClientRig.Q(5, (LogicalKey)k, true, (byte)(k * 10)));
            }
            keys.Add(ClientRig.Q(5, LogicalKey.Fire, false, 200));
            KeyMask physical = (KeyMask)(AllDuelKeys & ~(int)Bit(LogicalKey.Fire));
            rig.Client.OnLocalInput(keys.ToArray(), physical);

            Assert.Equal(9u, rig.Client.LastAssignedSeq);           // 8개 + 보정 1개. 버린 키는 번호가 없다
            ClientInputPacket? p = rig.Outgoing();
            Assert.Equal(9, p!.EventCount);
            InputEvent fix = p.Events[8];
            Assert.Equal(9u, fix.Seq);
            Assert.Equal((LogicalKey.Fire, false), (fix.Key, fix.Down));
            Assert.Equal(6u, fix.Tick);                              // 틱 5 는 찼다. 다음 틱 처음
            Assert.Equal((byte)0, fix.Offset);
            Assert.True(fix.Tick - 5u <= 2u);                        // 버린 키의 틱에서 2틱 안

            // 서버가 전부 받는다 → 틱 6 에서 서버의 KeyState = 물리 키 상태
            var ticks = new List<ConfirmedTick>();
            for (uint t = 0; t <= 6; t++)
            {
                InputEvent[]? slot0 = null;
                if (t == 5)
                {
                    slot0 = p.Events.Slice(0, 8).ToArray();
                }
                else if (t == 6)
                {
                    slot0 = new[] { fix };
                }
                ticks.Add(rig.StepServer(slot0: slot0));
            }
            Assert.Equal(physical, rig.Server.View.Player(new PlayerSlot(0)).Keys);
            rig.Deliver(rig.ServerTick(ticks));
            Assert.Equal(ticks[ticks.Count - 1].Hash, rig.Client.ConfirmedHash(6));
            rig.Client.Advance(4);
            rig.Client.OnLocalInput(ReadOnlySpan<QuantizedKey>.Empty, physical);
            Assert.Equal(9u, rig.Client.LastAssignedSeq);           // 맞았으니 더 고칠 것이 없다
            Assert.Equal(physical, rig.Client.View.Player(new PlayerSlot(0)).Keys);
        }

        // 시각을 직전 이벤트로 올린 키가 찬 틱에 걸려 버려져도 같다(번호 전 버림 → 보정)
        [Fact]
        public void KeyRaisedIntoAFullTick_IsDroppedAndCorrected()
        {
            var rig = new ClientRig(me: 0, inputDelay: 2);
            rig.Client.Advance(0);
            var keys = new List<QuantizedKey>();
            for (int i = 0; i < 8; i++)
            {
                keys.Add(ClientRig.Q(5, (i & 1) == 0 ? LogicalKey.Skill : LogicalKey.Stance, true, (byte)(i * 10)));
            }
            KeyMask held = Bit(LogicalKey.Skill) | Bit(LogicalKey.Stance);
            rig.Client.OnLocalInput(keys.ToArray(), held);
            Assert.Equal(8u, rig.Client.LastAssignedSeq);

            // 입력 시계 보정으로 뒤로 간 시각(틱 4) → 틱 5 의 마지막 시각으로 올라가고, 틱 5 는 찼다
            rig.Client.OnLocalInput(new[] { ClientRig.Q(4, LogicalKey.Skill, false, 30) }, Bit(LogicalKey.Stance));
            Assert.Equal(9u, rig.Client.LastAssignedSeq);
            ClientInputPacket? p = rig.Outgoing();
            InputEvent fix = p!.Events[p.EventCount - 1];
            Assert.Equal(9u, fix.Seq);
            Assert.Equal((LogicalKey.Skill, false), (fix.Key, fix.Down));
            Assert.Equal(6u, fix.Tick);
            Assert.True(fix.Time > p.Events[p.EventCount - 2].Time);
        }

        // 무작위: 한 틱에 키를 몰아 넣어(아홉 번째 버림, 시각 올림) 서버가 일부를 버려도, 입력이 멈추면 서버·확정·예측의 내 키 상태가
        // 물리 키 상태로 모인다. 버림 뒤 어긋난 채 남는 경우가 없다
        [Fact]
        public void Randomized_BurstsAndDrops_KeyStatesConvergeToPhysical()
        {
            const int d = 2;
            const uint activeTicks = 40;
            const uint quietTicks = 12;
            for (int seed = 1; seed <= 400; seed++)
            {
                var rnd = new Random(seed);
                var rig = new ClientRig(me: 0, inputDelay: d);
                var received = new SortedDictionary<uint, InputEvent>();
                var lost = new HashSet<uint>();
                int physical = 0;
                for (uint n = 0; n < activeTicks + quietTicks; n++)
                {
                    rig.Client.Advance(n);
                    if (n < activeTicks && rnd.Next(3) != 0)
                    {
                        int count = rnd.Next(1, 13);
                        var burst = new QuantizedKey[count];
                        uint tick = n + d - (uint)(rnd.Next(5) == 0 ? 1 : 0);
                        int offset = 0;
                        for (int i = 0; i < count; i++)
                        {
                            offset += rnd.Next(0, 20);
                            var key = (LogicalKey)rnd.Next((int)LogicalKey.Reverse + 1);
                            bool down = rnd.Next(2) == 0;
                            burst[i] = ClientRig.Q(tick, key, down, (byte)Math.Min(offset, 255));
                            physical = down ? physical | (1 << (int)key) : physical & ~(1 << (int)key);
                        }
                        rig.Client.OnLocalInput(burst, (KeyMask)physical);
                    }
                    else
                    {
                        rig.Client.OnLocalInput(ReadOnlySpan<QuantizedKey>.Empty, (KeyMask)physical);
                    }

                    ClientInputPacket? p;
                    while ((p = rig.Outgoing()) != null)
                    {
                        for (int i = 0; i < p.EventCount; i++)
                        {
                            InputEvent e = p.Events[i];
                            // 서버의 버림: 늦음(이미 확정된 틱), 그리고 무작위 유실(처음 볼 때 열에 하나쯤 버리고 다시 받지 않는다)
                            if (e.Tick < rig.Server.Tick || received.ContainsKey(e.Seq) || lost.Contains(e.Seq))
                            {
                                continue;
                            }
                            if (n < activeTicks && rnd.Next(10) == 0)
                            {
                                lost.Add(e.Seq);
                                continue;
                            }
                            received[e.Seq] = e;
                        }
                    }

                    // 서버는 n 까지 확정한다(입력 지연 D 만큼 여유)
                    var ticks = new List<ConfirmedTick>();
                    while (rig.Server.Tick <= n)
                    {
                        uint t = rig.Server.Tick;
                        var slot0 = new List<InputEvent>();
                        foreach (InputEvent e in received.Values)
                        {
                            if (e.Tick == t)
                            {
                                slot0.Add(e);
                            }
                        }
                        ticks.Add(rig.StepServer(slot0: slot0.ToArray()));
                    }
                    rig.DeliverInChunks(ticks);
                }

                int serverKeys = (int)rig.Server.View.Player(new PlayerSlot(0)).Keys & AllDuelKeys;
                Assert.True(serverKeys == physical, $"seed {seed}: server 0x{serverKeys:X2} physical 0x{physical:X2}");
                Assert.Equal((KeyMask)physical, rig.Client.View.Player(new PlayerSlot(0)).Keys);
            }
        }

        // 입력 시계 보정으로 양자화 결과가 뒤로 가도 한 패킷 안의 시각은 역행하지 않는다(역행은 패킷 거부, CONTRACTS 10.6 (0))
        [Fact]
        public void KeyTimeNeverGoesBackwards()
        {
            var rig = new ClientRig(me: 0);
            rig.Client.Advance(0);
            rig.Client.OnLocalInput(new[] { ClientRig.Q(6, LogicalKey.Up, true, 100) }, Bit(LogicalKey.Up));
            rig.Client.OnLocalInput(new[] { ClientRig.Q(6, LogicalKey.Up, false, 50) }, KeyMask.None);
            ClientInputPacket? p = rig.Outgoing();
            Assert.Equal(2, p!.EventCount);
            Assert.True(p.Events[1].Time >= p.Events[0].Time);
            Assert.Equal(p.Events[0].Time, p.Events[1].Time);
        }

        [Fact]
        public void EveryNewInputClockTickMakesOnePacket_PacketNumbersNeverRepeat()
        {
            var rig = new ClientRig(me: 0);
            var numbers = new List<ulong>();
            byte[] buffer = new byte[DuelWire.MaxPacketBytes];
            for (uint n = 0; n < 20; n++)
            {
                rig.Client.Advance(n);
                int len = rig.Client.PollOutgoing(buffer, rig.NowUs);
                Assert.True(len > 0);
                rig.Open(buffer.AsSpan(0, len).ToArray(), out DuelPacketHeader h);   // Open 이 번호 재사용을 막는다
                numbers.Add(h.PacketNumber);
                Assert.Equal(0, rig.Client.PollOutgoing(buffer, rig.NowUs));
            }
            for (int i = 0; i < numbers.Count; i++)
            {
                Assert.Equal((ulong)(i + 1), numbers[i]);
            }
        }

        [Fact]
        public void TooSmallBuffer_KeepsThePacketAndDoesNotSpendANumber()
        {
            var rig = new ClientRig(me: 0);
            rig.Client.Advance(0);
            Assert.Equal(0, rig.Client.PollOutgoing(new byte[10], rig.NowUs));
            byte[] buffer = new byte[DuelWire.MaxPacketBytes];
            int len = rig.Client.PollOutgoing(buffer, rig.NowUs);
            Assert.True(len > 0);
            rig.Open(buffer.AsSpan(0, len).ToArray(), out DuelPacketHeader h);
            Assert.Equal(1UL, h.PacketNumber);
        }

        // 뗌 이벤트 폐기 뒤 2틱 안에 서버와 클라이언트의 KeyState 가 같다. 보정은 새 순번이고 직전 이벤트보다 앞서지 않는다
        [Fact]
        public void DroppedRelease_IsCorrectedWithNewSeq_AndKeyStatesAgreeWithinTwoTicks()
        {
            const int d = 2;
            var rig = new ClientRig(me: 0, inputDelay: d);
            for (uint n = 0; n <= 10; n++)
            {
                rig.Client.Advance(n);
            }
            rig.Client.OnLocalInput(new[] { ClientRig.Q(12, LogicalKey.Fire, true) }, Bit(LogicalKey.Fire));
            rig.Client.Advance(11);
            rig.Client.Advance(12);
            rig.Client.OnLocalInput(new[] { ClientRig.Q(14, LogicalKey.Fire, false, 40) }, KeyMask.None);
            for (uint n = 13; n <= 16; n++)
            {
                rig.Client.Advance(n);
            }
            while (rig.Outgoing() != null)
            {
            }

            // 서버: 누름(순번 1)은 받고 뗌(순번 2)은 늦음으로 버렸다
            var ticks = new List<ConfirmedTick>();
            for (uint t = 0; t <= 14; t++)
            {
                ticks.Add(t == 12 ? rig.StepServer(slot0: new[] { ClientRig.Ev(12, LogicalKey.Fire, true, 1) }) : rig.StepServer());
            }
            const uint detectedAt = 16;
            rig.Deliver(rig.ServerTick(ticks));

            ClientInputPacket? p = rig.Outgoing();
            Assert.NotNull(p);
            InputEvent fix = p!.Events[p.EventCount - 1];
            Assert.Equal(3u, fix.Seq);                               // 새 순번(버려진 2 를 되살리지 않는다)
            Assert.Equal(LogicalKey.Fire, fix.Key);
            Assert.False(fix.Down);
            Assert.Equal(detectedAt + d, fix.Tick);                  // 만드는 순간의 입력 시계 + D
            Assert.True(fix.Time > SimTimeOf(14, 40));               // 직전 이벤트(14, 40)보다 앞서지 않는다

            // 서버가 보정을 받는다 → 그 틱에서 서버의 KeyState = 물리 키 상태(뗌)
            var more = new List<ConfirmedTick>();
            for (uint t = 15; t <= fix.Tick; t++)
            {
                more.Add(t == fix.Tick ? rig.StepServer(slot0: new[] { fix }) : rig.StepServer());
            }
            Assert.True(fix.Tick - detectedAt <= 2);
            Assert.Equal(KeyMask.None, rig.Server.View.Player(new PlayerSlot(0)).Keys);
            rig.Deliver(rig.ServerTick(more));
            Assert.Equal(more[more.Count - 1].Hash, rig.Client.ConfirmedHash(fix.Tick));
            Assert.Equal(3u, rig.Client.LastAssignedSeq);           // 보정이 받아들여졌으니 더 고칠 것이 없다
        }

        [Fact]
        public void Correction_IsRaisedAfterTheLastOwnEvent()
        {
            const int d = 2;
            var rig = new ClientRig(me: 0, inputDelay: d);
            for (uint n = 0; n <= 10; n++)
            {
                rig.Client.Advance(n);
            }
            rig.Client.OnLocalInput(new[] { ClientRig.Q(12, LogicalKey.Stance, true) }, Bit(LogicalKey.Stance));
            for (uint n = 11; n <= 14; n++)
            {
                rig.Client.Advance(n);
            }
            // 지금 막 누른 키의 시각이 입력 시계 + D 보다 뒤에 있다(16, 200)
            rig.Client.OnLocalInput(new[] { ClientRig.Q(16, LogicalKey.Up, true, 200) }, Bit(LogicalKey.Stance) | Bit(LogicalKey.Up));
            while (rig.Outgoing() != null)
            {
            }
            var ticks = new List<ConfirmedTick>();
            for (uint t = 0; t <= 12; t++)
            {
                ticks.Add(rig.StepServer());                          // 자세 누름(순번 1)이 버려졌다
            }
            rig.Deliver(rig.ServerTick(ticks));
            ClientInputPacket? p = rig.Outgoing();
            InputEvent fix = p!.Events[p.EventCount - 1];
            Assert.Equal(3u, fix.Seq);
            Assert.Equal(LogicalKey.Stance, fix.Key);
            Assert.True(fix.Down);
            Assert.Equal(SimTimeOf(16, 201), fix.Time);              // 직전 이벤트 (16, 200) 바로 뒤
        }

        // 재접속: resume.next_seq 부터 번호를 매기고 resume.key_state 와 물리 키 상태의 차이를 보정 이벤트로 낸다(CONTRACTS 10.1)
        [Fact]
        public void Resume_ContinuesSeqAndCorrectsServerKeyState()
        {
            var resume = new DuelResume { ConfirmedTick = 40, NextSeq = 10, KeyState = (ushort)Bit(LogicalKey.Fire) };
            var rig = new ClientRig(me: 1, resume: resume);
            rig.Client.Advance(44);
            rig.Client.OnLocalInput(System.ReadOnlySpan<QuantizedKey>.Empty, Bit(LogicalKey.Up));
            ClientInputPacket? p = rig.Outgoing();
            Assert.Equal(2, p!.EventCount);
            Assert.Equal(new[] { 10u, 11u }, new[] { p.Events[0].Seq, p.Events[1].Seq });
            Assert.Equal((LogicalKey.Up, true), (p.Events[0].Key, p.Events[0].Down));     // 방향(분류 1)이 J(분류 2)보다 먼저
            Assert.Equal((LogicalKey.Fire, false), (p.Events[1].Key, p.Events[1].Down));
            Assert.Equal(46u, p.Events[0].Tick);
            Assert.Equal(9u, p.BaseSeq);
        }

        [Fact]
        public void Resume_WithMatchingPhysicalKeys_SendsNothingExtra()
        {
            var resume = new DuelResume { ConfirmedTick = 40, NextSeq = 10, KeyState = (ushort)Bit(LogicalKey.Fire) };
            var rig = new ClientRig(me: 1, resume: resume);
            rig.Client.Advance(44);
            rig.Client.OnLocalInput(System.ReadOnlySpan<QuantizedKey>.Empty, Bit(LogicalKey.Fire));
            ClientInputPacket? p = rig.Outgoing();
            Assert.Equal(0, p!.EventCount);
            Assert.Equal(9u, p.BaseSeq);
        }

        private static long SimTimeOf(uint tick, byte offset) => (long)tick * 256 + offset;
    }
}

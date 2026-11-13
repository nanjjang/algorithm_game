using System;
using System.Collections.Generic;
using Wizard.Netcode.Clock;
using Wizard.Netcode.Duel;
using Wizard.Protocol.Duel;
using Xunit;

namespace Wizard.Netcode.Tests.Duel.Client
{
    // DuelClientCore.DrainClockSamples(T-62 9번, CONTRACTS 10.4 "경기 중에는 ServerTick 의 시각 칸으로 1초마다 다시 잰다").
    // 받은 ServerTick 의 echo_client_send_time_ms 가 내가 기억한 최근 보낸 시각과 맞으면 표본 하나:
    // T0 = 그 패킷을 보낸 시각(µs), Server = server_time_ms × 1,000, T1 = 그 ServerTick 이 닿은 시각.
    public sealed class DuelClientClockTests
    {
        private const long UsPerMs = 1000L;

        // 입력 시계를 한 틱 나아가게 하고 그 틱의 패킷을 NowUs 에 보낸다. 보낸 패킷의 client_send_time_ms 를 돌려준다
        private static uint SendAt(ClientRig rig, uint tick, long nowUs)
        {
            rig.NowUs = nowUs;
            rig.Client.Advance(tick);
            ClientInputPacket? p = rig.Outgoing();
            Assert.NotNull(p);
            return p!.ClientSendTimeMs;
        }

        private static void ReceiveAt(ClientRig rig, long nowUs, uint serverTimeMs, uint echoMs)
        {
            rig.NowUs = nowUs;
            rig.Deliver(rig.ServerTick(new List<ConfirmedTick>(), serverTimeMs: serverTimeMs, echoClientSendTimeMs: echoMs));
        }

        private static List<ClockSample> DrainAll(ClientRig rig)
        {
            var list = new List<ClockSample>();
            var buffer = new ClockSample[4];
            int n;
            while ((n = rig.Client.DrainClockSamples(buffer)) > 0)
            {
                for (int i = 0; i < n; i++)
                {
                    list.Add(buffer[i]);
                }
            }
            return list;
        }

        [Fact]
        public void MatchingEcho_MakesOneSampleFromRememberedSendTime()
        {
            var rig = new ClientRig(me: 0);
            uint sentMs = SendAt(rig, 0, 1_234_567L);
            Assert.Equal(1234u, sentMs);                 // 10.2 client_send_time_ms = 보낸 시각 / 1,000 내림
            ReceiveAt(rig, 1_300_250L, serverTimeMs: 500U, echoMs: sentMs);

            var buffer = new ClockSample[4];
            Assert.Equal(1, rig.Client.DrainClockSamples(buffer));
            Assert.Equal(1_234_567L, buffer[0].T0Us);    // ms 로 줄인 값이 아니라 기억한 µs
            Assert.Equal(500_000L, buffer[0].ServerUs);  // server_time_ms × 1,000
            Assert.Equal(1_300_250L, buffer[0].T1Us);    // OnPacket 의 clientTimeUs
            Assert.Equal(0, rig.Client.DrainClockSamples(buffer));
        }

        [Fact]
        public void NoMatch_MakesNoSample()
        {
            var rig = new ClientRig(me: 0);
            // 아직 아무것도 보내지 않았다: 서버가 돌려준 값은 아무 패킷의 것도 아니다
            ReceiveAt(rig, 900_000L, serverTimeMs: 10U, echoMs: 0U);
            ReceiveAt(rig, 910_000L, serverTimeMs: 11U, echoMs: 905U);
            uint sentMs = SendAt(rig, 0, 1_000_000L);
            // 서버가 아직 내 패킷을 받지 못했다(그 전의 값을 돌려준다)
            ReceiveAt(rig, 1_010_000L, serverTimeMs: 12U, echoMs: sentMs - 1U);
            ReceiveAt(rig, 1_020_000L, serverTimeMs: 13U, echoMs: sentMs + 1U);
            Assert.Empty(DrainAll(rig));
        }

        [Fact]
        public void SameSendTime_NeverMakesTwoSamples()
        {
            // 서버는 새 패킷을 받을 때까지 같은 보낸 시각을 ServerTick 마다 되풀이해 돌려준다
            var rig = new ClientRig(me: 0);
            uint a = SendAt(rig, 0, 2_000_000L);
            ReceiveAt(rig, 2_050_000L, 100U, a);
            ReceiveAt(rig, 2_066_000L, 116U, a);
            ReceiveAt(rig, 2_083_000L, 133U, a);
            uint b = SendAt(rig, 1, 2_090_000L);
            ReceiveAt(rig, 2_140_000L, 183U, b);
            ReceiveAt(rig, 2_150_000L, 193U, a);         // 늦게 온 옛 값도 다시 쓰지 않는다
            List<ClockSample> s = DrainAll(rig);
            Assert.Equal(2, s.Count);
            Assert.Equal(2_000_000L, s[0].T0Us);
            Assert.Equal(2_050_000L, s[0].T1Us);
            Assert.Equal(2_090_000L, s[1].T0Us);
            Assert.Equal(2_140_000L, s[1].T1Us);
        }

        [Fact]
        public void OlderRememberedSendTime_StillMatches()
        {
            // 서버가 받은 마지막 패킷이 내가 보낸 마지막 패킷이 아닐 수 있다(왕복 동안 여러 패킷이 나간다)
            var rig = new ClientRig(me: 0);
            uint first = SendAt(rig, 0, 3_000_000L);
            for (uint t = 1; t <= 5; t++)
            {
                SendAt(rig, t, 3_000_000L + t * 16_667L);
            }
            ReceiveAt(rig, 3_100_000L, 40U, first);
            List<ClockSample> s = DrainAll(rig);
            Assert.Single(s);
            Assert.Equal(3_000_000L, s[0].T0Us);
        }

        [Fact]
        public void TooOldSendTime_IsForgotten()
        {
            // 기억은 최근 보낸 시각 몇십 개뿐이다(약 1초). 그보다 오래된 값이 돌아오면 표본을 만들지 않는다
            var rig = new ClientRig(me: 0);
            uint first = SendAt(rig, 0, 4_000_000L);
            for (uint t = 1; t <= 200; t++)
            {
                SendAt(rig, t, 4_000_000L + t * 16_667L);
            }
            ReceiveAt(rig, 8_000_000L, 4000U, first);
            Assert.Empty(DrainAll(rig));
        }

        [Fact]
        public void TwoSendsInOneMs_UseTheEarlierSendTime()
        {
            // 같은 ms 에 두 패킷이 나가면 서버가 돌려준 ms 가 어느 쪽인지 가를 수 없다. 먼저 보낸 시각을 쓴다(왕복이 작게 잡히지 않는다)
            var rig = new ClientRig(me: 0);
            uint a = SendAt(rig, 0, 5_000_100L);
            uint b = SendAt(rig, 1, 5_000_900L);
            Assert.Equal(a, b);
            ReceiveAt(rig, 5_060_000L, 70U, a);
            List<ClockSample> s = DrainAll(rig);
            Assert.Single(s);
            Assert.Equal(5_000_100L, s[0].T0Us);
        }

        [Fact]
        public void UndrainedSamples_DropOldestBeyondClockSyncSamples()
        {
            var rig = new ClientRig(me: 0);
            int capacity = ClientRig.Db.Net.ClockSyncSamples;
            Assert.Equal(16, capacity);                  // spec 10 2.8 clock_sync_samples
            int made = capacity + 3;
            for (int i = 0; i < made; i++)
            {
                long sendUs = 6_000_000L + i * 20_000L;
                uint sent = SendAt(rig, (uint)i, sendUs);
                ReceiveAt(rig, sendUs + 10_000L, (uint)(100 + i), sent);
            }
            List<ClockSample> s = DrainAll(rig);
            Assert.Equal(capacity, s.Count);
            for (int k = 0; k < capacity; k++)
            {
                int i = k + 3;                            // 가장 오래된 셋이 버려졌다
                Assert.Equal(6_000_000L + i * 20_000L, s[k].T0Us);
                Assert.Equal((100L + i) * UsPerMs, s[k].ServerUs);
            }
        }

        [Fact]
        public void SmallBuffer_LeavesTheRestForTheNextDrain()
        {
            var rig = new ClientRig(me: 0);
            for (int i = 0; i < 3; i++)
            {
                long sendUs = 7_000_000L + i * 20_000L;
                uint sent = SendAt(rig, (uint)i, sendUs);
                ReceiveAt(rig, sendUs + 5_000L, (uint)i, sent);
            }
            var one = new ClockSample[1];
            Assert.Equal(1, rig.Client.DrainClockSamples(one));
            Assert.Equal(7_000_000L, one[0].T0Us);
            Assert.Equal(0, rig.Client.DrainClockSamples(Span<ClockSample>.Empty));
            Assert.Equal(1, rig.Client.DrainClockSamples(one));
            Assert.Equal(7_020_000L, one[0].T0Us);
            Assert.Equal(1, rig.Client.DrainClockSamples(one));
            Assert.Equal(7_040_000L, one[0].T0Us);
            Assert.Equal(0, rig.Client.DrainClockSamples(one));
        }

        [Fact]
        public void UnsentPacket_IsNotRemembered()
        {
            // 버퍼가 모자라 나가지 못한 패킷의 시각은 기억하지 않는다(서버가 그 값을 돌려줄 수 없다)
            var rig = new ClientRig(me: 0);
            rig.NowUs = 8_000_000L;
            rig.Client.Advance(0);
            Assert.Equal(0, rig.Client.PollOutgoing(new byte[4], rig.NowUs));
            ReceiveAt(rig, 8_050_000L, 1U, 8000U);
            Assert.Empty(DrainAll(rig));
        }

        [Fact]
        public void ReplayedServerTick_MakesNoSecondSample()
        {
            var rig = new ClientRig(me: 0);
            uint sent = SendAt(rig, 0, 9_000_000L);
            rig.NowUs = 9_040_000L;
            byte[] packet = rig.ServerTick(new List<ConfirmedTick>(), serverTimeMs: 1U, echoClientSendTimeMs: sent);
            rig.Deliver(packet);
            rig.NowUs = 9_041_000L;
            rig.Deliver(packet);
            Assert.Single(DrainAll(rig));
        }

        [Fact]
        public void SameCalls_SameSamples()
        {
            List<ClockSample> Run()
            {
                var rig = new ClientRig(me: 0);
                for (int i = 0; i < 40; i++)
                {
                    long sendUs = 10_000_000L + i * 16_667L;
                    uint sent = SendAt(rig, (uint)i, sendUs);
                    if (i % 3 != 2)
                    {
                        ReceiveAt(rig, sendUs + 30_000L + i * 7L, (uint)(1000 + i * 17), sent);
                    }
                }
                return DrainAll(rig);
            }
            List<ClockSample> a = Run();
            List<ClockSample> b = Run();
            Assert.Equal(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.Equal(a[i].T0Us, b[i].T0Us);
                Assert.Equal(a[i].ServerUs, b[i].ServerUs);
                Assert.Equal(a[i].T1Us, b[i].T1Us);
            }
        }

        [Fact]
        public void AdvancePollAndOnPacket_DoNotAllocate()
        {
            // Step·Advance·OnPacket·PollOutgoing 은 힙에 할당하지 않는다(CLAUDE.md 3절, CCR-1 수락 조건). 패킷은 미리 봉인해 둔다
            var rig = new ClientRig(me: 0);
            const int rounds = 64;
            var packets = new byte[rounds * 2][];
            for (int i = 0; i < packets.Length; i++)
            {
                // 틱마다 확정 하나를 실어 확정 세계와 예측 세계가 계속 Step 하게 한다(멈춤으로 빠지지 않는다)
                packets[i] = rig.ServerTick(new[] { rig.StepServer() }, serverTimeMs: (uint)i, echoClientSendTimeMs: (uint)(20_000 + i));
            }
            var outBuffer = new byte[DuelWire.MaxPacketBytes];
            var samples = new ClockSample[4];
            void Round(int i)
            {
                long now = (20_000L + i) * UsPerMs;
                rig.Client.Advance((uint)i);
                rig.Client.PollOutgoing(outBuffer, now);
                rig.Client.OnPacket(packets[i], now + 500L);
                rig.Client.DrainClockSamples(samples);
            }
            for (int i = 0; i < rounds; i++)
            {
                Round(i);                                 // 데우기(JIT)
            }
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = rounds; i < rounds * 2; i++)
            {
                Round(i);
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Equal(0L, allocated);
        }
    }
}

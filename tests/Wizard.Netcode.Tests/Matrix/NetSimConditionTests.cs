using System;
using System.Collections.Generic;
using System.Linq;
using Wizard.Core;
using Wizard.Netcode.Duel;
using Wizard.Netcode.Tests.Duel.Host;
using Wizard.Netcode.Tests.NetSim;
using Wizard.Sim;
using Wizard.Sim.Input;
using Xunit;
using Xunit.Abstractions;

namespace Wizard.Netcode.Tests.Matrix
{
    // NetSim 의 유실 모형(연속 유실, 수신 공백, 순서 바뀜, 중복)과 그 조건에서 넷코드가 지켜야 하는 성질(spec 10 14.7·14.8, CONTRACTS 6.4 "시계 둘")
    public sealed class NetSimConditionTests
    {
        private readonly ITestOutputHelper _out;

        public NetSimConditionTests(ITestOutputHelper output)
        {
            _out = output;
        }

        private static long Ms(long ms) => ms * 1000L;

        // ── 300ms 수신 공백(클라이언트 0 이 아무것도 받지 못한다) 뒤 재개: 예측은 한도에서 멈추고(Stalled) 기다렸다가 한 번에 따라잡는다.
        //    전체 상태를 청하지 않고, 해시 불일치로 세지 않고, 공백 중·재개 직후의 입력은 하나도 버려지지 않는다(입력 시계는 멈추지 않는다)
        [Fact]
        public void ReceiveGapOf300msStallsThenCatchesUpWithoutFullStateOrDrops()
        {
            NetSimMatch m = NetSimMatch.Symmetric(0x6761_7031UL, 60, 0, 0);
            long gapFrom = HostKit.TickStartUs(120) + Ms(3);
            long gapTo = gapFrom + Ms(300);
            m.Down[0].Profile.ReceiveGaps.Add((gapFrom, gapTo));
            m.Keys = (slot, n, rng) => n >= 100 && n < 200 && slot == 0 ? NetSimMatch.RandomKeys(rng, 2) : null;
            m.Run(260);

            MatrixChecks.All(m, 240);
            ClientTrace t = m.Trace[0];
            int limit = HostKit.Db.Net.RollbackLimitTicks;
            var during = t.Advances.Where(a => HostKit.TickStartUs(a.inputClock) >= gapFrom && HostKit.TickStartUs(a.inputClock) < gapTo).ToList();
            Assert.Contains(during, a => a.advance.Stalled);
            Assert.All(during, a => Assert.True((long)a.advance.PredictedTick - a.advance.ConfirmedTick <= limit));
            // 재개 뒤: 한 번의 Advance 에서 여러 틱을 돌려 따라잡고(1틱씩이 아니다), 곧 예측이 입력 시계에 다시 붙는다
            var after = t.Advances.Where(a => HostKit.TickStartUs(a.inputClock) >= gapTo).ToList();
            Assert.Contains(after.Take(10), a => a.advance.TicksSimulated > 1);
            Assert.Contains(after.Take(10), a => !a.advance.Stalled && a.advance.PredictedTick == a.inputClock + 1U);
            Assert.True(after.Skip(10).All(a => !a.advance.Stalled));
            Assert.Equal(0, m.Clients[0].HashMismatchCount);   // 멈춤은 해시 불일치 통계에 들어가지 않는다
            Assert.False(t.EverNeedsFullState);
            Assert.True(t.Pressed.Count(p => p.atUs >= gapTo && p.atUs < gapTo + Ms(200)) > 0);
            Assert.Equal(0, MatrixChecks.DroppedPresses(m, 0));
            DuelNetStats st = m.Host.StatsFor(HostKit.S0);
            Assert.Equal(0L, st.EventsLate);
            Assert.Equal(0L, st.SeqsLost);
        }

        // ── 호스트 쪽 300ms 수신 공백(클라이언트 0 의 패킷이 호스트에 닿지 않는다). 공백 안에서 8틱 창 밖으로 밀려난 키는 유실·늦음이지만
        //    몰수 계수는 0 이고, 재개 뒤에 누른 키는 하나도 버려지지 않으며 전체 상태를 청하지 않는다
        [Fact]
        public void HostReceiveGapOf300msLosesOnlyKeysInsideTheGap()
        {
            NetSimMatch m = NetSimMatch.Symmetric(0x6761_7032UL, 60, 0, 0);
            long gapFrom = HostKit.TickStartUs(120) + Ms(3);
            long gapTo = gapFrom + Ms(300);
            m.Up[0].Profile.ReceiveGaps.Add((gapFrom, gapTo));
            m.Keys = (slot, n, rng) => n >= 100 && n < 220 && slot == 0 ? NetSimMatch.RandomKeys(rng, 2) : null;
            m.Run(300);

            MatrixChecks.All(m, 280);
            Assert.True(MatrixChecks.DroppedPresses(m, 0) > 0, "the gap must cost the keys pressed inside it (otherwise the gap did not happen)");
            Assert.Equal(0, MatrixChecks.DroppedPresses(m, 0, gapTo));
            Assert.True(m.Trace[0].Pressed.Count(p => p.atUs >= gapTo) > 0);
            Assert.Equal(m.Trace[0].Physical, ConfirmedKeys(m, 0));   // 버려진 키는 보정 이벤트로 맞춰졌다
        }

        // ── 200ms 연속 유실(클라이언트 0 이 보낸 패킷이 200ms 동안 전부 사라진다) 중에 틱마다 키를 누른다: 몰수 0, 그 뒤 입력은 정상 수용
        [Fact]
        public void BurstLossOf200msWhilePressingKeysNeverForfeitsAndLaterKeysAreAccepted()
        {
            NetSimMatch m = NetSimMatch.Symmetric(0x6275_7273UL, 60, 0, 0);
            long from = HostKit.TickStartUs(100) + Ms(1);
            long to = from + Ms(200);
            m.Up[0].Profile.SendLoss.Add((from, to));
            m.Keys = (slot, n, rng) => slot == 0 && n >= 90 && n < 160
                ? new List<(byte, LogicalKey)> { ((byte)rng.Below(256), (LogicalKey)(n % 4)) }
                : null;
            m.Run(240);

            MatrixChecks.All(m, 220);
            DuelNetStats st = m.Host.StatsFor(HostKit.S0);
            Assert.Equal(0L, st.OrderViolations);
            Assert.True(st.OrderViolations < HostKit.Db.Net.OrderViolationForfeit);
            Assert.True(st.SeqsLost + st.EventsLate > 0, "the burst must have cost something");
            Assert.True(m.Trace[0].Pressed.Count(p => p.atUs >= to) > 10);
            Assert.Equal(0, MatrixChecks.DroppedPresses(m, 0, to));
            Assert.Equal(m.Trace[0].Physical, ConfirmedKeys(m, 0));
        }

        // ── 두 상태(Gilbert-Elliott) 연속 유실: 이어 잃는 길이가 독립 유실보다 길다. 그래도 확정 해시 열은 호스트와 같다
        [Fact]
        public void GilbertElliottBurstLossProducesRunsAndHashesStillMatch()
        {
            var up = new LinkProfile[2];
            var down = new LinkProfile[2];
            for (int s = 0; s < 2; s++)
            {
                up[s] = new LinkProfile { BaseUs = Ms(30), JitterUs = Ms(10), BurstEnterPpm = 20000, BurstExitPpm = 250000 };
                down[s] = new LinkProfile { BaseUs = Ms(30), JitterUs = Ms(10), BurstEnterPpm = 20000, BurstExitPpm = 250000 };
            }
            var m = new NetSimMatch(0x6765_6C6CUL, up, down);
            m.Keys = (slot, n, rng) => n < 600 ? NetSimMatch.RandomKeys(rng, 4) : null;
            m.Run(660);

            MatrixChecks.All(m, 600);
            var runs = m.Up.Concat(m.Down).SelectMany(l => l.LossRuns).ToList();
            Assert.True(runs.Count > 10);
            Assert.True(runs.Max() >= 4, $"longest run {runs.Max()}");
            Assert.True(runs.Average() > 2.0, $"mean run {runs.Average()}");   // 평균 = 1 / 0.25 = 4 가까이(독립 유실이면 1 에 가깝다)
            _out.WriteLine($"loss runs: count {runs.Count}, mean {runs.Average():F2}, max {runs.Max()}");
        }

        // ── 순서 바뀜과 중복: 호스트는 같은 번호를 두 번 받지 않고(재전송 창) 클라이언트도 마찬가지다. 확정 해시 열은 같다
        [Fact]
        public void ReorderAndDuplicationKeepHashesAndAreRejectedByReplayWindow()
        {
            var up = new LinkProfile[2];
            var down = new LinkProfile[2];
            for (int s = 0; s < 2; s++)
            {
                up[s] = new LinkProfile { BaseUs = Ms(40), JitterUs = Ms(5), ReorderPpm = 100000, ReorderExtraUs = Ms(40), DuplicatePpm = 50000, DuplicateExtraUs = Ms(20) };
                down[s] = new LinkProfile { BaseUs = Ms(40), JitterUs = Ms(5), ReorderPpm = 100000, ReorderExtraUs = Ms(40), DuplicatePpm = 50000, DuplicateExtraUs = Ms(20) };
            }
            var m = new NetSimMatch(0x726F_6475UL, up, down);
            m.Keys = (slot, n, rng) => n < 600 ? NetSimMatch.RandomKeys(rng, 4) : null;
            m.Run(660);

            MatrixChecks.All(m, 600);
            for (int s = 0; s < 2; s++)
            {
                Assert.True(m.Up[s].Reordered > 0 && m.Up[s].Duplicated > 0 && m.Down[s].Duplicated > 0);
                DuelNetStats st = m.Host.StatsFor(new PlayerSlot((byte)s));
                // 닿은 것 가운데 같은 바이트의 두 번째 이후만 정확히 버렸다(순서가 바뀐 것은 받는다). 끝날 때 아직 날고 있던 중복은 세지 않는다
                Assert.Equal(m.HostDeliveries[s] - m.HostDistinct[s].Count, st.PacketsReplayed);
                Assert.Equal(m.HostDistinct[s].Count, st.PacketsVerified);
            }
        }

        // ── 같은 씨앗이면 같은 바이트 열(호스트가 낸 패킷, 클라이언트의 확정 해시와 이벤트), 다른 씨앗이면 다르다
        [Fact]
        public void SameSeedGivesSameTraceAndDifferentSeedDiffers()
        {
            string a = Fingerprint(0x5EED_0001UL);
            string b = Fingerprint(0x5EED_0001UL);
            string c = Fingerprint(0x5EED_0002UL);
            Assert.Equal(a, b);
            Assert.NotEqual(a, c);
        }

        private static string Fingerprint(ulong seed)
        {
            NetSimMatch m = NetSimMatch.Symmetric(seed, 90, 20, 10000);
            m.Keys = (slot, n, rng) => n < 200 ? NetSimMatch.RandomKeys(rng, 3) : null;
            m.Run(240);
            ulong h = 1469598103934665603UL;
            foreach (byte[] p in m.HostOutgoing)
            {
                foreach (byte x in p)
                {
                    h = (h ^ x) * 1099511628211UL;
                }
            }
            return $"{h:X16}|{string.Join(",", m.Trace[0].ConfirmedHashes)}|{string.Join(",", m.Trace[1].ConfirmedAtUs)}|{m.Trace[0].Events.Count}";
        }

        // ── 마감 안에 도착한 입력의 결과 = 통신 없는 실행: 두 클라이언트가 낸 이벤트(호스트를 거치지 않은 열)로 WZDR 을 따로 써서
        //    ReplayRunner.RunDuel 로 돌리면 호스트의 최종 해시가 나오고(꼬리말 대조 통과), 틱마다의 해시도 호스트와 같다
        [Fact]
        public void InputsArrivingBeforeDeadlineGiveTheSameResultAsAnOfflineReplay()
        {
            NetSimMatch m = NetSimMatch.Symmetric(0x6F66_666CUL, 60, 0, 0);
            m.Keys = (slot, n, rng) => n < 500 ? NetSimMatch.RandomKeys(rng, 2) : null;
            m.Run(560);
            Assert.Equal(0, MatrixChecks.DroppedPresses(m, 0));
            Assert.Equal(0, MatrixChecks.DroppedPresses(m, 1));
            var (hostPerTick, hostFinal, file) = m.HostReference();
            uint end = file.Body.EndTick;

            // 누른 기기 쪽의 열: 각 클라이언트가 낸 이벤트(순번 순) 가운데 end 앞의 것. 호스트의 기록은 쓰지 않는다
            var offline = new List<PlayerInput>();
            for (int s = 0; s < 2; s++)
            {
                offline.AddRange(m.Trace[s].SentBySeq.Values.Where(e => e.Tick < end).Select(e => new PlayerInput(new PlayerSlot((byte)s), e)));
            }
            PlayerInput[] ordered = offline.ToArray();
            InputOrder.Sort(ordered);
            SimWorld w = SimWorld.CreateDuel(HostKit.Db, SimConfig.ForDuel(HostKit.Db, HostKit.Setup(m.WorldSeed).Options), HostKit.Setup(m.WorldSeed));
            var writer = new DuelRecordWriter(HostKit.Setup(m.WorldSeed));
            for (int s = 0; s < 2; s++)
            {
                writer.SetInputDelay(new PlayerSlot((byte)s), m.Trace[s].Delay);
            }
            int at = 0;
            var perTick = new List<ulong>();
            while (w.Tick < end)
            {
                int from = at;
                while (at < ordered.Length && ordered[at].Event.Tick == w.Tick)
                {
                    writer.Append(ordered[at]);
                    at++;
                }
                w.Step(new ReadOnlySpan<PlayerInput>(ordered, from, at - from), ReadOnlySpan<RunCommand>.Empty, Wizard.Netcode.Tests.Duel.Client.NullSink.Instance);
                perTick.Add(w.ComputeStateHash());
            }
            Assert.Equal(ordered.Length, at);
            Assert.Equal(hostPerTick, perTick);
            Assert.Equal(hostFinal, w.ComputeStateHash());
            ReadOnlyMemory<byte> record = writer.Finish(end, hostFinal);
            DuelReplayResult r = ReplayRunner.RunDuel(HostKit.Db, record.Span, Wzdr.Generous);
            Assert.Equal(ReplayStatus.EndedLate, r.Status);   // RecordCorrupt 가 아니다 = 꼬리말의 호스트 해시와 다시 돌린 해시가 같다
            Assert.Equal(end, r.TicksSimulated);
            Assert.Equal(m.Host.ConfirmedInputRecord().ToArray(), record.ToArray());   // 같은 열이므로 바이트도 같다
        }

        // 확정 세계에서 본 그 사람의 키 상태(호스트 기록을 끝까지 다시 돌린 세계)
        private static KeyMask ConfirmedKeys(NetSimMatch m, int slot)
        {
            Wzdr.File file = Wzdr.Read(m.Host.ConfirmedInputRecord());
            SimWorld w = SimWorld.CreateDuel(HostKit.Db, SimConfig.ForDuel(HostKit.Db, file.Setup.Options), file.Setup);
            int at = 0;
            var buf = new PlayerInput[16];
            while (w.Tick < file.Body.EndTick)
            {
                int n = 0;
                while (at < file.Inputs.Count && file.Inputs[at].Event.Tick == w.Tick)
                {
                    buf[n++] = file.Inputs[at++];
                }
                w.Step(buf.AsSpan(0, n), ReadOnlySpan<RunCommand>.Empty, Wizard.Netcode.Tests.Duel.Client.NullSink.Instance);
            }
            return w.View.Player(new PlayerSlot((byte)slot)).Keys;
        }
    }
}

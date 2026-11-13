using System.Collections.Generic;
using System.Linq;
using Wizard.Content;
using Wizard.Netcode.Duel;
using Wizard.Netcode.Tests.Duel.Client;
using Wizard.Netcode.Tests.Duel.Host;
using Wizard.Netcode.Tests.NetSim;
using Wizard.Sim;
using Wizard.Sim.Events;
using Wizard.Sim.Input;
using Xunit;

namespace Wizard.Netcode.Tests.Matrix
{
    // spec 10 20.14 의 TV-N02~N05(마감 여유), TV-N07~N09(상대 행동이 늦게 보이는 양), TV-N32(섬 판정은 지연과 무관).
    // 값은 공식으로 셈하지 않고 NetSim 에서 잰다: 지터·유실이 없는 링크, 틱의 처음(오프셋 0)에 누른 키.
    public sealed class NetSimVectorTests
    {
        private const long UsPerTenthMs = 100;
        private const byte LastOffset = 255;              // 틱 안의 마지막 위치(입력 시각 단위 = 1/256틱)

        // ── 마감 여유 = (D + 1)틱 − 편도. 호스트가 받은 이벤트마다 남기는 (그 틱의 끝 − 도착) µs 로 잰다(DuelNetStats.ArrivalMargin…)

        [Fact]
        public void S10_TV_N02__Rtt60D2MarginIs20_0ms() => AssertMargin(60, 2, 200);

        [Fact]
        public void S10_TV_N03__Rtt90D3MarginIs21_7ms() => AssertMargin(90, 3, 217);

        [Fact]
        public void S10_TV_N04__Rtt120D4MarginIs23_3ms() => AssertMargin(120, 4, 233);

        [Fact]
        public void S10_TV_N05__Rtt150D4MarginIs8_3ms() => AssertMargin(150, 4, 83);

        // D 는 벡터가 준 값을 그대로 건다(왕복 → D 의 표는 TV-N01 이 본다). 표가 조정돼도(OPEN-QUESTIONS Q-06) 이 벡터의 식은 그대로다
        private static void AssertMargin(int rttMs, int delay, long expectedTenthMs)
        {
            NetSimMatch m = NetSimMatch.Symmetric(0x4E02UL, rttMs, 0, 0, new[] { delay, delay });
            m.Keys = (slot, n, rng) => slot == 0 && n == 30U ? new List<(byte, LogicalKey)> { (0, LogicalKey.Fire) } : null;
            m.Run(80);
            DuelNetStats st = m.Host.StatsFor(HostKit.S0);
            Assert.Equal(1L, st.EventsAccepted);
            Assert.Equal(1L, st.ArrivalMarginCount);
            Assert.Equal(expectedTenthMs, RoundTenthMs(st.ArrivalMarginMinUs));
        }

        // ── 상대 행동이 내 화면에 늦게 보이는 양 = max(내 편도, 상대 편도) + 내 편도 − D (spec 10 15 의 4, OpponentLagMs).
        //    식의 뜻: 유효 틱 E 는 두 사람의 봉인이 서버에 다 닿아야 확정되고(max), 그 확정이 나에게 온다(+ 내 편도). 그 시각이
        //    "상대의 입력이 정해진 순간 + D"보다 얼마나 늦는가. 전제가 둘 있다(DECISIONS-TECH T-66):
        //      ① 상대의 입력이 정해지자마자 봉인이 나간다. 클라이언트는 틱 n 이 끝나야 유효 틱 n + D 를 봉인할 수 있으므로(CONTRACTS 10.2:
        //         입력 시계 틱 n 의 패킷은 n + D − 1 까지 봉인한다) 이 전제가 맞는 것은 틱의 끝에서 누른 키다. 틱 안 위치 f 에서 누른 키는
        //         (1 − f)틱의 봉인 대기가 더 붙는다(마감 여유의 식이 틱의 처음에 눌렀다는 전제인 것과 짝이다, spec 10 21 C8).
        //      ② 틱 E 가 패킷의 봉인으로 닫힌다. 봉인이 서버 시각 E + 1틱보다 늦게 닿으면 서버가 먼저 닫고(spec 10 14.6 (b)) 그 키는 늦음으로
        //         버려진다. 그때 내 화면이 늦는 양은 내 편도뿐이다.
        //    그래서 이렇게 잰다: 상대(슬롯 1)가 틱 n 의 마지막 위치에서 키를 누른다(유효 틱 E = n + D). 기준 시각 = 그 입력이 정해진 순간
        //    (틱 n 의 끝) + D틱 = 틱 E 의 끝. 잰 값 = (두 사람의 E 봉인 패킷이 호스트에 닿은 시각 가운데 늦은 쪽 + 내 편도) − 기준 시각.
        //    TV-N07·N08 은 표가 주는 조건(왕복 60 → D 2, 왕복 120 → D 4)이라 ②가 맞는다: 그 시각이 내 클라이언트에서 틱 E 가 실제로 확정된
        //    시각과 같고 상대의 키가 확정 열에 있다는 것까지 본다.
        //    TV-N09(상대 편도 60 = 왕복 120 에 D 2)는 표가 주지 않는 조건이고 ②가 깨진다: 식의 값(46.7ms)은 봉인 패킷의 길로 재고,
        //    실제로는 서버가 먼저 닫아 상대의 키가 늦음으로 버려지며 틱 E 의 확정이 내 편도(20ms)만큼만 늦게 닿는다는 것을 같이 고정한다

        [Fact]
        public void S10_TV_N07__BothOneWay30D2OpponentLag26_7ms() => AssertOpponentLag(30, 30, 2, 267, sealedByPackets: true);

        [Fact]
        public void S10_TV_N08__BothOneWay60D4OpponentLag53_3ms() => AssertOpponentLag(60, 60, 4, 533, sealedByPackets: true);

        [Fact]
        public void S10_TV_N09__MyOneWay20Opponent60D2OpponentLag46_7ms() => AssertOpponentLag(20, 60, 2, 467, sealedByPackets: false);

        private static void AssertOpponentLag(int myOneWayMs, int oppOneWayMs, int delay, long expectedTenthMs, bool sealedByPackets)
        {
            long myOneWayUs = myOneWayMs * 1000L;
            var up = new[] { LinkProfile.OneWay(myOneWayUs), LinkProfile.OneWay(oppOneWayMs * 1000L) };
            var down = new[] { LinkProfile.OneWay(myOneWayUs), LinkProfile.OneWay(oppOneWayMs * 1000L) };
            var m = new NetSimMatch(0x4E07UL, up, down, new[] { delay, delay });
            const uint pressTick = 40;
            m.Keys = (slot, n, rng) => slot == 1 && n == pressTick ? new List<(byte, LogicalKey)> { (LastOffset, LogicalKey.Fire) } : null;
            m.Run(100);
            uint e = pressTick + (uint)delay;
            long referenceUs = HostKit.TickStartUs(e + 1U);

            long bothSealedAtUs = System.Math.Max(FirstSealArrival(m, 0, e), FirstSealArrival(m, 1, e));
            long lagUs = bothSealedAtUs + myOneWayUs - referenceUs;
            Assert.True(expectedTenthMs == RoundTenthMs(lagUs), $"measured opponent lag {lagUs}us (want {expectedTenthMs / 10.0}ms)");

            Wzdr.File file = Wzdr.Read(m.Host.ConfirmedInputRecord());
            bool accepted = file.Inputs.Any(i => i.Slot.Value == 1 && i.Event.Tick == e && i.Event.Key == LogicalKey.Fire);
            long confirmedLagUs = m.Trace[0].ConfirmedAtUs[(int)e] - referenceUs;
            if (sealedByPackets)
            {
                Assert.True(accepted, "the opponent's key must be in the confirmed tick");
                Assert.Equal(lagUs, confirmedLagUs);   // 내 화면에서 실제로 확정된 시각이 그 값이다
            }
            else
            {
                // 봉인이 서버 시각 E + 1틱보다 늦게 닿는다: 서버가 먼저 닫고, 상대의 키는 늦음으로 버려지며, 확정은 내 편도만큼만 늦는다
                Assert.True(bothSealedAtUs > referenceUs, "this vector's condition must be one where the seal arrives after the forced seal");
                Assert.False(accepted, "the opponent's key arrives after the forced seal and must be dropped");
                // 누른 키와 그 뒤의 보정 이벤트(CONTRACTS 6.4 "버려진 내 입력")가 모두 늦음이다. 이 조건의 상대는 아무 입력도 넣지 못한다
                DuelNetStats st = m.Host.StatsFor(HostKit.S1);
                Assert.Equal(1, MatrixChecks.DroppedPresses(m, 1));
                Assert.True(st.EventsLate >= 1L);
                Assert.Equal(0L, st.EventsAccepted);
                Assert.Equal(myOneWayUs, confirmedLagUs);
            }
        }

        // 이 사람의 패킷 가운데 틱 tick 을 봉인하는 것(sealed_through_tick ≥ tick)이 호스트에 처음 닿은 시각
        private static long FirstSealArrival(NetSimMatch m, int slot, uint tick) =>
            m.Trace[slot].SealArrivals.Where(a => a.sealedThrough >= tick).Min(a => a.atUs);

        // ── 섬 판정: 방향을 누르고 0.05초(768 입력 단위) 뒤에 K. 왕복 20ms 와 120ms 에서 호스트가 확정한 두 이벤트의 간격이 768 이고,
        //    그 확정 열을 판정기(CommandParser, 지금은 연습 세계에서만 켜진다)에 넣은 판정이 통신 없이 누른 그대로 넣은 판정과 같다

        [Fact]
        public void S10_TV_N32__JustGapIsJudgedAs768AtRtt20AndRtt120()
        {
            List<SimEvent> offline = null!;
            var judged = new List<List<SimEvent>>();
            foreach (int rtt in new[] { 20, 120 })
            {
                var up = new[] { LinkProfile.OneWay(rtt * 500L), LinkProfile.OneWay(10000) };
                var down = new[] { LinkProfile.OneWay(rtt * 500L), LinkProfile.OneWay(10000) };
                var m = new NetSimMatch(0x4E32UL, up, down);
                m.Keys = (slot, n, rng) =>
                {
                    if (slot != 0)
                    {
                        return null;
                    }
                    switch (n)
                    {
                        case 10: return new List<(byte, LogicalKey)> { (0, LogicalKey.Stance) };
                        case 20: return new List<(byte, LogicalKey)> { (37, LogicalKey.Down) };
                        case 23: return new List<(byte, LogicalKey)> { (37, LogicalKey.Skill) };   // 3틱 = 768
                        default: return null;
                    }
                };
                m.Run(90);
                Assert.Equal(0, MatrixChecks.DroppedPresses(m, 0));
                List<InputEvent> confirmed = Wzdr.Read(m.Host.ConfirmedInputRecord()).Inputs
                    .Where(i => i.Slot.Value == 0).Select(i => i.Event).ToList();
                InputEvent dir = confirmed.Single(x => x.Key == LogicalKey.Down && x.Down);
                InputEvent k = confirmed.Single(x => x.Key == LogicalKey.Skill && x.Down);
                Assert.Equal(768L, k.Time - dir.Time);
                judged.Add(Judge(confirmed));
                if (offline == null)
                {
                    uint seq = 0;
                    offline = Judge(m.Trace[0].Pressed.Select(p => new InputEvent(p.key.Tick, p.key.Offset, p.key.Key, p.key.Down, ++seq)).ToList());
                }
            }
            SimEvent gap = offline.Single(x => x.Type == SimEventType.PracticeGapReport);
            Assert.Equal(768L, gap.V);
            foreach (List<SimEvent> j in judged)
            {
                Assert.Equal(Summ(offline), Summ(j));
            }
        }

        // 판정기가 켜진 세계(연습)에 한 사람의 이벤트를 틱마다 넣고 판정 이벤트(섬 간격 보고, 비기 발동·불발)를 모은다
        private static List<SimEvent> Judge(List<InputEvent> events)
        {
            ContentDb db = HostKit.Db;
            SimWorld w = SimWorld.CreatePractice(db, SimConfig.ForPractice(db, SimConfig.ForEndless(db)), new PracticeSetup());
            var sink = new ListSink();
            uint last = events.Max(e => e.Tick) + 30U;
            var byTick = events.GroupBy(e => e.Tick).ToDictionary(g => g.Key, g => g.ToArray());
            while (w.Tick < last)
            {
                PlayerInput[] inputs = byTick.TryGetValue(w.Tick, out InputEvent[]? es)
                    ? es.Select(e => new PlayerInput(HostKit.S0, e)).ToArray()
                    : System.Array.Empty<PlayerInput>();
                InputOrder.Sort(inputs);
                w.Step(inputs, System.ReadOnlySpan<RunCommand>.Empty, sink);
            }
            return sink.Events.Where(e => e.Type == SimEventType.PracticeGapReport || e.Type == SimEventType.ArtCast
                                          || e.Type == SimEventType.ArtCastDebt || e.Type == SimEventType.ArtFizzleNoCrystal || e.Type == SimEventType.ArtFizzleLate).ToList();
        }

        // 판정의 요약. 틱은 K 의 틱에서 센 상대 틱이고, V 는 섬 간격 보고의 간격만 싣는다(발동의 V 는 절대 시각이다):
        // D 는 두 이벤트를 같이 미룰 뿐 판정을 바꾸지 않는다(spec 10 15 의 3)
        private static string Summ(List<SimEvent> events)
        {
            uint k = events.First(e => e.Type == SimEventType.PracticeGapReport).Tick;
            return string.Join(";", events.Select(e => $"{e.Type}@{(long)e.Tick - k}:{e.A},{e.B},{e.C},{(e.Type == SimEventType.PracticeGapReport ? e.V : 0)}"));
        }

        private static long RoundTenthMs(long us) => (us + UsPerTenthMs / 2) / UsPerTenthMs;
    }
}

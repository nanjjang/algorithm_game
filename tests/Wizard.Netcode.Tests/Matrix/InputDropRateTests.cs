using System;
using System.Collections.Generic;
using System.Linq;
using Wizard.Netcode.Duel;
using Wizard.Netcode.Tests.Duel.Host;
using Wizard.Netcode.Tests.NetSim;
using Wizard.Sim.Input;
using Xunit;
using Xunit.Abstractions;

namespace Wizard.Netcode.Tests.Matrix
{
    // M2-NET-06 수락 조건 "누른 순간의 틱 안 위치를 균등 분포로 준 입력의 폐기율이 왕복 60/90/120ms, 지터 15ms 이하에서 0 이다.
    // 못 넘으면 측정값을 적고 입력 지연 표를 고치는 수치 조정 작업을 올린다(기준을 낮추지 않는다)".
    //
    // 잰 결과: 지금의 표(왕복 ≤60 → D 2, ≤90 → 3, ≤120 → 4. spec 10 14.5)로는 **못 넘는다**. 틱 안 위치 f 에서 누른 키의 마감 여유는
    // (D + 1 − f)틱 − 편도 − 지터이고(CONTRACTS 6.5 의 3), 틱의 끝(f → 1)에서는 D틱 − 편도 = 3.3 / 5.0 / 6.7ms 밖에 남지 않는다.
    // 지터가 그것을 넘으면 틱의 끝 쪽에서 누른 키가 늦는다. 측정값은 MEASURED 줄과 DECISIONS-TECH T-66 의 표에 있다.
    // 표를 고치는 일은 수치 조정 작업 FIX-8 과 OPEN-QUESTIONS Q-06 으로 올렸다. "지금의 표로 폐기율 0"의 단언은 FIX-8 이 표를 고치면서
    // 이 파일에 넣는다(그 작업의 수락 조건이다). 여기서는 표와 무관하게 참이어야 하는 것 셋을 고정한다:
    //   1. 넷코드가 까닭 없이 버리지 않는다: 최악의 지터로도 마감 안에 닿는 누름(여유 > 0)은 하나도 버려지지 않는다(지금의 표, 전 조건).
    //   2. spec 10 15 의 표 "마감 여유 20ms 이상, 지터 15ms 까지 영향 없음"은 그 표의 전제(틱의 처음에 누름, f = 0)에서 참이다(지금의 표).
    //   3. 기준 자체는 넘을 수 있다: D 가 "편도 + 지터 15ms"를 덮으면(D틱 ≥ 편도 + 15ms → 왕복 60/90/120 에 3/4/5) 틱 안 균등 위치의
    //      누름이 지터 15ms 이하에서 하나도 버려지지 않는다. FIX-8 의 조정안이 이 값이다.
    // 두 사람이 같은 조건, 유실 없음. 폐기 = 호스트가 버렸다고 알린 누름(늦음·이른 시각·수량·유실 모두, MatrixChecks.DroppedPresses)
    public sealed class InputDropRateTests
    {
        private const uint PressTicks = 1800;
        private const uint QuietTicks = 60;
        private const int CriterionJitterMs = 15;         // 수락 조건의 "지터 15ms 이하"
        private const long UsPerMs = 1000;

        private readonly ITestOutputHelper _out;

        public InputDropRateTests(ITestOutputHelper output)
        {
            _out = output;
        }

        public static IEnumerable<object[]> Conditions()
        {
            foreach (int rtt in new[] { 60, 90, 120 })
            {
                foreach (int jitter in new[] { 0, 5, 10, 15 })
                {
                    yield return new object[] { rtt, jitter };
                }
            }
        }

        public static IEnumerable<object[]> Rtts()
        {
            foreach (int rtt in new[] { 60, 90, 120 })
            {
                yield return new object[] { rtt };
            }
        }

        // 1. 지금의 표. 버려진 누름은 전부 "최악의 지터면 마감에 못 닿는" 누름이다. 틱의 끝에서도 여유가 남는 조건에서는 폐기가 0 이다
        [Theory]
        [MemberData(nameof(Conditions))]
        public void PressThatMeetsTheDeadlineUnderWorstJitterIsNeverDropped(int rttMs, int jitterMs)
        {
            NetSimMatch m = PlayUniform(rttMs, jitterMs, null, uniformOffset: true);
            long oneWayUs = rttMs * UsPerMs / 2;
            long jitterUs = jitterMs * UsPerMs;

            int pressed = 0;
            int dropped = 0;
            var unexplained = new List<string>();
            for (int s = 0; s < 2; s++)
            {
                ClientTrace t = m.Trace[s];
                pressed += t.Pressed.Count;
                dropped += MatrixChecks.DroppedPresses(m, s);
                foreach ((long atUs, QuantizedKey q) in t.Pressed)
                {
                    // 그 누름이 든 첫 패킷은 누른 순간에 나간다(CONTRACTS 6.5 의 1). 가장 늦게 닿는 시각 = 누른 시각 + 편도 + 지터 전부
                    long worstMarginUs = HostKit.TickStartUs(q.Tick + 1U) - (atUs + oneWayUs + jitterUs);
                    if (worstMarginUs <= 0)
                    {
                        continue;
                    }
                    InputEvent e = t.SentBySeq.Values.First(x => x.Time == q.Time && x.Key == q.Key && x.Down == q.Down);
                    if (t.DropNotices.ContainsKey(e.Seq))
                    {
                        unexplained.Add($"slot {s} seq {e.Seq} tick {q.Tick} offset {q.Offset} worst margin {worstMarginUs}us reason {t.DropNotices[e.Seq]}");
                    }
                }
                Report(m, s);
            }
            long ppm = pressed == 0 ? 0 : (long)dropped * 1000000L / pressed;
            long endOfTickMarginUs = HostKit.TickStartUs(m.Trace[0].Delay) - oneWayUs - jitterUs;   // f → 1 에서 남는 여유 = D틱 − 편도 − 지터
            _out.WriteLine($"MEASURED rtt {rttMs}ms jitter {jitterMs}ms D {m.Trace[0].Delay}: dropped {dropped} / {pressed} presses = {ppm} ppm, " +
                           $"end-of-tick margin {endOfTickMarginUs}us");
            MatrixChecks.All(m, (int)PressTicks);
            Assert.True(unexplained.Count == 0,
                        $"rtt {rttMs}ms jitter {jitterMs}ms: {unexplained.Count} presses dropped with margin to spare, first {string.Join(" | ", unexplained.Take(3))}");
            if (endOfTickMarginUs > 0)
            {
                Assert.Equal(0, dropped);
            }
        }

        // 2. 지금의 표, 틱의 처음에 누른 키(f = 0), 지터 15ms: spec 10 15 의 표 "지터 15ms 까지 영향 없음"
        [Theory]
        [MemberData(nameof(Rtts))]
        public void TickStartPressesAreNeverDroppedAtJitter15ms(int rttMs)
        {
            NetSimMatch m = PlayUniform(rttMs, CriterionJitterMs, null, uniformOffset: false);
            AssertNoDrops(m, rttMs, CriterionJitterMs);
        }

        // 3. D 가 편도 + 지터 15ms 를 덮으면 기준(틱 안 균등 위치, 지터 15ms 이하, 폐기 0)을 넘는다
        [Theory]
        [MemberData(nameof(Conditions))]
        public void UniformInTickPressesAreNeverDroppedUpToJitter15ms_WhenDelayCoversOneWayPlusJitter(int rttMs, int jitterMs)
        {
            int delay = DelayCovering(rttMs * UsPerMs / 2 + CriterionJitterMs * UsPerMs);
            NetSimMatch m = PlayUniform(rttMs, jitterMs, new[] { delay, delay }, uniformOffset: true);
            AssertNoDrops(m, rttMs, jitterMs);
        }

        // 가장 작은 D: D틱 ≥ us
        private static int DelayCovering(long us)
        {
            int d = 1;
            while (HostKit.TickStartUs(d) < us)
            {
                d++;
            }
            return d;
        }

        // 틱마다 키 하나를 누르거나 뗀다. 위치는 틱 안 균등(uniformOffset) 또는 틱의 처음
        private static NetSimMatch PlayUniform(int rttMs, int jitterMs, int[]? delayOverride, bool uniformOffset)
        {
            ulong seed = 0x6472_6F70UL ^ ((ulong)rttMs << 24) ^ (ulong)jitterMs;
            NetSimMatch m = NetSimMatch.Symmetric(seed, rttMs, jitterMs, 0, delayOverride);
            m.Keys = (slot, n, rng) =>
            {
                if (n >= PressTicks)
                {
                    return null;
                }
                byte offset = (byte)rng.Below(256);
                var key = (LogicalKey)rng.Below((int)LogicalKey.Pause);
                return new List<(byte, LogicalKey)> { (uniformOffset ? offset : (byte)0, key) };
            };
            m.Run(PressTicks + QuietTicks);
            return m;
        }

        private void AssertNoDrops(NetSimMatch m, int rttMs, int jitterMs)
        {
            int pressed = 0;
            int dropped = 0;
            for (int s = 0; s < 2; s++)
            {
                pressed += m.Trace[s].Pressed.Count;
                dropped += MatrixChecks.DroppedPresses(m, s);
                Report(m, s);
            }
            _out.WriteLine($"MEASURED rtt {rttMs}ms jitter {jitterMs}ms D {m.Trace[0].Delay}: dropped {dropped} / {pressed} presses");
            MatrixChecks.All(m, (int)PressTicks);
            Assert.True(pressed >= (int)PressTicks * 2, $"only {pressed} presses");
            Assert.True(dropped == 0, $"rtt {rttMs}ms jitter {jitterMs}ms D {m.Trace[0].Delay}: {dropped} of {pressed} presses dropped");
        }

        private void Report(NetSimMatch m, int s)
        {
            DuelNetStats st = m.Host.StatsFor(new Wizard.Core.PlayerSlot((byte)s));
            _out.WriteLine($"slot {s}: D {m.Trace[s].Delay}, late {st.EventsLate}, early {st.EventsEarly}, quantity {st.EventsQuantity}, lost {st.SeqsLost}, " +
                           $"min margin {st.ArrivalMarginMinUs}us");
        }
    }
}

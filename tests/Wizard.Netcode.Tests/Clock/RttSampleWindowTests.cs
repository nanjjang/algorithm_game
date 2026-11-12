using System;
using Wizard.Content.Rules;
using Wizard.Netcode.Clock;
using Xunit;

namespace Wizard.Netcode.Tests.Clock
{
    // CONTRACTS 10.5 "서버의 표본": 왕복 = 도착 시각 − 내가 보낸 시각 − hold, hold 가 RttHoldCapMs 를 넘으면 버림,
    // 최근 16개(ClockSyncSamples, 데이터 값)의 최솟값이 그 사람의 왕복, 그 절반이 편도 추정(spec 10 14.8).
    // CONTRACTS 6.4 MeasuredRttMs(표본 없으면 −1).
    public sealed class RttSampleWindowTests
    {
        private static NetRuleSet Net => NetData.RepoNet;

        private static long CapUs => Net.RttHoldCapMs * 1000L;

        [Fact]
        public void RoundTrip_IsArrivalMinusSentMinusHold()
        {
            // 서버가 1,000,000µs 에 보냈고 클라이언트가 8,000µs 붙들었다가 답했고 1,050,000µs 에 닿았다 → 42,000µs
            Assert.True(RttSampleWindow.TryRoundTripUs(1_050_000, 1_000_000, 8_000, Net.RttHoldCapMs, out long rtt));
            Assert.Equal(42_000, rtt);
            Assert.True(RttSampleWindow.TryRoundTripUs(1_000_000, 1_000_000, 0, Net.RttHoldCapMs, out rtt));
            Assert.Equal(0, rtt);
        }

        [Fact]
        public void RoundTrip_HoldAboveCapIsDropped()
        {
            // "hold 가 RttHoldCapMs 를 넘는 표본은 버린다": 같으면 받고 1µs 넘으면 버린다
            Assert.True(RttSampleWindow.TryRoundTripUs(10_000_000, 1_000_000, CapUs, Net.RttHoldCapMs, out long rtt));
            Assert.Equal(9_000_000 - CapUs, rtt);
            Assert.False(RttSampleWindow.TryRoundTripUs(10_000_000, 1_000_000, CapUs + 1, Net.RttHoldCapMs, out _));
        }

        [Fact]
        public void RoundTrip_NegativeIsDropped()
        {
            // 붙든 시간이 실제 간격보다 크다(거짓 hold) 또는 도착이 보낸 것보다 앞이다(거짓 에코)
            Assert.False(RttSampleWindow.TryRoundTripUs(1_010_000, 1_000_000, 10_001, Net.RttHoldCapMs, out _));
            Assert.False(RttSampleWindow.TryRoundTripUs(999_999, 1_000_000, 0, Net.RttHoldCapMs, out _));
            Assert.False(RttSampleWindow.TryRoundTripUs(2_000_000, 1_000_000, -1, Net.RttHoldCapMs, out _));
        }

        [Fact]
        public void Window_EmptyIsMinusOne()
        {
            var w = new RttSampleWindow(Net);
            Assert.Equal(16, w.Capacity);       // spec 10 2.8 clock_sync_samples, CONTRACTS 10.5 "최근 16개"
            Assert.Equal(0, w.Count);
            Assert.Equal(-1, w.MinRttUs);
            Assert.Equal(-1, w.MinRttMs);
            Assert.Equal(-1, w.OneWayUs);
        }

        [Fact]
        public void Window_MinOfLatest16()
        {
            var w = new RttSampleWindow(Net);
            // 첫 표본 10ms 가 가장 작지만 뒤의 16개에 밀려난다. 남은 16개(31..46ms 를 섞은 것)의 최솟값은 31ms
            Assert.True(w.Offer(10_000, 0, 0));
            long[] laterMs = { 50, 47, 48, 49, 40, 39, 38, 37, 36, 35, 31, 34, 33, 32, 41, 42, 43, 44, 45 };
            Assert.Equal(19, laterMs.Length);
            for (int i = 0; i < 3; i++)
            {
                Assert.True(w.Offer(1_000_000 + laterMs[i] * 1000, 1_000_000, 0));
                Assert.Equal(10_000, w.MinRttUs);   // 아직 10ms 가 창 안에 있다
            }
            for (int i = 3; i < laterMs.Length; i++)
            {
                Assert.True(w.Offer(1_000_000 + laterMs[i] * 1000, 1_000_000, 0));
            }
            Assert.Equal(16, w.Count);
            // 창에는 마지막 16개(laterMs[3..18])만 있다: 최솟값 31ms
            Assert.Equal(31_000, w.MinRttUs);
            Assert.Equal(31, w.MinRttMs);
            Assert.Equal(15_500, w.OneWayUs);
        }

        [Fact]
        public void Window_DroppedSamplesDoNotEnter()
        {
            var w = new RttSampleWindow(Net);
            Assert.True(w.Offer(1_060_000, 1_000_000, 0));                  // 60ms
            // hold 를 상한보다 크게 적어 왕복을 작게 보이려는 표본: 버린다
            Assert.False(w.Offer(1_060_000, 1_000_000, CapUs + 1));
            Assert.False(w.Offer(900_000, 1_000_000, 0));
            Assert.Equal(1, w.Count);
            Assert.Equal(60, w.MinRttMs);
        }

        [Theory]
        [InlineData(30_000L, 30)]
        [InlineData(30_001L, 31)]
        [InlineData(60_400L, 61)]
        [InlineData(999L, 1)]
        [InlineData(0L, 0)]
        public void Window_MillisRoundUp(long rttUs, int expectedMs)
        {
            // T-60 올림: 60.4ms 를 60 으로 내리면 "60ms 이하"가 아닌 왕복에 D = 2 가 나온다
            var w = new RttSampleWindow(Net);
            Assert.True(w.Offer(1_000_000 + rttUs, 1_000_000, 0));
            Assert.Equal(expectedMs, w.MinRttMs);
        }

        [Fact]
        public void Window_FeedsChooseInputDelay()
        {
            // 서버가 잰 왕복으로 D 를 정한다(CONTRACTS 10.5). 60.0ms → 2, 60.001ms → 61 → 3
            var a = new RttSampleWindow(Net);
            Assert.True(a.Offer(1_060_000, 1_000_000, 0));
            Assert.Equal(2, ClockSync.ChooseInputDelay(a.MinRttMs, Net));
            var b = new RttSampleWindow(Net);
            Assert.True(b.Offer(1_060_001, 1_000_000, 0));
            Assert.Equal(3, ClockSync.ChooseInputDelay(b.MinRttMs, Net));
            // 표본이 없으면 −1 → 가장 큰 D
            Assert.Equal(4, ClockSync.ChooseInputDelay(new RttSampleWindow(Net).MinRttMs, Net));
        }

        [Fact]
        public void Window_ClearForgetsSamples()
        {
            var w = new RttSampleWindow(Net);
            for (int i = 0; i < 20; i++)
            {
                Assert.True(w.Offer(1_000_000 + 20_000 + i, 1_000_000, 0));
            }
            w.Clear();
            Assert.Equal(0, w.Count);
            Assert.Equal(-1, w.MinRttMs);
            Assert.True(w.Offer(1_090_000, 1_000_000, 0));
            Assert.Equal(90, w.MinRttMs);
        }

        [Fact]
        public void Window_SizeAndHoldCapComeFromData()
        {
            // 명세와 다른 창(3개)과 hold 상한(7ms). 코드에 16 이나 50 이 있었다면 아래가 달라진다
            var w = new RttSampleWindow(NetData.WithWindow(3, 7));
            Assert.Equal(3, w.Capacity);
            Assert.True(w.Offer(1_005_000, 1_000_000, 0));      // 5ms
            Assert.True(w.Offer(1_040_000, 1_000_000, 0));      // 40ms
            Assert.True(w.Offer(1_030_000, 1_000_000, 0));      // 30ms
            Assert.Equal(5_000, w.MinRttUs);
            Assert.True(w.Offer(1_020_000, 1_000_000, 0));      // 20ms. 5ms 가 밀려난다
            Assert.Equal(3, w.Count);
            Assert.Equal(20_000, w.MinRttUs);
            Assert.True(w.Offer(1_050_000, 1_000_000, 7_000));  // hold 7ms 는 받는다(43ms). 40ms 가 밀려난다
            Assert.False(w.Offer(1_050_000, 1_000_000, 7_001)); // 7.001ms 는 버린다
            Assert.Equal(20_000, w.MinRttUs);
            Assert.True(w.Offer(1_060_000, 1_000_000, 0));      // 30ms 가 밀려난다 → 20, 43, 60
            Assert.True(w.Offer(1_070_000, 1_000_000, 0));      // 20ms 가 밀려난다 → 43, 60, 70
            Assert.Equal(43_000, w.MinRttUs);
            Assert.Equal(21_500, w.OneWayUs);
        }

        [Fact]
        public void Window_SameOffersSameResult()
        {
            // 인스턴스끼리 상태를 나누지 않는다(정적 버퍼 없음). 같은 표본 열이면 같은 값
            var a = new RttSampleWindow(Net);
            var b = new RttSampleWindow(Net);
            for (int i = 0; i < 40; i++)
            {
                long rtt = 20_000 + (i * 7_919) % 30_000;
                Assert.True(a.Offer(1_000_000 + rtt, 1_000_000, 0));
                Assert.Equal(a.MinRttUs, OfferAndMin(b, rtt));
            }
            var other = new RttSampleWindow(Net);
            Assert.Equal(-1, other.MinRttUs);
        }

        private static long OfferAndMin(RttSampleWindow w, long rttUs)
        {
            Assert.True(w.Offer(1_000_000 + rttUs, 1_000_000, 0));
            return w.MinRttUs;
        }

        [Fact]
        public void Window_RequiresRules()
        {
            Assert.Throws<ArgumentNullException>(() => new RttSampleWindow(null!));
            Assert.Throws<ArgumentException>(() => new RttSampleWindow(new NetRuleSet()));
            Assert.Throws<ArgumentException>(() => new RttSampleWindow(NetData.WithWindow(0, 50)));
        }
    }
}

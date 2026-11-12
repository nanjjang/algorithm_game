using System;
using Wizard.Content.Rules;
using Wizard.Netcode.Clock;
using Xunit;

namespace Wizard.Netcode.Tests.Clock
{
    // spec 10 14.4(시계 맞추기), 14.5(입력 지연 D), 2.8(clock_slew_max), 20.14 TV-N01·TV-N30·TV-N31. CONTRACTS 6.4 ClockSync.
    public sealed class ClockSyncTests
    {
        // ---- TV-N01: ChooseD -------------------------------------------------------------------------------

        [Theory]
        [InlineData(40, 2)]
        [InlineData(60, 2)]
        [InlineData(61, 3)]
        [InlineData(90, 3)]
        [InlineData(91, 4)]
        [InlineData(120, 4)]
        [InlineData(121, 4)]
        public void S10_TV_N01__ChooseInputDelayFromRepoTable(int rttMs, int expectedD)
        {
            // spec 10 20.14 TV-N01: 왕복 40/60/61/90/91/120/121ms → 2/2/3/3/4/4/4
            Assert.Equal(expectedD, ClockSync.ChooseInputDelay(rttMs, NetData.RepoNet));
        }

        [Fact]
        public void S10_TV_N01__Above120IsNotQueueable()
        {
            // TV-N01 의 "121 → 4(자동 매치 불가)": 자동 매치 한도(spec 10 14.5 "120ms 초과 … 불가")는 120 까지 받고 121 은 안 받는다
            NetRuleSet net = NetData.RepoNet;
            Assert.True(120 <= net.QueueRttLimitMs);
            Assert.False(121 <= net.QueueRttLimitMs);
        }

        [Theory]
        [InlineData(0, 5)]
        [InlineData(10, 5)]
        [InlineData(11, 6)]
        [InlineData(25, 6)]
        [InlineData(26, 9)]
        [InlineData(60, 9)]     // 명세의 60 경계가 코드에 있었다면 여기서 2 가 나온다
        [InlineData(1000, 9)]   // 마지막 경계를 넘으면 마지막 줄의 D
        public void ChooseInputDelay_ReadsOnlyTheDataTable(int rttMs, int expectedD)
        {
            NetRuleSet rules = NetData.WithTable((10, 5), (25, 6), (40, 9));
            Assert.Equal(expectedD, ClockSync.ChooseInputDelay(rttMs, rules));
        }

        [Fact]
        public void ChooseInputDelay_SingleRowTable()
        {
            NetRuleSet rules = NetData.WithTable((70, 3));
            Assert.Equal(3, ClockSync.ChooseInputDelay(0, rules));
            Assert.Equal(3, ClockSync.ChooseInputDelay(70, rules));
            Assert.Equal(3, ClockSync.ChooseInputDelay(int.MaxValue, rules));
        }

        [Fact]
        public void ChooseInputDelay_UnknownRttGivesLargestD()
        {
            // T-60: 표본이 없으면 MeasuredRttMs 는 −1 이다. 모르는 왕복에는 마감 여유가 가장 큰 D(마지막 줄)
            Assert.Equal(4, ClockSync.ChooseInputDelay(-1, NetData.RepoNet));
            Assert.Equal(4, ClockSync.ChooseInputDelay(int.MinValue, NetData.RepoNet));
        }

        [Fact]
        public void ChooseInputDelay_RejectsBrokenRules()
        {
            Assert.Throws<ArgumentNullException>(() => ClockSync.ChooseInputDelay(40, null!));
            Assert.Throws<ArgumentException>(() => ClockSync.ChooseInputDelay(40, new NetRuleSet()));
        }

        // ---- TV-N30: Sync ----------------------------------------------------------------------------------

        // 참 오프셋(서버 시각 − 클라 시각) 1,000,000µs. 표본 i 는 t0 = 100,000·i 에 보내고, 올라가는 길 up, 내려오는 길 down.
        // 왕복이 가장 짧은 표본은 i = 11(up = down = 10,000µs). 나머지는 왕복이 길고 길이 비대칭이라 다른 오프셋이 나온다.
        private static readonly long[] UpUs =
            { 40_000, 25_000, 60_000, 31_000, 22_000, 45_000, 80_000, 12_000, 35_000, 50_000, 28_000, 10_000, 33_000, 70_000, 24_000, 41_000 };
        private static readonly long[] DownUs =
            { 15_000, 30_000, 12_000, 11_000, 40_000, 18_000, 20_000, 30_000, 14_000, 25_000, 13_000, 10_000, 19_000, 16_000, 27_000, 12_000 };

        private const long TrueOffsetUs = 1_000_000;

        private static ClockSample[] Samples()
        {
            var samples = new ClockSample[16];
            for (int i = 0; i < samples.Length; i++)
            {
                long t0 = 100_000L * i;
                samples[i] = new ClockSample(t0, t0 + UpUs[i] + TrueOffsetUs, t0 + UpUs[i] + DownUs[i]);
            }
            return samples;
        }

        [Fact]
        public void S10_TV_N30__OffsetFromShortestRoundTripSample()
        {
            // spec 10 2.8 clock_sync_samples: 경기 전 왕복 횟수(16)는 데이터가 준다
            ClockSample[] samples = Samples();
            Assert.Equal(samples.Length, NetData.RepoNet.ClockSyncSamples);
            ClockOffset o = ClockSync.Estimate(samples);
            Assert.True(o.IsValid);
            Assert.Equal(11, o.SampleIndex);
            // spec 10 14.4: rtt = t1 − t0 = 20,000µs, offset = ts − (t0 + rtt / 2)
            //   t0 = 1,100,000, ts = 1,100,000 + 10,000 + 1,000,000 = 2,110,000 → 2,110,000 − (1,100,000 + 10,000) = 1,000,000
            Assert.Equal(20_000, o.RttUs);
            Assert.Equal(1_000_000, o.OffsetUs);
            // 서버 시각 = 클라 시각 + offset
            Assert.Equal(3_500_000, o.ToServerUs(2_500_000));
        }

        [Fact]
        public void S10_TV_N30__OtherSamplesWouldGiveOtherOffsets()
        {
            // 최소 왕복을 고르는 것이 결과를 바꾼다는 것을 보인다: 표본 0(up 40,000, down 15,000)은 rtt 55,000,
            // offset = (0 + 40,000 + 1,000,000) − (0 + 27,500) = 1,012,500
            ClockSample[] samples = Samples();
            Assert.Equal(1_012_500, ClockSync.Estimate(samples.AsSpan(0, 1)).OffsetUs);
            for (int i = 0; i < samples.Length; i++)
            {
                if (i != 11)
                {
                    ClockOffset alone = ClockSync.Estimate(samples.AsSpan(i, 1));
                    Assert.True(alone.RttUs > 20_000);
                    Assert.NotEqual(1_000_000, alone.OffsetUs);
                }
            }
        }

        [Fact]
        public void S10_TV_N30__SampleOrderDoesNotMatter()
        {
            ClockSample[] samples = Samples();
            Array.Reverse(samples);
            ClockOffset o = ClockSync.Estimate(samples);
            Assert.Equal(15 - 11, o.SampleIndex);
            Assert.Equal(20_000, o.RttUs);
            Assert.Equal(1_000_000, o.OffsetUs);
        }

        [Fact]
        public void Estimate_AnySampleCount()
        {
            // 표본 수는 호출자가 정한다(경기 중의 다시 재기는 16개가 아니다). 코드에 16 이 없다
            ClockSample[] samples = Samples();
            ClockOffset first12 = ClockSync.Estimate(samples.AsSpan(0, 12));
            Assert.Equal(11, first12.SampleIndex);
            // 앞의 10개에서는 표본 3(up 31,000, down 11,000, rtt 42,000)과 표본 7(up 12,000, down 30,000, rtt 42,000)이 같다 → 앞의 것
            ClockOffset first10 = ClockSync.Estimate(samples.AsSpan(0, 10));
            Assert.Equal(3, first10.SampleIndex);
            Assert.Equal(42_000, first10.RttUs);
            // offset = (300,000 + 31,000 + 1,000,000) − (300,000 + 21,000) = 1,010,000
            Assert.Equal(1_010_000, first10.OffsetUs);
        }

        [Fact]
        public void Estimate_TieTakesEarlierSample()
        {
            // 왕복 50, 30, 30, 100. 서버 시각을 달리 적어 어느 표본을 골랐는지 오프셋으로도 보인다
            var samples = new[]
            {
                new ClockSample(0, 1_000, 50), new ClockSample(100, 2_000, 130),
                new ClockSample(200, 3_000, 230), new ClockSample(300, 4_000, 400),
            };
            ClockOffset o = ClockSync.Estimate(samples);
            Assert.Equal(1, o.SampleIndex);
            Assert.Equal(30, o.RttUs);
            Assert.Equal(2_000 - (100 + 15), o.OffsetUs);
        }

        [Fact]
        public void Estimate_SkipsNegativeRoundTripAndReportsNoSample()
        {
            // 왕복 −10, 60, −50
            var samples = new[] { new ClockSample(100, 0, 90), new ClockSample(200, 0, 260), new ClockSample(300, 0, 250) };
            ClockOffset o = ClockSync.Estimate(samples);
            Assert.True(o.IsValid);
            Assert.Equal(1, o.SampleIndex);
            Assert.Equal(60, o.RttUs);

            ClockOffset none = ClockSync.Estimate(new[] { new ClockSample(10, 0, 5) });
            Assert.False(none.IsValid);
            Assert.Equal(0, none.OffsetUs);
            Assert.Equal(0, none.RttUs);
            Assert.False(ClockSync.Estimate(ReadOnlySpan<ClockSample>.Empty).IsValid);
            Assert.False(default(ClockOffset).IsValid);
        }

        [Fact]
        public void Estimate_ZeroRoundTripIsValid()
        {
            ClockOffset o = ClockSync.Estimate(new[] { new ClockSample(500, 700, 500) });
            Assert.True(o.IsValid);
            Assert.Equal(0, o.SampleIndex);
            Assert.Equal(0, o.RttUs);
            Assert.Equal(200, o.OffsetUs);
        }

        [Fact]
        public void Estimate_OddRoundTripHalvesDown()
        {
            // rtt 15,001 → rtt / 2 = 7,500. offset = 50,000 − (10,000 + 7,500) = 32,500
            ClockOffset o = ClockSync.Estimate(new[] { new ClockSample(10_000, 50_000, 25_001) });
            Assert.Equal(15_001, o.RttUs);
            Assert.Equal(32_500, o.OffsetUs);
        }

        [Fact]
        public void Estimate_NegativeOffsetWhenServerIsBehind()
        {
            // 서버 시각이 클라 시각보다 3초 뒤: t0 5,000,000, up = down = 8,000, ts = 5,008,000 − 3,000,000
            ClockOffset o = ClockSync.Estimate(new[] { new ClockSample(5_000_000, 2_008_000, 5_016_000) });
            Assert.Equal(-3_000_000, o.OffsetUs);
            Assert.Equal(2_000_000, o.ToServerUs(5_000_000));
        }

        [Fact]
        public void Estimate_SameSamplesSameResult()
        {
            // 순수 함수다: 같은 표본이면 같은 답, 입력을 바꾸지 않는다
            ClockSample[] samples = Samples();
            ClockSample[] copy = (ClockSample[])samples.Clone();
            ClockOffset a = ClockSync.Estimate(samples);
            ClockOffset b = ClockSync.Estimate(samples);
            Assert.Equal((a.IsValid, a.OffsetUs, a.RttUs, a.SampleIndex), (b.IsValid, b.OffsetUs, b.RttUs, b.SampleIndex));
            for (int i = 0; i < samples.Length; i++)
            {
                Assert.Equal((copy[i].T0Us, copy[i].ServerUs, copy[i].T1Us), (samples[i].T0Us, samples[i].ServerUs, samples[i].T1Us));
            }
        }

        // ---- TV-N31: 틱 길이 보정 --------------------------------------------------------------------------

        // 기본 틱 길이 1/60s 를 정수로 다루려고 "ns × 60" 단위를 쓴다: 기본 틱 = 1,000,000,000, ppm 하나 = 1,000.
        private const long BaseTickNsX60 = 1_000_000_000L;

        private static long TickNsX60(int ppm) => BaseTickNsX60 + 1000L * ppm;

        [Theory]
        [InlineData(0L, 0)]
        [InlineData(1L, -1)]
        [InlineData(-1L, 1)]
        [InlineData(1_000L, -1_000)]          // 1ms 뒤처짐 → 0.1% 짧게
        [InlineData(4_999L, -4_999)]
        [InlineData(5_000L, -5_000)]
        [InlineData(5_001L, -5_000)]
        [InlineData(-5_001L, 5_000)]
        [InlineData(200_000L, -5_000)]
        [InlineData(-200_000L, 5_000)]
        [InlineData(long.MaxValue, -5_000)]
        [InlineData(long.MinValue, 5_000)]
        public void TickLengthAdjustPpm_FollowsSpec10_14_4(long errorUs, int expectedPpm)
        {
            // spec 10 14.4: 틱 길이 = 16.667ms × (1 − clamp(err / 1s, −0.005, +0.005)). err(µs) / 1s = err ppm
            Assert.Equal(expectedPpm, ClockSync.TickLengthAdjustPpm(errorUs, NetData.RepoNet));
        }

        [Theory]
        [InlineData(0L, 0)]
        [InlineData(1_233L, -1_233)]
        [InlineData(1_234L, -1_234)]
        [InlineData(1_235L, -1_234)]
        [InlineData(5_000L, -1_234)]      // 명세의 5,000 이 코드에 있었다면 여기서 −5,000 이 나온다
        [InlineData(-5_000L, 1_234)]
        [InlineData(long.MinValue, 1_234)]
        public void TickLengthAdjustPpm_ReadsOnlyTheDataLimit(long errorUs, int expectedPpm)
        {
            Assert.Equal(expectedPpm, ClockSync.TickLengthAdjustPpm(errorUs, NetData.WithSlewMax(1_234)));
        }

        [Fact]
        public void TickLengthAdjustPpm_RejectsBrokenRules()
        {
            Assert.Throws<ArgumentNullException>(() => ClockSync.TickLengthAdjustPpm(0, null!));
            Assert.Throws<ArgumentException>(() => ClockSync.TickLengthAdjustPpm(0, new NetRuleSet()));
            Assert.Throws<ArgumentException>(() => ClockSync.TickLengthAdjustPpm(0, NetData.WithSlewMax(-1)));
        }

        [Fact]
        public void S10_TV_N31__TickLengthStaysWithin16_583And16_750Ms()
        {
            // spec 10 2.8 clock_slew_max: 틱 길이 16.583~16.750ms. ns 로 16,583,000 ≤ 길이 ≤ 16,750,000
            NetRuleSet net = NetData.RepoNet;
            for (long e = -20_000_000; e <= 20_000_000; e += 997)
            {
                AssertTickLengthInRange(ClockSync.TickLengthAdjustPpm(e, net));
            }
            AssertTickLengthInRange(ClockSync.TickLengthAdjustPpm(long.MaxValue, net));
            AssertTickLengthInRange(ClockSync.TickLengthAdjustPpm(long.MinValue, net));
        }

        [Theory]
        [InlineData(200_000L)]       // 내 입력 시계가 200ms 뒤처졌다
        [InlineData(-200_000L)]      // 200ms 앞섰다
        [InlineData(3_000L)]
        [InlineData(-37L)]
        public void S10_TV_N31__SlewConvergesWithoutSkippingTicks(long initialErrorUs)
        {
            // 입력 시계의 모형: 내 틱 k 가 시작하는 실제 시각 R_k(ns × 60). 서버 시계는 실제 시각과 같다.
            // 그 순간 서버 기준 목표 시각은 R_k, 내 입력 시계 시각은 k × 기본 틱이므로 err = R_k − k × 기본 틱(내가 뒤처지면 양수).
            // 1초(60틱)마다(spec 10 2.8 clock_resync_interval) err 를 재서 틱 길이를 바꾼다. 틱 번호는 늘 1 씩 오른다.
            NetRuleSet net = NetData.RepoNet;
            int resyncTicks = net.ClockResyncTicks;
            Assert.Equal(60, resyncTicks);
            long r = initialErrorUs * 60_000L;   // µs → ns × 60
            int ppm = 0;
            long lastErrorUs = initialErrorUs;
            long expectedIntervals = Math.Abs(initialErrorUs) / 5_000 + 2;
            bool converged = false;
            for (long k = 0; k <= 60 * resyncTicks; k++)
            {
                if (k % resyncTicks == 0)
                {
                    long errNsX60 = r - k * BaseTickNsX60;
                    long errUs = errNsX60 / 60_000L;
                    // 한 번의 보정 간격(1초)에 줄어드는 양은 0.5% × 1초 = 5,000µs 를 넘지 않는다(건너뛰지 않는다)
                    Assert.True(Math.Abs(lastErrorUs - errUs) <= 5_000 + 1, $"k={k} last={lastErrorUs} now={errUs}");
                    // 넘어서 반대쪽으로 튀지 않는다
                    Assert.True(Math.Sign(errUs) == Math.Sign(initialErrorUs) || Math.Abs(errUs) <= 1, $"overshoot k={k} err={errUs}");
                    if (k / resyncTicks >= expectedIntervals)
                    {
                        Assert.True(Math.Abs(errUs) <= 1, $"not converged k={k} err={errUs}");
                        converged = true;
                    }
                    lastErrorUs = errUs;
                    ppm = ClockSync.TickLengthAdjustPpm(errUs, net);
                }
                AssertTickLengthInRange(ppm);
                // 다음 틱의 시작 = 이 틱의 시작 + 이 틱의 길이(틱 번호 k → k + 1, 건너뛰는 틱 없음)
                r += TickNsX60(ppm);
            }
            Assert.True(converged);
        }

        private static void AssertTickLengthInRange(int ppm)
        {
            Assert.InRange(ppm, -5_000, 5_000);
            long ns = TickNsX60(ppm) / 60;
            Assert.InRange(ns, 16_583_000L, 16_750_000L);
        }
    }
}

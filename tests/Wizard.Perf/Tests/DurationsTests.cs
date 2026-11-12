using System.Diagnostics;
using Xunit;

namespace Wizard.Perf.Tests
{
    // 평균과 백분위(가장 가까운 순위: 정렬한 n 개 중 ceil(q·n) 번째)
    public sealed class DurationsTests
    {
        private static long[] OneToHundred()
        {
            var s = new long[100];
            for (int i = 0; i < s.Length; i++)
            {
                s[s.Length - 1 - i] = i + 1;     // 거꾸로 넣는다(정렬을 시험한다)
            }
            return s;
        }

        [Fact]
        public void Percentile_NearestRank()
        {
            long[] s = OneToHundred();
            Assert.Equal(Durations.ToMicros(99), Durations.PercentileMicros(s, 100, 99));
            Assert.Equal(Durations.ToMicros(100), Durations.PercentileMicros(s, 100, 100));
            Assert.Equal(Durations.ToMicros(50), Durations.PercentileMicros(s, 100, 50));
            Assert.Equal(Durations.ToMicros(1), Durations.PercentileMicros(s, 100, 1));
            Assert.Equal(100, s[0]);          // 원본은 그대로
        }

        [Fact]
        public void Percentile_SmallCounts()
        {
            var s = new long[] { 7, 3, 5 };
            Assert.Equal(Durations.ToMicros(7), Durations.PercentileMicros(s, 3, 99));     // ceil(2.97) = 3번째
            Assert.Equal(Durations.ToMicros(7), Durations.PercentileMicros(s, 1, 99));     // 앞 1칸(7)만 본다
            Assert.Equal(Durations.ToMicros(3), Durations.PercentileMicros(s, 3, 1));      // ceil(0.03) = 1번째
        }

        [Fact]
        public void Mean_AndUnits()
        {
            Assert.Equal(Durations.ToMicros(5050) / 100, Durations.MeanMicros(OneToHundred(), 100), 9);
            Assert.Equal(1_000_000.0, Durations.ToMicros(Stopwatch.Frequency), 6);
        }

        [Fact]
        public void ClockQuantum_IsPositiveAndBelowAMicrosecond()
        {
            double q = Durations.ClockQuantumMicros();
            Assert.True(q > 0 && q < 1, "clock quantum " + q);
        }
    }
}

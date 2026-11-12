using System;
using System.Diagnostics;

namespace Wizard.Perf
{
    // Stopwatch 눈금 → 마이크로초, 평균과 백분위(가장 가까운 순위: 정렬한 n 개 중 ceil(q·n) 번째)
    internal static class Durations
    {
        private const double MicrosPerSecond = 1_000_000.0;
        public const long MillisPerSecond = 1000;
        private const int PercentScale = 100;

        private const int QuantumProbes = 2000;

        public static double ToMicros(long stopwatchTicks) => stopwatchTicks * MicrosPerSecond / Stopwatch.Frequency;

        // 시계가 실제로 움직이는 가장 작은 걸음(마이크로초). Stopwatch.Frequency 는 macOS 에서 1e9 이지만 바탕 시계는 24MHz 라
        // 값이 약 41.7ns 단위로만 바뀐다. 읽기를 되풀이해 값이 바뀌는 순간의 차이 가운데 가장 작은 것을 쓴다
        public static double ClockQuantumMicros()
        {
            long best = long.MaxValue;
            for (int i = 0; i < QuantumProbes; i++)
            {
                long a = Stopwatch.GetTimestamp();
                long b;
                do
                {
                    b = Stopwatch.GetTimestamp();
                }
                while (b == a);
                best = Math.Min(best, b - a);
            }
            return ToMicros(best);
        }

        public static double MeanMicros(long[] samples, int count)
        {
            if (count <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }
            long sum = 0;
            for (int i = 0; i < count; i++)
            {
                sum += samples[i];
            }
            return ToMicros(sum) / count;
        }

        // percent = 99 → p99. samples 의 앞 count 칸을 정렬한 사본에서 고른다(원본은 그대로)
        public static double PercentileMicros(long[] samples, int count, int percent)
        {
            if (count <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }
            if (percent <= 0 || percent > PercentScale)
            {
                throw new ArgumentOutOfRangeException(nameof(percent));
            }
            var sorted = new long[count];
            Array.Copy(samples, sorted, count);
            Array.Sort(sorted);
            long rank = ((long)percent * count + PercentScale - 1) / PercentScale;   // ceil(percent·count/100), 1부터
            return ToMicros(sorted[rank - 1]);
        }
    }
}

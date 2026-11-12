using System;
using System.Diagnostics;
using Wizard.Sim;

namespace Wizard.Perf
{
    // 스냅숏 저장 + 복원(PIPELINE 7.5: 합쳐서 0.1ms 이하, 할당 0바이트). 되감기의 꼴로 잰다:
    //   해시 h → [저장] → 한 틱 진행 → [복원] → 해시가 h 인지 확인 → 두 틱 진행(다음 쌍은 한 틱 뒤의 상태에서)
    // [ ] 안만 시계와 할당 계수기로 잰다. 회차의 진행 전체가 GC 없는 구간(NoGcWindow)이고, 구간 중 GC 가 돈 회차의 할당은 쓰지 않는다. 복원 뒤 해시가 다르면 벤치가 틀린 수를 내지 않도록 예외로 멈춘다.
    internal static class SnapshotBench
    {
        private const int P99 = 99;

        public static SnapshotStats Measure(PerfScenario scenario, StepFeed feed, BenchSettings settings)
        {
            int n = settings.SnapshotIterations;
            var samples = new long[n];
            var stats = new SnapshotStats
            {
                MeanMicros = double.MaxValue,
                P99Micros = double.MaxValue,
                AllocBytes = long.MaxValue,
                AllocBytesMax = 0,
                PairsPerRound = n,
                Rounds = settings.Rounds,
            };
            int validAllocRounds = 0;
            for (int round = 0; round < settings.Rounds; round++)
            {
                SimWorld world = ScenarioInfo.Create(scenario);
                var sink = new CountingSink();
                while (world.Tick < (uint)settings.WarmupTicks)
                {
                    feed.StepAt(world, sink);
                }
                SimSnapshot snapshot = world.CreateSnapshotBuffer();     // 버퍼는 한 번 만든다(CONTRACTS 3.2)
                world.SaveSnapshot(snapshot);                             // 데우기
                world.LoadSnapshot(snapshot);
                long allocated = 0;
                bool gcFree;
                NoGcWindow window = NoGcWindow.Open();
                try
                {
                    for (int i = 0; i < n; i++)
                    {
                        ulong before = world.ComputeStateHash();
                        uint tick = world.Tick;

                        long a0 = GC.GetAllocatedBytesForCurrentThread();
                        long t0 = Stopwatch.GetTimestamp();
                        world.SaveSnapshot(snapshot);
                        long t1 = Stopwatch.GetTimestamp();
                        long a1 = GC.GetAllocatedBytesForCurrentThread();

                        feed.StepAt(world, sink);

                        long a2 = GC.GetAllocatedBytesForCurrentThread();
                        long t2 = Stopwatch.GetTimestamp();
                        world.LoadSnapshot(snapshot);
                        long t3 = Stopwatch.GetTimestamp();
                        long a3 = GC.GetAllocatedBytesForCurrentThread();

                        samples[i] = (t1 - t0) + (t3 - t2);
                        allocated += (a1 - a0) + (a3 - a2);
                        if (world.Tick != tick || world.ComputeStateHash() != before)
                        {
                            throw new InvalidOperationException("perf: LoadSnapshot did not restore the saved state (" + ScenarioInfo.Id(scenario)
                                                                + ", tick " + tick + ")");
                        }
                        feed.StepAt(world, sink);
                        feed.StepAt(world, sink);
                    }
                    gcFree = window.Valid;
                }
                finally
                {
                    window.Dispose();
                }
                if (gcFree)
                {
                    validAllocRounds++;
                    stats.AllocBytes = Math.Min(stats.AllocBytes, allocated);
                    stats.AllocBytesMax = Math.Max(stats.AllocBytesMax, allocated);
                }
                stats.MeanMicros = Math.Min(stats.MeanMicros, Durations.MeanMicros(samples, n));
                stats.P99Micros = Math.Min(stats.P99Micros, Durations.PercentileMicros(samples, n, P99));
            }
            if (validAllocRounds == 0)
            {
                throw new InvalidOperationException("perf: no snapshot round of " + ScenarioInfo.Id(scenario) + " ran without a GC (NoGcWindow)");
            }
            return stats;
        }
    }
}

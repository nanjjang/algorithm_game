using System;
using System.Diagnostics;
using Wizard.Sim;
using Wizard.Sim.Events;

namespace Wizard.Perf
{
    // 틱 시간(평균, p99)과 Step 의 관리 힙 할당(PIPELINE 7.5). 회차마다 새 세계를 만들어 데운 뒤 MeasureTicks 틱을 하나씩 잰다.
    // 잰 구간에는 Step 과 시계 읽기만 있다. 할당 계수기와 부하 표본(ISimView 의 목록 길이)은 시계 밖에서 읽는다(둘 다 할당 없음).
    // 할당은 틱마다 읽어 0단계 명령이 없는 틱(StepAllocBytes, 판정 항목)과 있는 틱(CommandTickAllocBytes, 참고)으로 나눈다:
    // 0단계의 주문 컴파일만 Step 할당 0의 예외다(CONTRACTS 3.2). M1 의 0단계 명령(시제품의 조립 바꾸기 SetSlot)은 SpellTable 에 컴파일하지 않으므로 둘 다 0이어야 한다
    // (StepAllocationTests 가 단언한다). 재는 구간은 GC 없는 구간(NoGcWindow)이고, 구간 중 GC 가 돈 회차의 할당은 쓰지 않는다.
    internal static class TickBench
    {
        private const int P99 = 99;
        private const int P100 = 100;

        public static TickStats Measure(PerfScenario scenario, BenchSettings settings)
        {
            StepFeed feed = StepFeed.From(KeyScript.Generate(ScenarioInfo.ScriptSeed(scenario), settings.ScriptTicks,
                                                             ScenarioInfo.Players(scenario), ScenarioInfo.UsesCommands(scenario)));
            return Measure(scenario, feed, settings);
        }

        public static TickStats Measure(PerfScenario scenario, StepFeed feed, BenchSettings settings) =>
            Measure(scenario, feed, settings, () => new CountingSink());

        // newSink: 회차마다의 받는 쪽. 시험이 일부러 할당하는 받는 쪽을 넣어 계수가 Step 안의 할당을 잡는지 본다
        public static TickStats Measure(PerfScenario scenario, StepFeed feed, BenchSettings settings, Func<ISimEventSink> newSink)
        {
            int n = settings.MeasureTicks;
            var samples = new long[n];
            var stats = new TickStats
            {
                MeanMicros = double.MaxValue,
                P99Micros = double.MaxValue,
                StepAllocBytes = long.MaxValue,
                StepAllocBytesMax = 0,
                CommandTickAllocBytes = long.MaxValue,
                TicksPerRound = n,
                Rounds = settings.Rounds,
            };
            int validAllocRounds = 0;
            for (int round = 0; round < settings.Rounds; round++)
            {
                SimWorld world = ScenarioInfo.Create(scenario);
                ISimEventSink sink = newSink();
                while (world.Tick < (uint)settings.WarmupTicks)
                {
                    feed.StepAt(world, sink);
                }
                CountingSink? counter = sink as CountingSink;
                long events0 = counter?.Count ?? 0, spawned0 = counter?.ProjectilesSpawned ?? 0;
                long enemies0 = counter?.EnemiesSpawned ?? 0, shots0 = counter?.EnemyShotsFired ?? 0;
                long viewProjectiles = 0, viewEnemies = 0;
                int maxViewProjectiles = 0, maxViewEnemies = 0, commandTicks = 0;
                long allocated = 0, commandAllocated = 0;
                bool gcFree;
                NoGcWindow window = NoGcWindow.Open();
                try
                {
                    for (int i = 0; i < n; i++)
                    {
                        bool commands = feed.HasCommandsAt(world.Tick);
                        long a0 = GC.GetAllocatedBytesForCurrentThread();
                        long t0 = Stopwatch.GetTimestamp();
                        feed.StepAt(world, sink);
                        long t1 = Stopwatch.GetTimestamp();
                        long a1 = GC.GetAllocatedBytesForCurrentThread();
                        samples[i] = t1 - t0;
                        if (commands)
                        {
                            commandAllocated += a1 - a0;
                            commandTicks++;
                        }
                        else
                        {
                            allocated += a1 - a0;
                        }

                        int vp = world.View.Projectiles.Length, ve = world.View.Enemies.Length;
                        viewProjectiles += vp;
                        viewEnemies += ve;
                        maxViewProjectiles = Math.Max(maxViewProjectiles, vp);
                        maxViewEnemies = Math.Max(maxViewEnemies, ve);
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
                    stats.StepAllocBytes = Math.Min(stats.StepAllocBytes, allocated);
                    stats.StepAllocBytesMax = Math.Max(stats.StepAllocBytesMax, allocated);
                    stats.CommandTickAllocBytes = Math.Min(stats.CommandTickAllocBytes, commandAllocated);
                }
                double mean = Durations.MeanMicros(samples, n);
                double p99 = Durations.PercentileMicros(samples, n, P99);
                stats.MeanMicros = Math.Min(stats.MeanMicros, mean);
                if (p99 < stats.P99Micros)
                {
                    stats.P99Micros = p99;
                    stats.MaxMicros = Durations.PercentileMicros(samples, n, P100);
                }
                stats.CommandTicks = commandTicks;
                stats.MeanViewProjectiles = (double)viewProjectiles / n;
                stats.MeanViewEnemies = (double)viewEnemies / n;
                stats.MaxViewProjectiles = maxViewProjectiles;
                stats.MaxViewEnemies = maxViewEnemies;
                if (counter != null)
                {
                    stats.EventsPerTick = (double)(counter.Count - events0) / n;
                    stats.ProjectilesSpawnedPerTick = (double)(counter.ProjectilesSpawned - spawned0) / n;
                    stats.EnemiesSpawnedPerTick = (double)(counter.EnemiesSpawned - enemies0) / n;
                    stats.EnemyShotsPerTick = (double)(counter.EnemyShotsFired - shots0) / n;
                }
            }
            if (validAllocRounds == 0)
            {
                throw new InvalidOperationException("perf: no round of " + ScenarioInfo.Id(scenario) + " ran without a GC (NoGcWindow)");
            }
            return stats;
        }

        // 재기 전에 앞선 할당(각본, 세계 생성)의 수거를 끝내 둔다. Step 은 할당하지 않으므로 재는 동안 GC 가 돌 일이 없다
        internal static void Settle()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }
}

using System;
using System.Diagnostics;
using System.Reflection;
using Wizard.Sim;

namespace Wizard.Perf
{
    // 벤치 전부를 돌려 PerfLimits.All 의 항목을 채운다. 항목 id 는 PerfLimits 의 것과 같아야 한다(PerfSuiteTests 가 본다)
    internal static class PerfSuite
    {
        private static readonly PerfScenario[] TickScenarios = { PerfScenario.Duel2p, PerfScenario.PracticeProto, PerfScenario.Endless1p };
        private static readonly PerfScenario[] SnapshotScenarios = { PerfScenario.Duel2p, PerfScenario.PracticeProto };

        public static PerfResults Run(BenchSettings settings, string settingsName, Action<string>? log)
        {
            var r = new PerfResults { Settings = settingsName };
            r.FillMachine();
            r.Machine["thread_qos"] = ThreadQos.Raise();
            r.Context[PerfCompare.ClockQuantumKey] = Durations.ClockQuantumMicros();
            foreach (PerfScenario s in TickScenarios)
            {
                string id = ScenarioInfo.Id(s);
                log?.Invoke("tick " + id);
                StepFeed feed = StepFeed.From(KeyScript.Generate(ScenarioInfo.ScriptSeed(s), settings.ScriptTicks,
                                                                 ScenarioInfo.Players(s), ScenarioInfo.UsesCommands(s)));
                JitWarmup.For(settings.JitWarmupMillis, () => JitWarmup.ScenarioPass(s, feed));
                TickStats t = TickBench.Measure(s, feed, settings);
                r.Metrics["tick." + id + ".mean_us"] = t.MeanMicros;
                r.Metrics["tick." + id + ".p99_us"] = t.P99Micros;
                r.Metrics["alloc." + id + ".step_bytes"] = t.StepAllocBytes;
                r.Context["tick." + id + ".max_us"] = t.MaxMicros;
                r.Context["tick." + id + ".ticks_per_round"] = t.TicksPerRound;
                r.Context["tick." + id + ".rounds"] = t.Rounds;
                r.Context["tick." + id + ".events_per_tick"] = t.EventsPerTick;
                r.Context["tick." + id + ".alloc_bytes_max_round"] = t.StepAllocBytesMax;
                r.Context["tick." + id + ".command_tick_alloc_bytes"] = t.CommandTickAllocBytes;
                r.Context["tick." + id + ".command_ticks"] = t.CommandTicks;
                r.Context["load." + id + ".view_projectiles_mean"] = t.MeanViewProjectiles;
                r.Context["load." + id + ".view_projectiles_max"] = t.MaxViewProjectiles;
                r.Context["load." + id + ".view_enemies_mean"] = t.MeanViewEnemies;
                r.Context["load." + id + ".view_enemies_max"] = t.MaxViewEnemies;
                r.Context["load." + id + ".projectiles_spawned_per_tick"] = t.ProjectilesSpawnedPerTick;
                r.Context["load." + id + ".enemies_spawned_per_tick"] = t.EnemiesSpawnedPerTick;
                r.Context["load." + id + ".enemy_shots_per_tick"] = t.EnemyShotsPerTick;

                if (Array.IndexOf(SnapshotScenarios, s) >= 0)
                {
                    log?.Invoke("snapshot " + id);
                    SnapshotStats sn = SnapshotBench.Measure(s, feed, settings);
                    r.Metrics["snapshot." + id + ".save_load_mean_us"] = sn.MeanMicros;
                    r.Metrics["snapshot." + id + ".save_load_p99_us"] = sn.P99Micros;
                    r.Metrics["snapshot." + id + ".alloc_bytes"] = sn.AllocBytes;
                    r.Context["snapshot." + id + ".pairs_per_round"] = sn.PairsPerRound;
                    r.Context["snapshot." + id + ".alloc_bytes_max_round"] = sn.AllocBytesMax;
                }
            }
            log?.Invoke("replay " + ScenarioInfo.Id(PerfScenario.Endless1p));
            ReplayStats rp = ReplayBench.Measure(settings);
            r.Metrics["replay.endless_1p.ticks_per_sec"] = rp.TicksPerSecond;
            r.Context["replay.endless_1p.realtime_multiple"] = rp.RealtimeMultiple;
            r.Context["replay.endless_1p.record_ticks"] = rp.RecordTicks;
            r.Context["replay.endless_1p.record_bytes"] = rp.RecordBytes;
            r.Context["replay.endless_1p.rounds"] = rp.Rounds;
            return r;
        }

        // 최적화를 끈 빌드(Debug)의 시간은 기준선과 견줄 수 없다. 재는 명령은 Release 에서만 돈다
        public static string? NotOptimized()
        {
            foreach (Assembly a in new[] { typeof(PerfSuite).Assembly, typeof(SimWorld).Assembly, typeof(Wizard.Core.Fix64).Assembly })
            {
                DebuggableAttribute? d = a.GetCustomAttribute<DebuggableAttribute>();
                if (d != null && d.IsJITOptimizerDisabled)
                {
                    return a.GetName().Name;
                }
            }
            return null;
        }
    }
}

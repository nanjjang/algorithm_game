using System.Collections.Generic;
using Xunit;

namespace Wizard.Perf.Tests
{
    // 하네스 전체(짧은 설정): 판정 항목을 빠짐없이 채우고, 할당 항목이 0이고, 그 결과가 한도 판정을 지난다(할당·형식 쪽만 본다.
    // 시간은 시험에서 판정하지 않는다: 흔들리는 시험을 남기지 않는다. 시간 한도는 perf.sh 가 Release 에서 본다)
    [Collection(AllocationCollection.Name)]
    public sealed class PerfSuiteTests
    {
        private static readonly PerfResults Quick = PerfSuite.Run(BenchSettings.Quick(), "quick", null);

        [Fact]
        public void FillsExactlyTheDefinedMetrics()
        {
            var defined = new SortedSet<string>();
            foreach (MetricDef d in PerfLimits.All)
            {
                defined.Add(d.Id);
            }
            Assert.Equal(defined, new SortedSet<string>(Quick.Metrics.Keys));
        }

        [Fact]
        public void AllocationMetrics_AreZero()
        {
            foreach (KeyValuePair<string, double> kv in Quick.Metrics)
            {
                if (kv.Key.EndsWith("_bytes") || kv.Key.EndsWith(".alloc_bytes"))
                {
                    Assert.True(kv.Value == 0, kv.Key + " = " + kv.Value);
                }
            }
            Assert.Equal(0, Quick.Context["tick.practice_proto.command_tick_alloc_bytes"]);
        }

        [Fact]
        public void RecordsLoadClockAndMachine()
        {
            Assert.True(Quick.Context[PerfCompare.ClockQuantumKey] > 0);
            Assert.True(Quick.Context["load.practice_proto.projectiles_spawned_per_tick"] > 0);
            Assert.True(Quick.Context["load.practice_proto.enemies_spawned_per_tick"] > 0);
            Assert.True(Quick.Context.ContainsKey("tick.practice_proto.command_ticks"));   // 짧은 설정의 창(900틱)에는 명령이 없을 수 있다
            Assert.Equal(BenchSettings.Quick().ReplayTicks, Quick.Context["replay.endless_1p.record_ticks"]);
            Assert.True(Quick.Machine.ContainsKey("arch"));
            Assert.Equal("quick", Quick.Settings);
        }

        [Fact]
        public void ResultsFile_RoundTrips()
        {
            PerfResults back = PerfResults.Parse(Quick.ToJson(), "quick");
            Assert.Equal(Quick.ToJson(), back.ToJson());
        }
    }
}

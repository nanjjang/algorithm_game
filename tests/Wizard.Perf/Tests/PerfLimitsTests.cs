using System.Collections.Generic;
using Xunit;

namespace Wizard.Perf.Tests
{
    // PerfLimits 의 값이 PIPELINE 7.5 표와 같다(표의 숫자를 그대로 옮긴 것. 한도를 바꾸려면 표가 먼저 바뀐다)
    public sealed class PerfLimitsTests
    {
        [Theory]
        [InlineData("tick.duel_2p.mean_us", 200.0)]           // 결투 틱 평균 0.2ms
        [InlineData("tick.duel_2p.p99_us", 500.0)]            // 결투 틱 p99 0.5ms
        [InlineData("tick.practice_proto.p99_us", 1000.0)]    // 최악 사례 틱 p99 1.0ms
        [InlineData("tick.endless_1p.p99_us", 1000.0)]
        [InlineData("alloc.duel_2p.step_bytes", 0.0)]         // Step 할당 0
        [InlineData("alloc.practice_proto.step_bytes", 0.0)]
        [InlineData("alloc.endless_1p.step_bytes", 0.0)]
        [InlineData("snapshot.duel_2p.save_load_p99_us", 100.0)]       // 저장 + 복원 0.1ms
        [InlineData("snapshot.practice_proto.save_load_p99_us", 100.0)]
        [InlineData("snapshot.duel_2p.alloc_bytes", 0.0)]              // 저장 + 복원 할당 0
        [InlineData("snapshot.practice_proto.alloc_bytes", 0.0)]
        [InlineData("replay.endless_1p.ticks_per_sec", 6000.0)]        // 실시간(60틱) × 100
        public void Limit_MatchesPipelineTable(string id, double limit)
        {
            MetricDef? d = PerfLimits.Find(id);
            Assert.NotNull(d);
            Assert.Equal(limit, d!.Limit);
        }

        [Fact]
        public void RegressionAllowance_IsFifteenPercent()
        {
            Assert.Equal(0.15, PerfLimits.RegressionPermille / PerfLimits.PermilleScale, 12);
        }

        [Fact]
        public void Ids_AreUnique_AndDirectionsMatchUnits()
        {
            var seen = new HashSet<string>();
            foreach (MetricDef d in PerfLimits.All)
            {
                Assert.True(seen.Add(d.Id), d.Id);
                Assert.Equal(d.Unit == "ticks/s" ? Better.Higher : Better.Lower, d.Better);
            }
        }
    }
}

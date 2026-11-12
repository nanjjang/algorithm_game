using System.IO;
using Xunit;

namespace Wizard.Perf.Tests
{
    // 명령줄의 끝 코드(perf.sh 가 그대로 돌려준다): 0 통과, 1 한도·회귀 실패, 2 사용법·꼴 오류.
    // compare 는 재지 않으므로 시험에서 게이트의 판정 자체를 끝까지 돌려 볼 수 있다(결함 주입: 기준선보다 20% 느린 결과).
    [Collection(ConsoleCollection.Name)]
    public sealed class ProgramTests
    {
        private static string Write(string dir, string name, PerfResults r)
        {
            string p = Path.Combine(dir, name);
            r.Save(p);
            return p;
        }

        private static PerfResults Passing()
        {
            var r = new PerfResults { Settings = "full" };
            foreach (MetricDef d in PerfLimits.All)
            {
                r.Metrics[d.Id] = d.Better == Better.Higher ? d.Limit!.Value * 10 : d.Limit.HasValue ? d.Limit.Value / 2 : 1;
            }
            r.Context[PerfCompare.ClockQuantumKey] = 0.0417;
            return r;
        }

        [Fact]
        public void Compare_SameAsBaseline_Passes()
        {
            string dir = PerfFixtures.TempDir();
            string b = Write(dir, "b.json", Passing());
            Assert.Equal(0, Program.Main(new[] { "compare", "--baseline", b, "--results", b }));
        }

        [Fact]
        public void Compare_TwentyPercentSlower_FailsWithOne()
        {
            string dir = PerfFixtures.TempDir();
            PerfResults slow = Passing();
            slow.Metrics["tick.duel_2p.mean_us"] *= 1.2;    // 100us → 120us: 한도 안, 기준선보다 20% 느림
            string b = Write(dir, "b.json", Passing());
            string m = Write(dir, "m.json", slow);
            Assert.Equal(1, Program.Main(new[] { "compare", "--baseline", b, "--results", m }));
        }

        [Fact]
        public void Compare_LimitBroken_FailsEvenAgainstTheSameBaseline()
        {
            string dir = PerfFixtures.TempDir();
            PerfResults bad = Passing();
            bad.Metrics["alloc.duel_2p.step_bytes"] = 24;
            string p = Write(dir, "m.json", bad);
            Assert.Equal(1, Program.Main(new[] { "compare", "--baseline", p, "--results", p }));
        }

        [Fact]
        public void Compare_DifferentSettings_Fails()
        {
            string dir = PerfFixtures.TempDir();
            PerfResults quick = Passing();
            quick.Settings = "quick";
            Assert.Equal(1, Program.Main(new[] { "compare", "--baseline", Write(dir, "b.json", Passing()), "--results", Write(dir, "m.json", quick) }));
        }

        [Fact]
        public void BadFileOrUsage_ExitsWithTwo()
        {
            string dir = PerfFixtures.TempDir();
            string junk = Path.Combine(dir, "junk.json");
            File.WriteAllText(junk, "{\"format\":9}");
            Assert.Equal(2, Program.Main(new[] { "compare", "--baseline", junk, "--results", junk }));
            Assert.Equal(2, Program.Main(new[] { "compare", "--baseline", Path.Combine(dir, "absent.json"), "--results", junk }));
            Assert.Equal(2, Program.Main(new string[0]));
            Assert.Equal(2, Program.Main(new[] { "nope" }));
            Assert.Equal(2, Program.Main(new[] { "compare", "--baseline" }));
            Assert.Equal(2, Program.Main(new[] { "run", "--baseline", junk }));
            Assert.Equal(2, Program.Main(new[] { "baseline", "--out", junk, "--allow-slower" }));
        }

        // 최적화하지 않은 빌드(Debug)의 시간은 기준선과 견줄 수 없으므로 재는 명령은 재지 않고 2로 끝난다. Release 빌드는 최적화되어 있다
        [Fact]
        public void OptimizationCheck_MatchesTheBuildConfiguration()
        {
#if DEBUG
            Assert.NotNull(PerfSuite.NotOptimized());
            Assert.Equal(2, Program.Main(new[] { "run", "--quick" }));
            Assert.Equal(2, Program.Main(new[] { "check", "--quick", "--baseline", "unused.json" }));
#else
            Assert.Null(PerfSuite.NotOptimized());
#endif
        }
    }

    [CollectionDefinition(Name, DisableParallelization = true)]
    public sealed class ConsoleCollection
    {
        public const string Name = "console";
    }
}

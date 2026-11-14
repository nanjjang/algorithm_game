using System.Collections.Generic;
using System.IO;
using Wizard.TestKit;
using Xunit;

namespace Wizard.Perf.Tests
{
    // tests/perf/baseline.json: perf.sh 가 견주는 기준선. 꼴이 맞고, 판정 항목을 빠짐없이 가지며, 그 자신이 한도를 지킨다
    // (Program baseline 은 한도를 넘는 측정을 쓰지 않는다). Full 설정으로 잰 것이어야 한다(Quick 은 JIT 데우기가 없다)
    public sealed class BaselineFileTests
    {
        private static string BaselinePath => Path.Combine(TestRepo.Root, "tests", "perf", "baseline.json");

        [Fact]
        public void Baseline_IsFullSettings_HasEveryMetric_AndKeepsTheLimits()
        {
            PerfResults b = PerfResults.Load(BaselinePath);
            Assert.Equal("full", b.Settings);
            List<MetricVerdict> v = PerfCompare.Compare(PerfLimits.All, b, null);
            Assert.True(PerfCompare.AllPassed(v), PerfCompare.Table(v));
            Assert.Equal(PerfLimits.All.Count, b.Metrics.Count);
            Assert.True(b.Context.ContainsKey(PerfCompare.ClockQuantumKey));
        }

        [Fact]
        public void Baseline_ComparedWithItself_Passes()
        {
            PerfResults b = PerfResults.Load(BaselinePath);
            Assert.True(PerfCompare.AllPassed(PerfCompare.Compare(PerfLimits.All, b, b)));
        }

        [Fact]
        public void Baseline_IsWrittenInCanonicalForm()
        {
            // 저장소의 파일은 LF 다. 기대 문자열은 '\n' 줄 끝으로 만든다(OS 의 줄 끝에 기대지 않는다. T-67 5번):
            // Utf8JsonWriter 의 들여쓰기 줄바꿈은 기본이 Environment.NewLine 이라 Windows 에서는 "\r\n" 으로 나온다
            string text = File.ReadAllText(BaselinePath);
            string expected = PerfResults.Parse(text, "baseline").ToJson().Replace("\r\n", "\n", System.StringComparison.Ordinal);
            Assert.Equal(expected, text);
        }
    }
}

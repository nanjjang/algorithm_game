using System;
using System.IO;
using Xunit;

namespace Wizard.Perf.Tests
{
    // 결과·기준선 파일의 꼴(PerfResults 머리말): 왕복, 같은 값이면 같은 바이트, 틀린 꼴은 FormatException
    public sealed class PerfResultsTests
    {
        private static PerfResults Sample()
        {
            var r = new PerfResults { Settings = "full", Note = "n" };
            r.Machine["os"] = "x";
            r.Metrics["b.metric"] = 2.5;
            r.Metrics["a.metric"] = 0;
            r.Context["c"] = 1.23456;
            return r;
        }

        [Fact]
        public void RoundTrip_KeepsEverything_AndRoundsToThreeDecimals()
        {
            PerfResults back = PerfResults.Parse(Sample().ToJson(), "t");
            Assert.Equal("full", back.Settings);
            Assert.Equal("n", back.Note);
            Assert.Equal("x", back.Machine["os"]);
            Assert.Equal(2.5, back.Metrics["b.metric"]);
            Assert.Equal(0, back.Metrics["a.metric"]);
            Assert.Equal(1.235, back.Context["c"]);
            Assert.Equal(Sample().ToJson(), back.ToJson());
        }

        [Fact]
        public void Keys_AreWrittenInOrdinalOrder()
        {
            string json = Sample().ToJson();
            Assert.True(json.IndexOf("\"a.metric\"", StringComparison.Ordinal) < json.IndexOf("\"b.metric\"", StringComparison.Ordinal));
        }

        [Fact]
        public void SaveLoad_ThroughAFile()
        {
            string path = Path.Combine(PerfFixtures.TempDir(), "sub", "r.json");
            Sample().Save(path);
            Assert.Equal(2.5, PerfResults.Load(path).Metrics["b.metric"]);
        }

        [Theory]
        [InlineData("[]")]
        [InlineData("not json")]
        [InlineData("{\"format\":2,\"kind\":\"wizard-perf\",\"metrics\":{}}")]
        [InlineData("{\"format\":1,\"kind\":\"other\",\"metrics\":{}}")]
        [InlineData("{\"format\":1,\"kind\":\"wizard-perf\"}")]
        [InlineData("{\"format\":1,\"kind\":\"wizard-perf\",\"metrics\":{},\"extra\":1}")]
        [InlineData("{\"format\":1,\"kind\":\"wizard-perf\",\"metrics\":{\"a\":-1}}")]
        [InlineData("{\"format\":1,\"kind\":\"wizard-perf\",\"metrics\":{\"a\":\"1\"}}")]
        [InlineData("{\"format\":1,\"kind\":\"wizard-perf\",\"metrics\":{\"a\":1,\"a\":2}}")]
        [InlineData("{\"format\":1,\"kind\":\"wizard-perf\",\"metrics\":[]}")]
        [InlineData("{\"format\":1,\"kind\":\"wizard-perf\",\"metrics\":{},\"machine\":{\"os\":1}}")]
        public void BadShape_IsRejected(string json)
        {
            Assert.Throws<FormatException>(() => PerfResults.Parse(json, "t"));
        }
    }
}

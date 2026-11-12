using System.Collections.Generic;
using Xunit;

namespace Wizard.Perf.Tests
{
    // PIPELINE 7.5 의 판정: 한도(방향별), 기준선보다 15% 넘게 나빠지면 회귀, 빠진 항목·모르는 항목은 실패.
    // 정의는 시험용으로 따로 만든다(한도 값 자체는 PerfLimitsTests 가 표와 맞춘다).
    public sealed class PerfCompareTests
    {
        private static readonly MetricDef TimeLow = new MetricDef("t.mean_us", "us", Better.Lower, 10, "test");
        private static readonly MetricDef BytesLow = new MetricDef("a.bytes", "bytes", Better.Lower, 0, "test");
        private static readonly MetricDef SpeedHigh = new MetricDef("r.ticks_per_sec", "ticks/s", Better.Higher, 100, "test");
        private static readonly MetricDef NoLimit = new MetricDef("t.p99_us", "us", Better.Lower, null, "test");
        private static readonly IReadOnlyList<MetricDef> Defs = new[] { TimeLow, BytesLow, SpeedHigh, NoLimit };

        private static PerfResults Results(double t, double a, double r, double p, double? quantum = null)
        {
            var x = new PerfResults();
            x.Metrics[TimeLow.Id] = t;
            x.Metrics[BytesLow.Id] = a;
            x.Metrics[SpeedHigh.Id] = r;
            x.Metrics[NoLimit.Id] = p;
            if (quantum.HasValue)
            {
                x.Context[PerfCompare.ClockQuantumKey] = quantum.Value;
            }
            return x;
        }

        private static MetricVerdict Verdict(List<MetricVerdict> v, string id) => v.Find(x => x.Id == id)!;

        [Fact]
        public void WithinLimits_NoBaseline_Passes()
        {
            List<MetricVerdict> v = PerfCompare.Compare(Defs, Results(10, 0, 100, 999), null);
            Assert.True(PerfCompare.AllPassed(v));
        }

        [Fact]
        public void LowerIsBetter_AboveLimit_Fails()
        {
            List<MetricVerdict> v = PerfCompare.Compare(Defs, Results(10.001, 0, 100, 1), null);
            Assert.False(Verdict(v, TimeLow.Id).Passed);
            Assert.StartsWith("limit:", Verdict(v, TimeLow.Id).Failures[0]);
            Assert.False(PerfCompare.AllPassed(v));
        }

        [Fact]
        public void ZeroByteLimit_OneByte_Fails()
        {
            List<MetricVerdict> v = PerfCompare.Compare(Defs, Results(1, 1, 100, 1), null);
            Assert.False(Verdict(v, BytesLow.Id).Passed);
        }

        [Fact]
        public void HigherIsBetter_BelowLimit_Fails()
        {
            List<MetricVerdict> v = PerfCompare.Compare(Defs, Results(1, 0, 99.9, 1), null);
            Assert.False(Verdict(v, SpeedHigh.Id).Passed);
            Assert.True(Verdict(v, TimeLow.Id).Passed);
        }

        // "15% 넘게 나빠지면 실패": 정확히 15% 는 통과, 그보다 조금이라도 크면 실패
        [Fact]
        public void Regression_Lower_ExactlyFifteenPercentPasses_MoreFails()
        {
            PerfResults baseline = Results(1, 0, 1000, 100);
            Assert.True(PerfCompare.AllPassed(PerfCompare.Compare(Defs, Results(1, 0, 1000, 115), baseline)));
            List<MetricVerdict> v = PerfCompare.Compare(Defs, Results(1, 0, 1000, 115.01), baseline);
            Assert.False(Verdict(v, NoLimit.Id).Passed);
            Assert.StartsWith("regression:", Verdict(v, NoLimit.Id).Failures[0]);
        }

        [Fact]
        public void Regression_Higher_ExactlyFifteenPercentPasses_MoreFails()
        {
            PerfResults baseline = Results(1, 0, 1000, 100);
            Assert.True(PerfCompare.AllPassed(PerfCompare.Compare(Defs, Results(1, 0, 850, 100), baseline)));
            List<MetricVerdict> v = PerfCompare.Compare(Defs, Results(1, 0, 849.9, 100), baseline);
            Assert.False(Verdict(v, SpeedHigh.Id).Passed);
        }

        [Fact]
        public void Improvement_IsNotARegression()
        {
            PerfResults baseline = Results(5, 0, 1000, 100);
            Assert.True(PerfCompare.AllPassed(PerfCompare.Compare(Defs, Results(1, 0, 5000, 10), baseline)));
        }

        // 시계 눈금 [임시]: 시간 항목은 차이가 눈금 2개 이하면 회귀로 보지 않는다. 눈금 2개를 넘으면 15% 를 그대로 본다
        // (눈금은 이진 실수로 정확한 1/16 을 쓴다. 경계에서 반올림 오차가 판정을 바꾸지 않게)
        [Fact]
        public void ClockQuantum_TwoTicksOrLessIsNotARegression()
        {
            const double q = 0.0625;
            PerfResults baseline = Results(0.25, 0, 1000, 0.25, q);
            Assert.True(PerfCompare.AllPassed(PerfCompare.Compare(Defs, Results(0.25 + 2 * q, 0, 1000, 0.25, q), baseline)));
            List<MetricVerdict> v = PerfCompare.Compare(Defs, Results(0.25 + 2 * q + 0.001, 0, 1000, 0.25, q), baseline);
            Assert.False(Verdict(v, TimeLow.Id).Passed);
        }

        [Fact]
        public void ClockQuantum_UsesTheLargerOfBothFiles_AndOnlyForTime()
        {
            PerfResults baseline = Results(1, 0, 1000, 1, 0.1);
            PerfResults measured = Results(1.2, 0, 1000, 1, 0.01);
            Assert.Equal(0.2, PerfCompare.TimeFloorMicros(measured, baseline), 9);
            Assert.True(PerfCompare.AllPassed(PerfCompare.Compare(Defs, measured, baseline)));
            // 단위가 시간이 아니면 눈금을 쓰지 않는다
            Assert.True(PerfCompare.Regressed(SpeedHigh, 1, 2, 1000));
            Assert.False(PerfCompare.Regressed(NoLimit, 2, 1, 1000));
        }

        [Fact]
        public void NoClockQuantum_JudgesEveryDifference()
        {
            Assert.Equal(0, PerfCompare.TimeFloorMicros(Results(1, 0, 1, 1), Results(1, 0, 1, 1)));
            List<MetricVerdict> v = PerfCompare.Compare(Defs, Results(1, 0, 1000, 0.3), Results(1, 0, 1000, 0.25));
            Assert.False(Verdict(v, NoLimit.Id).Passed);
        }

        [Fact]
        public void MissingMeasurement_Fails()
        {
            PerfResults m = Results(1, 0, 1000, 1);
            m.Metrics.Remove(BytesLow.Id);
            List<MetricVerdict> v = PerfCompare.Compare(Defs, m, null);
            Assert.StartsWith("missing:", Verdict(v, BytesLow.Id).Failures[0]);
        }

        [Fact]
        public void BaselineWithoutTheMetric_Fails()
        {
            PerfResults b = Results(1, 0, 1000, 1);
            b.Metrics.Remove(SpeedHigh.Id);
            List<MetricVerdict> v = PerfCompare.Compare(Defs, Results(1, 0, 1000, 1), b);
            Assert.StartsWith("no-baseline:", Verdict(v, SpeedHigh.Id).Failures[0]);
        }

        [Fact]
        public void UnknownMetric_InMeasuredOrBaseline_Fails()
        {
            PerfResults m = Results(1, 0, 1000, 1);
            m.Metrics["renamed.metric"] = 1;
            Assert.False(PerfCompare.AllPassed(PerfCompare.Compare(Defs, m, null)));

            PerfResults b = Results(1, 0, 1000, 1);
            b.Metrics["old.metric"] = 1;
            List<MetricVerdict> v = PerfCompare.Compare(Defs, Results(1, 0, 1000, 1), b);
            Assert.StartsWith("unknown:", Verdict(v, "old.metric").Failures[0]);
        }

        [Fact]
        public void Worse_FlagsAnySlowerValue_ButNotWithinTheClockFloor()
        {
            Assert.True(PerfCompare.Worse(NoLimit, 1.01, 1, 0));
            Assert.False(PerfCompare.Worse(NoLimit, 1.01, 1, 0.02));
            Assert.True(PerfCompare.Worse(SpeedHigh, 99, 100, 1000));
            Assert.False(PerfCompare.Worse(SpeedHigh, 101, 100, 0));
        }

        [Fact]
        public void Table_ShowsMeasuredLimitBaselineAndSource()
        {
            List<MetricVerdict> v = PerfCompare.Compare(Defs, Results(11, 0, 1000, 1), Results(1, 0, 1000, 1));
            string t = PerfCompare.Table(v);
            Assert.Contains("t.mean_us [us]", t);
            Assert.Contains("<= 10", t);
            Assert.Contains("limit source: test", t);
            Assert.Contains("FAIL", t);
        }
    }
}

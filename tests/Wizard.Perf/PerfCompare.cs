using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Wizard.Perf
{
    // 측정값을 PIPELINE 7.5 의 한도와 기준선에 대 본다. 실패 사유(하나라도 있으면 perf.sh 가 실패한다):
    //   limit      — 한도를 넘었다(Lower: 측정 > 한도, Higher: 측정 < 한도)
    //   regression — 기준선보다 15% 넘게 나빠졌다(Lower: 측정 > 기준 × 1.15, Higher: 측정 < 기준 × 0.85)
    //   missing    — 정의된 항목인데 측정값이 없다
    //   no-baseline— 기준선 파일이 주어졌는데 그 항목이 없다(새 항목은 기준선을 같이 재서 넣는다)
    //   unknown    — 측정값이나 기준선에 정의에 없는 항목이 있다(이름이 바뀐 항목이 조용히 빠지지 않게)
    // 허용 오차는 15% 하나뿐이다(PIPELINE 7.5). 기준선을 주지 않으면(baseline == null) 한도만 본다.
    //
    // 시계 눈금 [임시]: 시간 항목(us)의 회귀는 차이가 시계 눈금 2개(clock.quantum_us, 두 파일 중 큰 값)보다 클 때만 판정한다.
    // 이 맥의 Stopwatch 는 약 41.7ns 단위로만 움직인다. 표본 하나의 양자화 오차는 눈금 1개 미만이고 평균·백분위도 그렇다.
    // 측정값과 기준선이 각각 눈금 1개씩 어긋날 수 있으므로 눈금 2개 이하의 차이는 시계가 구별하지 못하는 값이다
    // (M1 의 빈 세계는 틱이 0.1~0.3us = 눈금 3~7개라 눈금 하나가 15% 를 넘는다). 이 규칙은 결과 보고에 올려 tech-lead 가 정한다.
    internal static class PerfCompare
    {
        public const string ClockQuantumKey = "clock.quantum_us";
        private const int QuantizationTicks = 2;
        private const string Us = "us";

        public static List<MetricVerdict> Compare(IReadOnlyList<MetricDef> defs, PerfResults measured, PerfResults? baseline)
        {
            double floor = TimeFloorMicros(measured, baseline);
            var verdicts = new List<MetricVerdict>();
            var known = new HashSet<string>(StringComparer.Ordinal);
            foreach (MetricDef def in defs)
            {
                known.Add(def.Id);
                var failures = new List<string>();
                double? m = measured.Metrics.TryGetValue(def.Id, out double mv) ? mv : (double?)null;
                double? b = null;
                if (baseline != null && baseline.Metrics.TryGetValue(def.Id, out double bv))
                {
                    b = bv;
                }
                if (m == null)
                {
                    failures.Add("missing: no measured value");
                }
                else
                {
                    if (def.Limit.HasValue && !WithinLimit(def, m.Value))
                    {
                        failures.Add("limit: " + Fmt(m.Value) + " " + def.Unit + (def.Better == Better.Lower ? " > " : " < ")
                                     + Fmt(def.Limit.Value) + " " + def.Unit + " (" + def.Source + ")");
                    }
                    if (baseline != null)
                    {
                        if (b == null)
                        {
                            failures.Add("no-baseline: the baseline has no value for this metric");
                        }
                        else if (Regressed(def, m.Value, b.Value, floor))
                        {
                            failures.Add("regression: " + Fmt(m.Value) + " vs baseline " + Fmt(b.Value) + " " + def.Unit
                                         + " (" + ChangeText(def, m.Value, b.Value) + ", allowed 15%)");
                        }
                    }
                }
                verdicts.Add(new MetricVerdict(def.Id, def, m, b, failures.ToArray()));
            }
            AddUnknown(verdicts, known, measured, "measured results");
            if (baseline != null)
            {
                AddUnknown(verdicts, known, baseline, "baseline");
            }
            return verdicts;
        }

        public static bool AllPassed(List<MetricVerdict> verdicts)
        {
            foreach (MetricVerdict v in verdicts)
            {
                if (!v.Passed)
                {
                    return false;
                }
            }
            return true;
        }

        public static bool WithinLimit(MetricDef def, double value)
        {
            if (!def.Limit.HasValue)
            {
                return true;
            }
            return def.Better == Better.Lower ? value <= def.Limit.Value : value >= def.Limit.Value;
        }

        // 15% 넘게 나빠졌는가. 경계(정확히 15%)는 통과다("15% 넘게").
        // 양변에 천분율의 눈금(1000)을 곱해 견준다: baseline × (1 + 0.15) 는 이진 실수에서 114.99999… 가 되어 정확히 15% 를 회귀로 본다.
        // timeFloorMicros: 시간 항목에서 이 값 이하의 차이는 회귀로 보지 않는다(머리말의 시계 눈금). 다른 단위에는 쓰지 않는다
        public static bool Regressed(MetricDef def, double measured, double baseline, double timeFloorMicros)
        {
            if (!BeyondFloor(def, measured, baseline, timeFloorMicros))
            {
                return false;
            }
            double m = measured * PerfLimits.PermilleScale;
            return def.Better == Better.Lower
                ? m > baseline * (PerfLimits.PermilleScale + PerfLimits.RegressionPermille)
                : m < baseline * (PerfLimits.PermilleScale - PerfLimits.RegressionPermille);
        }

        // 기준선보다 나쁜가(조금이라도. 시간 항목은 시계 눈금 2개를 넘게). 기준선 갱신이 더 느린 값을 쓰려는지 본다
        // (PIPELINE 7.5: 더 느린 값으로 갱신은 tech-lead 승인)
        public static bool Worse(MetricDef def, double measured, double baseline, double timeFloorMicros) =>
            BeyondFloor(def, measured, baseline, timeFloorMicros)
            && (def.Better == Better.Lower ? measured > baseline : measured < baseline);

        // 두 결과 파일의 시계 눈금 가운데 큰 값 × 2. 칸이 없으면 0(눈금을 모르면 모든 차이를 판정한다)
        public static double TimeFloorMicros(PerfResults measured, PerfResults? baseline)
        {
            double q = 0;
            if (measured.Context.TryGetValue(ClockQuantumKey, out double mq))
            {
                q = mq;
            }
            if (baseline != null && baseline.Context.TryGetValue(ClockQuantumKey, out double bq))
            {
                q = Math.Max(q, bq);
            }
            return q * QuantizationTicks;
        }

        private static bool BeyondFloor(MetricDef def, double measured, double baseline, double timeFloorMicros) =>
            !string.Equals(def.Unit, Us, StringComparison.Ordinal) || Math.Abs(measured - baseline) > timeFloorMicros;

        // 사람이 읽는 표(측정값, 한도, 기준선, 판정을 나란히)
        public static string Table(List<MetricVerdict> verdicts)
        {
            var sb = new StringBuilder();
            sb.Append(string.Format(CultureInfo.InvariantCulture, "{0,-44} {1,14} {2,14} {3,14}  {4}\n", "metric", "measured", "limit", "baseline", "verdict"));
            foreach (MetricVerdict v in verdicts)
            {
                string unit = v.Def?.Unit ?? string.Empty;
                string limit = v.Def?.Limit.HasValue == true ? (v.Def.Better == Better.Lower ? "<= " : ">= ") + Fmt(v.Def.Limit.Value) : "-";
                sb.Append(string.Format(CultureInfo.InvariantCulture, "{0,-44} {1,14} {2,14} {3,14}  {4}\n",
                                        v.Id + (unit.Length > 0 ? " [" + unit + "]" : string.Empty),
                                        v.Measured.HasValue ? Fmt(v.Measured.Value) : "-",
                                        limit,
                                        v.Baseline.HasValue ? Fmt(v.Baseline.Value) : "-",
                                        v.Passed ? "ok" : "FAIL"));
                foreach (string f in v.Failures)
                {
                    sb.Append("    ").Append(f).Append('\n');
                }
                if (v.Def != null && v.Def.Limit.HasValue)
                {
                    sb.Append("    limit source: ").Append(v.Def.Source).Append('\n');
                }
            }
            return sb.ToString();
        }

        private static void AddUnknown(List<MetricVerdict> verdicts, HashSet<string> known, PerfResults r, string where)
        {
            foreach (KeyValuePair<string, double> kv in r.Metrics)
            {
                if (!known.Contains(kv.Key))
                {
                    verdicts.Add(new MetricVerdict(kv.Key, null, kv.Value, null,
                                                   new[] { "unknown: " + where + " has a metric that is not defined (PerfLimits.All)" }));
                }
            }
        }

        private static string ChangeText(MetricDef def, double measured, double baseline)
        {
            if (baseline == 0)
            {
                return "baseline is 0";
            }
            double pct = (measured - baseline) / baseline * 100;
            return (pct >= 0 ? "+" : string.Empty) + pct.ToString("0.0", CultureInfo.InvariantCulture) + "%";
        }

        private static string Fmt(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
    }
}

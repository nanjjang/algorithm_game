using System;
using System.Collections.Generic;
using System.IO;

namespace Wizard.Perf
{
    // 성능 하네스(M1-PERF-01, PIPELINE 7.5). tools/ci/perf.sh 가 check 를 부른다.
    //
    //   run      [--quick] [--out <파일>]                       재고 한도만 본다
    //   check    --baseline <파일> [--quick] [--out <파일>]     재고 한도와 기준선(15%)을 본다
    //   compare  --baseline <파일> --results <파일>             재지 않고 결과 파일을 기준선에 대 본다(게이트 자체 확인용)
    //   baseline --out <파일> [--allow-slower --reason <글>]   재서 기준선을 쓴다. 한도를 넘으면 쓰지 않는다.
    //                                                         있던 기준선보다 나쁜 항목이 있으면 --allow-slower 와 이유가 있어야 쓴다
    //                                                         (PIPELINE 7.5: 더 느린 값으로 갱신은 tech-lead 의 승인과 이유)
    // 끝 코드: 0 통과, 1 한도·회귀 실패, 2 사용법·환경 오류(Debug 빌드, 파일 꼴, 벤치가 믿을 수 있는 값을 내지 못함).
    public static class Program
    {
        private const int Pass = 0;
        private const int Fail = 1;
        private const int Usage = 2;

        public static int Main(string[] args)
        {
            try
            {
                return Run(args);
            }
            catch (UsageException e)
            {
                Console.Error.WriteLine("perf: " + e.Message);
                Console.Error.WriteLine("usage: Wizard.Perf run|check|compare|baseline … (Program.cs 머리말)");
                return Usage;
            }
            catch (FormatException e)
            {
                Console.Error.WriteLine("perf: " + e.Message);
                return Usage;
            }
            catch (IOException e)
            {
                // 기준선·결과 파일이 없거나 읽고 쓸 수 없다(기준선이 없으면 baseline 명령으로 먼저 잰다)
                Console.Error.WriteLine("perf: " + e.Message);
                return Usage;
            }
            catch (UnauthorizedAccessException e)
            {
                Console.Error.WriteLine("perf: " + e.Message);
                return Usage;
            }
            catch (InvalidOperationException e)
            {
                // 벤치가 믿을 수 있는 값을 내지 못했다(복원 불일치, 재실행이 기록을 끝까지 돌지 못함, GC 없는 회차 없음)
                Console.Error.WriteLine(e.Message);
                return Usage;
            }
        }

        private static int Run(string[] args)
        {
            if (args.Length == 0)
            {
                throw new UsageException("no command");
            }
            string command = args[0];
            Dictionary<string, string?> o = Options(args);
            switch (command)
            {
                case "run":
                    Only(o, "--quick", "--out");
                    return Measure(o, null);
                case "check":
                    Only(o, "--quick", "--out", "--baseline");
                    return Measure(o, PerfResults.Load(Require(o, "--baseline")));
                case "compare":
                    Only(o, "--baseline", "--results");
                    return Report(PerfResults.Load(Require(o, "--results")), PerfResults.Load(Require(o, "--baseline")));
                case "baseline":
                    Only(o, "--out", "--allow-slower", "--reason");
                    return WriteBaseline(o);
                default:
                    throw new UsageException("unknown command " + command);
            }
        }

        private static int Measure(Dictionary<string, string?> o, PerfResults? baseline)
        {
            if (!Optimized())
            {
                return Usage;
            }
            bool quick = o.ContainsKey("--quick");
            PerfResults r = PerfSuite.Run(quick ? BenchSettings.Quick() : BenchSettings.Full(), quick ? "quick" : "full",
                                          s => Console.WriteLine("measuring " + s));
            if (o.TryGetValue("--out", out string? outPath) && outPath != null)
            {
                r.Save(outPath);
                Console.WriteLine("results: " + Path.GetFullPath(outPath));
            }
            return Report(r, baseline);
        }

        private static int Report(PerfResults measured, PerfResults? baseline)
        {
            if (baseline != null)
            {
                if (!string.Equals(baseline.Settings, measured.Settings, StringComparison.Ordinal))
                {
                    Console.WriteLine("FAIL  settings differ: measured \"" + measured.Settings + "\", baseline \"" + baseline.Settings + "\"");
                    return Fail;
                }
                foreach (KeyValuePair<string, string> kv in baseline.Machine)
                {
                    if (!measured.Machine.TryGetValue(kv.Key, out string? m) || m != kv.Value)
                    {
                        // 재는 기기는 이 맥이다(PIPELINE 7.5). 다른 기기면 기준선과의 비교가 뜻이 없으므로 적어 둔다(판정은 그대로 한다)
                        Console.WriteLine("note: machine." + kv.Key + " differs from the baseline (" + (m ?? "-") + " vs " + kv.Value + ")");
                    }
                }
            }
            List<MetricVerdict> verdicts = PerfCompare.Compare(PerfLimits.All, measured, baseline);
            Console.Write(PerfCompare.Table(verdicts));
            bool ok = PerfCompare.AllPassed(verdicts);
            Console.WriteLine(ok ? "perf: PASS" : "perf: FAIL");
            return ok ? Pass : Fail;
        }

        private static int WriteBaseline(Dictionary<string, string?> o)
        {
            if (!Optimized())
            {
                return Usage;
            }
            string outPath = Require(o, "--out");
            bool allowSlower = o.ContainsKey("--allow-slower");
            o.TryGetValue("--reason", out string? reason);
            if (allowSlower && string.IsNullOrWhiteSpace(reason))
            {
                throw new UsageException("--allow-slower needs --reason");
            }
            PerfResults r = PerfSuite.Run(BenchSettings.Full(), "full", s => Console.WriteLine("measuring " + s));
            List<MetricVerdict> limits = PerfCompare.Compare(PerfLimits.All, r, null);
            Console.Write(PerfCompare.Table(limits));
            if (!PerfCompare.AllPassed(limits))
            {
                Console.WriteLine("perf: the measurement breaks a limit. The baseline is not written");
                return Fail;
            }
            if (File.Exists(outPath))
            {
                PerfResults old = PerfResults.Load(outPath);
                double floor = PerfCompare.TimeFloorMicros(r, old);
                var worse = new List<string>();
                foreach (MetricDef d in PerfLimits.All)
                {
                    if (old.Metrics.TryGetValue(d.Id, out double b) && PerfCompare.Worse(d, r.Metrics[d.Id], b, floor))
                    {
                        worse.Add(d.Id);
                    }
                }
                if (worse.Count > 0 && !allowSlower)
                {
                    Console.WriteLine("perf: worse than the current baseline: " + string.Join(", ", worse));
                    Console.WriteLine("perf: a slower baseline needs tech-lead approval (PIPELINE 7.5): --allow-slower --reason <why>");
                    return Fail;
                }
            }
            r.Note = string.IsNullOrWhiteSpace(reason) ? string.Empty : reason!;
            r.Save(outPath);
            Console.WriteLine("baseline: " + Path.GetFullPath(outPath));
            return Pass;
        }

        private static bool Optimized()
        {
            string? debug = PerfSuite.NotOptimized();
            if (debug != null)
            {
                Console.Error.WriteLine("perf: " + debug + " is built without optimization. Run the Release build (dotnet run -c Release)");
                return false;
            }
            return true;
        }

        private static Dictionary<string, string?> Options(string[] args)
        {
            var o = new Dictionary<string, string?>(StringComparer.Ordinal);
            for (int i = 1; i < args.Length; i++)
            {
                string a = args[i];
                if (!a.StartsWith("--", StringComparison.Ordinal))
                {
                    throw new UsageException("unexpected argument " + a);
                }
                bool flag = a == "--quick" || a == "--allow-slower";
                string? value = null;
                if (!flag)
                {
                    if (i + 1 >= args.Length)
                    {
                        throw new UsageException(a + " needs a value");
                    }
                    value = args[++i];
                }
                if (o.ContainsKey(a))
                {
                    throw new UsageException(a + " given twice");
                }
                o[a] = value;
            }
            return o;
        }

        private static void Only(Dictionary<string, string?> o, params string[] allowed)
        {
            foreach (string k in o.Keys)
            {
                if (Array.IndexOf(allowed, k) < 0)
                {
                    throw new UsageException("option " + k + " is not for this command");
                }
            }
        }

        private static string Require(Dictionary<string, string?> o, string name)
        {
            if (!o.TryGetValue(name, out string? v) || string.IsNullOrEmpty(v))
            {
                throw new UsageException(name + " is required");
            }
            return v;
        }
    }
}

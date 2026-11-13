using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Wizard.Content;
using Wizard.ReplayCli;
using Wizard.TestKit.Shared.Arith;
using Wizard.TestKit.Shared.Golden;

namespace Wizard.Unity.App.Headless.DotnetHost
{
    // unity-golden.sh 의 dotnet 열과 표 비교(T-30, PIPELINE 7.2 2번). 이 폴더는 이름이 '~' 로 끝나 Unity 와 metagen 이 보지 않는다.
    // 프로젝트 파일은 저장소에 두지 않는다(unity/ 아래 csproj 는 추적하지 않는다. layout-check). unity-golden.sh 가
    // artifacts/golden/dotnet-host/ 에 만들어 이 파일을 dotnet 의 원본과 같이 컴파일한다:
    //   - 재실행: tools/Wizard.ReplayCli 의 GoldenRunner(dotnet 골든 시험 GoldenTests 와 같은 길), 팩은 RepoPack(저장소 data/ 를 메모리에서)
    //   - 산술: Wizard.TestKit.Shared.Arith.CoreArithOps(dotnet 시험 ArithGoldenTests 와 Unity 열이 쓰는 같은 표)
    // 그래서 dotnet 열은 dotnet 시험과 같은 코드가 CoreCLR(arm64, 또는 x64 dotnet 으로 띄우면 Rosetta 2 의 x64)에서 낸 값이다.
    //
    //   run --root <저장소> [--out <폴더>]                       이 프로세스의 조합(macos-dotnet-test-<arch>)으로 골든 전부와 산술 벡터를 돌려
    //                                                            <폴더>/golden-result.<조합>.json 을 쓴다(기본 <저장소>/artifacts/golden)
    //   compare --root <저장소> [--require <조합>]… <결과 파일>…   결과 파일들을 골든과 대조해 표를 낸다(GoldenComparison)
    // 종료 코드: 0 통과, 1 불일치·빠짐, 2 사용법·형식 오류.
    public static class GoldenDotnetHost
    {
        private const int ExitOk = 0;
        private const int ExitMismatch = 1;
        private const int ExitUsage = 2;

        public static int Main(string[] args)
        {
            try
            {
                return Run(args);
            }
            catch (GoldenFormatException ex)
            {
                Console.Error.WriteLine("golden-host: " + string.Join("\n  ", ex.Problems));
                return ExitUsage;
            }
        }

        private static int Run(string[] args)
        {
            if (args.Length == 0)
            {
                return Usage();
            }
            string? root = null;
            string? outDir = null;
            var required = new List<GoldenCombo>();
            var files = new List<string>();
            for (int i = 1; i < args.Length; i++)
            {
                string a = args[i];
                if (a == "--root" || a == "--out" || a == "--require")
                {
                    if (i + 1 >= args.Length)
                    {
                        return Usage();
                    }
                    string v = args[++i];
                    if (a == "--root")
                    {
                        root = Path.GetFullPath(v);
                    }
                    else if (a == "--out")
                    {
                        outDir = Path.GetFullPath(v);
                    }
                    else if (GoldenCombo.TryParse(v, out GoldenCombo c))
                    {
                        required.Add(c);
                    }
                    else
                    {
                        Console.Error.WriteLine("golden-host: --require " + v + " is not <os>-<runtime>-<host>-<arch>");
                        return ExitUsage;
                    }
                }
                else if (a.StartsWith("--", StringComparison.Ordinal))
                {
                    return Usage();
                }
                else
                {
                    files.Add(Path.GetFullPath(a));
                }
            }
            if (root == null)
            {
                return Usage();
            }
            GoldenCatalog catalog = GoldenCatalog.Load(Path.Combine(root, "tests", "golden"));
            switch (args[0])
            {
                case "run":
                    return files.Count == 0 && required.Count == 0 ? RunCombo(root, catalog, outDir ?? Path.Combine(root, "artifacts", "golden")) : Usage();
                case "compare":
                    return outDir == null ? Compare(catalog, required, files) : Usage();
                default:
                    return Usage();
            }
        }

        private static int RunCombo(string root, GoldenCatalog catalog, string outDir)
        {
            GoldenCombo combo = ThisCombo();
            ContentDb db = RepoPack.Build(root);
            var entries = new List<GoldenEntryResult>();
            foreach (GoldenEntry e in catalog.Entries)
            {
                entries.Add(RunEntry(db, e));
            }
            var arith = new List<ArithCaseResult>();
            foreach (ArithBundle b in catalog.ArithBundles)
            {
                arith.AddRange(ArithRunner.Run(b, new CoreArithOps()));
            }
            var result = new GoldenRunResult(combo, entries, arith);
            string path = Path.Combine(outDir, combo.ResultFileName);
            result.Save(path);
            Console.WriteLine("golden-host: " + combo.Name + ": " + entries.Count + " golden(s), " + arith.Count + " arith case(s); result " + path);
            return Report(GoldenComparison.Compare(catalog, new[] { result }));
        }

        // 두 번 돌려 같아야 한다(같은 기록 두 번. T-30). 다르면 그 항목은 error
        private static GoldenEntryResult RunEntry(ContentDb db, GoldenEntry e)
        {
            try
            {
                IReadOnlyList<GoldenHash> first = GoldenRunner.Run(db, e);
                IReadOnlyList<GoldenHash> second = GoldenRunner.Run(db, e);
                GoldenDiff? d = GoldenDiff.Compare(first, second);
                return d == null
                    ? new GoldenEntryResult(e.Id, first, null)
                    : new GoldenEntryResult(e.Id, Array.Empty<GoldenHash>(), "two runs of the same record differ: " + d);
            }
            catch (Exception ex)
            {
                string message = ex.Message;
                int nl = message.IndexOf('\n');
                return new GoldenEntryResult(e.Id, Array.Empty<GoldenHash>(), ex.GetType().Name + ": " + (nl >= 0 ? message.Substring(0, nl) : message));
            }
        }

        private static int Compare(GoldenCatalog catalog, List<GoldenCombo> required, List<string> files)
        {
            var results = new List<GoldenRunResult>();
            foreach (string f in files)
            {
                results.Add(GoldenRunResult.Load(f));
                Console.WriteLine("golden-host: " + f);
            }
            return Report(GoldenComparison.Compare(catalog, results, required));
        }

        private static int Report(GoldenComparison cmp)
        {
            Console.Write(cmp.Table);
            foreach (string f in cmp.Failures)
            {
                Console.WriteLine("FAIL  " + f);
            }
            Console.WriteLine(cmp.Passed ? "golden-host: ok" : "golden-host: FAILED (" + cmp.Failures.Count + ")");
            return cmp.Passed ? ExitOk : ExitMismatch;
        }

        private static GoldenCombo ThisCombo()
        {
            string os = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "macos"
                : RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows"
                : "linux";
            string arch = RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.Arm64 => "arm64",
                Architecture.X64 => "x64",
                Architecture other => throw new PlatformNotSupportedException("golden combos cover arm64 and x64, not " + other),
            };
            return new GoldenCombo(os, "dotnet", "test", arch);
        }

        private static int Usage()
        {
            Console.Error.WriteLine("usage: golden-host run --root <repo> [--out <dir>]\n" +
                                    "       golden-host compare --root <repo> [--require <combo>]... <result.json>...");
            return ExitUsage;
        }
    }
}

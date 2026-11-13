#nullable enable
using System;
using System.IO;
using Wizard.Content;
using Wizard.TestKit.Shared.Golden;

namespace Wizard.Unity.App.Headless
{
    // 머리 없는 골든 실행 한 번: 골든을 읽고, 팩을 읽고, 전부 돌려 결과 파일을 쓰고, 기대 해시와 대조한다.
    // 엔진에 기대지 않는다(플레이어의 입구 HeadlessGolden 과 EditMode 시험이 같이 부른다).
    // 종료 코드: 0 모든 항목·산술 벡터가 기대와 같다, 1 다르거나 돌지 못한 것이 있다(결과 파일은 쓴다), 2 사용법·환경 오류(결과 파일 없음).
    public static class HeadlessGoldenRun
    {
        public const int ExitOk = 0;
        public const int ExitMismatch = 1;
        public const int ExitUsage = 2;

        public static int Execute(HeadlessArgs args, GoldenCombo actual, TextWriter log)
        {
            if (args == null)
            {
                throw new ArgumentNullException(nameof(args));
            }
            if (log == null)
            {
                throw new ArgumentNullException(nameof(log));
            }
            if (args.ExpectedCombo != null && !string.Equals(args.ExpectedCombo, actual.Name, StringComparison.Ordinal))
            {
                log.WriteLine("golden: this process is " + actual.Name + " but " + HeadlessArgs.ComboFlag + " asked for " + args.ExpectedCombo);
                return ExitUsage;
            }

            GoldenCatalog catalog;
            try
            {
                catalog = GoldenCatalog.Load(args.GoldenRoot);
            }
            catch (GoldenFormatException ex)
            {
                log.WriteLine("golden: " + args.GoldenRoot + " is not a valid golden folder:");
                foreach (string p in ex.Problems)
                {
                    log.WriteLine("  " + p);
                }
                return ExitUsage;
            }

            ContentDb db;
            try
            {
                db = ContentDb.Build(ContentPack.Load(File.ReadAllBytes(args.PackPath)));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ContentException)
            {
                log.WriteLine("golden: cannot load the content pack " + args.PackPath + ": " + ex.Message +
                              " (build it with: dotnet run --project tools/Wizard.DataBuild -- build --out <folder>)");
                return ExitUsage;
            }

            GoldenRunResult result = GoldenSuite.Run(catalog, db, actual);
            string path = Path.Combine(args.OutDirectory, actual.ResultFileName);
            result.Save(path);
            log.WriteLine("golden: " + actual.Name + ": " + catalog.Entries.Count + " golden(s), " + result.Arith.Count +
                          " arith case(s); result " + path);

            GoldenComparison cmp = GoldenComparison.Compare(catalog, new[] { result });
            log.Write(cmp.Table);
            foreach (string f in cmp.Failures)
            {
                log.WriteLine("FAIL  " + f);
            }
            log.WriteLine(cmp.Passed ? "golden: ok" : "golden: FAILED (" + cmp.Failures.Count + ")");
            return cmp.Passed ? ExitOk : ExitMismatch;
        }
    }
}

#nullable enable
using System;
using System.Collections.Generic;
using Wizard.Content;
using Wizard.TestKit.Shared.Arith;
using Wizard.TestKit.Shared.Golden;

namespace Wizard.Unity.App.Headless
{
    // tests/golden/ 전체(모든 마일스톤의 항목과 산술 경계 벡터)를 이 런타임에서 돌려 조합 하나의 결과(GoldenRunResult)를 만든다(T-30, T-39).
    // 항목마다 두 번 돈다: 끊지 않은 실행, 그리고 되감기·상태 옮기기를 끼운 실행(GoldenReplay). 두 목록이 다르면 그 항목은 error 다.
    // 결과 파일에 싣는 해시는 끊지 않은 실행의 것이다. 기대 해시와의 대조는 하지 않는다(GoldenComparison 이 한다. 결과 파일은 기대를 모른다).
    public static class GoldenSuite
    {
        public static GoldenRunResult Run(GoldenCatalog catalog, ContentDb db, GoldenCombo combo)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }
            if (db == null)
            {
                throw new ArgumentNullException(nameof(db));
            }
            var entries = new List<GoldenEntryResult>(catalog.Entries.Count);
            foreach (GoldenEntry e in catalog.Entries)
            {
                entries.Add(RunEntry(db, e));
            }
            var arith = new List<ArithCaseResult>();
            var ops = new CoreArithOps();             // dotnet 열과 같은 표(TestKit.Shared)
            foreach (ArithBundle b in catalog.ArithBundles)
            {
                arith.AddRange(ArithRunner.Run(b, ops));
            }
            return new GoldenRunResult(combo, entries, arith);
        }

        public static GoldenEntryResult RunEntry(ContentDb db, GoldenEntry entry)
        {
            List<GoldenHash> plain;
            try
            {
                GoldenPlan plan = GoldenReplay.Prepare(db, entry);
                plain = GoldenReplay.Run(db, plan);
                uint end = entry.Expected[entry.Expected.Count - 1].Tick;
                List<GoldenHash> rewound = GoldenReplay.RunWithRewindAndTransfer(db, plan, end);
                GoldenDiff? d = GoldenDiff.Compare(plain, rewound);
                if (d != null)
                {
                    return new GoldenEntryResult(entry.Id, Array.Empty<GoldenHash>(),
                        "the run with snapshot rewind and state transfer differs from the plain run: " + d);
                }
            }
            catch (Exception ex)
            {
                return new GoldenEntryResult(entry.Id, Array.Empty<GoldenHash>(), Describe(ex));
            }
            // 결과 파일에는 label 을 싣는다(비교하지 않는다. 사람이 읽는다)
            return new GoldenEntryResult(entry.Id, plain, null);
        }

        // 결과 파일의 error 칸: 예외의 형식과 첫 줄. 실행마다 바뀌는 값(스택, 시각)은 넣지 않는다
        public static string Describe(Exception ex)
        {
            string message = ex.Message ?? string.Empty;
            int nl = message.IndexOf('\n');
            if (nl >= 0)
            {
                message = message.Substring(0, nl);
            }
            return ex.GetType().Name + ": " + message;
        }
    }
}

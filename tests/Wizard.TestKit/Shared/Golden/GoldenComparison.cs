using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Wizard.TestKit.Shared.Arith;

namespace Wizard.TestKit.Shared.Golden
{
    // 여러 조합의 결과 파일을 골든과 대조해 한 표로 낸다(T-30: 하나라도 다르면 실패).
    // 각 조합이 기대 해시와 같으면 조합끼리도 같다. 산술 벡터는 기대(값 또는 예외 분류)를 만족해야 한다.
    public sealed class GoldenComparison
    {
        private GoldenComparison(bool passed, IReadOnlyList<string> failures, string table)
        {
            Passed = passed;
            Failures = failures;
            Table = table;
        }

        public bool Passed { get; }

        public IReadOnlyList<string> Failures { get; }

        // 행 = 골든 항목과 산술 벡터, 열 = 조합. 칸: ok | diff@<틱> | missing | error
        public string Table { get; }

        public static GoldenComparison Compare(GoldenCatalog catalog, IReadOnlyList<GoldenRunResult> results,
                                               IReadOnlyList<GoldenCombo>? requiredCombos = null)
        {
            var failures = new List<string>();
            var combos = new List<string>();
            foreach (GoldenRunResult r in results)
            {
                if (combos.Contains(r.Combo.Name))
                {
                    failures.Add("combo " + r.Combo.Name + " appears twice");
                }
                combos.Add(r.Combo.Name);
            }
            if (results.Count == 0)
            {
                failures.Add("no result files");
            }
            if (requiredCombos != null)
            {
                foreach (GoldenCombo c in requiredCombos)
                {
                    if (!combos.Contains(c.Name))
                    {
                        failures.Add("combo " + c.Name + " has no result file");
                    }
                }
            }

            var rows = new List<string[]>();
            var header = new List<string> { "golden" };
            header.AddRange(combos);
            rows.Add(header.ToArray());

            foreach (GoldenEntry e in catalog.Entries)
            {
                var row = new List<string> { e.Id };
                foreach (GoldenRunResult r in results)
                {
                    GoldenEntryResult? got = FindEntry(r, e.Id);
                    if (got == null)
                    {
                        row.Add("missing");
                        failures.Add(r.Combo.Name + ": " + e.Id + " was not run");
                    }
                    else if (got.Error != null)
                    {
                        row.Add("error");
                        failures.Add(r.Combo.Name + ": " + e.Id + " failed: " + got.Error);
                    }
                    else
                    {
                        GoldenDiff? d = e.Compare(got.Hashes);
                        if (d == null)
                        {
                            row.Add("ok");
                        }
                        else
                        {
                            uint tick = d.Expected.HasValue ? d.Expected.Value.Tick : d.Actual!.Value.Tick;
                            row.Add("diff@" + tick.ToString(CultureInfo.InvariantCulture));
                            failures.Add(r.Combo.Name + ": " + e.Id + ": " + d);
                        }
                    }
                }
                rows.Add(row.ToArray());
            }

            foreach (ArithBundle b in catalog.ArithBundles)
            {
                foreach (ArithCase c in b.Cases)
                {
                    var row = new List<string> { "arith/" + b.Name + "/" + c.Id };
                    foreach (GoldenRunResult r in results)
                    {
                        ArithCaseResult? got = FindArith(r, b.Name, c.Id);
                        if (got == null)
                        {
                            row.Add("missing");
                            failures.Add(r.Combo.Name + ": arith " + b.Name + "/" + c.Id + " was not run");
                        }
                        else if (got.Satisfies(c))
                        {
                            row.Add("ok");
                        }
                        else
                        {
                            row.Add("diff");
                            string want = c.Fault != null ? "fault " + c.Fault : new ArithCaseResult(b.Name, c.Id, c.Expect, null).Describe();
                            failures.Add(r.Combo.Name + ": arith " + b.Name + "/" + c.Id + ": expected " + want + ", got " + got.Describe());
                        }
                    }
                    rows.Add(row.ToArray());
                }
            }

            foreach (GoldenRunResult r in results)
            {
                foreach (GoldenEntryResult got in r.Entries)
                {
                    if (!HasEntry(catalog, got.Id))
                    {
                        failures.Add(r.Combo.Name + ": result for unknown golden '" + got.Id + "'");
                    }
                }
                foreach (ArithCaseResult got in r.Arith)
                {
                    if (!HasArith(catalog, got.Bundle, got.CaseId))
                    {
                        failures.Add(r.Combo.Name + ": result for unknown arith case '" + got.Key + "'");
                    }
                }
            }

            return new GoldenComparison(failures.Count == 0, failures, Render(rows));
        }

        private static GoldenEntryResult? FindEntry(GoldenRunResult r, string id)
        {
            foreach (GoldenEntryResult e in r.Entries)
            {
                if (string.Equals(e.Id, id, StringComparison.Ordinal))
                {
                    return e;
                }
            }
            return null;
        }

        private static ArithCaseResult? FindArith(GoldenRunResult r, string bundle, string caseId)
        {
            foreach (ArithCaseResult a in r.Arith)
            {
                if (string.Equals(a.Bundle, bundle, StringComparison.Ordinal) && string.Equals(a.CaseId, caseId, StringComparison.Ordinal))
                {
                    return a;
                }
            }
            return null;
        }

        private static bool HasEntry(GoldenCatalog catalog, string id)
        {
            foreach (GoldenEntry e in catalog.Entries)
            {
                if (string.Equals(e.Id, id, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool HasArith(GoldenCatalog catalog, string bundle, string caseId)
        {
            foreach (ArithBundle b in catalog.ArithBundles)
            {
                if (!string.Equals(b.Name, bundle, StringComparison.Ordinal))
                {
                    continue;
                }
                foreach (ArithCase c in b.Cases)
                {
                    if (string.Equals(c.Id, caseId, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static string Render(List<string[]> rows)
        {
            int cols = rows[0].Length;
            var width = new int[cols];
            foreach (string[] r in rows)
            {
                for (int i = 0; i < cols && i < r.Length; i++)
                {
                    width[i] = Math.Max(width[i], r[i].Length);
                }
            }
            var sb = new StringBuilder();
            foreach (string[] r in rows)
            {
                for (int i = 0; i < cols; i++)
                {
                    string cell = i < r.Length ? r[i] : "";
                    sb.Append(cell.PadRight(width[i]));
                    sb.Append(i + 1 < cols ? "  " : "\n");
                }
            }
            return sb.ToString();
        }
    }
}

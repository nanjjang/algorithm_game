using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Wizard.TestKit.Shared.Vectors;

namespace Wizard.VectorCoverage
{
    // 검사 두 가지(PIPELINE 7.1 G1).
    //   Global: 명세 벡터 규칙에 문제가 없고, 모든 벡터가 정확히 한 작업에 배정되어 있고(미배정·이중 배정 0),
    //           끝난(done) 작업의 벡터마다 테스트가 있고, 벡터 테스트의 이름이 명세에 있는 벡터를 가리킨다.
    //           명세별로 테스트가 있는 벡터 수와 빠진 벡터 수를 같이 보고한다.
    //   Task:   작업 하나의 vectors 가 가리킨 벡터마다 테스트가 있다(--trx 를 주면 그 테스트가 통과했는지도 본다).
    // 출력은 같은 입력이면 같은 바이트다(명세 번호, 명세의 줄 순서).
    public sealed class CoverageCheck
    {
        // 보고에 벡터 id 를 늘어놓을 때 한 줄에 보이는 최대 개수(넘으면 개수만 붙인다)
        private const int ListLimit = 20;

        private readonly List<string> _lines = new List<string>();
        private readonly List<string> _failures = new List<string>();
        private readonly List<string> _warnings = new List<string>();

        private CoverageCheck()
        {
        }

        public IReadOnlyList<string> Lines => _lines;

        public IReadOnlyList<string> Failures => _failures;

        // 실패로 치지 않지만 알리는 것(벡터 장 밖의 미배정 벡터)
        public IReadOnlyList<string> Warnings => _warnings;

        public bool Passed => _failures.Count == 0;

        public int Targets { get; private set; }

        public int Covered { get; private set; }

        public int Missing { get; private set; }

        public static CoverageCheck Global(SpecVectorScan scan, IReadOnlyList<TaskEntry> tasks, ISet<string> done, TestIndex tests)
        {
            var c = new CoverageCheck();
            foreach (string p in scan.Problems)
            {
                c._failures.Add("명세 벡터 규칙: " + p);
            }
            Assignment a = Assignment.Resolve(scan, tasks);
            foreach (string p in a.Problems)
            {
                c._failures.Add(p);
            }
            foreach (string p in tests.Problems)
            {
                c._failures.Add(p);
            }

            var known = new HashSet<string>(scan.Vectors.Select(v => v.Key), StringComparer.Ordinal);
            foreach (VectorTest t in tests.Tests)
            {
                if (!known.Contains(t.Key))
                {
                    c._failures.Add(t.File + ":" + Num(t.Line) + ": test '" + t.Method + "' names vector " + t.Key + ", which is not in the spec");
                }
            }

            // 미배정: 명세·절 단위로 묶는다. G1 의 범위는 "테스트 벡터" 장이다(PIPELINE 7.1). 장 밖에서 정의된 벡터
            // (spec 11 의 TV-11-162~166 처럼 다른 장의 굵은 항목)가 배정되지 않았으면 실패로 치지 않고 경고로 알린다
            var unassigned = new List<SpecVector>();
            foreach (SpecVector v in scan.Vectors)
            {
                if (a.OwnerOf(v) == null)
                {
                    unassigned.Add(v);
                }
            }
            foreach (IGrouping<string, SpecVector> g in unassigned.Where(v => v.InVectorChapter).GroupBy(v => "S" + v.Spec + "§" + v.Section))
            {
                c._failures.Add("미배정 벡터 " + g.Key + ": " + Ids(g.ToList()));
            }
            foreach (IGrouping<string, SpecVector> g in unassigned.Where(v => !v.InVectorChapter).GroupBy(v => "S" + v.Spec + "§" + v.Section))
            {
                c._warnings.Add("테스트 벡터 장 밖에서 정의된 벡터가 어느 작업에도 배정되지 않았다 " + g.Key + ": " + Ids(g.ToList()) +
                                " (G1 의 범위 밖이라 실패로 치지 않는다. tasks.json 의 vectors 에 넣어야 테스트 대응을 볼 수 있다)");
            }

            // 끝난 작업의 벡터에 테스트가 있는가
            var taskIds = new HashSet<string>(tasks.Select(t => t.Id), StringComparer.Ordinal);
            foreach (string id in done)
            {
                if (!taskIds.Contains(id))
                {
                    continue;
                }
                var missing = a.VectorsOf(id).Where(v => tests.For(v).Count == 0).ToList();
                foreach (IGrouping<string, SpecVector> g in missing.GroupBy(v => v.Spec))
                {
                    c._failures.Add("끝난 작업 " + id + " 의 벡터에 테스트가 없다 S" + g.Key + ": " + Ids(g.ToList()));
                }
            }

            // 명세별 보고
            c._lines.Add("명세별 벡터 테스트(테스트 이름 S<명세>_<벡터 id>__…):");
            foreach (IGrouping<string, SpecVector> g in scan.Vectors.GroupBy(v => v.Spec))
            {
                List<SpecVector> list = g.ToList();
                int covered = list.Count(v => tests.For(v).Count > 0);
                c._lines.Add("  S" + g.Key + ": 벡터 " + Num(list.Count) + ", 테스트 있음 " + Num(covered) + ", 빠짐 " + Num(list.Count - covered));
                c.Targets += list.Count;
                c.Covered += covered;
            }
            c.Missing = c.Targets - c.Covered;
            int assigned = scan.Vectors.Count(v => a.OwnerOf(v) != null);
            c._lines.Add("명세 벡터 " + Num(scan.Vectors.Count) + "개, 배정 " + Num(assigned) + "개, 미배정 " + Num(unassigned.Count) +
                         "개, 벡터 테스트 " + Num(tests.Tests.Count) + "개, 끝난 작업 " + Num(done.Count(taskIds.Contains)) + "개");
            return c;
        }

        public static CoverageCheck ForTask(string taskId, SpecVectorScan scan, IReadOnlyList<TaskEntry> tasks, TestIndex tests, TrxResults? trx)
        {
            if (!tasks.Any(t => string.Equals(t.Id, taskId, StringComparison.Ordinal)))
            {
                throw new CoverageException("tasks.json 에 작업 " + taskId + " 이 없다", CoverageException.UsageExitCode);
            }
            var c = new CoverageCheck();
            foreach (string p in scan.Problems)
            {
                c._failures.Add("명세 벡터 규칙: " + p);
            }
            Assignment a = Assignment.Resolve(scan, tasks);
            foreach (string p in a.ProblemsOf(taskId))
            {
                c._failures.Add(p);
            }
            IReadOnlyList<SpecVector> mine = a.VectorsOf(taskId);
            int passed = 0;
            foreach (IGrouping<string, SpecVector> g in mine.GroupBy(v => v.Spec))
            {
                List<SpecVector> list = g.ToList();
                var missing = list.Where(v => tests.For(v).Count == 0).ToList();
                var failing = new List<string>();
                if (trx != null)
                {
                    foreach (SpecVector v in list)
                    {
                        if (tests.For(v).Count == 0)
                        {
                            continue;
                        }
                        string? why = trx.Verdict(v);
                        if (why == null)
                        {
                            passed++;
                        }
                        else
                        {
                            failing.Add(v.Id + ": " + why);
                        }
                    }
                }
                c._lines.Add("  S" + g.Key + ": 대상 " + Num(list.Count) + ", 테스트 있음 " + Num(list.Count - missing.Count) + ", 빠짐 " + Num(missing.Count));
                if (missing.Count > 0)
                {
                    c._failures.Add("작업 " + taskId + " 의 벡터에 테스트가 없다 S" + g.Key + ": " + Ids(missing));
                }
                foreach (string f in failing)
                {
                    c._failures.Add("작업 " + taskId + " 의 벡터 테스트가 통과하지 않았다 S" + g.Key + " " + f);
                }
                c.Targets += list.Count;
                c.Covered += list.Count - missing.Count;
            }
            c.Missing = c.Targets - c.Covered;
            string pass = trx == null ? "통과 여부는 보지 않음(--trx 없음)" : "통과 " + Num(passed) + "개(TRX " + Num(trx.FileCount) + "개)";
            c._lines.Add("vector-coverage --task " + taskId + ": 대상 " + Num(c.Targets) + "개, 테스트 있음 " + Num(c.Covered) + "개, " + pass +
                         ", 빠짐 " + Num(c.Missing) + "개");
            return c;
        }

        private static string Ids(List<SpecVector> list)
        {
            if (list.Count <= ListLimit)
            {
                return string.Join(", ", list.Select(v => v.Id));
            }
            return string.Join(", ", list.Take(ListLimit).Select(v => v.Id)) + " … (" + Num(list.Count) + "개)";
        }

        private static string Num(int n) => n.ToString(CultureInfo.InvariantCulture);
    }
}

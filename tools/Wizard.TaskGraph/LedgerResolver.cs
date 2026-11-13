using System;
using System.Collections.Generic;
using System.Linq;

namespace Wizard.TaskGraph
{
    // taskgraph resolve 의 판정(DECISIONS-TECH T-65 9번). 충돌한 장부의 세 판을 받아 합친 글자를 내거나 진짜 충돌을 적는다.
    // 색인·파일을 읽고 쓰는 일은 Cli 가 한다. 이 함수는 git 을 부르지 않는다(문서의 줄 병합만 git merge-file 로 파일을 합친다).
    //   - tasks.json: TasksMerge(구조 병합) → 합친 그래프가 validate(스키마, 그래프, 벡터 배정)를 통과해야 한다
    //   - DECISIONS-TECH.md, OPEN-QUESTIONS.md: TextMerge(줄 병합, 바탕이 빈 덩어리만)
    //   - 합친 결과(충돌하지 않은 장부는 작업 파일)가 ledger 검사를 통과해야 한다(번호 겹침, 충돌 표식)
    // 하나라도 풀지 못하면 Resolved 는 비고 Conflicts 에 무엇이 어디서 막혔는지 적힌다.
    public static class LedgerResolver
    {
        public sealed class Stages
        {
            public Stages(string? baseText, string? mainText, string? movingText)
            {
                Base = baseText;
                Main = mainText;
                Moving = movingText;
            }

            // 색인의 1(바탕), 2(main 쪽), 3(옮기는 커밋 쪽). 없는 판은 null
            public string? Base { get; }
            public string? Main { get; }
            public string? Moving { get; }
        }

        public sealed class Result
        {
            public Result(IReadOnlyDictionary<string, string> resolved, IReadOnlyList<string> conflicts)
            {
                Resolved = resolved;
                Conflicts = conflicts;
            }

            public IReadOnlyDictionary<string, string> Resolved { get; }
            public IReadOnlyList<string> Conflicts { get; }
            public bool Ok => Conflicts.Count == 0;
        }

        // conflicted: 충돌한 장부 경로 → 세 판(장부가 아닌 경로는 보지 않는다). readWorking: 충돌하지 않은 장부의 작업 파일(없으면 null).
        // history: 합친 그래프의 병합 기록(Cli 가 읽는다. 시험은 집합을 준다)
        public static Result Resolve(IReadOnlyDictionary<string, Stages> conflicted, Func<string, string?> readWorking,
            string? schemaPath, string? specsDir, Func<TaskDoc, MergeHistory> history, string workDir)
        {
            var conflicts = new List<string>();
            var merged = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string path in Ledger.Paths)
            {
                if (!conflicted.TryGetValue(path, out Stages? s))
                {
                    continue;
                }
                if (s.Main == null || s.Moving == null)
                {
                    conflicts.Add($"{path}: 한쪽이 파일을 지웠다({(s.Main == null ? "main 쪽" : "옮기는 쪽")})");
                    continue;
                }
                TasksMerge.Outcome o = path == Ledger.TasksPath
                    ? TasksMerge.Merge(s.Base ?? "{}", s.Main, s.Moving)
                    : TextMerge.Merge(path, s.Base ?? "", s.Main, s.Moving, workDir);
                if (!o.Ok)
                {
                    conflicts.AddRange(o.Conflicts);
                    continue;
                }
                merged[path] = o.Text!;
            }
            if (conflicts.Count > 0)
            {
                return new Result(new Dictionary<string, string>(StringComparer.Ordinal), conflicts);
            }

            if (merged.TryGetValue(Ledger.TasksPath, out string? tasksText))
            {
                try
                {
                    TaskDoc doc = TaskDoc.Parse(tasksText, Ledger.TasksPath);
                    ValidationReport rep = GraphValidator.Validate(doc, schemaPath, specsDir, history(doc));
                    foreach (string e in rep.Errors)
                    {
                        conflicts.Add($"{Ledger.TasksPath}: 합친 그래프가 validate 에 실패한다 — {e}");
                    }
                }
                catch (TaskGraphException e)
                {
                    conflicts.Add($"{Ledger.TasksPath}: 합친 결과를 읽지 못했다 — {e.Message}");
                }
            }
            string? Get(string p) => merged.TryGetValue(p, out string? t) ? t : readWorking(p);
            LedgerCheck.Result lc = LedgerCheck.Check(Get(Ledger.DecisionsPath), Get(Ledger.QuestionsPath), Get(Ledger.TasksPath));
            foreach (string e in lc.Errors)
            {
                conflicts.Add($"합친 결과가 ledger 검사에 실패한다 — {e}");
            }
            if (conflicts.Count > 0)
            {
                return new Result(new Dictionary<string, string>(StringComparer.Ordinal), conflicts);
            }
            return new Result(merged, conflicts);
        }
    }
}

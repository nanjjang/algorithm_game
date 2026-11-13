using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Wizard.TaskGraph
{
    // main 에 병합된 작업 id 의 집합(DECISIONS-TECH T-65 4·5번). 검사 함수(GraphValidator, TaskAdder)는 이것을 인자로 받고
    // git 을 부르지 않는다. 읽는 곳은 명령 줄 층(Cli → MergeHistoryReader) 한 곳이다.
    // 기록을 보지 않았거나 읽지 못했으면 집합이 비어 있다: 전부 병합 전으로 보고 엄격하게 검사한다.
    // Summary 는 validate 요약의 한 줄이고 tools/check-tasks.mjs 와 글자가 같다.
    public sealed class MergeHistory
    {
        // main 의 커밋 제목: <type>(<작업 id>): … (PIPELINE 4.2절). Lifecycle.CommitSubject 가 이것을 가리킨다
        public static readonly Regex CommitSubject = new Regex(
            "^(feat|fix|test|data|docs|build|refactor|perf|art|chore)\\(([^()\\s]+)\\): ", RegexOptions.CultureInvariant);

        private readonly HashSet<string> _merged;

        private MergeHistory(HashSet<string> merged, string summary)
        {
            _merged = merged;
            Summary = summary;
        }

        public string Summary { get; }

        public int Count => _merged.Count;

        public IReadOnlyCollection<string> Merged => _merged;

        public bool IsMerged(string id) => _merged.Contains(id);

        // --no-history, 또는 기록을 넘기지 않은 호출(시험, TaskAdder.Prepare 를 직접 부르는 곳)
        public static MergeHistory NotRead()
        {
            return new MergeHistory(new HashSet<string>(StringComparer.Ordinal), "병합 기록: 보지 않았다(--no-history). 전부 병합 전으로 본다");
        }

        // git 이 실패했거나 <base> 가 없다
        public static MergeHistory Unreadable(string baseRef)
        {
            return new MergeHistory(new HashSet<string>(StringComparer.Ordinal), $"병합 기록: {baseRef}을 읽지 못했다. 전부 병합 전으로 본다");
        }

        // git log --format=%s <base> 의 출력에서
        public static MergeHistory FromSubjects(string baseRef, string subjects, IReadOnlyDictionary<string, TaskDef> graph)
        {
            HashSet<string> ids = MergedIds(subjects, graph);
            return new MergeHistory(ids, $"병합 기록: {baseRef}의 커밋 제목에서 병합된 작업 {ids.Count}개");
        }

        // 시험용: 집합을 직접 준다
        public static MergeHistory Of(string baseRef, IEnumerable<string> ids)
        {
            var set = new HashSet<string>(ids, StringComparer.Ordinal);
            return new MergeHistory(set, $"병합 기록: {baseRef}의 커밋 제목에서 병합된 작업 {set.Count}개");
        }

        // 커밋 제목이 CommitSubject 에 맞고 그 id 가 그래프에 있는 것. Lifecycle.MergedIds(reconcile, done)와 같은 판정이다
        public static HashSet<string> MergedIds(string subjects, IReadOnlyDictionary<string, TaskDef> graph)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (string line in subjects.Split('\n'))
            {
                Match m = CommitSubject.Match(line);
                if (m.Success && graph.ContainsKey(m.Groups[2].Value))
                {
                    ids.Add(m.Groups[2].Value);
                }
            }
            return ids;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Wizard.TaskGraph
{
    // 작업의 수명(PIPELINE 3.3절):
    //   todo ─start─▶ in_progress ─review─▶ review ─done─▶ done
    //                      ▲                  │
    //                      └──────reopen──────┘ (반려, 병합 게이트 실패)
    //   blocked-on-assets ─unblock─▶ todo
    // 상태는 StateStore 에만 쓴다. tasks.json 은 읽기만 한다.
    public sealed class Lifecycle
    {
        // main 의 커밋 제목: <type>(<작업 id>): … (PIPELINE 4.2절)
        public static readonly Regex CommitSubject = new Regex(
            "^(feat|fix|test|data|docs|build|refactor|perf|art|chore)\\(([^()\\s]+)\\): ", RegexOptions.CultureInvariant);

        private readonly RepoLayout _layout;
        private readonly TaskDoc _doc;
        private readonly StateStore _store;
        private readonly Git _git;

        public Lifecycle(RepoLayout layout, TaskDoc doc)
        {
            _layout = layout;
            _doc = doc;
            _store = new StateStore(layout.StatePath);
            _git = new Git(layout.MainRepo);
        }

        public StateStore Store => _store;

        public static string Effective(TaskDef t, IReadOnlyDictionary<string, string> state)
        {
            return state.TryGetValue(t.Id, out string? s) ? s : t.Status;
        }

        public IReadOnlyDictionary<string, string> Snapshot() => _store.Read();

        // deps 가 전부 done 인 todo 작업을 parallel_group 별로. 묶음 순서는 tasks.json 에 처음 나온 순서, 묶음 안은 tasks.json 순서.
        public IReadOnlyList<(string Group, IReadOnlyList<TaskDef> Tasks)> Ready()
        {
            IReadOnlyDictionary<string, string> st = Snapshot();
            var groups = new List<(string, List<TaskDef>)>();
            foreach (TaskDef t in _doc.Tasks)
            {
                if (Effective(t, st) != "todo")
                {
                    continue;
                }
                bool depsDone = t.Deps.All(d => _doc.ById.TryGetValue(d, out TaskDef? dt) && Effective(dt, st) == "done");
                if (!depsDone)
                {
                    continue;
                }
                int at = groups.FindIndex(g => g.Item1 == t.ParallelGroup);
                if (at < 0)
                {
                    groups.Add((t.ParallelGroup, new List<TaskDef> { t }));
                }
                else
                {
                    groups[at].Item2.Add(t);
                }
            }
            var firstSeen = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < _doc.Tasks.Count; i++)
            {
                firstSeen.TryAdd(_doc.Tasks[i].ParallelGroup, i);
            }
            return groups.OrderBy(g => firstSeen[g.Item1]).Select(g => (g.Item1, (IReadOnlyList<TaskDef>)g.Item2)).ToList();
        }

        // 상태만 바꾸는 전이(review, reopen, unblock)
        public string Transition(string id, string from, string to)
        {
            TaskDef t = _doc.Get(id);
            return _store.Update(st =>
            {
                string cur = Effective(t, st);
                if (cur != from)
                {
                    throw new TaskGraphException($"{id}: 상태가 {cur} 이라서 {to} 로 바꿀 수 없다({from} 에서만 된다)");
                }
                st[id] = to;
                return $"{id}: {from} → {to}";
            });
        }

        public string Start(string id, string baseRef)
        {
            TaskDef t = _doc.Get(id);
            string wt = _layout.WorktreeOf(id);
            string branch = RepoLayout.BranchOf(id);
            return _store.Update(st =>
            {
                string cur = Effective(t, st);
                if (cur != "todo")
                {
                    throw new TaskGraphException($"{id}: 상태가 {cur} 이라서 시작할 수 없다(todo 에서만 된다)");
                }
                var notDone = t.Deps.Where(d => !_doc.ById.TryGetValue(d, out TaskDef? dt) || Effective(dt, st) != "done").ToList();
                if (notDone.Count > 0)
                {
                    throw new TaskGraphException($"{id}: deps 가 아직 done 이 아니다: {string.Join(", ", notDone)} (상태 파일을 잃었으면 `taskgraph reconcile`)");
                }
                if (Directory.Exists(wt) || File.Exists(wt))
                {
                    throw new TaskGraphException($"{id}: 작업 트리 자리가 이미 있다: {wt}");
                }
                Directory.CreateDirectory(_layout.WorktreesDir);
                if (_git.BranchExists(branch))
                {
                    _git.RunChecked("worktree", "add", wt, branch);
                }
                else
                {
                    if (!_git.RefExists(baseRef))
                    {
                        throw new TaskGraphException($"{id}: 갈라질 기준 '{baseRef}' 이 없다");
                    }
                    _git.RunChecked("worktree", "add", wt, "-b", branch, baseRef);
                }
                string note = CloneUnityLibrary(t, wt);
                st[id] = "in_progress";
                return $"{id}: todo → in_progress\nWT={wt}\n브랜치: {branch}{note}";
            });
        }

        // unity/ 아래를 가진 작업은 main 쪽의 unity/Library 를 APFS 복제로 가져온다(PIPELINE 4.3). 실패해도 작업은 시작된다(첫 가져오기가 길어질 뿐).
        private string CloneUnityLibrary(TaskDef t, string wt)
        {
            if (!t.OwnedPaths.Any(p => p.StartsWith("unity/", StringComparison.Ordinal)))
            {
                return "";
            }
            string src = Path.Combine(_layout.MainRepo, "unity", "Library");
            string dst = Path.Combine(wt, "unity", "Library");
            if (!Directory.Exists(src) || Directory.Exists(dst))
            {
                return "";
            }
            Directory.CreateDirectory(Path.Combine(wt, "unity"));
            ProcResult r = Proc.Run("/bin/cp", new[] { "-c", "-R", src, dst });
            return r.ExitCode == 0
                ? "\nunity/Library 를 APFS 복제로 가져왔다"
                : $"\n경고: unity/Library 를 복제하지 못했다(종료 {r.ExitCode}): {r.FirstErrorLine()}";
        }

        // review → done. main 에 이 작업의 커밋이 있어야 한다. 작업 트리와 브랜치를 지운다(PIPELINE 3.8).
        // 작업 트리에 커밋하지 않은 것이 있으면 git 이 거절하고, 상태는 바뀌지 않는다.
        public string Done(string id, string mainRef)
        {
            TaskDef t = _doc.Get(id);
            string wt = _layout.WorktreeOf(id);
            string branch = RepoLayout.BranchOf(id);
            return _store.Update(st =>
            {
                string cur = Effective(t, st);
                if (cur != "review")
                {
                    throw new TaskGraphException($"{id}: 상태가 {cur} 이라서 done 으로 바꿀 수 없다(review 에서만 된다)");
                }
                if (!MergedIds(mainRef).Contains(id))
                {
                    throw new TaskGraphException($"{id}: {mainRef} 에 '<type>({id}): …' 커밋이 없다. 병합한 뒤에 done 으로 바꾼다");
                }
                var notes = new List<string>();
                if (Directory.Exists(wt))
                {
                    _git.RunChecked("worktree", "remove", wt);
                    notes.Add("작업 트리를 지웠다: " + wt);
                }
                if (_git.BranchExists(branch))
                {
                    _git.RunChecked("branch", "-D", branch);
                    notes.Add("브랜치를 지웠다: " + branch);
                }
                st[id] = "done";
                return $"{id}: review → done" + string.Concat(notes.Select(n => "\n" + n));
            });
        }

        // 상태 파일을 잃었을 때: main 의 커밋 제목에서 done 을, 남아 있는 작업 트리에서 in_progress 를 다시 만든다.
        public IReadOnlyList<string> Reconcile(string mainRef)
        {
            HashSet<string> merged = MergedIds(mainRef);
            return _store.Update(st =>
            {
                var log = new List<string>();
                foreach (TaskDef t in _doc.Tasks)
                {
                    string cur = Effective(t, st);
                    if (merged.Contains(t.Id))
                    {
                        if (cur != "done")
                        {
                            st[t.Id] = "done";
                            log.Add($"{t.Id}: {cur} → done ({mainRef} 의 커밋)");
                        }
                    }
                    else if (cur == "done")
                    {
                        log.Add($"경고  {t.Id}: 상태는 done 인데 {mainRef} 에 이 작업의 커밋이 없다(그대로 둔다)");
                    }
                    else if ((cur == "todo") && Directory.Exists(_layout.WorktreeOf(t.Id)) && _git.BranchExists(RepoLayout.BranchOf(t.Id)))
                    {
                        st[t.Id] = "in_progress";
                        log.Add($"{t.Id}: {cur} → in_progress (작업 트리가 있다)");
                    }
                }
                return log;
            });
        }

        public HashSet<string> MergedIds(string mainRef)
        {
            if (!_git.RefExists(mainRef))
            {
                throw new TaskGraphException($"'{mainRef}' 이 없다");
            }
            string subjects = _git.RunChecked("log", "--format=%s", mainRef);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (string line in subjects.Split('\n'))
            {
                Match m = CommitSubject.Match(line);
                if (m.Success && _doc.ById.ContainsKey(m.Groups[2].Value))
                {
                    ids.Add(m.Groups[2].Value);
                }
            }
            return ids;
        }
    }
}

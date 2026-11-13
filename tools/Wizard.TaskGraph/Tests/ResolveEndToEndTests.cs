using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Xunit;

namespace Wizard.TaskGraph.Tests
{
    // FIX-7, DECISIONS-TECH T-65 9번 끝에서 끝까지(TempRepo): main 에서 갈라진 두 브랜치가 tech-lead 의 구조를 흉내 낸다
    // (새 T 절과 요약 줄, 새 Q 줄, 같은 마일스톤에 FIX 카드 하나, 그 카드의 owned_paths 에 장부 셋과 파일 하나).
    // 첫 브랜치를 main 에 넣고 둘째를 main 위로 옮기면 세 장부가 충돌해 멈춘다 → taskgraph resolve → rebase --continue.
    public sealed class ResolveEndToEndTests
    {
        private static readonly string CheckTasks = Path.Combine(Fx.RepoRoot, "tools", "check-tasks.mjs");

        private const string Tail = "## 아직 정하지 않은 기술 항목\n\n- 없음\n";

        private static string Decisions(params int[] extra)
        {
            string rows = "| T-01 | 첫 결정 | [신규] |\n" + string.Concat(extra.Select(n => $"| T-{n:00} | 결정 {n} | [신규] |\n"));
            string sections = "## T-01 첫 결정\n\n- **결정**: 하나\n- **이유**: 둘\n\n"
                + string.Concat(extra.Select(n => $"## T-{n:00} 결정 {n}\n\n- **결정**: 결정 {n}\n- **이유**:\n  - 이유 {n}\n\n"));
            return "# 기술 결정\n\n## 요약\n\n| # | 결정 | 표시 |\n|---|---|---|\n" + rows + "\n" + sections + Tail;
        }

        private static string Questions(params int[] extra)
        {
            return "# 물을 것\n\n| # | 물음 |\n|---|---|\n| Q-01 | 하나 |\n" + string.Concat(extra.Select(n => $"| Q-{n:00} | 물음 {n} |\n"));
        }

        private static JsonObject Graph()
        {
            return Fx.Doc(
                Fx.Task("M0-BT-01", group: "M0-W01", paths: new[] { "tools/" }),
                Fx.Task("M2-A-01", milestone: "M2", group: "M2-W01", deps: new[] { "M0-BT-01" }, paths: new[] { "src/a/" }),
                Fx.Task("M2-A-02", milestone: "M2", group: "M2-W02", deps: new[] { "M0-BT-01" }, paths: new[] { "src/b/" }));
        }

        private static TempRepo Setup()
        {
            TempRepo repo = TempRepo.Create();
            repo.Write("docs/spec/README.md", "명세 없음\n");
            repo.Write(Ledger.DecisionsPath, Decisions());
            repo.Write(Ledger.QuestionsPath, Questions());
            repo.Write(Ledger.TasksPath, Fx.Text(Graph()));
            repo.Commit("docs(M0-BT-01): 장부");
            return repo;
        }

        // tech-lead 의 구조 흉내: 새 T 절, 요약 줄, Q 줄, FIX 카드(taskgraph add 와 같은 길), 장부 셋과 파일 하나를 가진다
        private static void Branch(TempRepo repo, string fix, int t, int q, string group, Action<JsonObject>? alsoEdit = null)
        {
            repo.Git("checkout", "-q", "-b", "task/" + fix, "main");
            repo.Write(Ledger.DecisionsPath, Decisions(t));
            repo.Write(Ledger.QuestionsPath, Questions(q));
            JsonObject card = Fx.Task(fix, milestone: "M2", group: group, deps: new[] { "M0-BT-01" },
                paths: new[] { Ledger.DecisionsPath, Ledger.QuestionsPath, Ledger.TasksPath, "fix/" + fix + ".txt" });
            AddResult r = TaskAdder.Prepare(File.ReadAllText(repo.PathOf(Ledger.TasksPath)), "t", card.ToJsonString(), Fx.SchemaPath, null);
            Assert.True(r.Report.Ok, string.Join("\n", r.Report.Errors));
            string text = r.NewText;
            if (alsoEdit != null)
            {
                var d = (JsonObject)JsonNode.Parse(text)!;
                alsoEdit(d);
                text = Fx.Text(d);
            }
            repo.Write(Ledger.TasksPath, text);
            repo.Write("fix/" + fix + ".txt", fix + "\n");
            repo.Commit($"docs({fix}): 결정과 카드");
            repo.Git("checkout", "-q", "main");
        }

        private static void MergeFirstAndStartRebase(TempRepo repo, string first, string second)
        {
            repo.Git("merge", "-q", "--ff-only", "task/" + first);
            repo.Git("checkout", "-q", "task/" + second);
            ProcResult rebase = repo.TryGit("rebase", "main");
            Assert.NotEqual(0, rebase.ExitCode);
            string unmerged = repo.Git("diff", "--name-only", "--diff-filter=U");
            Assert.Equal(new[] { Ledger.DecisionsPath, Ledger.QuestionsPath, Ledger.TasksPath }, unmerged.Split('\n', StringSplitOptions.RemoveEmptyEntries).OrderBy(x => x, StringComparer.Ordinal).ToArray());
        }

        private static JsonObject Card(JsonObject d, string id) => ((JsonArray)d["tasks"]!).OfType<JsonObject>().Single(t => (string)t["id"]! == id);

        [Fact]
        public void TwoTechLeadRestructures_SecondRebasedOntoFirst_ResolveThenAllChecksPass()
        {
            using TempRepo repo = Setup();
            Branch(repo, "FIX-1", 2, 2, "M2-W11");
            Branch(repo, "FIX-2", 3, 3, "M2-W12");
            MergeFirstAndStartRebase(repo, "FIX-1", "FIX-2");
            string tasks = repo.PathOf(Ledger.TasksPath);

            CliRun r = CliRun.In(repo, tasks, "resolve");
            Assert.True(r.Code == 0, r.ToString());
            Assert.Contains("resolve: 풀었다 — docs/dev/tasks.json", r.Stdout);
            Assert.Equal("", repo.Git("diff", "--name-only", "--diff-filter=U"));
            ProcResult cont = repo.TryGit("rebase", "--continue");
            Assert.True(cont.ExitCode == 0, cont.Stderr + cont.Stdout);

            foreach (string[] cmd in new[] { new[] { "recount", "--check" }, new[] { "validate" }, new[] { "ledger" } })
            {
                CliRun c = CliRun.In(repo, tasks, cmd);
                Assert.True(c.Code == 0, string.Join(" ", cmd) + "\n" + c);
            }
            ProcResult node = Proc.Run("node", new[] { CheckTasks, "--tasks", tasks, "--specs", repo.PathOf("docs/spec"), "--repo", repo.Dir });
            Assert.True(node.ExitCode == 0, node.Stdout + node.Stderr);

            // 두 브랜치가 더한 것이 다 있다
            Assert.Equal(Decisions(2, 3), File.ReadAllText(repo.PathOf(Ledger.DecisionsPath)));
            Assert.Equal(Questions(2, 3), File.ReadAllText(repo.PathOf(Ledger.QuestionsPath)));
            TaskDoc doc = TaskDoc.Load(tasks);
            Assert.Equal(new[] { "M0-BT-01", "M2-A-01", "M2-A-02", "FIX-1", "FIX-2" }, doc.Tasks.Select(t => t.Id).ToArray());
            JsonObject m2 = ((JsonArray)doc.Root["milestones"]!).OfType<JsonObject>().Single(m => (string)m["id"]! == "M2");
            Assert.Equal(4, (int)m2["task_count"]!);
            Assert.True(File.Exists(repo.PathOf("fix/FIX-1.txt")) && File.Exists(repo.PathOf("fix/FIX-2.txt")));
            // 두 카드는 장부를 같이 갖고 deps 로 이어지지 않았다
            Assert.DoesNotContain("FIX-1", doc.Get("FIX-2").Deps);
            Assert.Contains(Ledger.TasksPath, doc.Get("FIX-1").OwnedPaths);
            Assert.Contains(Ledger.TasksPath, doc.Get("FIX-2").OwnedPaths);
        }

        [Fact]
        public void SameCardTitleChangedDifferently_ResolveFails_FilesAndIndexUntouched()
        {
            using TempRepo repo = Setup();
            Branch(repo, "FIX-1", 2, 2, "M2-W11", d => Card(d, "M2-A-01")["title"] = "첫 브랜치의 제목");
            Branch(repo, "FIX-2", 3, 3, "M2-W12", d => Card(d, "M2-A-01")["title"] = "둘째 브랜치의 제목");
            MergeFirstAndStartRebase(repo, "FIX-1", "FIX-2");
            string tasks = repo.PathOf(Ledger.TasksPath);

            byte[][] before = Ledger.Paths.Select(p => File.ReadAllBytes(repo.PathOf(p))).ToArray();
            string indexBefore = repo.Git("ls-files", "-u");
            string status = repo.Git("status", "--porcelain");

            CliRun r = CliRun.In(repo, tasks, "resolve");
            Assert.Equal(1, r.Code);
            Assert.Contains("FAIL  tasks.json: 카드 M2-A-01의 'title'를 양쪽이 다르게 고쳤다", r.Stdout);
            Assert.Contains("작업 파일과 색인은 그대로다", r.Stdout);

            Assert.Equal(before, Ledger.Paths.Select(p => File.ReadAllBytes(repo.PathOf(p))).ToArray());
            Assert.Equal(indexBefore, repo.Git("ls-files", "-u"));
            Assert.Equal(status, repo.Git("status", "--porcelain"));
            repo.Git("rebase", "--abort");
        }

        [Fact]
        public void NoLedgerConflict_SaysSo_AndLeavesOtherConflictsAlone()
        {
            using TempRepo repo = Setup();
            repo.Git("checkout", "-q", "-b", "task/X", "main");
            repo.Write("src/a/x.txt", "x\n");
            repo.Commit("feat(X): x");
            repo.Git("checkout", "-q", "main");
            repo.Write("src/a/x.txt", "main\n");
            repo.Commit("feat(M2-A-01): y");
            repo.Git("checkout", "-q", "task/X");
            Assert.NotEqual(0, repo.TryGit("rebase", "main").ExitCode);

            CliRun r = CliRun.In(repo, repo.PathOf(Ledger.TasksPath), "resolve");
            Assert.True(r.Code == 0, r.ToString());
            Assert.Contains("resolve: 충돌한 장부가 없다", r.Stdout);
            Assert.Contains("남은 충돌(장부가 아니다. 건드리지 않았다): src/a/x.txt", r.Stdout);
            Assert.Equal("src/a/x.txt\n", repo.Git("diff", "--name-only", "--diff-filter=U"));
            repo.Git("rebase", "--abort");
        }
    }
}

using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Xunit;

namespace Wizard.TaskGraph.Tests
{
    // 수락 5·9: card 가 PIPELINE 3.4절의 카드를 채우고 WT 절대 경로를 넣는다. add 가 CCR·FIX 를 검사를 거쳐 더한다.
    public sealed class AddAndCardTests
    {
        [Fact]
        public void Card_FillsEverySectionOfTheTemplate()
        {
            using TempRepo repo = TempRepo.Create();
            JsonObject t = Fx.Task("M1-UE-01", milestone: "M1", group: "M1-W01", deps: new[] { "M0-BT-01" }, paths: new[] { "unity/Assets/Game/Input/" },
                vectors: new[] { "S13:TV-051", "S07§17.3" }, humanCheck: "한글 입력기를 켠 채 확인한다");
            t["contracts"] = Fx.Arr(new[] { "docs/dev/CONTRACTS.md §13" });
            repo.Write("docs/dev/tasks.json", Fx.Text(Fx.Doc(Fx.Task("M0-BT-01", paths: new[] { "tools/" }), t)));
            CliRun r = CliRun.In(repo, repo.PathOf("docs/dev/tasks.json"), "card", "M1-UE-01");
            Assert.True(r.Code == 0, r.ToString());
            string card = r.Stdout;
            string wt = Path.Combine(repo.Dir, ".claude", "worktrees", "M1-UE-01");
            foreach (string expected in new[]
            {
                "# 작업 카드 M1-UE-01 — 작업 M1-UE-01",
                "- 마일스톤 / 묶음: M1 / M1-W01",
                "- 담당: build-tools-engineer (opus)   검토: opus",
                "- 작업 트리(절대 경로): WT=" + wt + "   브랜치: task/M1-UE-01",
                "## 목표", "## 명세 (먼저 읽는다)", "- docs/dev/PIPELINE.md §3",
                "## 계약 (시그니처는 여기 그대로)", "- docs/dev/CONTRACTS.md §13",
                "## 소유 파일 (이 밖은 고치지 않는다)", "- `unity/Assets/Game/Input/`",
                "## 앞선 작업", "- M0-BT-01 — 작업 M0-BT-01 (todo). 남긴 곳: `tools/`",
                "## 수락 테스트", "- M1-UE-01 의 수락 조건", "벡터: S13:TV-051, S07§17.3",
                "공통: $WT/tools/ci/ci.sh fast 통과",
                "## 금지 사항", "## 사람 확인 항목", "한글 입력기를 켠 채 확인한다",
            })
            {
                Assert.Contains(expected, card);
            }
            Assert.True(Path.IsPathRooted(wt));
        }

        [Fact]
        public void Card_RealTask_UsesMainRepoWorktreePath()
        {
            CliRun r = CliRun.Run("--repo", Fx.RepoRoot, "card", "M0-BT-06");
            Assert.True(r.Code == 0, r.ToString());
            string main = RepoLayout.FindMainRepo(Fx.RepoRoot);
            Assert.Contains("WT=" + Path.Combine(main, ".claude", "worktrees", "M0-BT-06") + "   브랜치: task/M0-BT-06", r.Stdout);
        }

        private static string Input(string id, string group = "M0-W09", string[]? deps = null, string[]? paths = null, string milestone = "M0")
        {
            JsonObject t = Fx.Task(id, milestone: milestone, group: group, deps: deps, paths: paths ?? new[] { "tools/Wizard.Fix/" });
            t.Remove("status");
            t.Remove("vectors");
            t.Remove("contracts");
            return t.ToJsonString();
        }

        private static JsonObject Graph()
        {
            return Fx.Doc(
                Fx.Task("M0-BT-01", group: "M0-W01", paths: new[] { "tools/" }),
                Fx.Task("M1-CORE-01", milestone: "M1", group: "M1-W01", deps: new[] { "M0-BT-01" }, paths: new[] { "src/Wizard.Core/" }));
        }

        [Fact]
        public void Add_AutoNumbersFix_UpdatesMilestone_AndLeavesOtherLinesAlone()
        {
            using TempRepo repo = TempRepo.Create();
            string tasks = repo.PathOf("docs/dev/tasks.json");
            string before = Fx.Text(Graph());
            repo.Write("docs/dev/tasks.json", before);
            repo.Write("in.json", Input("FIX", deps: new[] { "M0-BT-01" }));
            CliRun r = CliRun.In(repo, tasks, "add", repo.PathOf("in.json"), "--no-vectors");
            Assert.True(r.Code == 0, r.ToString());
            Assert.Contains("FIX-1", r.Stdout);

            string after = File.ReadAllText(tasks);
            TaskDoc doc = TaskDoc.Parse(after, "tasks.json");
            Assert.Equal(new[] { "M0-BT-01", "FIX-1", "M1-CORE-01" }, doc.Tasks.Select(t => t.Id).ToArray()); // 같은 마일스톤의 끝
            TaskDef fix = doc.Get("FIX-1");
            Assert.Equal("todo", fix.Status);
            Assert.Equal(new[] { "id", "milestone", "title", "module", "owner_agent", "owner_model", "reviewer_model", "deps", "spec_refs", "contracts",
                "owned_paths", "acceptance", "vectors", "parallel_group", "status" }, fix.Node.Select(kv => kv.Key).ToArray());
            JsonObject m0 = (JsonObject)doc.Root["milestones"]![0]!;
            Assert.Equal(2, (int)m0["task_count"]!);
            Assert.Equal("M0-W01,M0-W09", string.Join(",", ((JsonArray)m0["parallel_groups"]!).Select(x => (string)x!)));
            Assert.True(GraphValidator.Validate(doc, Fx.SchemaPath, null).Ok);

            // 없어진 줄은 M0 의 task_count 와, 쉼표가 붙은 마지막 묶음 이름뿐이다(나머지는 더한 줄). 줄 수를 세어 비교한다
            var removed = after.Split('\n').Aggregate(before.Split('\n').ToList(), (left, line) => { left.Remove(line); return left; });
            Assert.Equal(new[] { "    \"M0-W01\"", "   \"task_count\": 1," }, removed.OrderBy(x => x, StringComparer.Ordinal).ToArray());

            // 다음 번호
            repo.Write("in2.json", Input("FIX", group: "M0-W10", paths: new[] { "tools/Wizard.Fix2/" }, deps: new[] { "M0-BT-01" }));
            Assert.True(CliRun.In(repo, tasks, "add", repo.PathOf("in2.json"), "--no-vectors").Code == 0);
            Assert.True(TaskDoc.Load(tasks).ById.ContainsKey("FIX-2"));
        }

        [Fact]
        public void Add_ThatBreaksTheGraph_IsRejected_AndFileUnchanged()
        {
            using TempRepo repo = TempRepo.Create();
            string tasks = repo.PathOf("docs/dev/tasks.json");
            repo.Write("docs/dev/tasks.json", Fx.Text(Graph()));
            byte[] before = File.ReadAllBytes(tasks);

            // src/Wizard.Core/ 와 겹치는데 deps 가 없다
            repo.Write("in.json", Input("CCR-7", milestone: "M1", group: "M1-W02", paths: new[] { "src/Wizard.Core/Fix64.cs" }));
            CliRun r = CliRun.In(repo, tasks, "add", repo.PathOf("in.json"), "--no-vectors");
            Assert.Equal(1, r.Code);
            Assert.Contains("M1-CORE-01와 CCR-7의 owned_paths가 겹치는데 deps로 순서가 정해져 있지 않다", r.Stdout);
            Assert.Equal(before, File.ReadAllBytes(tasks));

            // 없는 dep
            repo.Write("in.json", Input("CCR-7", deps: new[] { "M9-NOPE-01" }));
            Assert.Equal(1, CliRun.In(repo, tasks, "add", repo.PathOf("in.json"), "--no-vectors").Code);
            Assert.Equal(before, File.ReadAllBytes(tasks));

            // CCR·FIX 가 아닌 id, 이미 있는 id
            repo.Write("in.json", Input("M0-BT-77"));
            CliRun wrongKind = CliRun.In(repo, tasks, "add", repo.PathOf("in.json"), "--no-vectors");
            Assert.Equal(2, wrongKind.Code);
            Assert.Contains("CCR·FIX 작업만", wrongKind.Stderr);
            Assert.Equal(before, File.ReadAllBytes(tasks));
        }

        [Fact]
        public void Add_ToCopyOfRealGraph_PassesFullValidationIncludingVectors()
        {
            string text = File.ReadAllText(Fx.RealTasksPath);
            JsonObject t = Fx.Task("CCR", group: "M0-W09", deps: new[] { "M0-TL-01" }, paths: new[] { "docs/dev/CONTRACTS.md" }, agent: "tech-lead", model: "fable");
            AddResult r = TaskAdder.Prepare(text, "tasks.json", t.ToJsonString(), Fx.SchemaPath, Fx.RealSpecsDir);
            Assert.True(r.Report.Ok, string.Join("\n", r.Report.Errors));
            Assert.Equal("CCR-1", r.Id);
            TaskDoc doc = TaskDoc.Parse(r.NewText, "x");
            JsonObject m0 = ((JsonArray)doc.Root["milestones"]!).OfType<JsonObject>().Single(m => (string)m["id"]! == "M0");
            JsonObject old = ((JsonArray)TaskDoc.Parse(text, "y").Root["milestones"]!).OfType<JsonObject>().Single(m => (string)m["id"]! == "M0");
            Assert.Equal((int)old["task_count"]! + 1, (int)m0["task_count"]!);
            Assert.Contains("\"id\": \"CCR-1\"", r.NewText);
        }
    }
}

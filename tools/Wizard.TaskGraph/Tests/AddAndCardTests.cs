using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
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

        // ---------- 실제 그래프의 복사본에 CCR 하나를 더해 본다 ----------
        // 실제 docs/dev/tasks.json 은 PIPELINE 3.7절대로 CCR·FIX 작업이 계속 늘어난다. 그래서 받은 id 를 상수로 적지 않고
        // "그 그래프의 가장 큰 CCR 번호 + 1"로 기대한다(FIX-2: 예전에는 "CCR-1"로 적혀 있어 실제 그래프에 CCR-1이 들어오면
        // Expected "CCR-1", Actual "CCR-2"로 fast 게이트가 막혔다). 기대값은 TaskAdder 를 거치지 않고 여기서 따로 센다.
        //
        // 알려진 한계(경로 겹침): 더해 보는 작업은 deps [M0-TL-01], owned_paths [docs/dev/CONTRACTS.md] 다. M0-TL-01 도
        // CONTRACTS.md 를 가지므로 deps 로 순서가 정해져 통과한다. 그런데 M0-TL-01 말고 다른 작업이 CONTRACTS.md(또는 그것을
        // 덮는 docs/dev/, docs/ 같은 경로)를 owned_paths 에 넣고, 그 작업과 이 작업이 deps 로 이어지지 않으면 검사 4
        // ("owned_paths가 겹치는데 deps로 순서가 정해져 있지 않다")로 이 시험이 실패한다. PIPELINE 3.7절의 CCR 작업은
        // CONTRACTS.md 를 고치는 작업이라 실제 CCR 이 들어오면 바로 이 경우가 된다. 그 경우는
        // Add_ToCopyOfRealGraph_WhenAnotherTaskAlsoOwnsContracts_FailsOnOverlap 이 그대로 고정해 둔다. 어떻게 피할지
        // (시험 쪽에서 deps 를 그래프에서 고르는지, PIPELINE 3.7절이 CCR 의 deps 를 정하는지)는 결과 보고의 물음으로 올렸다.
        private const string ContractsPath = "docs/dev/CONTRACTS.md";
        private static readonly Regex CcrOrFixId = new Regex("^(CCR|FIX)-([0-9]+)$", RegexOptions.CultureInvariant);

        private static JsonObject ContractsCcr()
        {
            return Fx.Task("CCR", group: "M0-W09", deps: new[] { "M0-TL-01" }, paths: new[] { ContractsPath }, agent: "tech-lead", model: "fable");
        }

        // 그래프에 있는 kind-<번호> 의 가장 큰 번호. 없으면 0
        private static int MaxNumber(string text, string kind)
        {
            int max = 0;
            foreach (TaskDef t in TaskDoc.Parse(text, "max").Tasks)
            {
                Match m = CcrOrFixId.Match(t.Id);
                if (m.Success && m.Groups[1].Value == kind)
                {
                    max = Math.Max(max, int.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture));
                }
            }
            return max;
        }

        // 실제 그래프를 쓰는 시험은 실제 저장소의 병합 기록을 읽어 넘긴다(Cli 와 같은 길. T-65 4·5번)
        private static MergeHistory RealHistory(string text)
        {
            return MergeHistoryReader.Read(new Git(Fx.RepoRoot), "main", TaskDoc.Parse(text, "tasks.json"));
        }

        private static string Ccr(int n) => "CCR-" + n.ToString(System.Globalization.CultureInfo.InvariantCulture);

        private static int M0TaskCount(string text)
        {
            JsonObject m0 = ((JsonArray)TaskDoc.Parse(text, "m0").Root["milestones"]!).OfType<JsonObject>().Single(m => (string)m["id"]! == "M0");
            return (int)m0["task_count"]!;
        }

        // 실제 그래프 흉내: 그래프에 이미 있던 것처럼 작업 하나를 더한 문서를 돌려준다(검사 전체를 통과해야 한다)
        private static string WithExisting(string text, JsonObject task)
        {
            AddResult r = TaskAdder.Prepare(text, "tasks.json", task.ToJsonString(), Fx.SchemaPath, Fx.RealSpecsDir, RealHistory(text));
            Assert.True(r.Report.Ok, string.Join("\n", r.Report.Errors));
            return r.NewText;
        }

        // 더해 보고, 받은 id = (더하기 전 문서의 가장 큰 CCR 번호 + 1)인지와 M0 의 task_count 가 하나 늘었는지 본다
        private static void AssertAddsNextCcr(string text, int expectedNumber)
        {
            Assert.Equal(expectedNumber, MaxNumber(text, "CCR") + 1); // 기대값 계산 자체를 확인한다
            AddResult r = TaskAdder.Prepare(text, "tasks.json", ContractsCcr().ToJsonString(), Fx.SchemaPath, Fx.RealSpecsDir, RealHistory(text));
            Assert.True(r.Report.Ok, string.Join("\n", r.Report.Errors));
            Assert.Equal(Ccr(expectedNumber), r.Id);
            Assert.Equal(M0TaskCount(text) + 1, M0TaskCount(r.NewText));
            Assert.Contains("\"id\": \"" + Ccr(expectedNumber) + "\"", r.NewText);
            Assert.Equal(1, TaskDoc.Parse(r.NewText, "x").Tasks.Count(x => x.Id == Ccr(expectedNumber)));
        }

        [Fact]
        public void Add_ToCopyOfRealGraph_PassesFullValidationIncludingVectors()
        {
            string text = File.ReadAllText(Fx.RealTasksPath);
            AssertAddsNextCcr(text, MaxNumber(text, "CCR") + 1);
        }

        [Fact]
        public void Add_ToCopyOfRealGraph_WithCcrAndFixAlreadyPresent_TakesHighestCcrPlusOne()
        {
            string real = File.ReadAllText(Fx.RealTasksPath);
            int ccr0 = MaxNumber(real, "CCR");
            int fix0 = MaxNumber(real, "FIX");

            // M1-SIM-06 에서 본 경우: 실제 그래프에 CCR 이 하나 들어와 있다(번호는 add 가 매긴다). 경로는 아무 작업과도 겹치지 않게 한다
            string withOne = WithExisting(real, Fx.Task("CCR", group: "M0-W10", deps: new[] { "M0-TL-01" },
                paths: new[] { "docs/dev/ccr-simulated/first.md" }, agent: "tech-lead", model: "fable"));
            Assert.Equal(ccr0 + 1, MaxNumber(withOne, "CCR"));
            AssertAddsNextCcr(withOne, ccr0 + 2);

            // 번호가 비어 있어도(개수가 아니라 가장 큰 번호) + 1 이고, FIX 번호는 CCR 번호에 섞이지 않는다
            string withGap = WithExisting(withOne, Fx.Task(Ccr(ccr0 + 7), group: "M0-W11", deps: new[] { "M0-TL-01" },
                paths: new[] { "docs/dev/ccr-simulated/gap.md" }, agent: "tech-lead", model: "fable"));
            string withFix = WithExisting(withGap, Fx.Task("FIX-" + (fix0 + ccr0 + 20).ToString(System.Globalization.CultureInfo.InvariantCulture),
                group: "M0-W12", deps: new[] { "M0-BT-06" }, paths: new[] { "docs/dev/ccr-simulated/fix.md" }));
            Assert.Equal(fix0 + ccr0 + 20, MaxNumber(withFix, "FIX"));
            AssertAddsNextCcr(withFix, ccr0 + 8);
        }

        // 위 주석의 "알려진 한계"를 고정한다: M0-TL-01 이 아닌 작업이 CONTRACTS.md 를 갖고 deps 로 이어지지 않으면
        // 실제 그래프 복사본에 더해 보는 시험은 번호가 아니라 경로 겹침으로 실패한다.
        // FIX-7(T-65): 병합 기록을 넘겨도 그대로 성립한다. 병합 전인 두 CCR 이 CONTRACTS.md 를 같이 가진다(CONTRACTS.md 는 장부가 아니다)
        [Fact]
        public void Add_ToCopyOfRealGraph_WhenAnotherTaskAlsoOwnsContracts_FailsOnOverlap()
        {
            string real = File.ReadAllText(Fx.RealTasksPath);
            int ccr0 = MaxNumber(real, "CCR");
            string withContractsCcr = WithExisting(real, Fx.Task("CCR", group: "M0-W10", deps: new[] { "M0-TL-01" },
                paths: new[] { ContractsPath }, agent: "tech-lead", model: "fable"));

            AddResult r = TaskAdder.Prepare(withContractsCcr, "tasks.json", ContractsCcr().ToJsonString(), Fx.SchemaPath, Fx.RealSpecsDir, RealHistory(withContractsCcr));
            Assert.Equal(Ccr(ccr0 + 2), r.Id);
            Assert.False(r.Report.Ok);
            Assert.Equal(
                new[] { $"{Ccr(ccr0 + 1)}와 {Ccr(ccr0 + 2)}의 owned_paths가 겹치는데 deps로 순서가 정해져 있지 않다 — {ContractsPath} ↔ {ContractsPath}" },
                r.Report.Errors.ToArray());
        }
    }
}

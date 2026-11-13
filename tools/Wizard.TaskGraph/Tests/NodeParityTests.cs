using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Xunit;

namespace Wizard.TaskGraph.Tests
{
    // 수락 7: validate 가 node tools/check-tasks.mjs 와 "같은 것을 본다". 같은 입력에 두 도구를 돌려
    // FAIL 줄(스키마 줄 제외)과 요약 줄이 순서까지 같은지 본다. node 가 없으면 건너뛰지 않고 실패한다.
    // FIX-7(T-65 4·5번): 병합 기록을 같이 넘긴다. node 에는 --repo·--base·--no-history 를, 우리 쪽에는 Cli 와 같은 길
    // (MergeHistoryReader)로 읽은 기록을 준다. (가) 실제 그래프와 실제 기록 (나) 망가뜨린 복사본과 --no-history
    // (다) 기록이 있는 TempRepo 의 작은 그래프 (라) <base>가 없는 저장소.
    public sealed class NodeParityTests
    {
        private static readonly string CheckTasks = Path.Combine(Fx.RepoRoot, "tools", "check-tasks.mjs");

        // history: null 이면 --no-history, 아니면 (저장소, 기준)
        private static (List<string> Fails, List<string> Summary) Node(string tasksPath, string specsDir, (string Repo, string Base)? history)
        {
            ProcResult r;
            var args = new List<string> { CheckTasks, "--tasks", tasksPath, "--specs", specsDir };
            if (history == null)
            {
                args.Add("--no-history");
            }
            else
            {
                args.AddRange(new[] { "--repo", history.Value.Repo, "--base", history.Value.Base });
            }
            try
            {
                r = Proc.Run("node", args);
            }
            catch (TaskGraphException e)
            {
                throw new InvalidOperationException("node 가 없다. Node.js 를 설치해야 한다(tools/check-tasks.mjs 대조 테스트): " + e.Message);
            }
            return Split(r.Stdout);
        }

        private static (List<string> Fails, List<string> Summary) Ours(string tasksPath, string specsDir, (string Repo, string Base)? history)
        {
            TaskDoc doc = TaskDoc.Load(tasksPath);
            MergeHistory h = history == null ? MergeHistory.NotRead() : MergeHistoryReader.Read(new Git(history.Value.Repo), history.Value.Base, doc);
            ValidationReport rep = GraphValidator.Validate(doc, schemaPath: null, specsDir, h);
            var w = new StringWriter();
            rep.Print(w);
            (List<string> fails, List<string> summary) = Split(w.ToString());
            summary.RemoveAll(s => s.StartsWith("스키마:", StringComparison.Ordinal));
            return (fails, summary);
        }

        private static (List<string>, List<string>) Split(string output)
        {
            var lines = output.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
            var fails = lines.Where(l => l.StartsWith("FAIL  ", StringComparison.Ordinal)).ToList();
            var summary = lines.Where(l => l.StartsWith("작업 ", StringComparison.Ordinal) || l.StartsWith("위상정렬", StringComparison.Ordinal)
                || l.StartsWith("병합 기록:", StringComparison.Ordinal)
                || l.StartsWith("벡터:", StringComparison.Ordinal) || l == "통과" || l.StartsWith("실패 ", StringComparison.Ordinal)).ToList();
            return (fails, summary);
        }

        private static (List<string> Fails, List<string> Summary) AssertSame(string tasksPath, string specsDir, bool expectFailures, (string Repo, string Base)? history)
        {
            var node = Node(tasksPath, specsDir, history);
            var ours = Ours(tasksPath, specsDir, history);
            Assert.Equal(string.Join("\n", node.Fails), string.Join("\n", ours.Fails));
            Assert.Equal(string.Join("\n", node.Summary), string.Join("\n", ours.Summary));
            Assert.Equal(expectFailures, ours.Fails.Count > 0);
            Assert.Contains(ours.Summary, l => l.StartsWith("병합 기록:", StringComparison.Ordinal));
            return ours;
        }

        // (가) 실제 그래프와 실제 저장소의 main 기록
        [Fact]
        public void RealTasksJson_SameAsCheckTasks()
        {
            var ours = AssertSame(Fx.RealTasksPath, Fx.RealSpecsDir, expectFailures: false, (Fx.RepoRoot, "main"));
            Assert.Contains(ours.Summary, l => l.StartsWith("병합 기록: main의 커밋 제목에서 병합된 작업 ", StringComparison.Ordinal));
        }

        // (다) 기록이 있는 TempRepo 의 작은 그래프: 병합된 작업과의 겹침(통과), 장부 겹침(통과), 병합 전인 겹침(실패)이 섞였다
        [Fact]
        public void SmallGraphWithHistory_SameAsCheckTasks()
        {
            using TempRepo repo = TempRepo.Create();
            repo.Write("docs/spec/README.md", "명세 없음\n");
            string tasks = repo.PathOf("docs/dev/tasks.json");
            repo.Write("docs/dev/tasks.json", Fx.Text(Fx.Doc(
                Fx.Task("A-01", group: "G1", paths: new[] { "src/x/" }),
                Fx.Task("A-02", group: "G2", paths: new[] { "src/x/y.cs" }),
                Fx.Task("A-03", group: "G1", paths: new[] { "src/x/z.cs" }),
                Fx.Task("L-01", group: "G3", paths: new[] { Ledger.DecisionsPath, Ledger.TasksPath, "tools/l1/" }),
                Fx.Task("L-02", group: "G4", paths: new[] { Ledger.DecisionsPath, Ledger.QuestionsPath, Ledger.TasksPath }),
                Fx.Task("U-01", group: "G5", paths: new[] { "docs/dev/CONTRACTS.md" }),
                Fx.Task("U-02", group: "G6", paths: new[] { "docs/dev/" }))));
            repo.Commit("feat(A-01): 병합된 작업");
            repo.Commit("chore: 형식이 다른 제목");
            repo.Commit("fix(NOPE-1): 그래프에 없는 id");
            var ours = AssertSame(tasks, repo.PathOf("docs/spec"), expectFailures: true, (repo.Dir, "main"));
            Assert.Equal(new[] { "FAIL  U-01와 U-02의 owned_paths가 겹치는데 deps로 순서가 정해져 있지 않다 — docs/dev/CONTRACTS.md ↔ docs/dev/" }, ours.Fails.ToArray());
            Assert.Contains("병합 기록: main의 커밋 제목에서 병합된 작업 1개", ours.Summary);
        }

        // (라) <base>가 없는 저장소: 둘 다 읽지 못했다고 적고 엄격하게 본다
        [Fact]
        public void MissingBase_SameAsCheckTasks_Strict()
        {
            using TempRepo repo = TempRepo.Create();
            repo.Write("docs/spec/README.md", "명세 없음\n");
            string tasks = repo.PathOf("docs/dev/tasks.json");
            repo.Write("docs/dev/tasks.json", Fx.Text(Fx.Doc(
                Fx.Task("A-01", group: "G1", paths: new[] { "src/x/" }),
                Fx.Task("A-02", group: "G2", paths: new[] { "src/x/y.cs" }))));
            repo.Commit("feat(A-01): 병합된 작업");
            var ours = AssertSame(tasks, repo.PathOf("docs/spec"), expectFailures: true, (repo.Dir, "no-such-base"));
            Assert.Contains("병합 기록: no-such-base을 읽지 못했다. 전부 병합 전으로 본다", ours.Summary);
            Assert.Single(ours.Fails);
        }

        // (나) 실제 그래프의 망가뜨린 복사본, --no-history(병합된 작업끼리의 겹침도 엄격하게 본다)
        [Fact]
        public void BrokenCopiesOfRealGraph_SameDiagnosticsAsCheckTasks()
        {
            using var dir = TempRepo.Create(git: false);
            var mutations = new List<Action<JsonObject>>
            {
                // 묶음 안 경로 겹침과 deps 없는 경로 겹침
                d => Task(d, "M0-BT-03")["owned_paths"] = Fx.Arr(new[] { "tools/Wizard.MetaGen/x.cs", "tools/Wizard.TaskGraph/" }),
                // 순환
                d => Task(d, "M0-TL-02")["deps"] = Fx.Arr(new[] { "M0-INT" }),
                // 없는 dep, 모델, 집계
                d =>
                {
                    Task(d, "M1-CORE-01")["deps"] = Fx.Arr(new[] { "M0-NOPE-01" });
                    Task(d, "M0-BT-02")["owner_model"] = "sonnet";
                    ((JsonObject)((JsonArray)d["milestones"]!)[0]!)["task_count"] = 99;
                },
                // 벡터: 이중 배정, 미배정, 틀린 꼴
                d =>
                {
                    JsonObject withVectors = ((JsonArray)d["tasks"]!).OfType<JsonObject>().First(t => ((JsonArray)t["vectors"]!).Count > 1);
                    var v = (JsonArray)withVectors["vectors"]!;
                    v.RemoveAt(0);
                    v.Add("S1:bad");
                    JsonObject other = ((JsonArray)d["tasks"]!).OfType<JsonObject>().Last(t => ((JsonArray)t["vectors"]!).Count > 0);
                    ((JsonArray)other["vectors"]!).Add((string)((JsonArray)other["vectors"]!)[0]!);
                },
                // 처음 분류가 아닌 status, 빈 acceptance, 모호한 벡터 표현
                d =>
                {
                    Task(d, "M0-BT-04")["status"] = "done";
                    Task(d, "M0-BT-05")["acceptance"] = new JsonArray();
                    Task(d, "M0-TE-01")["acceptance"] = Fx.Arr(new[] { "7.3절 중 첫 다섯 벡터를 통과한다" });
                },
            };
            int i = 0;
            foreach (Action<JsonObject> mutate in mutations)
            {
                JsonObject doc = (JsonObject)JsonNode.Parse(File.ReadAllText(Fx.RealTasksPath))!;
                mutate(doc);
                string path = dir.PathOf($"tasks-{i++}.json");
                File.WriteAllText(path, Fx.Text(doc));
                AssertSame(path, Fx.RealSpecsDir, expectFailures: true, history: null);
            }
        }

        private static JsonObject Task(JsonObject doc, string id)
        {
            return ((JsonArray)doc["tasks"]!).OfType<JsonObject>().Single(t => (string)t["id"]! == id);
        }
    }
}

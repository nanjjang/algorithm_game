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
    public sealed class NodeParityTests
    {
        private static readonly string CheckTasks = Path.Combine(Fx.RepoRoot, "tools", "check-tasks.mjs");

        private static (List<string> Fails, List<string> Summary) Node(string tasksPath, string specsDir)
        {
            ProcResult r;
            try
            {
                r = Proc.Run("node", new[] { CheckTasks, "--tasks", tasksPath, "--specs", specsDir });
            }
            catch (TaskGraphException e)
            {
                throw new InvalidOperationException("node 가 없다. Node.js 를 설치해야 한다(tools/check-tasks.mjs 대조 테스트): " + e.Message);
            }
            return Split(r.Stdout);
        }

        private static (List<string> Fails, List<string> Summary) Ours(string tasksPath, string specsDir)
        {
            ValidationReport rep = GraphValidator.Validate(TaskDoc.Load(tasksPath), schemaPath: null, specsDir);
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
                || l.StartsWith("벡터:", StringComparison.Ordinal) || l == "통과" || l.StartsWith("실패 ", StringComparison.Ordinal)).ToList();
            return (fails, summary);
        }

        private static void AssertSame(string tasksPath, string specsDir, bool expectFailures)
        {
            var node = Node(tasksPath, specsDir);
            var ours = Ours(tasksPath, specsDir);
            Assert.Equal(string.Join("\n", node.Fails), string.Join("\n", ours.Fails));
            Assert.Equal(string.Join("\n", node.Summary), string.Join("\n", ours.Summary));
            Assert.Equal(expectFailures, ours.Fails.Count > 0);
        }

        [Fact]
        public void RealTasksJson_SameAsCheckTasks()
        {
            AssertSame(Fx.RealTasksPath, Fx.RealSpecsDir, expectFailures: false);
        }

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
                AssertSame(path, Fx.RealSpecsDir, expectFailures: true);
            }
        }

        private static JsonObject Task(JsonObject doc, string id)
        {
            return ((JsonArray)doc["tasks"]!).OfType<JsonObject>().Single(t => (string)t["id"]! == id);
        }
    }
}

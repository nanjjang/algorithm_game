using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Xunit;

namespace Wizard.TaskGraph.Tests
{
    // FIX-7, DECISIONS-TECH T-65 6번: next-ids 가 main, task/* 브랜치의 끝, 모든 작업 트리의 작업 파일(커밋하지 않은 것 포함)을 본다.
    // taskgraph add 의 자동 번호도 그 최댓값의 다음이다.
    public sealed class NextIdsTests
    {
        private static string Decisions(params int[] sections)
        {
            string rows = string.Concat(sections.Select(n => $"| T-{n:00} | 결정 {n} | [신규] |\n"));
            string body = string.Concat(sections.Select(n => $"## T-{n:00} 결정 {n}\n\n- **결정**: 무엇\n\n"));
            return "# 기술 결정\n\n## 요약\n\n| # | 결정 | 표시 |\n|---|---|---|\n" + rows + "\n" + body + "## 아직 정하지 않은 기술 항목\n";
        }

        private static string Questions(params int[] ids)
        {
            return "# 물을 것\n\n| # | 물음 | 임시 | 곳 | 언제 |\n|---|---|---|---|---|\n" + string.Concat(ids.Select(n => $"| Q-{n:00} | 물음 {n} | 임시 | - | - |\n"));
        }

        private static string Graph(params string[] fixIds)
        {
            var tasks = new[] { Fx.Task("M0-BT-01", group: "M0-W01", paths: new[] { "tools/" }) }
                .Concat(fixIds.Select((id, i) => Fx.Task(id, group: "M0-W0" + (i + 2), deps: new[] { "M0-BT-01" }, paths: new[] { "fix/" + id + "/" })))
                .ToArray();
            return Fx.Text(Fx.Doc(tasks));
        }

        [Fact]
        public void Scan_PureFunction_FindsMaxAndWhere()
        {
            var sources = new[]
            {
                new NextIds.Source("main", new System.Collections.Generic.Dictionary<string, string>
                {
                    [Ledger.DecisionsPath] = Decisions(1, 4),
                    [Ledger.QuestionsPath] = Questions(2),
                    [Ledger.TasksPath] = Graph("FIX-3"),
                    ["docs/dev/PIPELINE.md"] = "CCR-4 를 본다. FIX-10 은 다른 곳의 이야기다\n",
                    ["docs/dev/gates/x.md"] = "FIX-99 는 docs/dev 바로 아래가 아니다\n",
                }),
                new NextIds.Source("task/X", new System.Collections.Generic.Dictionary<string, string>
                {
                    [Ledger.DecisionsPath] = Decisions(1, 4, 9),
                }),
            };
            var e = NextIds.Scan(sources).ToDictionary(x => x.Kind);
            Assert.Equal(9, e["T"].Max);
            Assert.Equal("T-10", e["T"].Next);
            Assert.Equal(new[] { "task/X" }, e["T"].Where.ToArray());
            Assert.Equal("Q-03", e["Q"].Next);
            Assert.Equal(10, e["FIX"].Max);
            Assert.Equal("FIX-11", e["FIX"].Next);
            Assert.Equal("CCR-5", e["CCR"].Next);
        }

        [Fact]
        public void NextIds_ReadsMainBranchesAndUncommittedWorktrees_AndAddUsesIt()
        {
            using TempRepo repo = TempRepo.Create();
            repo.Git("branch", "task/EMPTY");   // 장부가 없는 브랜치는 건너뛴다

            repo.Write(Ledger.DecisionsPath, Decisions(1, 4));
            repo.Write(Ledger.QuestionsPath, Questions(1, 2));
            repo.Write(Ledger.TasksPath, Graph("FIX-3"));
            repo.Commit("docs(M0-BT-01): 장부");

            // task/X 의 커밋에 FIX-5, T-07
            repo.Git("checkout", "-q", "-b", "task/X");
            repo.Write(Ledger.TasksPath, Graph("FIX-3", "FIX-5"));
            repo.Write(Ledger.DecisionsPath, Decisions(1, 4, 7));
            repo.Commit("docs(FIX-5): 카드");
            repo.Git("checkout", "-q", "main");

            // 다른 작업 트리의 커밋하지 않은 tasks.json 에 FIX-6, OPEN-QUESTIONS 에 Q-09
            string wt = Path.Combine(repo.Dir, ".claude", "worktrees", "W");
            repo.Git("worktree", "add", "-q", wt, "-b", "task/W", "main");
            File.WriteAllText(Path.Combine(wt, "docs", "dev", "tasks.json"), Graph("FIX-3", "FIX-6"));
            File.WriteAllText(Path.Combine(wt, "docs", "dev", "OPEN-QUESTIONS.md"), Questions(1, 2, 9));

            CliRun r = CliRun.In(repo, repo.PathOf(Ledger.TasksPath), "next-ids", "--json");
            Assert.True(r.Code == 0, r.ToString());
            var json = (JsonObject)JsonNode.Parse(r.Stdout)!;
            Assert.Equal("FIX-7", (string)json["FIX"]!["next"]!);
            // 도구가 적는 경로는 '/' 구분이다(T-67 5번). 기대값은 OS 의 구분자로 만들지 않는다
            Assert.Equal("작업 트리 " + wt.Replace('\\', '/'), (string)json["FIX"]!["where"]![0]!);
            Assert.Equal("T-08", (string)json["T"]!["next"]!);
            Assert.Equal("task/X", (string)json["T"]!["where"]![0]!);
            Assert.Equal("Q-10", (string)json["Q"]!["next"]!);
            Assert.Equal("CCR-1", (string)json["CCR"]!["next"]!);

            CliRun text = CliRun.In(repo, repo.PathOf(Ledger.TasksPath), "next-ids");
            Assert.Contains("FIX 최댓값 FIX-6  다음 FIX-7", text.Stdout);

            // add 의 자동 번호도 FIX-7 이다(main 의 tasks.json 에는 FIX-3 뿐이다)
            JsonObject input = Fx.Task("FIX", group: "M0-W09", deps: new[] { "M0-BT-01" }, paths: new[] { "fix/new/" });
            input.Remove("status");
            repo.Write("in.json", input.ToJsonString());
            CliRun add = CliRun.In(repo, repo.PathOf(Ledger.TasksPath), "add", repo.PathOf("in.json"), "--no-vectors");
            Assert.True(add.Code == 0, add.ToString());
            Assert.Contains("FIX-7", add.Stdout);
            Assert.True(TaskDoc.Load(repo.PathOf(Ledger.TasksPath)).ById.ContainsKey("FIX-7"));
        }

        // Prepare 를 직접 부르고 최댓값을 넘기지 않으면 지금처럼 자기 문서만 본다
        [Fact]
        public void Prepare_WithoutUsedMax_LooksAtOwnDocumentOnly_WithUsedMax_TakesTheLarger()
        {
            string text = Graph("FIX-3");
            JsonObject input = Fx.Task("FIX", group: "M0-W09", deps: new[] { "M0-BT-01" }, paths: new[] { "fix/new/" });
            Assert.Equal("FIX-4", TaskAdder.Prepare(text, "t", input.ToJsonString(), Fx.SchemaPath, null).Id);
            var used = new System.Collections.Generic.Dictionary<string, int> { ["FIX"] = 6, ["CCR"] = 2 };
            Assert.Equal("FIX-7", TaskAdder.Prepare(text, "t", input.ToJsonString(), Fx.SchemaPath, null, null, used).Id);
            used["FIX"] = 1;
            Assert.Equal("FIX-4", TaskAdder.Prepare(text, "t", input.ToJsonString(), Fx.SchemaPath, null, null, used).Id);
        }
    }
}

using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Xunit;

namespace Wizard.TaskGraph.Tests
{
    // FIX-7, DECISIONS-TECH T-65 2·4·5번: 경로 겹침에서 개정 장부를 빼고, 겹침 규칙 둘은 둘 다 병합 전인 쌍에만 건다.
    // 작은 그래프(Fx)와 TempRepo 로만 쓴다(실제 그래프의 특정 작업 이름·개수에 기대지 않는다).
    public sealed class LedgerOverlapTests
    {
        private static readonly string[] AllLedgers = { Ledger.DecisionsPath, Ledger.QuestionsPath, Ledger.TasksPath };

        private static string[] Fails(JsonObject doc, MergeHistory? history = null)
        {
            return GraphValidator.Validate(Fx.Parse(doc), Fx.SchemaPath, null, history).Errors.ToArray();
        }

        // ---------- 장부 제외(2번) ----------

        [Fact]
        public void TwoUnmergedTasksOwningAllLedgers_PassWithoutDeps_SameOrDifferentGroup()
        {
            string[] a = AllLedgers.Concat(new[] { "tools/a/" }).ToArray();
            string[] b = AllLedgers.Concat(new[] { "tools/b/" }).ToArray();
            Assert.Empty(Fails(Fx.Doc(Fx.Task("M0-A-01", group: "G1", paths: a), Fx.Task("M0-B-01", group: "G2", paths: b))));
            Assert.Empty(Fails(Fx.Doc(Fx.Task("M0-A-01", group: "G1", paths: a), Fx.Task("M0-B-01", group: "G1", paths: b))));
        }

        [Fact]
        public void DocsDevFolder_AgainstLedgerFilesOnly_Passes()
        {
            Assert.Empty(Fails(Fx.Doc(
                Fx.Task("M0-A-01", group: "G1", paths: new[] { "docs/dev/" }),
                Fx.Task("M0-B-01", group: "G2", paths: AllLedgers))));
        }

        [Theory]
        [InlineData("docs/dev/", "docs/dev/CONTRACTS.md")]
        [InlineData("docs/dev/", "docs/dev/")]
        [InlineData("docs/dev/CONTRACTS.md", "docs/dev/CONTRACTS.md")]
        public void NonLedgerOverlap_StillFails_WithTheSameWording(string pa, string pb)
        {
            Assert.Equal(new[] { $"M0-A-01와 M0-B-01의 owned_paths가 겹치는데 deps로 순서가 정해져 있지 않다 — {pa} ↔ {pb}" },
                Fails(Fx.Doc(Fx.Task("M0-A-01", group: "G1", paths: new[] { pa, Ledger.TasksPath }), Fx.Task("M0-B-01", group: "G2", paths: new[] { Ledger.TasksPath, pb }))));
            Assert.Equal(new[] { $"묶음 G1: M0-A-01와 M0-B-01의 owned_paths가 겹친다 — {pa} ↔ {pb}" },
                Fails(Fx.Doc(Fx.Task("M0-A-01", group: "G1", paths: new[] { pa }), Fx.Task("M0-B-01", group: "G1", paths: new[] { pb }))));
        }

        [Fact]
        public void CardOwningOnlyLedgers_IsNotEmptyOwnedPaths()
        {
            string[] errors = Fails(Fx.Doc(Fx.Task("M0-A-01", group: "G1", paths: new[] { Ledger.TasksPath })));
            Assert.DoesNotContain(errors, e => e.Contains("owned_paths가 비어 있다", StringComparison.Ordinal));
            Assert.Empty(errors);
        }

        [Fact]
        public void Summary_CountsOverlapsAfterLedgerExclusion_AndUnmergedPairs()
        {
            JsonObject doc = Fx.Doc(
                Fx.Task("M0-A-01", group: "G1", paths: new[] { "src/x/", Ledger.TasksPath }),
                Fx.Task("M0-B-01", group: "G2", deps: new[] { "M0-A-01" }, paths: new[] { "src/x/y.cs", Ledger.TasksPath }),
                Fx.Task("M0-C-01", group: "G3", paths: new[] { Ledger.TasksPath }));
            ValidationReport r = GraphValidator.Validate(Fx.Parse(doc), Fx.SchemaPath, null, MergeHistory.Of("main", new[] { "M0-A-01" }));
            Assert.True(r.Ok, string.Join("\n", r.Errors));
            Assert.Contains("위상정렬: 됨 / 경로가 겹치는 작업 쌍 1개(장부 제외), 그 가운데 둘 다 병합 전인 쌍 0개(deps로 순서가 있어야 한다)", r.Summary);
            Assert.Contains("병합 기록: main의 커밋 제목에서 병합된 작업 1개", r.Summary);
        }

        // ---------- 병합 전인 쌍에만(4번), 집합을 직접 준다 ----------

        private static JsonObject Overlapping(string groupB, string[]? depsB = null)
        {
            return Fx.Doc(
                Fx.Task("M0-A-01", group: "G1", paths: new[] { "src/x/" }),
                Fx.Task("M0-B-01", group: groupB, deps: depsB, paths: new[] { "src/x/y.cs" }));
        }

        [Fact]
        public void OneMerged_OverlapWithoutDeps_Passes_DifferentAndSameGroup()
        {
            Assert.Empty(Fails(Overlapping("G2"), MergeHistory.Of("main", new[] { "M0-A-01" })));
            Assert.Empty(Fails(Overlapping("G1"), MergeHistory.Of("main", new[] { "M0-B-01" })));
            Assert.Empty(Fails(Overlapping("G1"), MergeHistory.Of("main", new[] { "M0-A-01", "M0-B-01" })));
        }

        [Fact]
        public void BothUnmerged_OverlapFails_WithTheSameWording()
        {
            Assert.Equal(new[] { "M0-A-01와 M0-B-01의 owned_paths가 겹치는데 deps로 순서가 정해져 있지 않다 — src/x/ ↔ src/x/y.cs" },
                Fails(Overlapping("G2"), MergeHistory.Of("main", new[] { "M0-Z-99" })));
            Assert.Equal(new[] { "묶음 G1: M0-A-01와 M0-B-01의 owned_paths가 겹친다 — src/x/ ↔ src/x/y.cs" },
                Fails(Overlapping("G1"), MergeHistory.NotRead()));
        }

        [Fact]
        public void SameGroupWithDeps_FailsEvenIfOneIsMerged()
        {
            JsonObject doc = Fx.Doc(
                Fx.Task("M0-A-01", group: "G1", paths: new[] { "src/a/" }),
                Fx.Task("M0-B-01", group: "G1", deps: new[] { "M0-A-01" }, paths: new[] { "src/b/" }));
            Assert.Equal(new[] { "묶음 G1: M0-A-01와 M0-B-01가 서로 의존한다(같은 묶음은 의존이 없어야 한다)" },
                Fails(doc, MergeHistory.Of("main", new[] { "M0-A-01" })));
        }

        // ---------- 기록을 읽는 길(Cli, TempRepo) ----------

        private static (TempRepo Repo, string Tasks) Repo(JsonObject doc, params string[] subjects)
        {
            TempRepo repo = TempRepo.Create();
            string tasks = repo.PathOf("docs/dev/tasks.json");
            repo.Write("docs/dev/tasks.json", Fx.Text(doc));
            foreach (string s in subjects)
            {
                repo.Commit(s);
            }
            return (repo, tasks);
        }

        private static CliRun Validate(TempRepo repo, string tasks, params string[] extra)
        {
            return CliRun.In(repo, tasks, new[] { "validate", "--no-vectors" }.Concat(extra).ToArray());
        }

        [Theory]
        [InlineData("G2")]
        [InlineData("G1")]
        public void Cli_MergedOnMain_OverlapPasses(string groupB)
        {
            var (repo, tasks) = Repo(Overlapping(groupB), "feat(M0-A-01): 병합");
            using (repo)
            {
                CliRun r = Validate(repo, tasks);
                Assert.True(r.Code == 0, r.ToString());
                Assert.Contains("병합 기록: main의 커밋 제목에서 병합된 작업 1개", r.Stdout);
                Assert.Contains("그 가운데 둘 다 병합 전인 쌍 0개", r.Stdout);
            }
        }

        [Fact]
        public void Cli_BothUnmerged_Fails_IgnoringForeignAndMalformedSubjects()
        {
            var (repo, tasks) = Repo(Overlapping("G2"), "fix(NOPE-1): 그래프에 없는 id", "chore: 형식이 다르다", "feat(M0-A-01) 콜론이 붙지 않았다");
            using (repo)
            {
                CliRun r = Validate(repo, tasks);
                Assert.Equal(1, r.Code);
                Assert.Contains("FAIL  M0-A-01와 M0-B-01의 owned_paths가 겹치는데 deps로 순서가 정해져 있지 않다 — src/x/ ↔ src/x/y.cs", r.Stdout);
                Assert.Contains("병합 기록: main의 커밋 제목에서 병합된 작업 0개", r.Stdout);
            }
        }

        [Fact]
        public void Cli_BothMerged_Passes()
        {
            var (repo, tasks) = Repo(Overlapping("G1"), "feat(M0-A-01): 하나", "test(M0-B-01): 둘");
            using (repo)
            {
                CliRun r = Validate(repo, tasks);
                Assert.True(r.Code == 0, r.ToString());
                Assert.Contains("병합된 작업 2개", r.Stdout);
            }
        }

        [Fact]
        public void Cli_NoHistory_IsStrict_AndSaysSo()
        {
            var (repo, tasks) = Repo(Overlapping("G2"), "feat(M0-A-01): 병합");
            using (repo)
            {
                CliRun r = Validate(repo, tasks, "--no-history");
                Assert.Equal(1, r.Code);
                Assert.Contains("M0-A-01와 M0-B-01의 owned_paths가 겹치는데", r.Stdout);
                Assert.Contains("병합 기록: 보지 않았다(--no-history). 전부 병합 전으로 본다", r.Stdout);
            }
        }

        // git 저장소인 TempRepo 에 없는 <base> 를 준다(git 이 아닌 폴더에서는 위쪽의 실제 저장소를 읽으므로 쓰지 않는다)
        [Fact]
        public void Cli_MissingBase_IsStrict_AndSaysSo()
        {
            var (repo, tasks) = Repo(Overlapping("G2"), "feat(M0-A-01): 병합");
            using (repo)
            {
                CliRun r = Validate(repo, tasks, "--base", "no-such-ref");
                Assert.Equal(1, r.Code);
                Assert.Contains("M0-A-01와 M0-B-01의 owned_paths가 겹치는데", r.Stdout);
                Assert.Contains("병합 기록: no-such-ref을 읽지 못했다. 전부 병합 전으로 본다", r.Stdout);
            }
        }

        [Fact]
        public void Cli_SameGroupDeps_FailsEvenIfOneMerged()
        {
            JsonObject doc = Fx.Doc(
                Fx.Task("M0-A-01", group: "G1", paths: new[] { "src/a/" }),
                Fx.Task("M0-B-01", group: "G1", deps: new[] { "M0-A-01" }, paths: new[] { "src/b/" }));
            var (repo, tasks) = Repo(doc, "feat(M0-A-01): 병합");
            using (repo)
            {
                CliRun r = Validate(repo, tasks);
                Assert.Equal(1, r.Code);
                Assert.Contains("묶음 G1: M0-A-01와 M0-B-01가 서로 의존한다", r.Stdout);
            }
        }

        // add 도 같은 길로 기록을 읽는다: main 에 병합된 작업과 겹치는 FIX 를 deps 없이 더할 수 있다
        [Fact]
        public void Cli_Add_UsesHistory()
        {
            var (repo, tasks) = Repo(Fx.Doc(Fx.Task("M0-A-01", group: "G1", paths: new[] { "src/x/" })), "feat(M0-A-01): 병합");
            using (repo)
            {
                JsonObject t = Fx.Task("FIX", group: "G9", paths: new[] { "src/x/fix.cs" });
                t.Remove("status");
                repo.Write("in.json", t.ToJsonString());
                CliRun strict = CliRun.In(repo, tasks, "add", repo.PathOf("in.json"), "--no-vectors", "--no-history");
                Assert.Equal(1, strict.Code);
                CliRun r = CliRun.In(repo, tasks, "add", repo.PathOf("in.json"), "--no-vectors");
                Assert.True(r.Code == 0, r.ToString());
                Assert.True(TaskDoc.Load(tasks).ById.ContainsKey("FIX-1"));
            }
        }
    }
}

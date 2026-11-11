using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Xunit;

namespace Wizard.TaskGraph.Tests
{
    // 수락 1·7: validate 가 스키마, deps, 순환, 경로 겹침과 deps 순서, 묶음 안 겹침, 벡터, 마일스톤 집계, 모델을 본다.
    public sealed class ValidateTests
    {
        private static void AssertFails(ValidationReport r, string fragment)
        {
            Assert.False(r.Ok, "통과하면 안 된다");
            Assert.True(r.Errors.Any(e => e.Contains(fragment, System.StringComparison.Ordinal)),
                $"'{fragment}' 가 든 진단이 없다. 나온 것:\n{string.Join("\n", r.Errors)}");
        }

        [Fact]
        public void RealTasksJson_PassesSchemaGraphAndVectors()
        {
            TaskDoc doc = TaskDoc.Load(Fx.RealTasksPath);
            ValidationReport r = GraphValidator.Validate(doc, Fx.SchemaPath, Fx.RealSpecsDir);
            Assert.True(r.Ok, string.Join("\n", r.Errors));
            Assert.Contains("스키마: 통과", r.Summary);
            Assert.Contains(r.Summary, s => s.StartsWith("벡터: 명세 벡터 ", System.StringComparison.Ordinal) && s.EndsWith("미배정 0개", System.StringComparison.Ordinal));
        }

        [Fact]
        public void ValidFixture_Passes()
        {
            ValidationReport r = Fx.Validate(Fx.Doc(
                Fx.Task("M0-BT-01", paths: new[] { "tools/" }),
                Fx.Task("M0-BT-02", group: "M0-W02", deps: new[] { "M0-BT-01" }, paths: new[] { "tools/Wizard.A/" }),
                Fx.Task("M0-BT-03", group: "M0-W03", deps: new[] { "M0-BT-02" }, paths: new[] { "tools/Wizard.A/x.cs" })));
            Assert.True(r.Ok, string.Join("\n", r.Errors));
        }

        [Fact]
        public void MissingDep_Fails()
        {
            AssertFails(Fx.Validate(Fx.Doc(Fx.Task("M0-BT-01", deps: new[] { "M0-BT-09" }))), "M0-BT-01: deps의 'M0-BT-09'가 없다");
        }

        [Fact]
        public void SelfDep_Fails()
        {
            AssertFails(Fx.Validate(Fx.Doc(Fx.Task("M0-BT-01", deps: new[] { "M0-BT-01" }))), "M0-BT-01: 자기 자신에 의존한다");
        }

        [Fact]
        public void Cycle_Fails()
        {
            ValidationReport r = Fx.Validate(Fx.Doc(
                Fx.Task("M0-BT-01", group: "M0-W01", deps: new[] { "M0-BT-03" }),
                Fx.Task("M0-BT-02", group: "M0-W02", deps: new[] { "M0-BT-01" }),
                Fx.Task("M0-BT-03", group: "M0-W03", deps: new[] { "M0-BT-02" })));
            AssertFails(r, "deps에 순환이 있다: M0-BT-01, M0-BT-02, M0-BT-03");
            Assert.Contains("위상정렬: 순환 있음", r.Summary.Single(s => s.StartsWith("위상정렬", System.StringComparison.Ordinal)));
        }

        [Fact]
        public void OverlappingPathsWithoutDeps_Fail()
        {
            AssertFails(Fx.Validate(Fx.Doc(
                Fx.Task("M0-BT-01", group: "M0-W01", paths: new[] { "src/*/Generated/" }),
                Fx.Task("M0-BT-02", group: "M0-W02", paths: new[] { "src/Wizard.Core/Generated/SimVersion.g.cs" }))),
                "M0-BT-01와 M0-BT-02의 owned_paths가 겹치는데 deps로 순서가 정해져 있지 않다 — src/*/Generated/ ↔ src/Wizard.Core/Generated/SimVersion.g.cs");
        }

        [Fact]
        public void OverlappingPathsOrderedTransitively_Pass()
        {
            ValidationReport r = Fx.Validate(Fx.Doc(
                Fx.Task("M0-BT-01", group: "M0-W01", paths: new[] { "tools/*/*.csproj" }),
                Fx.Task("M0-BT-02", group: "M0-W02", deps: new[] { "M0-BT-01" }, paths: new[] { "docs/a.md" }),
                Fx.Task("M0-BT-03", group: "M0-W03", deps: new[] { "M0-BT-02" }, paths: new[] { "tools/Wizard.TaskGraph/" })));
            Assert.True(r.Ok, string.Join("\n", r.Errors));
        }

        [Fact]
        public void SameGroupOverlap_Fails()
        {
            AssertFails(Fx.Validate(Fx.Doc(
                Fx.Task("M0-BT-01", group: "M0-W03", paths: new[] { "tools/ci/" }),
                Fx.Task("M0-BT-02", group: "M0-W03", paths: new[] { "tools/ci/gen-check.sh" }))),
                "묶음 M0-W03: M0-BT-01와 M0-BT-02의 owned_paths가 겹친다 — tools/ci/ ↔ tools/ci/gen-check.sh");
        }

        [Fact]
        public void SameGroupDependency_Fails()
        {
            AssertFails(Fx.Validate(Fx.Doc(
                Fx.Task("M0-BT-01", group: "M0-W03"),
                Fx.Task("M0-BT-02", group: "M0-W03", deps: new[] { "M0-BT-01" }))),
                "묶음 M0-W03: M0-BT-01와 M0-BT-02가 서로 의존한다");
        }

        [Fact]
        public void OwnerModelNotAgentDefault_Fails()
        {
            AssertFails(Fx.Validate(Fx.Doc(Fx.Task("M1-CONT-01", milestone: "M1", group: "M1-W01", agent: "content-data-engineer", model: "opus"))),
                "M1-CONT-01: owner_model 'opus' ≠ content-data-engineer의 default_model 'sonnet'");
        }

        [Fact]
        public void UnknownAgent_Fails()
        {
            AssertFails(Fx.Validate(Fx.Doc(Fx.Task("M0-BT-01", agent: "nobody-engineer"))), "M0-BT-01: owner_agent 'nobody-engineer'가 agents에 없다");
        }

        [Fact]
        public void MilestoneAggregates_Fail()
        {
            JsonObject doc = Fx.Doc(Fx.Task("M0-BT-01"), Fx.Task("V-01", milestone: "V", group: "V-assets", status: "blocked-on-assets"));
            JsonObject m0 = (JsonObject)doc["milestones"]![0]!;
            JsonObject v = (JsonObject)doc["milestones"]![1]!;
            m0["task_count"] = 5;
            m0["parallel_groups"] = Fx.Arr(new[] { "M0-W01", "M0-W09" });
            v["blocked_on_assets"] = 0;
            ValidationReport r = Fx.Validate(doc);
            AssertFails(r, "milestone M0: task_count 5 ≠ 계산값 1");
            AssertFails(r, "milestone M0: parallel_groups가 tasks와 다르다 — 있어야 할 것 [M0-W01], 적힌 것 [M0-W01, M0-W09]");
            AssertFails(r, "milestone V: blocked_on_assets 0 ≠ 계산값 1");
        }

        [Fact]
        public void GateOfAnotherMilestone_Fails()
        {
            JsonObject doc = Fx.Doc(Fx.Task("M0-INT"), Fx.Task("M1-INT", milestone: "M1", group: "M1-W01", deps: new[] { "M0-INT" }));
            ((JsonObject)doc["milestones"]![0]!)["gate"] = "M1-INT";
            AssertFails(Fx.Validate(doc), "milestone M0: gate 'M1-INT'의 milestone이 다르다");
        }

        [Fact]
        public void MilestoneMissing_Fails()
        {
            JsonObject doc = Fx.Doc(Fx.Task("M0-BT-01"));
            ((JsonObject)doc["tasks"]![0]!)["milestone"] = "M7";
            AssertFails(Fx.Validate(doc), "M0-BT-01: milestone 'M7'가 milestones에 없다");
        }

        [Fact]
        public void Schema_ProgressStatusInTasksJson_Fails()
        {
            ValidationReport r = Fx.Validate(Fx.Doc(Fx.Task("M0-BT-01", status: "done")));
            AssertFails(r, "M0-BT-01: status 'done' — tasks.json에는 todo 또는 blocked-on-assets만 둔다");
            AssertFails(r, "스키마: /tasks/0/status");
        }

        [Fact]
        public void Schema_UnknownField_Fails()
        {
            JsonObject doc = Fx.Doc(Fx.Task("M0-BT-01"));
            ((JsonObject)doc["tasks"]![0]!)["human_checks"] = "오타";
            AssertFails(Fx.Validate(doc), "스키마: /tasks/0");
        }

        [Fact]
        public void Schema_BadOwnedPath_Fails()
        {
            foreach (string bad in new[] { "../outside/", "/abs/path", "src/**/x.cs", "a//b", "a\\b", "src/./x" })
            {
                AssertFails(Fx.Validate(Fx.Doc(Fx.Task("M0-BT-01", paths: new[] { bad }))), "스키마: /tasks/0/owned_paths/0");
            }
        }

        [Fact]
        public void MissingFieldAndWrongType_Fail()
        {
            JsonObject doc = Fx.Doc(Fx.Task("M0-BT-01"));
            JsonObject t = (JsonObject)doc["tasks"]![0]!;
            t.Remove("acceptance");
            t["deps"] = "M0-BT-00";
            ValidationReport r = Fx.Validate(doc);
            AssertFails(r, "M0-BT-01: 필드 'acceptance'가 없다");
            AssertFails(r, "M0-BT-01: 'deps'는 배열이어야 한다");
        }

        [Fact]
        public void EmptyOwnedPathsAndAcceptance_Fail()
        {
            JsonObject doc = Fx.Doc(Fx.Task("M0-BT-01"));
            JsonObject t = (JsonObject)doc["tasks"]![0]!;
            t["owned_paths"] = new JsonArray();
            t["acceptance"] = new JsonArray();
            ValidationReport r = Fx.Validate(doc);
            AssertFails(r, "M0-BT-01: owned_paths가 비어 있다");
            AssertFails(r, "M0-BT-01: acceptance가 비어 있다");
        }

        [Fact]
        public void DuplicateId_Fails()
        {
            AssertFails(Fx.Validate(Fx.Doc(Fx.Task("M0-BT-01"), Fx.Task("M0-BT-01", group: "M0-W02", paths: new[] { "x/" }))), "M0-BT-01: id가 겹친다");
        }

        [Fact]
        public void VagueVectorSubsetInAcceptance_Fails()
        {
            AssertFails(Fx.Validate(Fx.Doc(Fx.Task("M0-BT-01", acceptance: "7.3절 중 첫 다섯 벡터를 통과한다"))),
                "M0-BT-01: acceptance가 벡터의 부분집합을 말로 가리킨다");
        }

        // ---------- 벡터 ----------

        private static string SpecsDir(TempRepo dir)
        {
            dir.Write("spec/01-core.md", string.Join("\n",
                "# 명세 01", "", "## 3. 규칙", "### 3.1 x", "| TV-999 | 규칙 절의 표는 벡터가 아니다 |", "",
                "## 9. 테스트 벡터", "",
                "### 9.1 기본", "| ID | 입력 | 기대 |", "|---|---|---|", "| TV-001 | a | b |", "| `TV-002` | a | b |", "- **TV-003** 굵은 글씨", "",
                "### 9.2 번호", "1. given a", "2. given b", "3. given c", ""));
            dir.Write("spec/README.md", "| TV-500 | 번호 없는 파일은 보지 않는다 |\n");
            return dir.PathOf("spec");
        }

        [Fact]
        public void Vectors_AllAssigned_Pass()
        {
            using var dir = TempRepo.Create(git: false);
            ValidationReport r = Fx.Validate(Fx.Doc(
                Fx.Task("M0-BT-01", group: "M0-W01", vectors: new[] { "S01§9.1" }),
                Fx.Task("M0-BT-02", group: "M0-W02", paths: new[] { "b/" }, vectors: new[] { "S01:N1..N2", "S01:N3" })), SpecsDir(dir));
            Assert.True(r.Ok, string.Join("\n", r.Errors));
            Assert.Contains("벡터: 명세 벡터 6개, 배정 6개, 미배정 0개", r.Summary);
        }

        [Fact]
        public void Vectors_Unassigned_Fail()
        {
            using var dir = TempRepo.Create(git: false);
            AssertFails(Fx.Validate(Fx.Doc(Fx.Task("M0-BT-01", vectors: new[] { "S01§9.1", "S01:N2" })), SpecsDir(dir)),
                "미배정 벡터 S01§9.2: N1, N3");
        }

        [Fact]
        public void Vectors_DoubleAssigned_Fail()
        {
            using var dir = TempRepo.Create(git: false);
            AssertFails(Fx.Validate(Fx.Doc(
                Fx.Task("M0-BT-01", group: "M0-W01", vectors: new[] { "S01§9.1", "S01§9.2" }),
                Fx.Task("M0-BT-02", group: "M0-W02", paths: new[] { "b/" }, vectors: new[] { "S01:TV-002" })), SpecsDir(dir)),
                "벡터 S01:TV-002(§9.1)가 두 작업에 배정되어 있다: M0-BT-01, M0-BT-02");
        }

        [Fact]
        public void Vectors_SameTaskTwice_Fail()
        {
            using var dir = TempRepo.Create(git: false);
            AssertFails(Fx.Validate(Fx.Doc(Fx.Task("M0-BT-01", vectors: new[] { "S01§9.1", "S01§9.2", "S01:TV-001" })), SpecsDir(dir)),
                "M0-BT-01: 벡터 S01:TV-001를 두 번 가리킨다");
        }

        [Fact]
        public void Vectors_BadSelectors_Fail()
        {
            using var dir = TempRepo.Create(git: false);
            ValidationReport r = Fx.Validate(Fx.Doc(Fx.Task("M0-BT-01", vectors: new[]
            {
                "S01§9.1", "S01§9.2", "S1:TV-001", "S01§9.7", "S01:TV-404", "S01:N3..N1", "S01:TV-003..N1",
            })), SpecsDir(dir));
            AssertFails(r, "M0-BT-01: vectors의 'S1:TV-001' — 꼴이 틀렸다");
            AssertFails(r, "M0-BT-01: 'S01§9.7' — 명세 01에 그런 벡터 절이 없다");
            AssertFails(r, "M0-BT-01: 'S01:TV-404' — 명세 01에 그런 벡터 id가 없다");
            AssertFails(r, "M0-BT-01: 'S01:N3..N1' — 범위가 거꾸로다");
            AssertFails(r, "M0-BT-01: 'S01:TV-003..N1' — 범위가 절을 넘는다(§9.1 → §9.2)");
        }

        [Fact]
        public void Vectors_DuplicateIdInSpec_Fail()
        {
            using var dir = TempRepo.Create(git: false);
            string specs = SpecsDir(dir);
            File.AppendAllText(Path.Combine(specs, "01-core.md"), "### 9.3 겹침\n| TV-001 | 다시 |\n");
            AssertFails(Fx.Validate(Fx.Doc(Fx.Task("M0-BT-01", vectors: new[] { "S01§9.1", "S01§9.2" })), specs),
                "명세 01: 벡터 id 'TV-001'가 두 번 나온다(§9.3)");
        }

        [Fact]
        public void Vectors_MissingSpecDir_Fails()
        {
            AssertFails(Fx.Validate(Fx.Doc(Fx.Task("M0-BT-01")), "/nonexistent/spec"), "명세 폴더가 없다: /nonexistent/spec");
        }

        [Fact]
        public void Cli_Validate_ExitCodes()
        {
            using var repo = TempRepo.Create();
            repo.Write("docs/dev/tasks.json", Fx.Text(Fx.Doc(Fx.Task("M0-BT-01"))));
            CliRun ok = CliRun.In(repo, repo.PathOf("docs/dev/tasks.json"), "validate", "--no-vectors");
            Assert.True(ok.Code == 0, ok.ToString());
            Assert.Contains("통과", ok.Stdout);

            repo.Write("docs/dev/tasks.json", Fx.Text(Fx.Doc(Fx.Task("M0-BT-01", deps: new[] { "M0-BT-02" }))));
            CliRun bad = CliRun.In(repo, repo.PathOf("docs/dev/tasks.json"), "validate", "--no-vectors");
            Assert.Equal(1, bad.Code);
            Assert.Contains("FAIL  M0-BT-01: deps의 'M0-BT-02'가 없다", bad.Stdout);

            repo.Write("docs/dev/tasks.json", "{ not json");
            CliRun broken = CliRun.In(repo, repo.PathOf("docs/dev/tasks.json"), "validate");
            Assert.Equal(1, broken.Code);
            Assert.Contains("JSON 이 아니다", broken.Stderr);
        }
    }
}

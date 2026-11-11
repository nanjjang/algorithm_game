using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Xunit;

namespace Wizard.Ci.Tests
{
    // pre-commit 의 부분 검사: 진단 끄기 금지, 이름 표 형식, 경로별 스크립트 선택. 그리고 게이트 파일 합치기
    public sealed class PreCommitPartsTests
    {
        [Theory]
        [InlineData(".editorconfig", "[*.cs]\ndotnet_diagnostic.WZ0001.severity = none\n")]
        [InlineData("src/x.globalconfig", "dotnet_diagnostic.WZ0003.severity = warning\n")]
        [InlineData("src/Wizard.Sim/Wizard.Sim.csproj", "<Project><PropertyGroup><NoWarn>CS1591;WZ0004</NoWarn></PropertyGroup></Project>")]
        [InlineData("Directory.Build.props", "<Project><PropertyGroup><WarningsNotAsErrors>WZ0001</WarningsNotAsErrors></PropertyGroup></Project>")]
        [InlineData("src/Wizard.Sim/A.cs", "#pragma warning disable WZ0001\nclass A {}\n")]
        [InlineData("src/Wizard.Sim/B.cs", "[System.Diagnostics.CodeAnalysis.SuppressMessage(\"Det\", \"WZ0002\")]\nclass B {}\n")]
        [InlineData("src/Wizard.Core/csc.rsp", "-nowarn:CS0168\n")]
        [InlineData("tests/Wizard.TestKit/Shared/csc.rsp", "-warnaserror-\n")]
        public void Suppression_IsRejected(string path, string content)
        {
            Assert.Single(PreCommit.SuppressionProblems(path, () => content));
        }

        [Theory]
        [InlineData(".editorconfig", "[*.cs]\nindent_size = 4\ndotnet_diagnostic.CA1000.severity = none\n")]
        [InlineData("src/Wizard.Sim/Wizard.Sim.csproj", "<Project><PropertyGroup><NoWarn>CS1591</NoWarn></PropertyGroup></Project>")]
        [InlineData("src/Wizard.Sim/A.cs", "#pragma warning disable CS0168\nclass A {}\n")]
        [InlineData("src/Wizard.Core/csc.rsp", "-warnaserror+\n")]
        [InlineData("unity/Assets/Game/csc.rsp", "-nowarn:CS0649\n")]
        public void NonSuppression_IsAllowed(string path, string content)
        {
            Assert.Empty(PreCommit.SuppressionProblems(path, () => content));
        }

        [Fact]
        public void ScriptChecks_AreChosenByTouchedPaths()
        {
            Assert.Empty(PreCommit.ScriptsFor(new[] { "README.md", "server/Wizard.Server.Api/A.cs", "art/x.png" }));
            Assert.Equal(new[] { "tools/ci/lib/meta-check.sh" }, PreCommit.ScriptsFor(new[] { "src/Wizard.Sim/A.cs" }).ToArray());
            Assert.Equal(new[] { "tools/ci/lib/meta-check.sh" }, PreCommit.ScriptsFor(new[] { "unity/Assets/Game/A.cs" }).ToArray());
            Assert.Equal(new[] { "tools/ci/tasks-check.sh" }, PreCommit.ScriptsFor(new[] { "docs/dev/tasks.json" }).ToArray());
            Assert.Equal(new[] { "tools/ci/tasks-check.sh" }, PreCommit.ScriptsFor(new[] { "docs/spec/01-sim-core.md" }).ToArray());
            Assert.Equal(new[] { "tools/ci/agent-config-check.sh" }, PreCommit.ScriptsFor(new[] { ".claude/agents/x.md" }).ToArray());
            Assert.Equal(new[] { "tools/ci/layout-check.sh" }, PreCommit.ScriptsFor(new[] { "tools/Wizard.Ci/Wizard.Ci.csproj" }).ToArray());
            Assert.Equal(new[] { "tools/ci/lib/meta-check.sh", "tools/ci/tasks-check.sh", "tools/ci/agent-config-check.sh", "tools/ci/layout-check.sh" },
                PreCommit.ScriptsFor(new[] { "CLAUDE.md", "src/Wizard.Core/package.json", "docs/dev/tasks.schema.json" }).ToArray());
        }

        private const string GoodNames = "{\n  \"char.1\": \"제자 1\",\n  \"char.2\": \"제자 2\",\n  \"ui.ok\": \"확인\",\n  \"~end\": \"\"\n}\n";

        [Fact]
        public void NamesTable_WellFormed_Passes()
        {
            Assert.Empty(NamesFormat.Check(GoodNames));
            Assert.Empty(NamesFormat.Check("{\n  \"~end\": \"\"\n}\n"));
            Assert.Empty(NamesFormat.Check("{\n  \"a.\\\"q\\\"\": \"따옴표 \\\" 값\",\n  \"~end\": \"\"\n}\n"));
        }

        [Theory]
        [InlineData("{\n  \"char.2\": \"b\",\n  \"char.1\": \"a\",\n  \"~end\": \"\"\n}\n", "오름차순")]
        [InlineData("{\n  \"char.1\": \"a\",\n  \"char.1\": \"b\",\n  \"~end\": \"\"\n}\n", "두 번")]
        [InlineData("{\n  \"char.1\": \"a\",\n  \"char.2\": \"b\"\n}\n", "~end")]
        [InlineData("{\n  \"char.1\": \"a\",\n  \"~end\": \"x\"\n}\n", "~end")]
        [InlineData("{\n  \"char.1\": \"a\", \"char.2\": \"b\",\n  \"~end\": \"\"\n}\n", "한 줄에")]
        [InlineData("{ \"char.1\": \"a\", \"~end\": \"\" }\n", "첫 줄")]
        [InlineData("{\n  \"~end\": \"\",\n  \"z\": \"a\"\n}\n", "마지막")]
        [InlineData("{\n  \"char.1\": \"a\",\n", "JSON")]
        public void NamesTable_FormatErrors_AreCaught(string text, string expected)
        {
            List<string> p = NamesFormat.Check(text);
            Assert.Contains(p, x => x.Contains(expected, StringComparison.Ordinal));
        }

        [Fact]
        public void GateFile_FirstWrite_HasPendingHumanChecks_AndNoMachinePaths()
        {
            var stages = new List<StageResult>
            {
                new StageResult("fast", DateTime.UtcNow, DateTime.UtcNow, Array.Empty<StepResult>()),
                new StageResult("full", DateTime.UtcNow, DateTime.UtcNow, Array.Empty<StepResult>()),
                new StageResult("integration", DateTime.UtcNow, DateTime.UtcNow, Array.Empty<StepResult>()),
            };
            var hcs = new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>("M1-A", "사람이 본다") };
            JsonNode doc = JsonNode.Parse(GateFile.Merge(null, "M1", stages, "/machine/path", "main", "abc", hcs))!;
            Assert.True((bool)doc["ci"]!["passed"]!);
            Assert.Equal(GateFile.Pending, (string)doc["human_checks"]![0]!["status"]!);
            Assert.DoesNotContain("/machine/path", doc.ToJsonString(), StringComparison.Ordinal);

            // 단계가 모자라면(예: 중간에 멈춤) 통과가 아니다
            JsonNode partial = JsonNode.Parse(GateFile.Merge(null, "M1", stages.Take(2).ToList(), "/r", null, null, hcs))!;
            Assert.False((bool)partial["ci"]!["passed"]!);
        }

        [Fact]
        public void GateFile_KeepsHumanRecordsThatLeftTasksJson_AndRejectsNonObjectFiles()
        {
            string existing = "{\"human_checks\":[{\"task\":\"OLD\",\"check\":\"예전 항목\",\"status\":\"확인함\"}]}";
            JsonNode doc = JsonNode.Parse(GateFile.Merge(existing, "M1", Array.Empty<StageResult>(), "/r", null, null,
                new List<KeyValuePair<string, string>>()))!;
            Assert.Equal("OLD", (string)doc["human_checks"]![0]!["task"]!);
            Assert.Throws<CiException>(() => GateFile.Merge("[1,2]", "M1", Array.Empty<StageResult>(), "/r", null, null, new List<KeyValuePair<string, string>>()));
            Assert.Throws<CiException>(() => GateFile.Merge("{", "M1", Array.Empty<StageResult>(), "/r", null, null, new List<KeyValuePair<string, string>>()));
        }

        [Fact]
        public void GateFile_HumanChecksComeFromTheMilestonesTasks()
        {
            string tasks = File.ReadAllText(Path.Combine(TempGit.RepoRoot, "docs", "dev", "tasks.json"));
            var m1 = GateFile.HumanChecks(tasks, "M1");
            Assert.Contains(m1, kv => kv.Key == "M1-UE-03");
            Assert.All(m1, kv => Assert.StartsWith("M1-", kv.Key, StringComparison.Ordinal));
            Assert.Throws<CiException>(() => GateFile.HumanChecks(tasks, "M99"));
        }
    }
}

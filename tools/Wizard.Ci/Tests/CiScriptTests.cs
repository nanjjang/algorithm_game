using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json.Nodes;
using Xunit;

namespace Wizard.Ci.Tests
{
    // 진짜 tools/ci/ci.sh 를 임시 저장소에 복사해 돌린다(실행기는 이 테스트 어셈블리. WIZARD_CI_RUNNER).
    public sealed class CiScriptTests
    {
        // 자기 위치와 받은 WIZARD_ROOT 를 적는 가짜 검사
        private const string Probe =
            "#!/bin/bash\n# ci-stage: fast\nset -u\nhere=\"$(cd \"${0%/*}\" && pwd -P)\"\n" +
            "/bin/mkdir -p \"$here/../../artifacts\"\necho \"root=$WIZARD_ROOT stage=$WIZARD_CI_STAGE force=$WIZARD_CI_FORCE_ALL\" > \"$here/../../artifacts/probe.txt\"\n" +
            "echo probe-ran\n";

        private static JsonNode ReadJson(string path) => JsonNode.Parse(File.ReadAllText(path))!;

        [Fact]
        public void CiShOfAWorktree_CalledFromTheMainCheckout_ChecksTheWorktree()
        {
            using TempGit g = TempGit.Create(withCiScripts: true);
            g.Write("tools/ci/a-probe.sh", Probe);
            g.Commit("test: probe");
            string wt = g.PathOf(".claude/worktrees/T1");
            g.Git("worktree", "add", "-q", wt, "-b", "task/T1");

            // 메인 체크아웃을 현재 폴더로 두고 작업 트리의 ci.sh 를 절대 경로로 부른다
            ProcResult r = TempGit.RunScript(Path.Combine(wt, "tools", "ci", "ci.sh"), g.Dir, null, "fast");
            Assert.True(r.ExitCode == 0, r.Stdout + r.Stderr);
            Assert.Contains("probe-ran", r.Stdout, StringComparison.Ordinal);

            Assert.Equal("root=" + wt + " stage=fast force=0", File.ReadAllText(Path.Combine(wt, "artifacts", "probe.txt")).Trim());
            Assert.False(File.Exists(g.PathOf("artifacts/probe.txt")), "the main checkout was checked instead of the worktree");
            Assert.False(File.Exists(g.PathOf("artifacts/ci/fast.json")));

            JsonNode fast = ReadJson(Path.Combine(wt, "artifacts", "ci", "fast.json"));
            Assert.True((bool)fast["passed"]!);
            Assert.Equal(wt, (string)fast["root"]!);
            Assert.Equal("task/T1", (string)fast["branch"]!);
            Assert.Equal("a-probe", (string)fast["steps"]![0]!["name"]!);
            Assert.True(File.Exists(Path.Combine(wt, "artifacts", "ci", "fast", "a-probe.json")));
            Assert.Contains("probe-ran", File.ReadAllText(Path.Combine(wt, "artifacts", "ci", "fast", "a-probe.log")), StringComparison.Ordinal);
        }

        [Fact]
        public void Full_RunsFastThenFull_AndWritesASummaryPerStage_ContinuingAfterAFailure()
        {
            using TempGit g = TempGit.Create(withCiScripts: true);
            g.Write("tools/ci/a.sh", "#!/bin/bash\n# ci-stage: fast\necho a; exit 3\n");
            g.Write("tools/ci/b.sh", "#!/bin/bash\n# ci-stage: fast\necho b\n");
            g.Write("tools/ci/lib/c.sh", "#!/bin/bash\n# ci-stage: full\necho c\n");
            g.Write("tools/ci/d.sh", "#!/bin/bash\n# ci-stage: integration\necho d\n");
            ProcResult r = TempGit.RunScript(g.PathOf("tools/ci/ci.sh"), "/", null, "full");
            Assert.Equal(1, r.ExitCode);
            JsonNode fast = ReadJson(g.PathOf("artifacts/ci/fast.json"));
            JsonNode full = ReadJson(g.PathOf("artifacts/ci/full.json"));
            Assert.False((bool)fast["passed"]!);
            Assert.Equal(1, (int)fast["failed_steps"]!);
            Assert.Equal(1, (int)fast["passed_steps"]!);
            Assert.Equal(3, (int)fast["steps"]![0]!["exit_code"]!);
            Assert.True((bool)full["passed"]!);
            Assert.Equal("c", (string)full["steps"]![0]!["name"]!);
            Assert.False(File.Exists(g.PathOf("artifacts/ci/integration.json")));
            Assert.Contains("FAIL  [fast] a", r.Stdout, StringComparison.Ordinal);
        }

        [Fact]
        public void HungStep_IsCutAtItsTimeout_AndRecordedAsTimedOut()
        {
            using TempGit g = TempGit.Create(withCiScripts: true);
            g.Write("tools/ci/hang.sh", "#!/bin/bash\n# ci-stage: fast\n# ci-timeout: 2\necho start\n/bin/sleep 600\n");
            g.Write("tools/ci/next.sh", "#!/bin/bash\n# ci-stage: fast\necho next-ran\n");
            var sw = Stopwatch.StartNew();
            ProcResult r = TempGit.RunScript(g.PathOf("tools/ci/ci.sh"), g.Dir, null, "fast");
            sw.Stop();
            Assert.Equal(1, r.ExitCode);
            Assert.True(sw.Elapsed < TimeSpan.FromSeconds(60), "took " + sw.Elapsed);
            Assert.Contains("next-ran", r.Stdout, StringComparison.Ordinal);
            JsonNode step = ReadJson(g.PathOf("artifacts/ci/fast/hang.json"));
            Assert.True((bool)step["timed_out"]!);
            Assert.Equal(ProcessRunner.TimeoutExitCode, (int)step["exit_code"]!);
            Assert.Contains("start", step["tail"]!.ToJsonString(), StringComparison.Ordinal);
        }

        [Fact]
        public void SkipNote_IsRecordedAsSkipped_ButOnlyWhenTheStepExitsZero()
        {
            using TempGit g = TempGit.Create(withCiScripts: true);
            g.Write("tools/ci/s1.sh", "#!/bin/bash\n# ci-stage: fast\necho 'skipped: 닿지 않는다' >> \"$WIZARD_CI_NOTE\"\necho 'plain note' >> \"$WIZARD_CI_NOTE\"\n");
            g.Write("tools/ci/s2.sh", "#!/bin/bash\n# ci-stage: fast\necho 'skipped: 거짓' >> \"$WIZARD_CI_NOTE\"\nexit 1\n");
            ProcResult r = TempGit.RunScript(g.PathOf("tools/ci/ci.sh"), g.Dir, null, "fast");
            Assert.Equal(1, r.ExitCode);
            JsonNode s1 = ReadJson(g.PathOf("artifacts/ci/fast/s1.json"));
            JsonNode s2 = ReadJson(g.PathOf("artifacts/ci/fast/s2.json"));
            Assert.True((bool)s1["skipped"]!);
            Assert.Equal("닿지 않는다", (string)s1["skip_reason"]!);
            Assert.Equal("plain note", (string)s1["notes"]![0]!);
            Assert.False((bool)s2["skipped"]!);
            Assert.False((bool)s2["passed"]!);
            Assert.Equal(1, (int)ReadJson(g.PathOf("artifacts/ci/fast.json"))["skipped_steps"]!);
        }

        [Fact]
        public void Run_DoesOneScript_EvenWithoutAStageHeader()
        {
            using TempGit g = TempGit.Create(withCiScripts: true);
            g.Write("tools/ci/one.sh", "#!/bin/bash\n# ci-stage: full\necho one-ran\n");
            g.Write("tools/ci/two.sh", "#!/bin/bash\n# ci-stage: full\necho two-ran\n");
            g.Write("tools/ci/lib/helper.sh", "#!/bin/bash\necho helper-ran\n");
            ProcResult r = TempGit.RunScript(g.PathOf("tools/ci/ci.sh"), "/", null, "run", "one.sh");
            Assert.Equal(0, r.ExitCode);
            Assert.Contains("one-ran", r.Stdout, StringComparison.Ordinal);
            Assert.DoesNotContain("two-ran", r.Stdout, StringComparison.Ordinal);
            Assert.True(File.Exists(g.PathOf("artifacts/ci/run.json")));
            ProcResult h = TempGit.RunScript(g.PathOf("tools/ci/ci.sh"), "/", null, "run", "lib/helper.sh");
            Assert.Equal(0, h.ExitCode);
            Assert.Contains("helper-ran", h.Stdout, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("nightly")]
        [InlineData("fast", "--everything")]
        [InlineData("gate")]
        [InlineData("run")]
        public void BadUsage_ExitsTwo(params string[] args)
        {
            using TempGit g = TempGit.Create(withCiScripts: true);
            ProcResult r = TempGit.RunScript(g.PathOf("tools/ci/ci.sh"), g.Dir, null, args);
            Assert.Equal(2, r.ExitCode);
        }

        [Fact]
        public void SecondRunInTheSameCheckout_WaitsForTheLock_AndFailsWhenItCannotGetIt()
        {
            using TempGit g = TempGit.Create(withCiScripts: true);
            g.Write("tools/ci/a.sh", "#!/bin/bash\n# ci-stage: fast\necho a\n");
            using (CiLock.Acquire(g.Dir, TimeSpan.Zero, TextWriter.Null))
            {
                ProcResult r = TempGit.RunScript(g.PathOf("tools/ci/ci.sh"), g.Dir, new Dictionary<string, string?> { ["WIZARD_CI_LOCK_WAIT"] = "1" }, "fast");
                Assert.Equal(CiException.UsageExitCode, r.ExitCode);
                Assert.Contains("잠금", r.Stderr, StringComparison.Ordinal);
            }
            ProcResult ok = TempGit.RunScript(g.PathOf("tools/ci/ci.sh"), g.Dir, null, "fast");
            Assert.Equal(0, ok.ExitCode);
        }

        [Fact]
        public void Gate_RunsEveryStageWithoutNarrowing_AndWritesTheGateFile_KeepingRecords()
        {
            using TempGit g = TempGit.Create(withCiScripts: true);
            g.Write("tools/ci/a.sh", Probe);
            g.Write("tools/ci/b.sh", "#!/bin/bash\n# ci-stage: integration\necho integration-ran\n");
            g.Write("docs/dev/tasks.json",
                "{\"milestones\":[{\"id\":\"M1\"}],\"tasks\":[" +
                "{\"id\":\"M1-A\",\"milestone\":\"M1\",\"human_check\":\"사람이 손맛을 본다\"}," +
                "{\"id\":\"M1-B\",\"milestone\":\"M1\"}," +
                "{\"id\":\"M1-C\",\"milestone\":\"M1\",\"human_check\":\"사람이 설치를 본다\"}," +
                "{\"id\":\"M2-A\",\"milestone\":\"M2\",\"human_check\":\"다른 마일스톤\"}]}");
            g.Write("docs/dev/gates/M1.json",
                "{\"retro_check_diff\":{\"ok\":true},\"human_checks\":[{\"task\":\"M1-C\",\"check\":\"사람이 설치를 본다\",\"status\":\"확인함\",\"recorded_by\":\"user\"}]}");

            ProcResult r = TempGit.RunScript(g.PathOf("tools/ci/ci.sh"), g.Dir, null, "gate", "M1");
            Assert.True(r.ExitCode == 0, r.Stdout + r.Stderr);
            Assert.Contains("integration-ran", r.Stdout, StringComparison.Ordinal);
            Assert.EndsWith("force=1", File.ReadAllText(g.PathOf("artifacts/probe.txt")).Trim(), StringComparison.Ordinal);

            JsonNode gate = ReadJson(g.PathOf("docs/dev/gates/M1.json"));
            Assert.Equal("M1", (string)gate["milestone"]!);
            Assert.True((bool)gate["retro_check_diff"]!["ok"]!);
            Assert.True((bool)gate["ci"]!["passed"]!);
            Assert.NotNull(gate["ci"]!["stages"]!["fast"]);
            Assert.NotNull(gate["ci"]!["stages"]!["full"]);
            Assert.NotNull(gate["ci"]!["stages"]!["integration"]);
            Assert.Null(gate["ci"]!["stages"]!["fast"]!["root"]);
            JsonArray hc = gate["human_checks"]!.AsArray();
            Assert.Equal(2, hc.Count);
            Assert.Equal("M1-A", (string)hc[0]!["task"]!);
            Assert.Equal(GateFile.Pending, (string)hc[0]!["status"]!);
            Assert.Equal("M1-C", (string)hc[1]!["task"]!);
            Assert.Equal("확인함", (string)hc[1]!["status"]!);
            Assert.Equal("user", (string)hc[1]!["recorded_by"]!);
        }

        [Fact]
        public void Gate_UnknownMilestone_FailsBeforeRunningAnything()
        {
            using TempGit g = TempGit.Create(withCiScripts: true);
            g.Write("tools/ci/a.sh", Probe);
            g.Write("docs/dev/tasks.json", "{\"milestones\":[{\"id\":\"M1\"}],\"tasks\":[]}");
            ProcResult r = TempGit.RunScript(g.PathOf("tools/ci/ci.sh"), g.Dir, null, "gate", "M9");
            Assert.Equal(CiException.UsageExitCode, r.ExitCode);
            Assert.Contains("M9", r.Stderr, StringComparison.Ordinal);
            Assert.False(File.Exists(g.PathOf("artifacts/probe.txt")));
        }
    }
}

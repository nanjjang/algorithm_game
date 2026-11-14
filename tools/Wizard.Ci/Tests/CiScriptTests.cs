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

            // 실행기는 뿌리를 '/' 구분으로 넘긴다(Windows 에서 D:/… 꼴. 맥에서는 글자가 그대로다. T-67 4번)
            Assert.Equal("root=" + BashLocator.SlashPath(wt) + " stage=fast force=0", File.ReadAllText(Path.Combine(wt, "artifacts", "probe.txt")).Trim());
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

        // ---------- T-67 4번: 실행기는 스크립트의 경로와 경로가 든 환경 변수를 '/' 구분으로 넘긴다 ----------

        // 받은 $0 과 경로 변수를 적는 검사(실행기가 띄운다)
        private const string PathProbe =
            "#!/bin/bash\n# ci-stage: fast\nset -u\n" +
            "printf '%s\\n' \"zero=$0\" \"root=$WIZARD_ROOT\" \"note=$WIZARD_CI_NOTE\" \"dll=$WIZARD_CI_DLL\" \"trx=$WIZARD_CI_TRX_DIR\" \"bash=$WIZARD_BASH\" > \"$WIZARD_ROOT/artifacts/paths.txt\"\n";

        [Fact]
        public void StepScripts_GetSlashPaths_ForTheirOwnPathAndThePathVariables()
        {
            using TempGit g = TempGit.Create(withCiScripts: true);
            g.Write("tools/ci/paths.sh", PathProbe);
            Directory.CreateDirectory(g.PathOf("artifacts"));
            ProcResult r = TempGit.RunScript(g.PathOf("tools/ci/ci.sh"), g.Dir, null, "run", "paths.sh");
            Assert.True(r.ExitCode == 0, r.Stdout + r.Stderr);
            string[] lines = File.ReadAllLines(g.PathOf("artifacts/paths.txt"));
            Assert.Equal(6, lines.Length);
            foreach (string l in lines)
            {
                Assert.DoesNotContain("\\", l, StringComparison.Ordinal);
                Assert.False(l.Substring(l.IndexOf('=', StringComparison.Ordinal) + 1).Length == 0, l);
            }
            Assert.Equal("zero=" + BashLocator.SlashPath(g.PathOf("tools/ci/paths.sh")), lines[0]);
            Assert.Equal("root=" + BashLocator.SlashPath(g.Dir), lines[1]);
            if (OperatingSystem.IsWindows())
            {
                // /d/… 꼴이 아니라 D:/… 꼴이다(dotnet 에 그대로 넘길 수 있다)
                Assert.Matches("^root=[A-Za-z]:/", lines[1]);
            }
        }

        // 다른 검사 스크립트와 같은 머리(case "$0" in */*) …)로 lib/common.sh 를 source 하고, ci_root_from 이 낸 뿌리를 dotnet 에 넘긴다.
        // 고치기 전 Windows 에서는 역슬래시 $0 때문에 './lib/common.sh: No such file or directory' 로 죽었고, 뿌리가 /d/… 꼴이면
        // dotnet 이 D:/d/… 로 읽어 --root 가 저장소가 아니라고 실패한다.
        [Fact]
        public void ScriptSourcingCommonSh_RunsUnderTheStage_AndItsRootWorksForDotnet()
        {
            using TempGit g = TempGit.Create(withCiScripts: true);
            g.Write("tools/ci/uses-common.sh",
                "#!/bin/bash\n# ci-stage: fast\nset -u\n" +
                "case \"$0\" in */*) _here=\"${0%/*}\" ;; *) _here=\".\" ;; esac\n" +
                ". \"$_here/lib/common.sh\"\n" +
                "ROOT=\"$(ci_root_from \"$0\")\" || exit 2\n" +
                "echo \"root-from=$ROOT\"\n" +
                "[ -f \"$ROOT/Wizard.sln\" ] || { echo \"no sln under $ROOT\" >&2; exit 1; }\n" +
                "ci_tool changed --root \"$ROOT\" --all || exit 1\n" +
                "dotnet \"$WIZARD_CI_DLL\" require nothing-such --root \"$ROOT\" 2>\"$ROOT/artifacts/require.err\"; echo \"require-exit=$?\"\n");
            Directory.CreateDirectory(g.PathOf("artifacts"));
            ProcResult r = TempGit.RunScript(g.PathOf("tools/ci/ci.sh"), "/", null, "fast");
            Assert.True(r.ExitCode == 0, r.Stdout + r.Stderr);
            Assert.Contains("PASS  uses-common", r.Stdout, StringComparison.Ordinal);
            Assert.Contains("root-from=" + BashLocator.SlashPath(g.Dir) + "\n", r.Stdout.Replace("\r", "", StringComparison.Ordinal), StringComparison.Ordinal);
            // --root 가 제 폴더를 가리켰다: 알 수 없는 요구는 사용법 오류(2)다. 뿌리가 틀렸으면 그보다 먼저 '저장소 뿌리가 아니다'(2)로 끝난다
            string err = File.ReadAllText(g.PathOf("artifacts/require.err"));
            Assert.Contains("알 수 없는 요구 'nothing-such'", err, StringComparison.Ordinal);
            Assert.DoesNotContain("뿌리가 아니다", err, StringComparison.Ordinal);
        }

        // ---------- T-67 9번: agent-config-check 는 있는 것만 본다 ----------

        private static TempGit AgentConfigRepo()
        {
            TempGit g = TempGit.Create(withCiScripts: true);
            g.CopyFromRepo("tools/ci/agent-config-check.sh");
            return g;
        }

        private static ProcResult RunAgentConfig(TempGit g) => TempGit.RunScript(g.PathOf("tools/ci/ci.sh"), g.Dir, null, "run", "agent-config-check.sh");

        private static void WriteOriginals(TempGit g, string claude, string agent)
        {
            g.Write("docs/dev/agent-config/CLAUDE.proposed.md", claude);
            g.Write("docs/dev/agent-config/agents/a.md", "---\nname: a\n---\n" + agent);
            g.Write("docs/dev/agent-config/agents/a-opus.md", "---\nname: a-opus\nmodel: opus\n---\n" + agent);
        }

        [Fact]
        public void AgentConfig_NeitherOriginalsNorInstalled_PassesWithANote_NotASkip()
        {
            using TempGit g = AgentConfigRepo();
            ProcResult r = RunAgentConfig(g);
            Assert.True(r.ExitCode == 0, r.Stdout + r.Stderr);
            JsonNode step = ReadJson(g.PathOf("artifacts/ci/run/agent-config-check.json"));
            Assert.True((bool)step["passed"]!);
            Assert.False((bool)step["skipped"]!);
            Assert.Contains("이 체크아웃에는 에이전트 설정이 없다(git이 추적하지 않는다)", (string)step["notes"]![0]!, StringComparison.Ordinal);
        }

        [Fact]
        public void AgentConfig_InstalledWithoutOriginals_Fails()
        {
            using TempGit g = AgentConfigRepo();
            g.Write("CLAUDE.md", "rules\n");
            ProcResult r = RunAgentConfig(g);
            Assert.NotEqual(0, r.ExitCode);
            Assert.Contains("원본 docs/dev/agent-config/ 가 없다", r.Stdout + r.Stderr, StringComparison.Ordinal);

            using TempGit h = AgentConfigRepo();
            h.Write(".claude/agents/a.md", "---\nname: a\n---\nbody\n");
            Assert.NotEqual(0, RunAgentConfig(h).ExitCode);
        }

        [Fact]
        public void AgentConfig_OriginalsOnly_AreCheckedAgainstEachOther()
        {
            using TempGit g = AgentConfigRepo();
            WriteOriginals(g, "rules\n", "body\n");
            ProcResult ok = RunAgentConfig(g);
            Assert.True(ok.ExitCode == 0, ok.Stdout + ok.Stderr);
            // 모델별 변형의 본문이 다르면 실패(원본끼리 본다)
            g.Write("docs/dev/agent-config/agents/a-opus.md", "---\nname: a-opus\n---\nother body\n");
            ProcResult bad = RunAgentConfig(g);
            Assert.NotEqual(0, bad.ExitCode);
            Assert.Contains("body of a-opus.md differs", bad.Stdout + bad.Stderr, StringComparison.Ordinal);
        }

        [Fact]
        public void AgentConfig_InstalledEqualToOriginals_Passes_AndDifferentFails()
        {
            using TempGit g = AgentConfigRepo();
            WriteOriginals(g, "rules\n", "body\n");
            g.Write("CLAUDE.md", "rules\n");
            g.Write(".claude/agents/a.md", "---\nname: a\n---\nbody\n");
            g.Write(".claude/agents/a-opus.md", "---\nname: a-opus\nmodel: opus\n---\nbody\n");
            ProcResult same = RunAgentConfig(g);
            Assert.True(same.ExitCode == 0, same.Stdout + same.Stderr);

            g.Write("CLAUDE.md", "changed rules\n");
            ProcResult differ = RunAgentConfig(g);
            Assert.NotEqual(0, differ.ExitCode);
            Assert.Contains("CLAUDE.md differs", differ.Stdout + differ.Stderr, StringComparison.Ordinal);
        }
    }
}

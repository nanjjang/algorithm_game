using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Xunit;

namespace Wizard.Ci.Tests
{
    // PIPELINE 7.1 G0 의 "변경이 owned_paths 안"과 "커밋 메시지 형식"을 ci.sh fast 가 본다(tools/ci/lib/task-branch.sh).
    // commit-msg 훅이 걸리지 않았거나 --no-verify 로 지나간 커밋도 fast 가 잡아야 한다.
    public sealed class TaskBranchTests
    {
        private const string Opus = "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>";
        private const string Id = "M0-BT-05";

        private static string Good(string id, string summary) => "build(" + id + "): " + summary + "\n\n본문.\n\n" + Opus + "\n";

        private static readonly HashSet<string> Ids = new HashSet<string>(StringComparer.Ordinal) { Id, "M0-BT-06" };

        // 임시 저장소: main 에 첫 커밋, task/<Id> 작업 트리
        private static (TempGit G, string Wt) Branch()
        {
            TempGit g = TempGit.Create();
            string wt = g.PathOf(".claude/worktrees/" + Id);
            g.Git("worktree", "add", "-q", wt, "-b", "task/" + Id);
            return (g, wt);
        }

        private static void Change(TempGit g, string wt, string rel, string content, string message)
        {
            string p = Path.Combine(wt, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(p)!);
            File.WriteAllText(p, content);
            g.Commit(message, wt);
        }

        [Fact]
        public void WellFormedCommits_Pass_AndMergeCommitsAreNotChecked()
        {
            (TempGit g, string wt) = Branch();
            using (g)
            {
                Change(g, wt, "a.txt", "a\n", Good(Id, "하나"));
                Change(g, wt, "b.txt", "b\n", Good(Id, "둘"));
                // main 이 앞서 나간 것을 작업 브랜치로 병합한다(병합 커밋의 메시지는 git 이 만든다)
                g.Write("main.txt", "m\n");
                g.Commit("chore: main 쪽 변경");
                TempGit.GitIn(wt, "-c", "user.name=t", "-c", "user.email=t@example.invalid", "merge", "-q", "--no-edit", "main");
                List<BranchCommits.Problem> p = BranchCommits.Check(wt, "main", Id, Ids, out int count, out string range);
                Assert.Empty(p);
                Assert.Equal(2, count);
                Assert.Contains("main", range, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void NoCommitsYet_Passes()
        {
            (TempGit g, string wt) = Branch();
            using (g)
            {
                Assert.Empty(BranchCommits.Check(wt, "main", Id, Ids, out int count, out _));
                Assert.Equal(0, count);
            }
        }

        [Fact]
        public void WipCommitMadeWithoutTheHook_Fails_NamingTheCommit()
        {
            (TempGit g, string wt) = Branch();
            using (g)
            {
                Change(g, wt, "a.txt", "a\n", Good(Id, "좋은 커밋"));
                Change(g, wt, "b.txt", "b\n", "wip");
                string sha = TempGit.GitIn(wt, "rev-parse", "HEAD").Trim();
                List<BranchCommits.Problem> p = BranchCommits.Check(wt, "main", Id, Ids, out int count, out _);
                Assert.Equal(2, count);
                Assert.NotEmpty(p);
                Assert.All(p, x => Assert.Equal(sha, x.Commit));
                Assert.Contains(p, x => x.Message.Contains("꼴이 아니다", StringComparison.Ordinal));
                Assert.Contains(p, x => x.Message.Contains("Co-Authored-By 줄이 없다", StringComparison.Ordinal));
                Assert.StartsWith(sha.Substring(0, 12) + " \"wip\": ", p[0].ToString(), StringComparison.Ordinal);
            }
        }

        [Fact]
        public void CommitForAnotherTask_OnATaskBranch_Fails()
        {
            (TempGit g, string wt) = Branch();
            using (g)
            {
                Change(g, wt, "a.txt", "a\n", Good("M0-BT-06", "남의 작업 id"));
                Assert.Empty(BranchCommits.Check(wt, "main", null, Ids, out _, out _));
                List<BranchCommits.Problem> p = BranchCommits.Check(wt, "main", Id, Ids, out _, out _);
                Assert.Single(p);
                Assert.Contains("'M0-BT-06'", p[0].Message, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void UnknownBase_IsAUsageError()
        {
            (TempGit g, string wt) = Branch();
            using (g)
            {
                CiException e = Assert.Throws<CiException>(() => BranchCommits.Check(wt, "no-such-ref", Id, Ids, out _, out _));
                Assert.Contains("no-such-ref", e.Message, StringComparison.Ordinal);
            }
        }

        // ---- ci.sh fast 로 끝까지(진짜 task-branch.sh, 진짜 taskgraph, 저장소의 진짜 tasks.json) ----

        private static readonly Lazy<string> TaskGraphDll = new Lazy<string>(() =>
        {
            string art = Path.Combine(TempGit.RepoRoot, "artifacts", "ci-tests-build");
            var req = new ProcRequest("dotnet", new[]
            {
                "build", Path.Combine(TempGit.RepoRoot, "tools", "Wizard.TaskGraph", "Wizard.TaskGraph.csproj"),
                "-c", "Release", "-nologo", "-v", "quiet", "-p:ArtifactsPath=" + art,
            })
            {
                Capture = true,
                Timeout = TimeSpan.FromMinutes(10),
            };
            TempGit.Isolate(req);
            ProcResult r = ProcessRunner.Run(req);
            Assert.True(r.ExitCode == 0, "taskgraph 빌드 실패: " + r.Stdout + r.Stderr);
            string dll = Path.Combine(art, "bin", "Wizard.TaskGraph", "release", "Wizard.TaskGraph.dll");
            Assert.True(File.Exists(dll), dll);
            return dll;
        });

        private static TempGit RepoWithTaskBranchStep()
        {
            TempGit g = TempGit.Create(withCiScripts: true);
            g.CopyFromRepo("tools/ci/lib/task-branch.sh");
            g.CopyFromRepo("docs/dev/tasks.json");
            g.Write("docs/DECISIONS.md", "# 결정\n");
            g.Commit("chore: task-branch step");
            return g;
        }

        private static ProcResult Fast(string wt, string? ciBase = null) =>
            TempGit.RunScript(Path.Combine(wt, "tools", "ci", "ci.sh"), "/",
                new Dictionary<string, string?> { ["WIZARD_TASKGRAPH_DLL"] = TaskGraphDll.Value, ["WIZARD_CI_BASE"] = ciBase }, "fast");

        private static JsonNode StepJson(string wt) =>
            JsonNode.Parse(File.ReadAllText(Path.Combine(wt, "artifacts", "ci", "fast", "task-branch.json")))!;

        [Fact]
        public void Fast_OnATaskBranch_PassesForOwnedWellFormedCommits_AndFailsForAWipCommitOutsideOwnedPaths()
        {
            using TempGit g = RepoWithTaskBranchStep();
            string wt = g.PathOf(".claude/worktrees/" + Id);
            g.Git("worktree", "add", "-q", wt, "-b", "task/" + Id);

            // 소유 경로(tools/ci/lib/) 안, 형식에 맞는 메시지
            Change(g, wt, "tools/ci/lib/notes.txt", "x\n", Good(Id, "소유 경로 안의 변경"));
            ProcResult ok = Fast(wt);
            Assert.True(ok.ExitCode == 0, ok.Stdout + ok.Stderr);
            Assert.Contains("commit-msgs:", ok.Stdout, StringComparison.Ordinal);
            Assert.Contains("위반 0건", ok.Stdout, StringComparison.Ordinal);
            Assert.False((bool)StepJson(wt)["skipped"]!);

            // 검토자의 재현: 소유 밖(docs/DECISIONS.md)을 'wip' 로 훅 없이 커밋한다
            Change(g, wt, "docs/DECISIONS.md", "# 결정\n고침\n", "wip");
            ProcResult bad = Fast(wt);
            string output = bad.Stdout + bad.Stderr;
            Assert.Equal(1, bad.ExitCode);
            Assert.Contains("docs/DECISIONS.md — 소유 파일 밖이다", output, StringComparison.Ordinal);
            Assert.Contains("\"wip\": 첫 줄이", output, StringComparison.Ordinal);
            Assert.Contains("FAIL  [fast] task-branch", output, StringComparison.Ordinal);
            JsonNode fast = JsonNode.Parse(File.ReadAllText(Path.Combine(wt, "artifacts", "ci", "fast.json")))!;
            Assert.Equal(1, (int)fast["failed_steps"]!);
        }

        [Fact]
        public void Fast_OnATaskBranch_FailsForAnOwnedChangeWithABadMessage_AndForAWellFormedChangeOutsideOwnedPaths()
        {
            using TempGit g = RepoWithTaskBranchStep();
            string wt = g.PathOf(".claude/worktrees/" + Id);
            g.Git("worktree", "add", "-q", wt, "-b", "task/" + Id);
            Change(g, wt, "tools/ci/lib/notes.txt", "x\n", "build(M0-BT-05): 본문이 없다\n");
            ProcResult msgOnly = Fast(wt);
            Assert.Equal(1, msgOnly.ExitCode);
            Assert.Contains("본문이 없다(", msgOnly.Stdout + msgOnly.Stderr, StringComparison.Ordinal);
            Assert.Contains("위반 0건", msgOnly.Stdout, StringComparison.Ordinal);

            using TempGit g2 = RepoWithTaskBranchStep();
            string wt2 = g2.PathOf(".claude/worktrees/" + Id);
            g2.Git("worktree", "add", "-q", wt2, "-b", "task/" + Id);
            Change(g2, wt2, "README.md", "changed\n", Good(Id, "소유 밖"));
            ProcResult pathOnly = Fast(wt2);
            Assert.Equal(1, pathOnly.ExitCode);
            Assert.Contains("README.md — 소유 파일 밖이다", pathOnly.Stdout + pathOnly.Stderr, StringComparison.Ordinal);
            Assert.Contains("문제 0건", pathOnly.Stdout, StringComparison.Ordinal);
        }

        // DECISIONS-TECH T-48 의 길: 계약 개정(소유 밖 문서)을 브랜치의 첫 커밋 하나로 따로 두고, 게이트는 그 커밋을 기준으로 준다
        // (WIZARD_CI_BASE=<그 커밋> tools/ci/ci.sh fast). 기준을 주면 task-branch 의 check-diff 와 commit-msgs 가 나머지 커밋만 보고 통과한다.
        // 기준이 없으면 둘 다 개정 커밋까지 본다(이 시험에서는 개정 커밋의 첫 줄이 다른 작업 id 라 commit-msgs 도 실패한다).
        [Fact]
        public void Fast_WithTheBaseAtAContractAmendmentCommit_ChecksOnlyTheRest()
        {
            using TempGit g = RepoWithTaskBranchStep();
            string wt = g.PathOf(".claude/worktrees/" + Id);
            g.Git("worktree", "add", "-q", wt, "-b", "task/" + Id);
            Change(g, wt, "docs/dev/CONTRACTS.md", "# 계약\n개정\n", Good("M0-BT-06", "계약 개정"));
            string amendment = TempGit.GitIn(wt, "rev-parse", "HEAD").Trim();
            Change(g, wt, "tools/ci/lib/notes.txt", "x\n", Good(Id, "소유 경로 안의 변경 하나"));
            Change(g, wt, "tools/ci/lib/notes2.txt", "y\n", Good(Id, "소유 경로 안의 변경 둘"));

            // 명령 줄 층: --base 없이 WIZARD_CI_BASE 로 기준을 받는다
            var stdout = new StringWriter();
            var stderr = new StringWriter();
            int code = Program.Run(new[] { "commit-msgs", "--task", Id, "--root", wt }, stdout, stderr,
                k => k == "WIZARD_CI_BASE" ? amendment : null);
            Assert.True(code == 0, stdout.ToString() + stderr.ToString());
            Assert.Contains("(" + amendment + " 와의 갈림점)..HEAD 커밋 2개(병합 커밋 제외), 문제 0건", stdout.ToString(), StringComparison.Ordinal);

            stdout = new StringWriter();
            stderr = new StringWriter();
            Assert.Equal(1, Program.Run(new[] { "commit-msgs", "--task", Id, "--root", wt }, stdout, stderr, _ => null));
            Assert.Contains("커밋 3개(병합 커밋 제외), 문제 1건", stderr.ToString(), StringComparison.Ordinal);
            Assert.Contains("'M0-BT-06'", stderr.ToString(), StringComparison.Ordinal);

            // ci.sh fast 로 끝까지(진짜 task-branch.sh 와 taskgraph check-diff)
            ProcResult withBase = Fast(wt, amendment);
            string okOut = withBase.Stdout + withBase.Stderr;
            Assert.True(withBase.ExitCode == 0, okOut);
            Assert.Contains("커밋 2개(병합 커밋 제외), 문제 0건", okOut, StringComparison.Ordinal);
            Assert.Contains("위반 0건", okOut, StringComparison.Ordinal);
            Assert.DoesNotContain("docs/dev/CONTRACTS.md", okOut, StringComparison.Ordinal);

            ProcResult noBase = Fast(wt);
            string badOut = noBase.Stdout + noBase.Stderr;
            Assert.Equal(1, noBase.ExitCode);
            Assert.Contains("docs/dev/CONTRACTS.md — 소유 파일 밖이다", badOut, StringComparison.Ordinal);
            Assert.Contains("커밋 3개(병합 커밋 제외), 문제 1건", badOut, StringComparison.Ordinal);
        }

        [Fact]
        public void Fast_NotOnATaskBranch_RecordsTheStepAsSkipped()
        {
            using TempGit g = RepoWithTaskBranchStep();
            ProcResult r = Fast(g.Dir);
            Assert.True(r.ExitCode == 0, r.Stdout + r.Stderr);
            JsonNode step = StepJson(g.Dir);
            Assert.True((bool)step["skipped"]!);
            Assert.Contains("task/<id> 가 아니다", (string)step["skip_reason"]!, StringComparison.Ordinal);
        }

        [Fact]
        public void Fast_NotesUncommittedChanges_ThatItDidNotCheck()
        {
            using TempGit g = RepoWithTaskBranchStep();
            string wt = g.PathOf(".claude/worktrees/" + Id);
            g.Git("worktree", "add", "-q", wt, "-b", "task/" + Id);
            File.WriteAllText(Path.Combine(wt, "README.md"), "uncommitted\n");
            ProcResult r = Fast(wt);
            Assert.True(r.ExitCode == 0, r.Stdout + r.Stderr);
            JsonArray notes = StepJson(wt)["notes"]!.AsArray();
            Assert.Contains(notes, n => ((string)n!).Contains("커밋하지 않은 변경 1개", StringComparison.Ordinal));
        }
    }
}

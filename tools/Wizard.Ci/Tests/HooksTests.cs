using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace Wizard.Ci.Tests
{
    // install-hooks.sh 가 거는 pre-commit·commit-msg 훅(PIPELINE 4.6). 진짜 훅 스크립트를 임시 저장소에 복사해 git commit 으로 확인한다.
    public sealed class HooksTests
    {
        private const string Good = "build(M0-BT-05): 훅을 확인한다\n\n본문.\n\nCo-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>\n";

        private static readonly Dictionary<string, string?> HookEnv = new Dictionary<string, string?> { ["WIZARD_CI_DLL"] = TempGit.RunnerDll };

        private static TempGit RepoWithHooks()
        {
            TempGit g = TempGit.Create(withCiScripts: true);
            g.CopyFromRepo("tools/ci/install-hooks.sh");
            g.CopyFromRepo("tools/ci/hooks/pre-commit");
            g.CopyFromRepo("tools/ci/hooks/commit-msg");
            // src/ 를 올리면 훅이 부르는 .meta 검사(진짜는 MetaGen 을 빌드한다). 여기서는 불렸다는 것만 남긴다
            g.Write("tools/ci/lib/meta-check.sh", "#!/bin/bash\necho meta-stub-ran\n", executable: true);
            g.Write("docs/dev/tasks.json", "{\"tasks\":[{\"id\":\"M0-BT-05\"}]}\n");
            g.Commit("chore: hooks");
            ProcResult r = TempGit.RunScript(g.PathOf("tools/ci/install-hooks.sh"), "/", null);
            Assert.True(r.ExitCode == 0, r.Stdout + r.Stderr);
            return g;
        }

        private static ProcResult Commit(string dir, string message)
        {
            File.WriteAllText(Path.Combine(dir, "msg.txt"), message);
            TempGit.GitIn(dir, "add", "-A");
            return TempGit.GitTry(dir, new[] { "commit", "-q", "-F", Path.Combine(dir, "msg.txt") }, HookEnv);
        }

        [Fact]
        public void InstallHooks_SetsARelativeHooksPath_AndCheckReportsIt()
        {
            using TempGit g = RepoWithHooks();
            Assert.Equal("tools/ci/hooks", g.Git("config", "--get", "core.hooksPath").Trim());
            Assert.Equal(0, TempGit.RunScript(g.PathOf("tools/ci/install-hooks.sh"), g.Dir, null, "--check").ExitCode);
            g.Git("config", "--unset", "core.hooksPath");
            Assert.Equal(1, TempGit.RunScript(g.PathOf("tools/ci/install-hooks.sh"), g.Dir, null, "--check").ExitCode);
        }

        [Fact]
        public void WellFormedCommit_IsAccepted()
        {
            using TempGit g = RepoWithHooks();
            g.Write("README.md", "changed\n");
            ProcResult r = Commit(g.Dir, Good);
            Assert.True(r.ExitCode == 0, r.Stdout + r.Stderr);
            Assert.Contains("훅을 확인한다", g.Git("log", "-1", "--format=%s"), StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("훅을 확인한다\n")]
        [InlineData("build(M0-BT-05): 요약\n\n본문.\n")]
        [InlineData("build(NO-SUCH): 요약\n\n본문.\n\nCo-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>\n")]
        public void BadMessage_IsRejected(string message)
        {
            using TempGit g = RepoWithHooks();
            g.Write("README.md", "changed\n");
            string before = g.Git("rev-parse", "HEAD");
            ProcResult r = Commit(g.Dir, message);
            Assert.NotEqual(0, r.ExitCode);
            Assert.Contains("commit-msg: 커밋을 거절한다", r.Stdout + r.Stderr, StringComparison.Ordinal);
            Assert.Equal(before, g.Git("rev-parse", "HEAD"));
        }

        [Fact]
        public void ForbiddenApiInCore_IsRejectedByPreCommit()
        {
            using TempGit g = RepoWithHooks();
            g.Write("src/Wizard.Core/Bad.cs", "namespace Wizard.Core\n{\n    public static class Bad\n    {\n        public static float Speed;\n    }\n}\n");
            string before = g.Git("rev-parse", "HEAD");
            ProcResult r = Commit(g.Dir, Good);
            string output = r.Stdout + r.Stderr;
            Assert.NotEqual(0, r.ExitCode);
            Assert.Contains("src/Wizard.Core/Bad.cs:5: 실수형 'float'", output, StringComparison.Ordinal);
            Assert.Contains("meta-stub-ran", output, StringComparison.Ordinal);
            Assert.Equal(before, g.Git("rev-parse", "HEAD"));
        }

        [Fact]
        public void StagedContent_IsWhatGetsChecked_NotTheWorkingTree()
        {
            using TempGit g = RepoWithHooks();
            g.Write("src/Wizard.Sim/A.cs", "namespace Wizard.Sim { public static class A { public static double D; } }\n");
            TempGit.GitIn(g.Dir, "add", "-A");
            // 작업 트리에서 고쳤지만 올리지 않았다: 올린 내용(double)이 커밋되므로 거절해야 한다
            g.Write("src/Wizard.Sim/A.cs", "namespace Wizard.Sim { public static class A { public static long D; } }\n");
            File.WriteAllText(g.PathOf("msg.txt"), Good);
            ProcResult r = TempGit.GitTry(g.Dir, new[] { "commit", "-q", "-F", g.PathOf("msg.txt") }, HookEnv);
            Assert.NotEqual(0, r.ExitCode);
            Assert.Contains("'double'", r.Stdout + r.Stderr, StringComparison.Ordinal);
        }

        [Fact]
        public void SuppressionAndBadNamesTable_AreRejected()
        {
            using TempGit g = RepoWithHooks();
            g.Write(".editorconfig", "[*.cs]\ndotnet_diagnostic.WZ0001.severity = none\n");
            g.Write("data/names/names.ko.json", "{\n  \"b\": \"2\",\n  \"a\": \"1\",\n  \"~end\": \"\"\n}\n");
            ProcResult r = Commit(g.Dir, Good);
            string output = r.Stdout + r.Stderr;
            Assert.NotEqual(0, r.ExitCode);
            Assert.Contains("dotnet_diagnostic.WZ", output, StringComparison.Ordinal);
            Assert.Contains("오름차순", output, StringComparison.Ordinal);
        }

        [Fact]
        public void HooksRunInAWorktree_WithTheWorktreesOwnScripts()
        {
            using TempGit g = RepoWithHooks();
            string wt = g.PathOf(".claude/worktrees/W1");
            g.Git("worktree", "add", "-q", wt, "-b", "task/W1");
            File.WriteAllText(Path.Combine(wt, "README.md"), "in worktree\n");
            ProcResult bad = Commit(wt, "no format\n");
            Assert.NotEqual(0, bad.ExitCode);
            Assert.Contains("commit-msg: 커밋을 거절한다", bad.Stdout + bad.Stderr, StringComparison.Ordinal);
            // pre-commit 은 자기 체크아웃과 커밋하는 체크아웃이 같은지 본다. 상대 경로 hooksPath 라 작업 트리의 훅이 돌아 통과한다
            ProcResult good = Commit(wt, Good);
            Assert.True(good.ExitCode == 0, good.Stdout + good.Stderr);
        }
    }
}

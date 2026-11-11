using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Wizard.VectorCoverage.Tests
{
    // 실제 저장소에서의 동작: 뿌리 찾기, 명령줄, 전역 검사
    public sealed class RepoTests
    {
        [Fact]
        public void FindRoot_FromTheToolLocation_IsTheCheckoutThatHoldsThisTool()
        {
            string root = Program.FindRoot();
            Assert.True(File.Exists(Path.Combine(root, "tools", "Wizard.VectorCoverage", "Wizard.VectorCoverage.csproj")));
            Assert.True(File.Exists(Path.Combine(root, "tests", "vectors.config.json")));
            // 도구 어셈블리는 그 체크아웃의 artifacts/ 아래에 있다(작업 트리의 도구는 작업 트리를 본다)
            Assert.StartsWith(root + Path.DirectorySeparatorChar, AppContext.BaseDirectory, StringComparison.Ordinal);
        }

        [Fact]
        public void TasksJson_ParsesIntoTasksWithSelectors()
        {
            var tasks = TaskEntry.Load(Path.Combine(Fx.RepoRoot, "docs", "dev", "tasks.json"));
            Assert.Contains(tasks, t => t.Id == "M0-BT-05");
            Assert.Contains(tasks, t => t.Vectors.Count > 0);
        }

        [Fact]
        public void Global_OnThisRepository_HasNoFailures()
        {
            string state = Path.Combine(Fx.RepoRoot, "artifacts", "vector-coverage-tests", "no-state.json");
            var stdout = new StringWriter();
            var stderr = new StringWriter();
            int code = Program.Run(new[] { "--root", Fx.RepoRoot, "--state", state }, stdout, stderr);
            Assert.True(code == 0, stdout + "\n" + stderr);
            Assert.Contains("vector-coverage: ok", stdout.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void TaskWithoutVectors_OnThisRepository_Passes()
        {
            var stdout = new StringWriter();
            int code = Program.Run(new[] { "--root", Fx.RepoRoot, "--task", "M0-BT-05" }, stdout, new StringWriter());
            Assert.Equal(0, code);
            Assert.Contains("대상 0개", stdout.ToString(), StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("--bogus")]
        [InlineData("--task")]
        [InlineData("--trx", "x.trx")]
        public void BadArguments_AreUsageErrors(params string[] args)
        {
            CoverageException e = Assert.Throws<CoverageException>(() => Program.Run(args, new StringWriter(), new StringWriter()));
            Assert.Equal(CoverageException.UsageExitCode, e.ExitCode);
        }

        [Fact]
        public void UnknownTask_IsAUsageError()
        {
            CoverageException e = Assert.Throws<CoverageException>(() =>
                Program.Run(new[] { "--root", Fx.RepoRoot, "--task", "NO-SUCH-TASK" }, new StringWriter(), new StringWriter()));
            Assert.Equal(CoverageException.UsageExitCode, e.ExitCode);
        }

        [Fact]
        public void StateFile_DoneTasksAreRead()
        {
            var done = TaskState.ParseDone("{\"schema\":1,\"status\":{\"A\":\"done\",\"B\":\"review\",\"C\":\"done\"}}", "x");
            Assert.Equal(new[] { "A", "C" }, done.ToArray());
            Assert.Throws<CoverageException>(() => TaskState.ParseDone("{\"schema\":1}", "x"));
            Assert.Empty(TaskState.ReadDone(Path.Combine(Fx.RepoRoot, "artifacts", "no-such-dir", "taskstate.json")));
        }

        [Fact]
        public void DefaultStatePath_IsInTheMainCheckoutsClaudeFolder()
        {
            string p = TaskState.DefaultPath(Fx.RepoRoot);
            Assert.EndsWith(Path.Combine(".claude", "taskstate.json"), p, StringComparison.Ordinal);
            // 작업 트리(.claude/worktrees/<id>)에서 돌아도 상태 파일은 메인 체크아웃의 것이다
            Assert.DoesNotContain(Path.Combine(".claude", "worktrees"), p, StringComparison.Ordinal);
        }
    }
}

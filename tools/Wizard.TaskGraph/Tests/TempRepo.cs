using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;

namespace Wizard.TaskGraph.Tests
{
    // 저장소의 artifacts/ 아래(git 이 무시한다)에 임시 git 저장소를 만든다. main 브랜치에 첫 커밋이 있다.
    internal sealed class TempRepo : IDisposable
    {
        private TempRepo(string dir)
        {
            Dir = dir;
        }

        public string Dir { get; }

        public static TempRepo Create(bool git = true)
        {
            string dir = Path.Combine(Fx.RepoRoot, "artifacts", "taskgraph-tests", Guid.NewGuid().ToString("N").Substring(0, 12));
            Directory.CreateDirectory(dir);
            var r = new TempRepo(RepoLayout.Normalize(dir));
            if (git)
            {
                r.Git("init", "-q", "-b", "main");
                r.Write("README.md", "test\n");
                r.Write(".gitignore", "/artifacts/\n/.claude/worktrees/\n/.claude/taskstate.json\n/unity/Library/\n");
                r.Commit("chore: init");
            }
            return r;
        }

        public string PathOf(string rel) => Path.Combine(Dir, rel);

        public void Write(string rel, string content)
        {
            string p = PathOf(rel);
            Directory.CreateDirectory(Path.GetDirectoryName(p)!);
            File.WriteAllText(p, content, new UTF8Encoding(false));
        }

        public void Delete(string rel) => File.Delete(PathOf(rel));

        public string Git(params string[] args) => GitIn(Dir, args);

        public static string GitIn(string dir, params string[] args)
        {
            ProcResult r = TryGitIn(dir, args);
            Assert.True(r.ExitCode == 0, $"git {string.Join(" ", args)}: {r.Stderr}");
            return r.Stdout;
        }

        // 실패해도 되는 git(rebase 가 충돌로 멈추는 것 등). 편집기는 띄우지 않는다
        public ProcResult TryGit(params string[] args) => TryGitIn(Dir, args);

        public static ProcResult TryGitIn(string dir, params string[] args)
        {
            var all = new List<string>
            {
                "-C", dir, "-c", "user.name=taskgraph-test", "-c", "user.email=test@example.invalid",
                "-c", "commit.gpgsign=false", "-c", "core.hooksPath=/dev/null",
            };
            all.AddRange(args);
            return Proc.Run("git", all, null, new Dictionary<string, string> { ["GIT_EDITOR"] = "true", ["GIT_SEQUENCE_EDITOR"] = "true" });
        }

        public void Commit(string message, string? dir = null)
        {
            GitIn(dir ?? Dir, "add", "-A");
            GitIn(dir ?? Dir, "commit", "-q", "--allow-empty", "-m", message);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Dir, true);
            }
            catch (IOException)
            {
                // 테스트 결과에 영향이 없다. artifacts/ 는 git 이 무시한다
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}

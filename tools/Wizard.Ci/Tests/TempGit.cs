using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;

namespace Wizard.Ci.Tests
{
    // 저장소의 artifacts/ 아래(git 이 무시한다)에 임시 git 저장소를 만든다. main 브랜치에 첫 커밋이 있다.
    // ci.sh 를 돌리는 테스트를 위해 진짜 tools/ci/ci.sh 와 tools/ci/lib/common.sh 를 복사해 둘 수 있다.
    internal sealed class TempGit : IDisposable
    {
        public static readonly string RepoRoot = RepoPaths.FindFrom(AppContext.BaseDirectory);

        // 이 테스트 어셈블리가 곧 Wizard.Ci 실행기다(같은 어셈블리. ci.sh 에는 WIZARD_CI_RUNNER 로 준다)
        public static readonly string RunnerDll = typeof(Program).Assembly.Location;

        private TempGit(string dir)
        {
            Dir = dir;
        }

        public string Dir { get; }

        public static TempGit Create(bool withCiScripts = false)
        {
            string dir = Path.Combine(RepoRoot, "artifacts", "ci-tests", Guid.NewGuid().ToString("N").Substring(0, 12));
            Directory.CreateDirectory(dir);
            var g = new TempGit(RepoPaths.Normalize(new DirectoryInfo(dir).FullName));
            g.Git("init", "-q", "-b", "main");
            // 시스템 설정(Windows 의 core.autocrlf=true)을 물려받지 않는다: 시험의 파일은 쓴 바이트 그대로다(T-67 5번)
            g.Git("config", "core.autocrlf", "false");
            g.Write(".gitignore", "/artifacts/\n/.claude/worktrees/\n");
            g.Write("Wizard.sln", "\n");
            g.Write("README.md", "test\n");
            if (withCiScripts)
            {
                g.CopyFromRepo("tools/ci/ci.sh");
                g.CopyFromRepo("tools/ci/lib/common.sh");
            }
            else
            {
                g.Write("tools/ci/ci.sh", "#!/bin/bash\nexit 0\n");
            }
            g.Commit("chore: init");
            return g;
        }

        public string PathOf(string rel) => RepoPaths.Combine(Dir, rel);

        public void Write(string rel, string content, bool executable = false)
        {
            string p = PathOf(rel);
            Directory.CreateDirectory(Path.GetDirectoryName(p)!);
            File.WriteAllText(p, content, new UTF8Encoding(false));
            if (executable && !OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(p, File.GetUnixFileMode(p) | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
            }
        }

        // 가짜 실행 파일(T-67 1·7번): 표준 출력·표준 오류에 줄을 쓰고 exitCode 로 끝난다. 맥·리눅스는 실행 권한이 있는 #!/bin/bash 스크립트(rel),
        // Windows 는 같은 일을 하는 rel + ".cmd" 다. 만든 파일의 경로를 돌려준다. 줄에는 cmd 의 특수 문자(& | < > ^ %)를 넣지 않는다
        public string WriteFakeProgram(string rel, string[] stdoutLines, string[] stderrLines, int exitCode)
        {
            if (OperatingSystem.IsWindows())
            {
                var cmd = new StringBuilder("@echo off\r\n");
                foreach (string l in stdoutLines)
                {
                    cmd.Append("echo ").Append(l).Append("\r\n");
                }
                foreach (string l in stderrLines)
                {
                    cmd.Append("echo ").Append(l).Append(" 1>&2\r\n");
                }
                cmd.Append("exit /b ").Append(exitCode.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append("\r\n");
                Write(rel + ".cmd", cmd.ToString());
                return PathOf(rel + ".cmd");
            }
            var sh = new StringBuilder("#!/bin/bash\n");
            foreach (string l in stdoutLines)
            {
                sh.Append("echo '").Append(l).Append("'\n");
            }
            foreach (string l in stderrLines)
            {
                sh.Append("echo '").Append(l).Append("' >&2\n");
            }
            sh.Append("exit ").Append(exitCode.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append('\n');
            Write(rel, sh.ToString(), executable: true);
            return PathOf(rel);
        }

        public void CopyFromRepo(string rel, string? to = null)
        {
            string src = RepoPaths.Combine(RepoRoot, rel);
            string dst = PathOf(to ?? rel);
            Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
            File.Copy(src, dst, true);
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(dst, File.GetUnixFileMode(src));
            }
        }

        public string Git(params string[] args) => GitIn(Dir, args);

        public static string GitIn(string dir, params string[] args)
        {
            ProcResult r = GitTry(dir, args);
            Assert.True(r.ExitCode == 0, "git " + string.Join(" ", args) + ": " + r.Stderr + r.Stdout);
            return r.Stdout;
        }

        public static ProcResult GitTry(string dir, string[] args, IDictionary<string, string?>? env = null)
        {
            var all = new List<string>
            {
                "-C", dir, "-c", "user.name=ci-test", "-c", "user.email=test@example.invalid", "-c", "commit.gpgsign=false",
            };
            all.AddRange(args);
            var req = new ProcRequest("git", all) { Capture = true, Timeout = TimeSpan.FromMinutes(3) };
            Isolate(req);
            if (env != null)
            {
                foreach (KeyValuePair<string, string?> kv in env)
                {
                    req.Environment[kv.Key] = kv.Value;
                }
            }
            return ProcessRunner.Run(req);
        }

        public void Commit(string message, string? dir = null)
        {
            GitIn(dir ?? Dir, "-c", "core.hooksPath=/dev/null", "add", "-A");
            GitIn(dir ?? Dir, "-c", "core.hooksPath=/dev/null", "commit", "-q", "--allow-empty", "-m", message);
        }

        // ci.sh 를 돌린다(작업 폴더는 cwd). 바깥 ci 실행에서 물려받은 변수는 지운다
        public static ProcResult RunScript(string script, string cwd, IDictionary<string, string?>? env, params string[] args)
        {
            // Git Bash 에서 사람이 치는 것과 같은 '/' 꼴로 부른다(T-67 4번)
            var all = new List<string> { BashLocator.SlashPath(script) };
            all.AddRange(args);
            var req = new ProcRequest(TestBash.Path, all)
            {
                WorkingDirectory = cwd,
                Capture = true,
                Timeout = TimeSpan.FromMinutes(5),
            };
            Isolate(req);
            req.Environment["WIZARD_CI_RUNNER"] = RunnerDll;
            if (env != null)
            {
                foreach (KeyValuePair<string, string?> kv in env)
                {
                    req.Environment[kv.Key] = kv.Value;
                }
            }
            return ProcessRunner.Run(req);
        }

        public static void Isolate(ProcRequest req)
        {
            foreach (string v in new[]
                     {
                         "WIZARD_ROOT", "WIZARD_CI_STAGE", "WIZARD_CI_FORCE_ALL", "WIZARD_CI_NOTE", "WIZARD_CI_DLL", "WIZARD_CI_TRX_DIR",
                         "WIZARD_CI_BASE", "WIZARD_CI_RUNNER", "WIZARD_CI_LOCK_WAIT", "WIZARD_TASKGRAPH_DLL", "CI_STEP_TIMEOUT", "GIT_DIR", "GIT_INDEX_FILE", "GIT_WORK_TREE",
                     })
            {
                req.Environment[v] = null;
            }
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Dir, true);
            }
            catch (IOException)
            {
                // 결과에 영향이 없다. artifacts/ 는 git 이 무시한다
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}

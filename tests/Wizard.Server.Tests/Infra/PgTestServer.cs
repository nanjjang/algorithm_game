using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Wizard.TestKit;

namespace Wizard.Server.Tests.Infra
{
    // 시험용 PostgreSQL 의 관리 연결 문자열(DECISIONS-TECH T-38).
    //   WIZARD_PG_ADMIN 이 있으면 그것(tools/ci/pg-test.sh 의 ci 단계가 준다).
    //   없으면 이 작업 트리의 tools/ci/pg-test.sh up --print 를 불러 컨테이너를 띄우거나 떠 있는 것을 쓴다.
    // Docker 가 없거나 돌고 있지 않으면 그 스크립트의 설명을 실은 예외로 실패한다. 건너뛰지 않는다.
    public static class PgTestServer
    {
        public const string AdminEnv = "WIZARD_PG_ADMIN";

        private static readonly Lazy<Task<string>> Admin = new Lazy<Task<string>>(DiscoverAsync);

        public static Task<string> AdminConnectionStringAsync() => Admin.Value;

        // 스크립트 경로는 '/' 구분으로 넘긴다(Windows 에서 D:/… 꼴. 역슬래시 경로면 스크립트가 lib/common.sh 를 찾지 못한다. T-67 4번)
        public static string ScriptPath => TestRepo.Path("tools/ci/pg-test.sh").Replace(System.IO.Path.DirectorySeparatorChar, '/');

        private static readonly Lazy<string> BashLazy = new Lazy<string>(FindBash);

        // 스크립트를 띄우는 bash. 순서는 DECISIONS-TECH T-67 3번과 같다(tools/Wizard.Ci 의 BashLocator 를 이 어셈블리는 참조하지 않는다):
        //   WIZARD_BASH(주어졌는데 없으면 실패) > Windows 면 Git for Windows 의 bin/bash.exe(`git --exec-path` 에서 위로, 없으면 %ProgramFiles%/Git) > /bin/bash.
        // PATH 의 bash 는 쓰지 않는다(Windows 에서는 WSL 의 System32/bash.exe 가 잡힌다).
        public static string Bash => BashLazy.Value;

        private static string FindBash()
        {
            string? given = Environment.GetEnvironmentVariable("WIZARD_BASH");
            if (!string.IsNullOrEmpty(given))
            {
                if (System.IO.File.Exists(given))
                {
                    return given;
                }
                throw new InvalidOperationException("WIZARD_BASH=" + given + " 이 가리키는 bash 가 없다. 사용자가 할 일: 있는 bash 실행 파일의 경로를 주거나 지운다");
            }
            if (!OperatingSystem.IsWindows())
            {
                return "/bin/bash";
            }
            var psi = new ProcessStartInfo("git") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            psi.ArgumentList.Add("--exec-path");
            try
            {
                using Process? p = Process.Start(psi);
                if (p != null)
                {
                    string exec = p.StandardOutput.ReadToEnd().Trim().Replace(System.IO.Path.DirectorySeparatorChar, '/').TrimEnd('/');
                    p.WaitForExit();
                    int slash;
                    while (p.ExitCode == 0 && (slash = exec.LastIndexOf('/')) > 0)
                    {
                        exec = exec.Substring(0, slash);
                        string c = exec + "/bin/bash.exe";
                        if (System.IO.File.Exists(c))
                        {
                            return c;
                        }
                    }
                }
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // git 이 없다. 아래의 기본 자리를 본다
            }
            string pf = (Environment.GetEnvironmentVariable("ProgramFiles") ?? "C:/Program Files").Replace(System.IO.Path.DirectorySeparatorChar, '/');
            string fallback = pf + "/Git/bin/bash.exe";
            if (System.IO.File.Exists(fallback))
            {
                return fallback;
            }
            throw new InvalidOperationException("Git for Windows 의 bash 가 없다. 사용자가 할 일: Git for Windows 를 설치하거나 WIZARD_BASH 에 bash 실행 파일의 경로를 준다");
        }

        private static async Task<string> DiscoverAsync()
        {
            string? given = Environment.GetEnvironmentVariable(AdminEnv);
            if (!string.IsNullOrEmpty(given))
            {
                return given;
            }
            ScriptResult r = await RunScriptAsync("up", "--print").ConfigureAwait(false);
            if (r.ExitCode != 0)
            {
                throw new InvalidOperationException("시험용 PostgreSQL 을 띄우지 못했다(" + ScriptPath + " up --print, 종료 코드 " + r.ExitCode + "):\n" + r.Stderr);
            }
            string[] lines = r.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (lines.Length == 0)
            {
                throw new InvalidOperationException("pg-test.sh up --print 가 연결 문자열을 내지 않았다:\n" + r.Stderr);
            }
            return lines[lines.Length - 1];
        }

        public static async Task<ScriptResult> RunScriptAsync(params string[] args) => await RunScriptAsync(null, args).ConfigureAwait(false);

        // env: 바꿀 환경 변수(값이 null 이면 지운다)
        public static async Task<ScriptResult> RunScriptAsync(System.Collections.Generic.IDictionary<string, string?>? env, params string[] args)
        {
            var psi = new ProcessStartInfo(Bash)
            {
                // 스크립트의 출력은 UTF-8 이다(기본값은 Windows 의 콘솔 코드 페이지라 한글이 깨진다. T-67 5번)
                StandardOutputEncoding = new System.Text.UTF8Encoding(false),
                StandardErrorEncoding = new System.Text.UTF8Encoding(false),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            psi.ArgumentList.Add(ScriptPath);
            foreach (string a in args)
            {
                psi.ArgumentList.Add(a);
            }
            // ci 실행기의 Docker 확인을 쓰지 않는다(스크립트가 직접 본다)
            psi.Environment.Remove("WIZARD_CI_DLL");
            if (env != null)
            {
                foreach (var kv in env)
                {
                    if (kv.Value == null)
                    {
                        psi.Environment.Remove(kv.Key);
                    }
                    else
                    {
                        psi.Environment[kv.Key] = kv.Value;
                    }
                }
            }
            using Process p = Process.Start(psi) ?? throw new InvalidOperationException("could not start " + Bash);
            Task<string> stdout = p.StandardOutput.ReadToEndAsync();
            Task<string> stderr = p.StandardError.ReadToEndAsync();
            await p.WaitForExitAsync().ConfigureAwait(false);
            return new ScriptResult(p.ExitCode, await stdout.ConfigureAwait(false), await stderr.ConfigureAwait(false));
        }
    }
}

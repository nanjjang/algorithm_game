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

        public static string ScriptPath => TestRepo.Path("tools/ci/pg-test.sh");

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
            var psi = new ProcessStartInfo("/bin/bash")
            {
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
            using Process p = Process.Start(psi) ?? throw new InvalidOperationException("could not start /bin/bash");
            Task<string> stdout = p.StandardOutput.ReadToEndAsync();
            Task<string> stderr = p.StandardError.ReadToEndAsync();
            await p.WaitForExitAsync().ConfigureAwait(false);
            return new ScriptResult(p.ExitCode, await stdout.ConfigureAwait(false), await stderr.ConfigureAwait(false));
        }
    }
}

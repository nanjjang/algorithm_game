using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace Wizard.TaskGraph
{
    // 자식 프로세스 실행. 셸을 거치지 않는다(zsh 의 git 래퍼가 깨져 있다. CLAUDE.md 9절).
    public static class Proc
    {
        public static ProcResult Run(string file, IEnumerable<string> args, string? workingDirectory = null, IReadOnlyDictionary<string, string>? env = null)
        {
            var psi = new ProcessStartInfo(file)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = new UTF8Encoding(false),
                StandardErrorEncoding = new UTF8Encoding(false),
            };
            if (workingDirectory != null)
            {
                psi.WorkingDirectory = workingDirectory;
            }
            foreach (string a in args)
            {
                psi.ArgumentList.Add(a);
            }
            psi.Environment["GIT_TERMINAL_PROMPT"] = "0";
            psi.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
            psi.Environment["DOTNET_NOLOGO"] = "1";
            if (env != null)
            {
                foreach (KeyValuePair<string, string> kv in env)
                {
                    psi.Environment[kv.Key] = kv.Value;
                }
            }
            Process p;
            try
            {
                p = Process.Start(psi) ?? throw new TaskGraphException($"{file} 을 띄우지 못했다");
            }
            catch (System.ComponentModel.Win32Exception e)
            {
                throw new TaskGraphException($"{file} 을 띄우지 못했다: {e.Message}");
            }
            using (p)
            {
                Task<string> stdout = p.StandardOutput.ReadToEndAsync();
                Task<string> stderr = p.StandardError.ReadToEndAsync();
                p.WaitForExit();
                return new ProcResult(p.ExitCode, stdout.GetAwaiter().GetResult(), stderr.GetAwaiter().GetResult());
            }
        }
    }
}

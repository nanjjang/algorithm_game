using System;
using System.IO;

namespace Wizard.TaskGraph.Tests
{
    // 시험이 셸 명령을 띄울 bash. 순서는 DECISIONS-TECH T-67 3번과 같다(tools/Wizard.Ci 의 BashLocator 를 이 어셈블리는 참조하지 않는다):
    //   WIZARD_BASH(주어졌는데 없으면 실패) > Windows 면 Git for Windows 의 bin/bash.exe(`git --exec-path` 에서 위로, 없으면 %ProgramFiles%/Git) > /bin/bash
    // PATH 의 bash 는 쓰지 않는다(Windows 에서는 WSL 의 System32/bash.exe 가 잡힌다).
    internal static class TestShell
    {
        private static readonly Lazy<string> Found = new Lazy<string>(Locate);

        public static string Bash => Found.Value;

        // bash -c <명령>
        public static string[] Command(string script) => new[] { Bash, "-c", script };

        private static string Locate()
        {
            string? given = Environment.GetEnvironmentVariable("WIZARD_BASH");
            if (!string.IsNullOrEmpty(given))
            {
                if (File.Exists(given))
                {
                    return given;
                }
                throw new InvalidOperationException("WIZARD_BASH=" + given + " 이 가리키는 bash 가 없다");
            }
            if (!OperatingSystem.IsWindows())
            {
                return "/bin/bash";
            }
            ProcResult r = Proc.Run("git", new[] { "--exec-path" });
            if (r.ExitCode == 0)
            {
                string dir = r.Stdout.Trim().Replace('\\', '/').TrimEnd('/');
                int slash;
                while ((slash = dir.LastIndexOf('/')) > 0)
                {
                    dir = dir.Substring(0, slash);
                    string c = dir + "/bin/bash.exe";
                    if (File.Exists(c))
                    {
                        return c;
                    }
                }
            }
            string pf = (Environment.GetEnvironmentVariable("ProgramFiles") ?? "C:/Program Files").Replace('\\', '/');
            string fallback = pf + "/Git/bin/bash.exe";
            if (File.Exists(fallback))
            {
                return fallback;
            }
            throw new InvalidOperationException("Git for Windows 의 bash 가 없다. Git for Windows 를 설치하거나 WIZARD_BASH 를 준다");
        }
    }
}

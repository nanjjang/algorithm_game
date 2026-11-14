using System;
using System.Collections.Generic;
using System.IO;

namespace Wizard.Ci
{
    // 검사 스크립트를 띄울 bash 를 찾는 곳은 여기 하나다(DECISIONS-TECH T-67 3번).
    //   1. WIZARD_BASH. 주어졌는데 그 파일이 없으면 다른 곳을 찾지 않고 실패한다
    //   2. Windows: Git for Windows 의 bin/bash.exe. `git --exec-path` 가 낸 폴더(<Git>/mingw64/libexec/git-core)에서 위로 올라가
    //      bin/bash.exe 가 있는 첫 폴더, 없으면 %ProgramFiles%/Git/bin/bash.exe. usr/bin/bash.exe 가 아니다(bin/ 쪽이 PATH 를 갖춰 준다)
    //   3. 그 밖의 OS: /bin/bash
    // PATH 에서 bash 를 찾지 않는다(Windows 에서는 WSL 을 띄우는 System32/bash.exe 가 먼저 잡힌다).
    // 판정(Locate)은 환경·OS·파일 확인을 인자로 받는 순수 함수다: 시험이 한 기기에서 두 OS 의 가지를 다 본다.
    public static class BashLocator
    {
        public const string EnvVar = "WIZARD_BASH";
        public const string UnixBash = "/bin/bash";

        // 명령 줄 층이 받은 환경 함수로 찾는다(이 기기의 OS 와 파일)
        public static string FromEnv(Func<string, string?> env) => Locate(env, OperatingSystem.IsWindows(), File.Exists, GitExecPath);

        // env: 환경 변수를 읽는 함수, windows: Windows 의 규칙을 쓸지, exists: 파일이 있는지, gitExecPath: `git --exec-path` 의 출력(없으면 null)
        public static string Locate(Func<string, string?> env, bool windows, Func<string, bool> exists, Func<string?> gitExecPath)
        {
            string? given = env(EnvVar);
            if (!string.IsNullOrEmpty(given))
            {
                if (exists(given))
                {
                    return ToSlash(given, windows);
                }
                throw new CiException(EnvVar + "=" + given + " 이 가리키는 bash 실행 파일이 없다. 사용자가 할 일: " + EnvVar +
                                      " 에 있는 bash 실행 파일의 경로를 주거나 지운다");
            }
            if (!windows)
            {
                if (exists(UnixBash))
                {
                    return UnixBash;
                }
                throw new CiException(UnixBash + " 이 없다. 사용자가 할 일: bash 를 설치하거나 " + EnvVar + " 에 bash 실행 파일의 경로를 준다");
            }
            List<string> tried = WindowsCandidates(env, gitExecPath);
            foreach (string c in tried)
            {
                if (exists(c))
                {
                    return c;
                }
            }
            throw new CiException("Git for Windows 의 bash 가 없다(찾아본 곳: " + string.Join(", ", tried) + "). 사용자가 할 일: Git for Windows 를 설치하거나 " +
                                  EnvVar + " 에 bash 실행 파일(예: C:/Program Files/Git/bin/bash.exe)의 경로를 준다");
        }

        // Windows 의 후보(순서대로, '/' 구분): exec-path 에서 위로 올라간 각 폴더의 bin/bash.exe, 그다음 %ProgramFiles%/Git/bin/bash.exe
        public static List<string> WindowsCandidates(Func<string, string?> env, Func<string?> gitExecPath)
        {
            var list = new List<string>();
            string? exec = gitExecPath();
            if (!string.IsNullOrWhiteSpace(exec))
            {
                string dir = exec.Trim().Replace('\\', '/').TrimEnd('/');
                while (dir.Length > 0)
                {
                    int slash = dir.LastIndexOf('/');
                    if (slash <= 0)
                    {
                        break;
                    }
                    dir = dir.Substring(0, slash);
                    if (dir.EndsWith(":", StringComparison.Ordinal))
                    {
                        // 드라이브 뿌리(D:)
                        list.Add(dir + "/bin/bash.exe");
                        break;
                    }
                    list.Add(dir + "/bin/bash.exe");
                }
            }
            string pf = env("ProgramFiles") is string p && p.Length > 0 ? p : "C:/Program Files";
            string fallback = pf.Replace('\\', '/').TrimEnd('/') + "/Git/bin/bash.exe";
            if (!list.Contains(fallback))
            {
                list.Add(fallback);
            }
            return list;
        }

        private static string ToSlash(string path, bool windows) => windows ? path.Replace('\\', '/') : path;

        // `git --exec-path` 의 출력. git 이 없거나 실패하면 null
        public static string? GitExecPath()
        {
            try
            {
                ProcResult r = ProcessRunner.Capture("git", new[] { "--exec-path" }, null, TimeSpan.FromSeconds(30));
                return r.ExitCode == 0 ? r.Stdout.Trim() : null;
            }
            catch (CiException)
            {
                return null;
            }
        }

        // 스크립트와 환경 변수에 넘기는 경로의 꼴(T-67 4번): Windows 에서는 '\' 를 '/' 로(D:/…). 맥에서는 글자가 그대로다
        public static string SlashPath(string path) => SlashPath(path, OperatingSystem.IsWindows());

        public static string SlashPath(string path, bool windows) => windows ? path.Replace('\\', '/') : path;
    }
}

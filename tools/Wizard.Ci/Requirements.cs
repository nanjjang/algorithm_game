using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Wizard.Ci
{
    // 검사에 필요한 것이 기기에 있는가(PIPELINE 4.1·4.6, T-38). 없으면 검사는 건너뛰지 않는다: 무엇이 없는지와 사용자가 할 일을 적고 실패한다.
    // 에이전트는 설치하지 않는다(sudo 도 쓰지 않는다). 찾는 곳은 환경 변수로 바꿀 수 있다(테스트와 사용자 폴더 설치):
    //   unity       UNITY_EDITOR(편집기 실행 파일) 또는 WIZARD_UNITY_HUB_EDITORS(기본 /Applications/Unity/Hub/Editor)/<판>/Unity.app
    //               판은 unity/ProjectSettings/ProjectVersion.txt 의 m_EditorVersion
    //   il2cpp      그 편집기의 PlaybackEngines/MacStandaloneSupport/Variations/*il2cpp*
    //   x64-dotnet  DOTNET_ROOT_X64(기본 /usr/local/share/dotnet/x64)/dotnet 이 Microsoft.NETCore.App 의 global.json 주 판 런타임을 가진다
    //   docker      WIZARD_DOCKER(실행 파일) 또는 PATH, /usr/local/bin, /opt/homebrew/bin, Docker.app 안의 docker 가 있고 `docker info` 가 된다
    public static class Requirements
    {
        public static readonly string[] Known = { "unity", "il2cpp", "x64-dotnet", "docker" };

        public const string HubInstallIl2Cpp =
            "\"/Applications/Unity Hub.app/Contents/MacOS/Unity Hub\" -- --headless install-modules --version {0} -m mac-il2cpp";

        // 없으면 사용자에게 보일 한 줄(무엇이 없는지 + 할 일). 있으면 null
        public static string? Check(string what, string root, Func<string, string?> env)
        {
            switch (what)
            {
                case "unity": return CheckUnity(root, env);
                case "il2cpp": return CheckIl2Cpp(root, env);
                case "x64-dotnet": return CheckX64Dotnet(root, env);
                case "docker": return CheckDocker(env);
                default: throw new CiException("알 수 없는 요구 '" + what + "' (" + string.Join("|", Known) + ")");
            }
        }

        public static string UnityVersion(string root)
        {
            string file = Path.Combine(root, "unity", "ProjectSettings", "ProjectVersion.txt");
            if (!File.Exists(file))
            {
                throw new CiException("Unity 프로젝트의 판을 모른다: " + file + " 이 없다");
            }
            Match m = Regex.Match(File.ReadAllText(file), "^m_EditorVersion:\\s*(\\S+)", RegexOptions.Multiline | RegexOptions.CultureInvariant);
            if (!m.Success)
            {
                throw new CiException(file + " 에 m_EditorVersion 이 없다");
            }
            return m.Groups[1].Value;
        }

        // 편집기 실행 파일 경로(있든 없든)
        public static string UnityEditorPath(string root, Func<string, string?> env)
        {
            string? editor = env("UNITY_EDITOR");
            if (!string.IsNullOrEmpty(editor))
            {
                return editor;
            }
            string hub = env("WIZARD_UNITY_HUB_EDITORS") is string h && h.Length > 0 ? h : "/Applications/Unity/Hub/Editor";
            return Path.Combine(hub, UnityVersion(root), "Unity.app", "Contents", "MacOS", "Unity");
        }

        private static string? CheckUnity(string root, Func<string, string?> env)
        {
            string path = UnityEditorPath(root, env);
            if (File.Exists(path))
            {
                return null;
            }
            return "Unity " + UnityVersion(root) + " 편집기가 없다(" + path + "). 사용자가 할 일: Unity Hub 로 " + UnityVersion(root) +
                   " 을 설치하거나, 다른 곳에 있으면 UNITY_EDITOR 에 편집기 실행 파일 경로를 준다";
        }

        private static string? CheckIl2Cpp(string root, Func<string, string?> env)
        {
            string? unity = CheckUnity(root, env);
            if (unity != null)
            {
                return unity;
            }
            // .../Unity.app/Contents/MacOS/Unity → .../Unity.app/Contents/PlaybackEngines/MacStandaloneSupport/Variations
            string contents = Path.GetDirectoryName(Path.GetDirectoryName(UnityEditorPath(root, env))!)!;
            string variations = Path.Combine(contents, "PlaybackEngines", "MacStandaloneSupport", "Variations");
            if (Directory.Exists(variations) && Directory.GetDirectories(variations, "*il2cpp*").Length > 0)
            {
                return null;
            }
            return "Unity " + UnityVersion(root) + " 에 mac-il2cpp 모듈이 없다(" + variations + " 에 *il2cpp* 가 없다). 사용자가 할 일: " +
                   string.Format(System.Globalization.CultureInfo.InvariantCulture, HubInstallIl2Cpp, UnityVersion(root)) + " (sudo 없이 된다)";
        }

        private static string? CheckX64Dotnet(string root, Func<string, string?> env)
        {
            string major = DotnetMajor(root);
            string dir = env("DOTNET_ROOT_X64") is string d && d.Length > 0 ? d : "/usr/local/share/dotnet/x64";
            string exe = Path.Combine(dir, "dotnet");
            string todo = " 사용자가 할 일: dotnet-install.sh --architecture x64 --runtime dotnet --channel " + major +
                          ".0 --install-dir <폴더> 로 사용자 폴더에 설치하고 DOTNET_ROOT_X64=<폴더> 를 환경에 둔다(PIPELINE 4.1)";
            if (!File.Exists(exe))
            {
                return "x64 dotnet 런타임이 없다(" + exe + ")." + todo;
            }
            ProcResult r;
            try
            {
                r = ProcessRunner.Capture(exe, new[] { "--list-runtimes" }, null, TimeSpan.FromSeconds(60));
            }
            catch (CiException e)
            {
                return "x64 dotnet(" + exe + ")을 돌리지 못했다: " + e.Message + "." + todo;
            }
            if (r.ExitCode != 0)
            {
                return "x64 dotnet(" + exe + ") --list-runtimes 가 " + r.ExitCode + " 로 끝났다: " + r.FirstErrorLine() + "." + todo;
            }
            if (!Regex.IsMatch(r.Stdout, "^Microsoft\\.NETCore\\.App " + Regex.Escape(major) + "\\.", RegexOptions.Multiline | RegexOptions.CultureInvariant))
            {
                return "x64 dotnet(" + exe + ")에 Microsoft.NETCore.App " + major + ".x 런타임이 없다." + todo;
            }
            return null;
        }

        // global.json 의 sdk.version 의 주 판("10.0.201" → "10")
        public static string DotnetMajor(string root)
        {
            string file = Path.Combine(root, "global.json");
            Match m = File.Exists(file)
                ? Regex.Match(File.ReadAllText(file), "\"version\"\\s*:\\s*\"(?<major>[0-9]+)\\.", RegexOptions.CultureInvariant)
                : Match.Empty;
            if (!m.Success)
            {
                throw new CiException("global.json 에서 SDK 판을 읽지 못했다: " + file);
            }
            return m.Groups["major"].Value;
        }

        private static string? CheckDocker(Func<string, string?> env)
        {
            string? exe = FindDocker(env);
            if (exe == null)
            {
                return "Docker 가 없다(PATH, /usr/local/bin, /opt/homebrew/bin, /Applications/Docker.app 에서 docker 를 찾지 못했다). " +
                       "사용자가 할 일: Docker Desktop 을 설치하고 띄운다(서버 통합 테스트의 PostgreSQL 이 Docker 에서 돈다. T-38)";
            }
            ProcResult r;
            try
            {
                r = ProcessRunner.Capture(exe, new[] { "info", "--format", "{{.ServerVersion}}" }, null, TimeSpan.FromSeconds(30));
            }
            catch (CiException e)
            {
                return "Docker(" + exe + ")를 돌리지 못했다: " + e.Message + ". 사용자가 할 일: Docker Desktop 을 띄운다(open -a Docker)";
            }
            if (r.ExitCode != 0 || r.Stdout.Trim().Length == 0)
            {
                return "Docker 가 돌고 있지 않다(" + exe + " info: " + (r.TimedOut ? "시간 제한" : r.FirstErrorLine()) + "). " +
                       "사용자가 할 일: Docker Desktop 을 띄운다(open -a Docker) 뒤 다시 돌린다";
            }
            return null;
        }

        private static string? FindDocker(Func<string, string?> env)
        {
            string? given = env("WIZARD_DOCKER");
            if (!string.IsNullOrEmpty(given))
            {
                return File.Exists(given) ? given : null;
            }
            var candidates = new List<string>();
            string? path = env("PATH");
            if (path != null)
            {
                foreach (string p in path.Split(':'))
                {
                    if (p.Length > 0)
                    {
                        candidates.Add(Path.Combine(p, "docker"));
                    }
                }
            }
            candidates.Add("/usr/local/bin/docker");
            candidates.Add("/opt/homebrew/bin/docker");
            candidates.Add("/Applications/Docker.app/Contents/Resources/bin/docker");
            foreach (string c in candidates)
            {
                if (File.Exists(c))
                {
                    return c;
                }
            }
            return null;
        }
    }
}

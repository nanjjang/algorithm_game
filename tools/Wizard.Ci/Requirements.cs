using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace Wizard.Ci
{
    // 검사에 필요한 것이 기기에 있는가(PIPELINE 4.1·4.6, T-38, T-67 7번). 없으면 검사는 건너뛰지 않는다: 무엇이 없는지와 사용자가 할 일을
    // 그 OS 의 말로 적고 실패한다. 에이전트는 설치하지 않는다(sudo 도 쓰지 않는다). 찾는 곳은 환경 변수로 바꿀 수 있다(테스트와 사용자 폴더 설치).
    //   unity       UNITY_EDITOR(편집기 실행 파일) > WIZARD_UNITY_HUB_EDITORS(기본: 맥 /Applications/Unity/Hub/Editor,
    //               Windows %ProgramFiles%/Unity/Hub/Editor) 아래 <판>/Unity.app/Contents/MacOS/Unity(맥) 또는 <판>/Editor/Unity.exe(Windows).
    //               판은 unity/ProjectSettings/ProjectVersion.txt 의 m_EditorVersion
    //   il2cpp      맥: 그 편집기의 Contents/PlaybackEngines/MacStandaloneSupport/Variations/*il2cpp* (mac-il2cpp 모듈)
    //               Windows: <판>/Editor/Data/PlaybackEngines/windowsstandalonesupport/Variations/*il2cpp* (windows-il2cpp 모듈)
    //   x64-dotnet  arm64 기기: DOTNET_ROOT_X64(기본 /usr/local/share/dotnet/x64)/dotnet. x64 기기: DOTNET_ROOT_X64 가 주어지면 그 dotnet,
    //               아니면 PATH 의 dotnet. 어느 쪽이든 Microsoft.NETCore.App <global.json 의 주 판>.x 런타임을 가져야 한다
    //   docker      WIZARD_DOCKER(실행 파일) > PATH(그 OS 의 구분자. Windows 는 docker.exe 도) > OS 별 기본 자리. `docker info` 가 되어야 한다
    // 판정 함수는 OS 와 아키텍처를 인자로 받는다(시험이 한 기기에서 두 OS 의 경로 짓기와 문구를 본다). 실제 파일 확인과 실행은 이 기기에서 한다.
    // Windows 에서 실행 파일 이름은 <이름>.exe, <이름>.cmd, <이름>.bat 순서로 찾는다(PATHEXT 와 같은 뜻. 시험의 가짜 실행 파일은 .cmd 다).
    public static class Requirements
    {
        public static readonly string[] Known = { "unity", "il2cpp", "x64-dotnet", "docker" };

        public const string MacHubInstallIl2Cpp =
            "\"/Applications/Unity Hub.app/Contents/MacOS/Unity Hub\" -- --headless install-modules --version {0} -m mac-il2cpp";

        public const string WindowsHubInstallIl2Cpp =
            "\"C:/Program Files/Unity Hub/Unity Hub.exe\" -- --headless install-modules --version {0} -m windows-il2cpp";

        // 없으면 사용자에게 보일 한 줄(무엇이 없는지 + 할 일). 있으면 null. 이 기기의 OS 와 아키텍처로 판정한다
        public static string? Check(string what, string root, Func<string, string?> env) =>
            Check(what, root, env, HostOsInfo.Current, RuntimeInformation.OSArchitecture);

        public static string? Check(string what, string root, Func<string, string?> env, HostOs os, Architecture arch)
        {
            switch (what)
            {
                case "unity": return CheckUnity(root, env, os);
                case "il2cpp": return CheckIl2Cpp(root, env, os);
                case "x64-dotnet": return CheckX64Dotnet(root, env, os, arch);
                case "docker": return CheckDocker(env, os);
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

        // '/' 로 잇는다(두 OS 의 경로를 한 기기에서 지어 보는 시험이 같은 글자를 얻게). Windows 는 '/' 경로를 그대로 받는다
        private static string Join(string dir, params string[] parts)
        {
            string s = dir.Replace('\\', '/').TrimEnd('/');
            foreach (string p in parts)
            {
                s += "/" + p;
            }
            return s;
        }

        private static string ProgramFiles(Func<string, string?> env) =>
            env("ProgramFiles") is string p && p.Length > 0 ? p.Replace('\\', '/') : "C:/Program Files";

        // Hub 가 편집기를 까는 폴더의 기본값
        public static string DefaultHubEditors(HostOs os, Func<string, string?> env) =>
            os == HostOs.Windows ? Join(ProgramFiles(env), "Unity", "Hub", "Editor") : "/Applications/Unity/Hub/Editor";

        // 편집기 실행 파일 경로(있든 없든)
        public static string UnityEditorPath(string root, Func<string, string?> env) => UnityEditorPath(root, env, HostOsInfo.Current);

        public static string UnityEditorPath(string root, Func<string, string?> env, HostOs os)
        {
            string? editor = env("UNITY_EDITOR");
            if (!string.IsNullOrEmpty(editor))
            {
                return editor;
            }
            string hub = env("WIZARD_UNITY_HUB_EDITORS") is string h && h.Length > 0 ? h : DefaultHubEditors(os, env);
            return os == HostOs.Windows
                ? Join(hub, UnityVersion(root), "Editor", "Unity.exe")
                : Join(hub, UnityVersion(root), "Unity.app", "Contents", "MacOS", "Unity");
        }

        // il2cpp 변형이 놓이는 폴더(편집기 경로에서 짓는다)
        public static string Il2CppVariationsDir(string editorPath, HostOs os)
        {
            string editorDir = Path.GetDirectoryName(editorPath.Replace('\\', '/'))!.Replace('\\', '/');
            if (os == HostOs.Windows)
            {
                // …/<판>/Editor/Unity.exe → …/<판>/Editor/Data/PlaybackEngines/windowsstandalonesupport/Variations
                return Join(editorDir, "Data", "PlaybackEngines", "windowsstandalonesupport", "Variations");
            }
            // …/Unity.app/Contents/MacOS/Unity → …/Unity.app/Contents/PlaybackEngines/MacStandaloneSupport/Variations
            string contents = Path.GetDirectoryName(editorDir)!.Replace('\\', '/');
            return Join(contents, "PlaybackEngines", "MacStandaloneSupport", "Variations");
        }

        private static string? CheckUnity(string root, Func<string, string?> env, HostOs os)
        {
            string path = UnityEditorPath(root, env, os);
            if (File.Exists(path))
            {
                return null;
            }
            return "Unity " + UnityVersion(root) + " 편집기가 없다(" + path + "). 사용자가 할 일: Unity Hub 로 " + UnityVersion(root) +
                   " 을 설치하거나, 다른 곳에 있으면 UNITY_EDITOR 에 편집기 실행 파일 경로를 준다";
        }

        private static string? CheckIl2Cpp(string root, Func<string, string?> env, HostOs os)
        {
            string? unity = CheckUnity(root, env, os);
            if (unity != null)
            {
                return unity;
            }
            string variations = Il2CppVariationsDir(UnityEditorPath(root, env, os), os);
            if (Directory.Exists(variations) && Directory.GetDirectories(variations, "*il2cpp*").Length > 0)
            {
                return null;
            }
            return Il2CppMissingMessage(UnityVersion(root), variations, os);
        }

        public static string Il2CppMissingMessage(string version, string variations, HostOs os)
        {
            if (os == HostOs.Windows)
            {
                return "Unity " + version + " 에 windows-il2cpp 모듈이 없다(" + variations + " 에 *il2cpp* 가 없다). 사용자가 할 일: " +
                       string.Format(CultureInfo.InvariantCulture, WindowsHubInstallIl2Cpp, version) +
                       " 로 모듈을 설치하고, Visual Studio 의 C++ 빌드 도구(Desktop development with C++)를 설치한다";
            }
            return "Unity " + version + " 에 mac-il2cpp 모듈이 없다(" + variations + " 에 *il2cpp* 가 없다). 사용자가 할 일: " +
                   string.Format(CultureInfo.InvariantCulture, MacHubInstallIl2Cpp, version) + " (sudo 없이 된다)";
        }

        // 그 OS 에서 실행 파일 이름으로 찾아볼 파일 이름들
        public static string[] ExecutableNames(string name, HostOs os) =>
            os == HostOs.Windows ? new[] { name + ".exe", name + ".cmd", name + ".bat" } : new[] { name };

        private static string? FindIn(string dir, string name, HostOs os)
        {
            foreach (string n in ExecutableNames(name, os))
            {
                string c = Join(dir, n);
                if (File.Exists(c))
                {
                    return c;
                }
            }
            return null;
        }

        // PATH 에서 실행 파일을 찾는다(그 OS 의 구분자로 나눈다)
        public static string? FindOnPath(string name, Func<string, string?> env, HostOs os)
        {
            string? path = env("PATH");
            if (path == null)
            {
                return null;
            }
            foreach (string p in path.Split(HostOsInfo.PathSeparator(os)))
            {
                if (p.Length > 0)
                {
                    string? found = FindIn(p, name, os);
                    if (found != null)
                    {
                        return found;
                    }
                }
            }
            return null;
        }

        private static string? CheckX64Dotnet(string root, Func<string, string?> env, HostOs os, Architecture arch)
        {
            string major = DotnetMajor(root);
            string? given = env("DOTNET_ROOT_X64") is string d && d.Length > 0 ? d : null;
            string todo;
            string? exe;
            string where;
            if (arch == Architecture.X64)
            {
                // x64 기기: 이 기기의 dotnet 이 곧 x64 다(T-67 7번, T-68 7번)
                todo = " 사용자가 할 일: .NET " + major + " 런타임(x64)을 설치한다(.NET SDK " + major + " 에 들어 있다). 다른 곳에 있으면 DOTNET_ROOT_X64=<폴더> 를 환경에 둔다(PIPELINE 4.1)";
                if (given != null)
                {
                    exe = FindIn(given, "dotnet", os);
                    where = Join(given, ExecutableNames("dotnet", os)[0]);
                }
                else
                {
                    exe = FindOnPath("dotnet", env, os);
                    where = "PATH 의 dotnet";
                }
            }
            else
            {
                // arm64 기기(맥): Rosetta 2 로 도는 x64 런타임을 따로 둔다
                todo = " 사용자가 할 일: dotnet-install.sh --architecture x64 --runtime dotnet --channel " + major +
                       ".0 --install-dir <폴더> 로 사용자 폴더에 설치하고 DOTNET_ROOT_X64=<폴더> 를 환경에 둔다(PIPELINE 4.1)";
                string dir = given ?? "/usr/local/share/dotnet/x64";
                exe = FindIn(dir, "dotnet", os);
                where = Join(dir, ExecutableNames("dotnet", os)[0]);
            }
            if (exe == null)
            {
                return "x64 dotnet 런타임이 없다(" + where + ")." + todo;
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
                return "x64 dotnet(" + exe + ") --list-runtimes 가 " + r.ExitCode.ToString(CultureInfo.InvariantCulture) + " 로 끝났다: " + r.FirstErrorLine() + "." + todo;
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

        // Docker 를 띄우라는 안내(OS 별)
        public static string StartDockerHint(HostOs os) =>
            os == HostOs.Windows ? "Docker Desktop 을 띄운다(시작 메뉴의 Docker Desktop)"
            : os == HostOs.MacOS ? "Docker Desktop 을 띄운다(open -a Docker)"
            : "Docker 데몬을 띄운다";

        // Docker 를 찾아본 곳(문구용)
        public static List<string> DockerDefaultLocations(HostOs os, Func<string, string?> env)
        {
            if (os == HostOs.Windows)
            {
                return new List<string> { Join(ProgramFiles(env), "Docker", "Docker", "resources", "bin", "docker.exe") };
            }
            return new List<string> { "/usr/local/bin/docker", "/opt/homebrew/bin/docker", "/Applications/Docker.app/Contents/Resources/bin/docker" };
        }

        public static string DockerMissingMessage(HostOs os, Func<string, string?> env) =>
            "Docker 가 없다(WIZARD_DOCKER, PATH, " + string.Join(", ", DockerDefaultLocations(os, env)) + " 에서 docker 를 찾지 못했다). " +
            "사용자가 할 일: Docker Desktop 을 설치하고 띄운다(서버 통합 테스트의 PostgreSQL 이 Docker 에서 돈다. T-38)";

        private static string? CheckDocker(Func<string, string?> env, HostOs os)
        {
            string? exe = FindDocker(env, os);
            if (exe == null)
            {
                return DockerMissingMessage(os, env);
            }
            ProcResult r;
            try
            {
                r = ProcessRunner.Capture(exe, new[] { "info", "--format", "{{.ServerVersion}}" }, null, TimeSpan.FromSeconds(30));
            }
            catch (CiException e)
            {
                return "Docker(" + exe + ")를 돌리지 못했다: " + e.Message + ". 사용자가 할 일: " + StartDockerHint(os);
            }
            if (r.ExitCode != 0 || r.Stdout.Trim().Length == 0)
            {
                return "Docker 가 돌고 있지 않다(" + exe + " info: " + (r.TimedOut ? "시간 제한" : r.FirstErrorLine()) + "). " +
                       "사용자가 할 일: " + StartDockerHint(os) + " 뒤 다시 돌린다";
            }
            return null;
        }

        public static string? FindDocker(Func<string, string?> env, HostOs os)
        {
            string? given = env("WIZARD_DOCKER");
            if (!string.IsNullOrEmpty(given))
            {
                return File.Exists(given) ? given : null;
            }
            string? onPath = FindOnPath("docker", env, os);
            if (onPath != null)
            {
                return onPath;
            }
            foreach (string c in DockerDefaultLocations(os, env))
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

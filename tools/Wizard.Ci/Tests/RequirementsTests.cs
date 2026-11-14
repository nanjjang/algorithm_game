using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Xunit;

namespace Wizard.Ci.Tests
{
    // 필요한 것이 없을 때 검사는 건너뛰지 않고 무엇이 없는지와 사용자가 할 일을 적고 실패한다(PIPELINE 4.1·4.6, T-38).
    // 판정은 OS 별이고(T-67 7번) OS 를 인자로 받는다: 경로 짓기와 문구는 한 기기에서 두 OS 의 가지를 다 본다.
    // 가짜 실행 파일은 그 기기에서 돌아야 하므로 TempGit.WriteFakeProgram(맥·리눅스 #!/bin/bash, Windows .cmd)으로 만든다.
    public sealed class RequirementsTests
    {
        private static Func<string, string?> Env(Dictionary<string, string?> d) => k => d.TryGetValue(k, out string? v) ? v : null;

        private static TempGit RepoWithUnityVersion()
        {
            TempGit g = TempGit.Create();
            g.Write("unity/ProjectSettings/ProjectVersion.txt", "m_EditorVersion: 6000.3.20f1\nm_EditorVersionWithRevision: 6000.3.20f1 (abc)\n");
            g.Write("global.json", "{ \"sdk\": { \"version\": \"10.0.201\", \"rollForward\": \"latestPatch\" } }\n");
            return g;
        }

        [Fact]
        public void MissingUnityEditor_SaysWhatIsMissingAndWhatToDo()
        {
            using TempGit g = RepoWithUnityVersion();
            foreach (HostOs os in new[] { HostOs.MacOS, HostOs.Windows })
            {
                string? msg = Requirements.Check("unity", g.Dir, Env(new Dictionary<string, string?> { ["WIZARD_UNITY_HUB_EDITORS"] = g.PathOf("no-hub") }),
                    os, Architecture.X64);
                Assert.NotNull(msg);
                Assert.Contains("6000.3.20f1", msg, StringComparison.Ordinal);
                Assert.Contains("사용자가 할 일", msg, StringComparison.Ordinal);
            }
        }

        // 맥 꼴의 Hub 폴더(<hub>/<판>/Unity.app/Contents/MacOS/Unity)와 mac-il2cpp
        [Fact]
        public void Il2Cpp_MissingModule_GivesTheHubInstallCommand_AndPresentModulePasses()
        {
            using TempGit g = RepoWithUnityVersion();
            string hub = g.PathOf("hub");
            string contents = Path.Combine(hub, "6000.3.20f1", "Unity.app", "Contents");
            g.Write("hub/6000.3.20f1/Unity.app/Contents/MacOS/Unity", "#!/bin/bash\n", executable: true);
            Directory.CreateDirectory(Path.Combine(contents, "PlaybackEngines", "MacStandaloneSupport", "Variations", "macos_arm64_player_development_mono"));
            var env = Env(new Dictionary<string, string?> { ["WIZARD_UNITY_HUB_EDITORS"] = hub });

            Assert.Null(Requirements.Check("unity", g.Dir, env, HostOs.MacOS, Architecture.Arm64));
            string? msg = Requirements.Check("il2cpp", g.Dir, env, HostOs.MacOS, Architecture.Arm64);
            Assert.NotNull(msg);
            Assert.Contains("mac-il2cpp", msg, StringComparison.Ordinal);
            Assert.Contains("install-modules --version 6000.3.20f1 -m mac-il2cpp", msg, StringComparison.Ordinal);

            Directory.CreateDirectory(Path.Combine(contents, "PlaybackEngines", "MacStandaloneSupport", "Variations", "macos_arm64_player_development_il2cpp"));
            Assert.Null(Requirements.Check("il2cpp", g.Dir, env, HostOs.MacOS, Architecture.Arm64));
        }

        // Windows 꼴의 Hub 폴더(<hub>/<판>/Editor/Unity.exe)와 windows-il2cpp(이 PC 에는 *_mono 변형뿐이다)
        [Fact]
        public void WindowsHubLayout_UnityPasses_Il2CppNeedsTheWindowsModule_AndItsVariationPasses()
        {
            using TempGit g = RepoWithUnityVersion();
            string hub = g.PathOf("winhub");
            g.Write("winhub/6000.3.20f1/Editor/Unity.exe", "MZ");
            string variations = Path.Combine(hub, "6000.3.20f1", "Editor", "Data", "PlaybackEngines", "windowsstandalonesupport", "Variations");
            Directory.CreateDirectory(Path.Combine(variations, "win64_player_development_mono"));
            Directory.CreateDirectory(Path.Combine(variations, "win64_player_nondevelopment_mono"));
            var env = Env(new Dictionary<string, string?> { ["WIZARD_UNITY_HUB_EDITORS"] = hub });

            Assert.Null(Requirements.Check("unity", g.Dir, env, HostOs.Windows, Architecture.X64));
            string? msg = Requirements.Check("il2cpp", g.Dir, env, HostOs.Windows, Architecture.X64);
            Assert.NotNull(msg);
            Assert.Contains("windows-il2cpp", msg, StringComparison.Ordinal);
            Assert.Contains("\"C:/Program Files/Unity Hub/Unity Hub.exe\" -- --headless install-modules --version 6000.3.20f1 -m windows-il2cpp", msg, StringComparison.Ordinal);
            Assert.Contains("C++ 빌드 도구", msg, StringComparison.Ordinal);
            Assert.DoesNotContain("mac-il2cpp", msg, StringComparison.Ordinal);

            Directory.CreateDirectory(Path.Combine(variations, "win64_player_development_il2cpp"));
            Assert.Null(Requirements.Check("il2cpp", g.Dir, env, HostOs.Windows, Architecture.X64));

            // 맥 꼴로 보면 같은 폴더에 편집기가 없다(두 OS 의 경로가 섞이지 않는다)
            Assert.NotNull(Requirements.Check("unity", g.Dir, env, HostOs.MacOS, Architecture.Arm64));
        }

        [Fact]
        public void EditorPaths_AreBuiltPerOs_FromTheHubDefaults()
        {
            using TempGit g = RepoWithUnityVersion();
            Assert.Equal("/Applications/Unity/Hub/Editor/6000.3.20f1/Unity.app/Contents/MacOS/Unity",
                Requirements.UnityEditorPath(g.Dir, _ => null, HostOs.MacOS));
            Assert.Equal("C:/Program Files/Unity/Hub/Editor/6000.3.20f1/Editor/Unity.exe",
                Requirements.UnityEditorPath(g.Dir, Env(new Dictionary<string, string?> { ["ProgramFiles"] = "C:\\Program Files" }), HostOs.Windows));
            Assert.Equal("/x/Unity.app/Contents/PlaybackEngines/MacStandaloneSupport/Variations",
                Requirements.Il2CppVariationsDir("/x/Unity.app/Contents/MacOS/Unity", HostOs.MacOS));
            Assert.Equal("D:/U/6000.3.20f1/Editor/Data/PlaybackEngines/windowsstandalonesupport/Variations",
                Requirements.Il2CppVariationsDir("D:\\U\\6000.3.20f1\\Editor\\Unity.exe", HostOs.Windows));
            // UNITY_EDITOR 가 먼저다
            Assert.Equal("E:/u.exe", Requirements.UnityEditorPath(g.Dir, Env(new Dictionary<string, string?> { ["UNITY_EDITOR"] = "E:/u.exe" }), HostOs.Windows));
        }

        [Fact]
        public void X64Dotnet_MissingHostOrRuntime_Fails_AndTheRightRuntimePasses()
        {
            using TempGit g = RepoWithUnityVersion();
            string? missing = Requirements.Check("x64-dotnet", g.Dir, Env(new Dictionary<string, string?> { ["DOTNET_ROOT_X64"] = g.PathOf("nope") }));
            Assert.NotNull(missing);
            Assert.Contains("x64 dotnet 런타임이 없다", missing, StringComparison.Ordinal);
            Assert.Contains("DOTNET_ROOT_X64", missing, StringComparison.Ordinal);

            g.WriteFakeProgram("old/dotnet", new[] { "Microsoft.NETCore.App 8.0.10 [/x]" }, Array.Empty<string>(), 0);
            string? old = Requirements.Check("x64-dotnet", g.Dir, Env(new Dictionary<string, string?> { ["DOTNET_ROOT_X64"] = g.PathOf("old") }));
            Assert.NotNull(old);
            Assert.Contains("Microsoft.NETCore.App 10.x", old, StringComparison.Ordinal);

            g.WriteFakeProgram("good/dotnet", new[] { "Microsoft.AspNetCore.App 10.0.1 [/x]", "Microsoft.NETCore.App 10.0.1 [/x]" }, Array.Empty<string>(), 0);
            Assert.Null(Requirements.Check("x64-dotnet", g.Dir, Env(new Dictionary<string, string?> { ["DOTNET_ROOT_X64"] = g.PathOf("good") })));
        }

        // x64 기기: DOTNET_ROOT_X64 가 없으면 PATH 의 dotnet 을 본다(그 OS 의 PATH 구분자로)
        [Fact]
        public void X64Dotnet_OnAnX64Host_UsesTheDotnetOnPath()
        {
            using TempGit g = RepoWithUnityVersion();
            g.WriteFakeProgram("p2/dotnet", new[] { "Microsoft.NETCore.App 10.0.5 [/x]" }, Array.Empty<string>(), 0);
            HostOs os = HostOsInfo.Current;
            string path = g.PathOf("p1") + HostOsInfo.PathSeparator(os) + g.PathOf("p2");
            Assert.Null(Requirements.Check("x64-dotnet", g.Dir, Env(new Dictionary<string, string?> { ["PATH"] = path }), os, Architecture.X64));

            string? none = Requirements.Check("x64-dotnet", g.Dir, Env(new Dictionary<string, string?> { ["PATH"] = g.PathOf("p1") }), os, Architecture.X64);
            Assert.NotNull(none);
            Assert.Contains("x64 dotnet 런타임이 없다(PATH 의 dotnet)", none, StringComparison.Ordinal);
            Assert.Contains(".NET 10 런타임(x64)", none, StringComparison.Ordinal);
        }

        [Fact]
        public void Docker_MissingOrNotRunning_Fails_WithTheUsersAction()
        {
            using TempGit g = RepoWithUnityVersion();
            string? missing = Requirements.Check("docker", g.Dir, Env(new Dictionary<string, string?> { ["WIZARD_DOCKER"] = g.PathOf("no-docker") }));
            Assert.NotNull(missing);
            Assert.Contains("Docker 가 없다", missing, StringComparison.Ordinal);

            string down = g.WriteFakeProgram("bin/docker", Array.Empty<string>(), new[] { "Cannot connect to the Docker daemon" }, 1);
            string? downMsg = Requirements.Check("docker", g.Dir, Env(new Dictionary<string, string?> { ["WIZARD_DOCKER"] = down }));
            Assert.NotNull(downMsg);
            Assert.Contains("돌고 있지 않다", downMsg, StringComparison.Ordinal);
            Assert.Contains(Requirements.StartDockerHint(HostOsInfo.Current), downMsg, StringComparison.Ordinal);

            string ok = g.WriteFakeProgram("ok/docker", new[] { "27.0.1" }, Array.Empty<string>(), 0);
            Assert.Null(Requirements.Check("docker", g.Dir, Env(new Dictionary<string, string?> { ["WIZARD_DOCKER"] = ok })));
        }

        [Fact]
        public void Docker_Wording_AndDefaultLocations_ArePerOs()
        {
            Assert.Contains("open -a Docker", Requirements.StartDockerHint(HostOs.MacOS), StringComparison.Ordinal);
            Assert.DoesNotContain("open -a", Requirements.StartDockerHint(HostOs.Windows), StringComparison.Ordinal);
            Assert.Contains("Docker Desktop", Requirements.StartDockerHint(HostOs.Windows), StringComparison.Ordinal);

            Assert.Equal(new[] { "C:/Program Files/Docker/Docker/resources/bin/docker.exe" },
                Requirements.DockerDefaultLocations(HostOs.Windows, Env(new Dictionary<string, string?> { ["ProgramFiles"] = "C:\\Program Files" })));
            Assert.Contains("/Applications/Docker.app/Contents/Resources/bin/docker", Requirements.DockerDefaultLocations(HostOs.MacOS, _ => null));
            Assert.Contains("Docker 가 없다", Requirements.DockerMissingMessage(HostOs.Windows, _ => null), StringComparison.Ordinal);
            Assert.Contains("Docker 가 없다", Requirements.DockerMissingMessage(HostOs.MacOS, _ => null), StringComparison.Ordinal);

            Assert.Equal(new[] { "docker.exe", "docker.cmd", "docker.bat" }, Requirements.ExecutableNames("docker", HostOs.Windows));
            Assert.Equal(new[] { "docker" }, Requirements.ExecutableNames("docker", HostOs.MacOS));
            Assert.Equal(';', HostOsInfo.PathSeparator(HostOs.Windows));
            Assert.Equal(':', HostOsInfo.PathSeparator(HostOs.MacOS));
        }

        [Fact]
        public void Docker_IsFoundOnPath_SplitByTheOsSeparator()
        {
            using TempGit g = RepoWithUnityVersion();
            string fake = g.WriteFakeProgram("pb/docker", new[] { "27.0.1" }, Array.Empty<string>(), 0);
            HostOs os = HostOsInfo.Current;
            string path = g.PathOf("pa") + HostOsInfo.PathSeparator(os) + g.PathOf("pb");
            string? found = Requirements.FindDocker(Env(new Dictionary<string, string?> { ["PATH"] = path }), os);
            Assert.NotNull(found);
            Assert.Equal(Path.GetFullPath(fake), Path.GetFullPath(found!));
            Assert.Null(Requirements.Check("docker", g.Dir, Env(new Dictionary<string, string?> { ["PATH"] = path }), os, RuntimeInformation.OSArchitecture));
        }

        [Fact]
        public void UnknownRequirement_IsAUsageError()
        {
            using TempGit g = RepoWithUnityVersion();
            Assert.Throws<CiException>(() => Requirements.Check("gpu", g.Dir, _ => null));
        }

        [Fact]
        public void RequireCommand_ExitsOneAndPrintsTheMessage()
        {
            using TempGit g = RepoWithUnityVersion();
            var stdout = new StringWriter();
            var stderr = new StringWriter();
            // 명령 줄 층은 환경을 인자(env)로 받는다. 프로세스 환경을 바꾸지 않는다(같이 도는 다른 시험에 새지 않게)
            int code = Program.Run(new[] { "require", "x64-dotnet", "--root", g.Dir }, stdout, stderr,
                Env(new Dictionary<string, string?> { ["DOTNET_ROOT_X64"] = g.PathOf("nope") }));
            Assert.Equal(1, code);
            Assert.StartsWith("FAIL  x64 dotnet 런타임이 없다", stderr.ToString(), StringComparison.Ordinal);
        }
    }
}

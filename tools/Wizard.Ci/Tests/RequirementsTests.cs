using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace Wizard.Ci.Tests
{
    // 필요한 것이 없을 때 검사는 건너뛰지 않고 무엇이 없는지와 사용자가 할 일을 적고 실패한다(PIPELINE 4.1·4.6, T-38)
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
            string? msg = Requirements.Check("unity", g.Dir, Env(new Dictionary<string, string?> { ["WIZARD_UNITY_HUB_EDITORS"] = g.PathOf("no-hub") }));
            Assert.NotNull(msg);
            Assert.Contains("6000.3.20f1", msg, StringComparison.Ordinal);
            Assert.Contains("사용자가 할 일", msg, StringComparison.Ordinal);
        }

        [Fact]
        public void Il2Cpp_MissingModule_GivesTheHubInstallCommand_AndPresentModulePasses()
        {
            using TempGit g = RepoWithUnityVersion();
            string hub = g.PathOf("hub");
            string contents = Path.Combine(hub, "6000.3.20f1", "Unity.app", "Contents");
            g.Write("hub/6000.3.20f1/Unity.app/Contents/MacOS/Unity", "#!/bin/bash\n", executable: true);
            Directory.CreateDirectory(Path.Combine(contents, "PlaybackEngines", "MacStandaloneSupport", "Variations", "macos_arm64_player_development_mono"));
            var env = Env(new Dictionary<string, string?> { ["WIZARD_UNITY_HUB_EDITORS"] = hub });

            Assert.Null(Requirements.Check("unity", g.Dir, env));
            string? msg = Requirements.Check("il2cpp", g.Dir, env);
            Assert.NotNull(msg);
            Assert.Contains("mac-il2cpp", msg, StringComparison.Ordinal);
            Assert.Contains("install-modules --version 6000.3.20f1 -m mac-il2cpp", msg, StringComparison.Ordinal);

            Directory.CreateDirectory(Path.Combine(contents, "PlaybackEngines", "MacStandaloneSupport", "Variations", "macos_arm64_player_development_il2cpp"));
            Assert.Null(Requirements.Check("il2cpp", g.Dir, env));
        }

        [Fact]
        public void X64Dotnet_MissingHostOrRuntime_Fails_AndTheRightRuntimePasses()
        {
            using TempGit g = RepoWithUnityVersion();
            string? missing = Requirements.Check("x64-dotnet", g.Dir, Env(new Dictionary<string, string?> { ["DOTNET_ROOT_X64"] = g.PathOf("nope") }));
            Assert.NotNull(missing);
            Assert.Contains("x64 dotnet 런타임이 없다", missing, StringComparison.Ordinal);
            Assert.Contains("DOTNET_ROOT_X64", missing, StringComparison.Ordinal);

            g.Write("old/dotnet", "#!/bin/bash\necho 'Microsoft.NETCore.App 8.0.10 [/x]'\n", executable: true);
            string? old = Requirements.Check("x64-dotnet", g.Dir, Env(new Dictionary<string, string?> { ["DOTNET_ROOT_X64"] = g.PathOf("old") }));
            Assert.NotNull(old);
            Assert.Contains("Microsoft.NETCore.App 10.x", old, StringComparison.Ordinal);

            g.Write("good/dotnet", "#!/bin/bash\necho 'Microsoft.AspNetCore.App 10.0.1 [/x]'\necho 'Microsoft.NETCore.App 10.0.1 [/x]'\n", executable: true);
            Assert.Null(Requirements.Check("x64-dotnet", g.Dir, Env(new Dictionary<string, string?> { ["DOTNET_ROOT_X64"] = g.PathOf("good") })));
        }

        [Fact]
        public void Docker_MissingOrNotRunning_Fails_WithTheUsersAction()
        {
            using TempGit g = RepoWithUnityVersion();
            string? missing = Requirements.Check("docker", g.Dir, Env(new Dictionary<string, string?> { ["WIZARD_DOCKER"] = g.PathOf("no-docker") }));
            Assert.NotNull(missing);
            Assert.Contains("Docker 가 없다", missing, StringComparison.Ordinal);

            g.Write("bin/docker", "#!/bin/bash\necho 'Cannot connect to the Docker daemon' >&2\nexit 1\n", executable: true);
            string? down = Requirements.Check("docker", g.Dir, Env(new Dictionary<string, string?> { ["WIZARD_DOCKER"] = g.PathOf("bin/docker") }));
            Assert.NotNull(down);
            Assert.Contains("돌고 있지 않다", down, StringComparison.Ordinal);
            Assert.Contains("open -a Docker", down, StringComparison.Ordinal);

            g.Write("ok/docker", "#!/bin/bash\necho 27.0.1\n", executable: true);
            Assert.Null(Requirements.Check("docker", g.Dir, Env(new Dictionary<string, string?> { ["WIZARD_DOCKER"] = g.PathOf("ok/docker") })));
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

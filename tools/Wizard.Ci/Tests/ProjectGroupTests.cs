using System;
using Xunit;

namespace Wizard.Ci.Tests
{
    // DECISIONS-TECH T-67 6번: Directory.Build.props 의 프로젝트 묶음(WizardGroup)이 두 OS 에서 같은 값이다.
    // 묶음이 비면 분석기 연결과 공유 패키지의 LangVersion 9.0 이 조용히 빠진다(Windows 에서 그랬다). determinism-guard 의 탐침 말고도 여기서 잡는다.
    public sealed class ProjectGroupTests
    {
        [Theory]
        [InlineData("src/Wizard.Core/Wizard.Core.csproj", "shared")]
        [InlineData("tests/Wizard.TestKit/Shared/Wizard.TestKit.Shared.csproj", "shared")]
        [InlineData("server/Wizard.Server.Api/Wizard.Server.Api.csproj", "server")]
        [InlineData("tests/Wizard.Core.Tests/Wizard.Core.Tests.csproj", "tests")]
        [InlineData("tools/Wizard.Ci/Wizard.Ci.csproj", "tools")]
        public void EachProject_GetsTheExpectedGroup(string csproj, string group)
        {
            string dotnet = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet";
            ProcResult r = ProcessRunner.Run(new ProcRequest(dotnet, new[] { "msbuild", RepoPaths.Combine(TempGit.RepoRoot, csproj), "-getProperty:WizardGroup", "-nologo" })
            {
                Capture = true,
                Timeout = TimeSpan.FromMinutes(5),
            });
            Assert.True(r.ExitCode == 0, r.Stdout + r.Stderr);
            Assert.Equal(group, r.Stdout.Trim());
        }

        [Theory]
        [InlineData("src/Wizard.Core/Wizard.Core.csproj", "9.0")]
        [InlineData("tests/Wizard.TestKit/Shared/Wizard.TestKit.Shared.csproj", "9.0")]
        public void SharedProjects_GetCSharp9(string csproj, string langVersion)
        {
            string dotnet = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet";
            ProcResult r = ProcessRunner.Run(new ProcRequest(dotnet, new[] { "msbuild", RepoPaths.Combine(TempGit.RepoRoot, csproj), "-getProperty:LangVersion", "-nologo" })
            {
                Capture = true,
                Timeout = TimeSpan.FromMinutes(5),
            });
            Assert.True(r.ExitCode == 0, r.Stdout + r.Stderr);
            Assert.Equal(langVersion, r.Stdout.Trim());
        }
    }
}

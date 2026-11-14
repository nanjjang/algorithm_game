using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace Wizard.Ci.Tests
{
    // DECISIONS-TECH T-67 3번: 검사 스크립트를 띄울 bash 를 찾는 순서. 판정은 OS 와 파일 확인을 인자로 받아 두 OS 의 가지를 이 기기에서 본다.
    public sealed class BashLocatorTests
    {
        private static Func<string, string?> Env(Dictionary<string, string?> d) => k => d.TryGetValue(k, out string? v) ? v : null;

        [Fact]
        public void WizardBash_ThatExists_IsUsed_OnBothOses()
        {
            using TempGit g = TempGit.Create();
            g.Write("mybash", "x");
            string given = g.PathOf("mybash");
            var env = Env(new Dictionary<string, string?> { ["WIZARD_BASH"] = given });
            Assert.Equal(BashLocator.SlashPath(given, true), BashLocator.Locate(env, true, File.Exists, () => "C:/Program Files/Git/mingw64/libexec/git-core"));
            Assert.Equal(given, BashLocator.Locate(env, false, File.Exists, () => null));
        }

        [Fact]
        public void WizardBash_ThatIsMissing_Fails_WithoutLookingElsewhere()
        {
            var env = Env(new Dictionary<string, string?> { ["WIZARD_BASH"] = "Z:/no/such/bash.exe" });
            bool lookedElsewhere = false;
            CiException e = Assert.Throws<CiException>(() => BashLocator.Locate(env, true, p =>
            {
                lookedElsewhere |= p != "Z:/no/such/bash.exe";
                return false;
            }, () => "C:/Program Files/Git/mingw64/libexec/git-core"));
            Assert.Contains("Z:/no/such/bash.exe", e.Message, StringComparison.Ordinal);
            Assert.Contains("WIZARD_BASH", e.Message, StringComparison.Ordinal);
            Assert.False(lookedElsewhere);
        }

        [Fact]
        public void Windows_WalksUpFromGitExecPath_ToTheFirstBinBash_ThenProgramFiles()
        {
            var env = Env(new Dictionary<string, string?> { ["ProgramFiles"] = "C:\\Program Files" });
            List<string> c = BashLocator.WindowsCandidates(env, () => "E:\\Tools\\Git\\mingw64\\libexec\\git-core\n");
            Assert.Equal(new[]
            {
                "E:/Tools/Git/mingw64/libexec/bin/bash.exe",
                "E:/Tools/Git/mingw64/bin/bash.exe",
                "E:/Tools/Git/bin/bash.exe",
                "E:/Tools/bin/bash.exe",
                "E:/bin/bash.exe",
                "C:/Program Files/Git/bin/bash.exe",
            }, c);
            // 첫 번째로 있는 것. usr/bin/bash.exe 는 후보가 아니다
            string found = BashLocator.Locate(env, true, p => p == "E:/Tools/Git/bin/bash.exe" || p == "E:/Tools/Git/usr/bin/bash.exe", () => "E:/Tools/Git/mingw64/libexec/git-core");
            Assert.Equal("E:/Tools/Git/bin/bash.exe", found);
            // git 이 없으면 %ProgramFiles%/Git
            Assert.Equal("C:/Program Files/Git/bin/bash.exe", BashLocator.Locate(env, true, p => p == "C:/Program Files/Git/bin/bash.exe", () => null));
        }

        [Fact]
        public void Windows_NothingFound_SaysWhatToDo_AndNeverFallsBackToPath()
        {
            var env = Env(new Dictionary<string, string?> { ["PATH"] = "C:\\Windows\\System32", ["ProgramFiles"] = "C:/PF" });
            CiException e = Assert.Throws<CiException>(() => BashLocator.Locate(env, true, p => p.StartsWith("C:/Windows", StringComparison.OrdinalIgnoreCase) || p.StartsWith("C:\\Windows", StringComparison.OrdinalIgnoreCase), () => null));
            Assert.Contains("Git for Windows", e.Message, StringComparison.Ordinal);
            Assert.Contains("WIZARD_BASH", e.Message, StringComparison.Ordinal);
            Assert.Contains("C:/PF/Git/bin/bash.exe", e.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void NonWindows_IsBinBash()
        {
            Assert.Equal("/bin/bash", BashLocator.Locate(_ => null, false, p => p == "/bin/bash", () => "/usr/libexec/git-core"));
            CiException e = Assert.Throws<CiException>(() => BashLocator.Locate(_ => null, false, _ => false, () => null));
            Assert.Contains("WIZARD_BASH", e.Message, StringComparison.Ordinal);
        }

        // 이 기기에서 실제로 찾은 셸: Windows 에서는 WSL 을 띄우는 System32·WindowsApps 아래가 아니고, 그 셸로 명령이 돈다
        [Fact]
        public void TheShellFoundOnThisMachine_Runs_AndIsNotWsl()
        {
            string bash = BashLocator.FromEnv(Environment.GetEnvironmentVariable);
            if (OperatingSystem.IsWindows())
            {
                Assert.DoesNotContain("system32", bash, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("windowsapps", bash, StringComparison.OrdinalIgnoreCase);
                Assert.EndsWith("/bin/bash.exe", bash, StringComparison.OrdinalIgnoreCase);
            }
            else if (Environment.GetEnvironmentVariable("WIZARD_BASH") is not { Length: > 0 })
            {
                Assert.Equal("/bin/bash", bash);
            }
            ProcResult r = ProcessRunner.Run(new ProcRequest(bash, new[] { "-c", "echo ok" }) { Capture = true });
            Assert.Equal(0, r.ExitCode);
            Assert.Equal("ok\n", r.Stdout);
        }

        [Fact]
        public void SlashPath_ChangesOnlyWindowsPaths()
        {
            Assert.Equal("D:/a/b.sh", BashLocator.SlashPath("D:\\a\\b.sh", true));
            Assert.Equal("/Users/x/a b.sh", BashLocator.SlashPath("/Users/x/a b.sh", false));
            Assert.Equal("/odd\\name", BashLocator.SlashPath("/odd\\name", false));
        }
    }
}

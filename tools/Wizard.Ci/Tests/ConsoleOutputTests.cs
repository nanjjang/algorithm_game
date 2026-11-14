using System;
using System.IO;
using Xunit;

namespace Wizard.Ci.Tests
{
    // DECISIONS-TECH T-67 5번: Wizard.Ci.dll 을 자식 프로세스로 띄워 관으로 받은 표준 출력·표준 오류가 BOM 없는 UTF-8 이고 줄 끝이 '\n' 이다.
    // ProcessRunner 는 UTF-8 로 읽으므로 한글이 그대로 읽히면 UTF-8 이다. ReadLine 은 '\r' 을 지우므로 바이트를 그대로 보는 길로 받는다.
    public sealed class ConsoleOutputTests
    {
        private static (int Code, byte[] Stdout, byte[] Stderr) RunRaw(params string[] args)
        {
            string dotnet = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet";
            var psi = new System.Diagnostics.ProcessStartInfo(dotnet)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            psi.ArgumentList.Add(TempGit.RunnerDll);
            foreach (string a in args)
            {
                psi.ArgumentList.Add(a);
            }
            foreach (string v in new[] { "WIZARD_CI_BASE", "WIZARD_CI_FORCE_ALL", "WIZARD_CI_NOTE" })
            {
                psi.Environment.Remove(v);
            }
            using var p = System.Diagnostics.Process.Start(psi)!;
            var o = new MemoryStream();
            var e = new MemoryStream();
            System.Threading.Tasks.Task to = p.StandardOutput.BaseStream.CopyToAsync(o);
            System.Threading.Tasks.Task te = p.StandardError.BaseStream.CopyToAsync(e);
            p.WaitForExit();
            System.Threading.Tasks.Task.WaitAll(to, te);
            return (p.ExitCode, o.ToArray(), e.ToArray());
        }

        private static string Utf8(byte[] b) => new System.Text.UTF8Encoding(false, true).GetString(b);

        [Fact]
        public void Changed_AsAChildProcess_WritesUtf8Lines_WithoutCarriageReturns()
        {
            using TempGit g = TempGit.Create();
            g.Git("checkout", "-q", "-b", "task/X");
            g.Write("a.txt", "a\n");
            g.Commit("chore: a");
            (int code, byte[] stdout, byte[] stderr) = RunRaw("changed", "--root", g.Dir);
            Assert.True(code == 0, Utf8(stderr));
            Assert.DoesNotContain((byte)'\r', stdout);
            Assert.False(stdout.Length >= 3 && stdout[0] == 0xEF && stdout[1] == 0xBB && stdout[2] == 0xBF, "BOM");
            string text = Utf8(stdout);   // UTF-8 이 아니면 여기서 예외
            string[] lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            Assert.StartsWith("# ", lines[0], StringComparison.Ordinal);
            Assert.Equal(new[] { "a.txt" }, lines[1..]);
        }

        [Fact]
        public void UsageError_AsAChildProcess_WritesKoreanUtf8_OnStderr_WithoutCarriageReturns()
        {
            using TempGit g = TempGit.Create();
            (int code, byte[] _, byte[] stderr) = RunRaw("require", "gpu", "--root", g.Dir);
            Assert.Equal(CiException.UsageExitCode, code);
            Assert.DoesNotContain((byte)'\r', stderr);
            Assert.Equal("ci: 알 수 없는 요구 'gpu' (unity|il2cpp|x64-dotnet|docker)\n", Utf8(stderr));
        }
    }
}

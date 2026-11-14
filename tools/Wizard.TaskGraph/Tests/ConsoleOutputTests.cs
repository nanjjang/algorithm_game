using System;
using Xunit;

namespace Wizard.TaskGraph.Tests
{
    // DECISIONS-TECH T-67 5번: 도구를 자식 프로세스로 띄워 관으로 받은 표준 출력이 BOM 없는 UTF-8 이고 줄 끝이 '\n' 이다.
    // (Windows 에서 .NET 의 기본은 "\r\n" 과 콘솔 코드 페이지라서, 고치기 전에는 `taskgraph ledger` 의 '통과' 가 깨져 나왔다.)
    public sealed class ConsoleOutputTests
    {
        [Fact]
        public void Ledger_AsAChildProcess_WritesUtf8WithLfOnly()
        {
            string dotnet = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet";
            ProcResult r = Proc.Run(dotnet, new[] { typeof(Program).Assembly.Location, "--repo", Fx.RepoRoot, "ledger" });
            Assert.True(r.ExitCode == 0, r.Stdout + r.Stderr);
            Assert.DoesNotContain("\r", r.Stdout, StringComparison.Ordinal);
            Assert.DoesNotContain("\r", r.Stderr, StringComparison.Ordinal);
            Assert.False(r.Stdout.StartsWith("﻿", StringComparison.Ordinal), "BOM");
            Assert.EndsWith("\n통과\n", r.Stdout, StringComparison.Ordinal);
        }

        [Fact]
        public void UsageError_AsAChildProcess_WritesUtf8WithLfOnly_OnStderr()
        {
            string dotnet = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet";
            ProcResult r = Proc.Run(dotnet, new[] { typeof(Program).Assembly.Location, "--repo", Fx.RepoRoot, "no-such-command" });
            Assert.NotEqual(0, r.ExitCode);
            Assert.DoesNotContain("\r", r.Stderr, StringComparison.Ordinal);
            Assert.DoesNotContain("�", r.Stderr, StringComparison.Ordinal);
            Assert.True(r.Stderr.Length > 0);
        }
    }
}

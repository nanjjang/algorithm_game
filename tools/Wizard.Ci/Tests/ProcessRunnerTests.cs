using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Xunit;

namespace Wizard.Ci.Tests
{
    // 시간 제한(T-40: bash 3.2 에는 timeout 이 없다. 멈춘 Unity 같은 자식을 여기서 끊는다)
    public sealed class ProcessRunnerTests
    {
        [Fact]
        public void HungChild_IsKilledWithItsChildren_AtTheTimeout()
        {
            using TempGit g = TempGit.Create();
            string pidFile = g.PathOf("grandchild.pid");
            // bash(자식)가 sleep(손자)을 띄우고 기다린다. 둘 다 스스로는 끝나지 않는다
            var req = new ProcRequest("/bin/bash", new[] { "-c", "/bin/sleep 600 & echo $! > '" + pidFile + "'; wait" })
            {
                Name = "hang",
                Timeout = TimeSpan.FromSeconds(2),
                Capture = true,
                TailLines = 5,
            };
            var sw = Stopwatch.StartNew();
            ProcResult r = ProcessRunner.Run(req);
            sw.Stop();

            Assert.True(r.TimedOut);
            Assert.Equal(ProcessRunner.TimeoutExitCode, r.ExitCode);
            Assert.True(sw.Elapsed < TimeSpan.FromSeconds(30), "took " + sw.Elapsed);
            Assert.Contains(r.Tail, l => l.StartsWith("TIMEOUT  hang", StringComparison.Ordinal));

            int pid = int.Parse(File.ReadAllText(pidFile).Trim(), System.Globalization.CultureInfo.InvariantCulture);
            Assert.True(WaitGone(pid), "grandchild sleep " + pid + " is still alive");
        }

        [Fact]
        public void FinishedChild_ExitCodeAndOutputPassThrough()
        {
            var req = new ProcRequest("/bin/bash", new[] { "-c", "echo out; echo err >&2; exit 7" }) { Capture = true, TailLines = 10 };
            ProcResult r = ProcessRunner.Run(req);
            Assert.False(r.TimedOut);
            Assert.Equal(7, r.ExitCode);
            Assert.Equal("out\n", r.Stdout);
            Assert.Equal("err\n", r.Stderr);
            Assert.Equal(new[] { "out", "err" }.Length, r.Tail.Count);
        }

        [Fact]
        public void ChildThatLeavesABackgroundProcessHoldingTheOutput_DoesNotHangTheRunner()
        {
            using TempGit g = TempGit.Create();
            string pidFile = g.PathOf("bg.pid");
            // 자식은 바로 끝나지만, 출력 관을 물려받은 백그라운드 sleep 이 남는다(빌드 서버와 같은 꼴)
            var req = new ProcRequest("/bin/bash", new[] { "-c", "/bin/sleep 60 & echo $! > '" + pidFile + "'; echo done" })
            {
                Capture = true,
                Timeout = TimeSpan.FromSeconds(30),
            };
            var sw = Stopwatch.StartNew();
            ProcResult r = ProcessRunner.Run(req);
            Assert.Equal(0, r.ExitCode);
            Assert.False(r.TimedOut);
            Assert.Contains("done", r.Stdout, StringComparison.Ordinal);
            Assert.True(sw.Elapsed < TimeSpan.FromSeconds(20), "took " + sw.Elapsed);
            int pid = int.Parse(File.ReadAllText(pidFile).Trim(), System.Globalization.CultureInfo.InvariantCulture);
            try
            {
                Process.GetProcessById(pid).Kill();
            }
            catch (ArgumentException)
            {
                // 이미 끝났다
            }
            catch (InvalidOperationException)
            {
            }
        }

        [Fact]
        public void LogFile_GetsEveryLine()
        {
            using TempGit g = TempGit.Create();
            string log = g.PathOf("out/step.log");
            ProcessRunner.Run(new ProcRequest("/bin/bash", new[] { "-c", "echo a; echo b >&2; echo c" }) { LogPath = log });
            string[] lines = File.ReadAllLines(log);
            Assert.Equal(3, lines.Length);
            Assert.Contains("a", lines);
            Assert.Contains("b", lines);
            Assert.Contains("c", lines);
        }

        [Fact]
        public void MissingExecutable_IsAClearError()
        {
            CiException e = Assert.Throws<CiException>(() => ProcessRunner.Run(new ProcRequest("/no/such/program", Array.Empty<string>())));
            Assert.Contains("/no/such/program", e.Message, StringComparison.Ordinal);
        }

        private static bool WaitGone(int pid)
        {
            for (int i = 0; i < 100; i++)
            {
                try
                {
                    using Process p = Process.GetProcessById(pid);
                    if (p.HasExited)
                    {
                        return true;
                    }
                }
                catch (ArgumentException)
                {
                    return true;
                }
                Thread.Sleep(100);
            }
            return false;
        }
    }
}

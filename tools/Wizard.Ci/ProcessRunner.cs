using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Wizard.Ci
{
    // 자식 프로세스를 셸 없이 띄운다(zsh 의 git 래퍼가 깨져 있다. CLAUDE.md 9절). bash 3.2 에는 timeout 이 없어서
    // 시간 제한은 여기서 건다(T-40): 시간이 넘으면 프로세스 나무 전체를 끊고 종료 코드 124 를 돌려준다.
    // 출력은 줄 단위로 화면(Echo), 기록 파일(LogPath), 메모리(Capture), 마지막 줄들(Tail)로 나눠 보낸다.
    // 자식이 끝난 뒤에도 그 자식이 띄운 다른 프로세스(빌드 서버 등)가 출력 관을 쥐고 있을 수 있어서, 출력의 끝은 OutputGrace 까지만 기다린다.
    public static class ProcessRunner
    {
        public const int TimeoutExitCode = 124;
        public static readonly TimeSpan OutputGrace = TimeSpan.FromSeconds(5);
        public static readonly TimeSpan KillGrace = TimeSpan.FromSeconds(10);

        public static ProcResult Run(ProcRequest req)
        {
            var psi = new ProcessStartInfo(req.File)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                StandardOutputEncoding = new UTF8Encoding(false),
                StandardErrorEncoding = new UTF8Encoding(false),
            };
            foreach (string a in req.Args)
            {
                psi.ArgumentList.Add(a);
            }
            if (req.WorkingDirectory != null)
            {
                psi.WorkingDirectory = req.WorkingDirectory;
            }
            psi.Environment["GIT_TERMINAL_PROMPT"] = "0";
            psi.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
            psi.Environment["DOTNET_NOLOGO"] = "1";
            foreach (KeyValuePair<string, string?> kv in req.Environment)
            {
                if (kv.Value == null)
                {
                    psi.Environment.Remove(kv.Key);
                }
                else
                {
                    psi.Environment[kv.Key] = kv.Value;
                }
            }

            var sink = new OutputSink(req);
            var sw = Stopwatch.StartNew();
            Process p;
            try
            {
                p = Process.Start(psi) ?? throw new CiException(req.File + " 을 띄우지 못했다");
            }
            catch (System.ComponentModel.Win32Exception e)
            {
                sink.Dispose();
                throw new CiException(req.File + " 을 띄우지 못했다: " + e.Message);
            }
            using (p)
            using (sink)
            {
                p.StandardInput.Close();
                Task outTask = Pump(p.StandardOutput, sink, false);
                Task errTask = Pump(p.StandardError, sink, true);
                bool exited = p.WaitForExit(ToMillis(req.Timeout));
                bool timedOut = false;
                if (!exited)
                {
                    timedOut = true;
                    try
                    {
                        p.Kill(entireProcessTree: true);
                    }
                    catch (InvalidOperationException)
                    {
                        // 그 사이에 끝났다
                    }
                    catch (System.ComponentModel.Win32Exception)
                    {
                        // 끊을 권한이 없는 손자(드묾). 아래에서 기다리는 시간도 제한이 있다
                    }
                    p.WaitForExit(ToMillis(KillGrace));
                    sink.Line("TIMEOUT  " + req.Name + ": " + ((int)req.Timeout.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture) +
                              "초를 넘겨 프로세스 나무를 끊었다", true);
                }
                Task.WaitAll(new[] { outTask, errTask }, OutputGrace);
                int code = timedOut ? TimeoutExitCode : (p.HasExited ? p.ExitCode : TimeoutExitCode);
                return new ProcResult(code, timedOut, sw.Elapsed.TotalSeconds, sink.Stdout(), sink.Stderr(), sink.TailLines());
            }
        }

        // 짧은 명령(git 등)을 돌려 출력을 받는다
        public static ProcResult Capture(string file, IEnumerable<string> args, string? workingDirectory = null, TimeSpan? timeout = null)
        {
            var req = new ProcRequest(file, args)
            {
                WorkingDirectory = workingDirectory,
                Timeout = timeout ?? TimeSpan.FromMinutes(5),
                Capture = true,
            };
            return Run(req);
        }

        private static int ToMillis(TimeSpan t) => (int)Math.Min(int.MaxValue, Math.Max(0, t.TotalMilliseconds));

        private static async Task Pump(StreamReader reader, OutputSink sink, bool isError)
        {
            string? line;
            while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) != null)
            {
                sink.Line(line, isError);
            }
        }

        private sealed class OutputSink : IDisposable
        {
            private readonly object _gate = new object();
            private readonly ProcRequest _req;
            private readonly StreamWriter? _log;
            private readonly StringBuilder _out = new StringBuilder();
            private readonly StringBuilder _err = new StringBuilder();
            private readonly Queue<string> _tail = new Queue<string>();

            public OutputSink(ProcRequest req)
            {
                _req = req;
                if (req.LogPath != null)
                {
                    string? dir = Path.GetDirectoryName(req.LogPath);
                    if (dir != null)
                    {
                        Directory.CreateDirectory(dir);
                    }
                    _log = new StreamWriter(req.LogPath, false, new UTF8Encoding(false)) { NewLine = "\n", AutoFlush = true };
                }
            }

            public void Line(string line, bool isError)
            {
                lock (_gate)
                {
                    if (_req.Echo != null)
                    {
                        (isError ? _req.EchoError ?? _req.Echo : _req.Echo).WriteLine(line);
                    }
                    _log?.WriteLine(line);
                    if (_req.Capture)
                    {
                        (isError ? _err : _out).Append(line).Append('\n');
                    }
                    if (_req.TailLines > 0)
                    {
                        _tail.Enqueue(line);
                        while (_tail.Count > _req.TailLines)
                        {
                            _tail.Dequeue();
                        }
                    }
                }
            }

            public string Stdout()
            {
                lock (_gate)
                {
                    return _out.ToString();
                }
            }

            public string Stderr()
            {
                lock (_gate)
                {
                    return _err.ToString();
                }
            }

            public IReadOnlyList<string> TailLines()
            {
                lock (_gate)
                {
                    return _tail.ToArray();
                }
            }

            public void Dispose()
            {
                lock (_gate)
                {
                    _log?.Dispose();
                }
            }
        }
    }
}

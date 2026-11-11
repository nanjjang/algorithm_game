using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace Wizard.Ci
{
    // CI 의 로직(T-40). tools/ci/*.sh 는 bash 3.2 껍데기이고 시간 제한·결과 기록은 여기서 한다.
    //   ci exec --name <단계 이름> --timeout <초> --out <결과 json> -- <명령> [인자…]
    //     자식 프로세스를 돌리고, 시간이 넘으면 프로세스 나무를 끊고(종료 코드 124), 결과를 json 으로 남긴다.
    //     종료 코드는 자식의 것을 그대로 돌려준다.
    // 잠금, 바뀐 경로에 따른 단계 선택, 벡터 대응은 M0-BT-05 가 더한다.
    public static class Program
    {
        public const int TimeoutExitCode = 124;

        public static int Main(string[] args)
        {
            if (args.Length == 0 || args[0] != "exec")
            {
                Console.Error.WriteLine("usage: ci exec --name <n> --timeout <sec> --out <json> -- <cmd> [args…]");
                return 64;
            }
            string name = "step";
            int timeoutSec = 1800;
            string? outPath = null;
            int i = 1;
            for (; i < args.Length; i++)
            {
                string a = args[i];
                if (a == "--")
                {
                    i++;
                    break;
                }
                if (i + 1 >= args.Length)
                {
                    Console.Error.WriteLine($"missing value for {a}");
                    return 64;
                }
                switch (a)
                {
                    case "--name": name = args[++i]; break;
                    case "--timeout": timeoutSec = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                    case "--out": outPath = args[++i]; break;
                    default:
                        Console.Error.WriteLine($"unknown option {a}");
                        return 64;
                }
            }
            if (i >= args.Length)
            {
                Console.Error.WriteLine("no command after --");
                return 64;
            }
            var result = Exec(args[i], new ArraySegment<string>(args, i + 1, args.Length - i - 1), TimeSpan.FromSeconds(timeoutSec));
            if (outPath != null)
            {
                WriteResult(outPath, name, result);
            }
            if (result.TimedOut)
            {
                Console.Error.WriteLine($"TIMEOUT  {name} ({timeoutSec}s)");
            }
            return result.ExitCode;
        }

        public readonly struct ExecResult
        {
            public readonly int ExitCode;
            public readonly bool TimedOut;
            public readonly double Seconds;

            public ExecResult(int exitCode, bool timedOut, double seconds)
            {
                ExitCode = exitCode;
                TimedOut = timedOut;
                Seconds = seconds;
            }
        }

        public static ExecResult Exec(string file, IEnumerable<string> arguments, TimeSpan timeout)
        {
            var psi = new ProcessStartInfo(file) { UseShellExecute = false };
            foreach (string a in arguments)
            {
                psi.ArgumentList.Add(a);
            }
            var sw = Stopwatch.StartNew();
            using var p = Process.Start(psi) ?? throw new InvalidOperationException("failed to start " + file);
            if (!p.WaitForExit((int)Math.Min(int.MaxValue, timeout.TotalMilliseconds)))
            {
                try
                {
                    p.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                    // 이미 끝났다
                }
                p.WaitForExit();
                return new ExecResult(TimeoutExitCode, true, sw.Elapsed.TotalSeconds);
            }
            p.WaitForExit();
            return new ExecResult(p.ExitCode, false, sw.Elapsed.TotalSeconds);
        }

        private static void WriteResult(string path, string name, ExecResult r)
        {
            string? dir = Path.GetDirectoryName(Path.GetFullPath(path));
            if (dir != null)
            {
                Directory.CreateDirectory(dir);
            }
            var sb = new StringBuilder();
            sb.Append("{\"name\":\"").Append(Escape(name)).Append("\",");
            sb.Append("\"exit_code\":").Append(r.ExitCode.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append("\"passed\":").Append(r.ExitCode == 0 ? "true" : "false").Append(',');
            sb.Append("\"timed_out\":").Append(r.TimedOut ? "true" : "false").Append(',');
            sb.Append("\"seconds\":").Append(r.Seconds.ToString("0.###", CultureInfo.InvariantCulture)).Append(',');
            sb.Append("\"finished_at\":\"").Append(DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)).Append("\"}\n");
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        }

        private static string Escape(string s) => s.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}

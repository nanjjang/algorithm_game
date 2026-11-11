using System.Collections.Generic;

namespace Wizard.Ci
{
    // ProcessRunner.Run 의 결과
    public sealed class ProcResult
    {
        public ProcResult(int exitCode, bool timedOut, double seconds, string stdout, string stderr, IReadOnlyList<string> tail)
        {
            ExitCode = exitCode;
            TimedOut = timedOut;
            Seconds = seconds;
            Stdout = stdout;
            Stderr = stderr;
            Tail = tail;
        }

        public int ExitCode { get; }
        public bool TimedOut { get; }
        public double Seconds { get; }
        public string Stdout { get; }
        public string Stderr { get; }
        public IReadOnlyList<string> Tail { get; }

        public string FirstErrorLine()
        {
            foreach (string l in (Stderr + "\n" + Stdout).Split('\n'))
            {
                if (l.Trim().Length > 0)
                {
                    return l.Trim();
                }
            }
            return "(출력 없음)";
        }
    }
}

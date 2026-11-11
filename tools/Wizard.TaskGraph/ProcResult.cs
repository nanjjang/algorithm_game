namespace Wizard.TaskGraph
{
    public sealed class ProcResult
    {
        public ProcResult(int exitCode, string stdout, string stderr)
        {
            ExitCode = exitCode;
            Stdout = stdout;
            Stderr = stderr;
        }

        public int ExitCode { get; }
        public string Stdout { get; }
        public string Stderr { get; }

        public string FirstErrorLine()
        {
            string s = Stderr.Trim().Length > 0 ? Stderr : Stdout;
            s = s.Trim();
            int nl = s.IndexOf('\n');
            return nl < 0 ? s : s.Substring(0, nl).TrimEnd();
        }
    }
}

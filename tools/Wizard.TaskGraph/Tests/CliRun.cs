using System.Collections.Generic;
using System.IO;

namespace Wizard.TaskGraph.Tests
{
    // Cli.Run 을 프로세스 안에서 부르고 출력과 종료 코드를 모은다.
    internal sealed class CliRun
    {
        private CliRun(int code, string stdout, string stderr)
        {
            Code = code;
            Stdout = stdout;
            Stderr = stderr;
        }

        public int Code { get; }
        public string Stdout { get; }
        public string Stderr { get; }

        public static CliRun In(TempRepo repo, string tasksPath, params string[] args)
        {
            var all = new List<string> { "--repo", repo.Dir, "--tasks", tasksPath, "--schema", Fx.SchemaPath };
            all.AddRange(args);
            return Run(all.ToArray());
        }

        public static CliRun Run(params string[] args)
        {
            var o = new StringWriter();
            var e = new StringWriter();
            int code = Cli.Run(args, o, e);
            return new CliRun(code, o.ToString(), e.ToString());
        }

        public override string ToString() => $"exit {Code}\n{Stdout}\n{Stderr}";
    }
}

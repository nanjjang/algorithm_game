using System.Collections.Generic;

namespace Wizard.TaskGraph
{
    public sealed class CliOptions
    {
        public string? Repo { get; set; }
        public string? Tasks { get; set; }
        public string? Specs { get; set; }
        public string? Schema { get; set; }
        public string? State { get; set; }
        public string Base { get; set; } = "main";
        public string? Head { get; set; }
        public bool NoVectors { get; set; }
        public bool Json { get; set; }
        public bool NoHistory { get; set; }
        public bool Check { get; set; }
        public List<string> Positional { get; } = new List<string>();

        public static CliOptions Parse(IReadOnlyList<string> args)
        {
            var o = new CliOptions();
            for (int i = 0; i < args.Count; i++)
            {
                string a = args[i];
                switch (a)
                {
                    case "--repo": o.Repo = Value(args, ref i); break;
                    case "--tasks": o.Tasks = Value(args, ref i); break;
                    case "--specs": o.Specs = Value(args, ref i); break;
                    case "--schema": o.Schema = Value(args, ref i); break;
                    case "--state": o.State = Value(args, ref i); break;
                    case "--base": o.Base = Value(args, ref i); break;
                    case "--head": o.Head = Value(args, ref i); break;
                    case "--no-vectors": o.NoVectors = true; break;
                    case "--json": o.Json = true; break;
                    case "--no-history": o.NoHistory = true; break;
                    case "--check": o.Check = true; break;
                    default:
                        if (a.StartsWith("--", System.StringComparison.Ordinal))
                        {
                            throw new TaskGraphException($"모르는 옵션 {a}", TaskGraphException.UsageExitCode);
                        }
                        o.Positional.Add(a);
                        break;
                }
            }
            return o;
        }

        private static string Value(IReadOnlyList<string> args, ref int i)
        {
            if (i + 1 >= args.Count)
            {
                throw new TaskGraphException($"{args[i]} 뒤에 값이 없다", TaskGraphException.UsageExitCode);
            }
            return args[++i];
        }
    }
}

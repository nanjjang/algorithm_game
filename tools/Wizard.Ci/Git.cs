using System;
using System.Collections.Generic;

namespace Wizard.Ci
{
    // git 호출. 항상 -C <뿌리> 로 그 체크아웃을 가리킨다(현재 폴더에 기대지 않는다).
    public static class Git
    {
        public static string Run(string root, params string[] args)
        {
            ProcResult r = TryRun(root, args);
            if (r.ExitCode != 0)
            {
                throw new CiException("git " + string.Join(" ", args) + " 실패(" + root + "): " + r.FirstErrorLine());
            }
            return r.Stdout;
        }

        public static ProcResult TryRun(string root, params string[] args)
        {
            var all = new List<string> { "-C", root, "-c", "core.quotepath=off" };
            all.AddRange(args);
            return ProcessRunner.Capture("git", all, null, TimeSpan.FromMinutes(2));
        }

        // NUL 로 나눈 경로 목록(-z)
        public static List<string> Paths(string root, params string[] args)
        {
            var list = new List<string>();
            foreach (string p in Run(root, args).Split('\0'))
            {
                string s = p.Trim('\n');
                if (s.Length > 0)
                {
                    list.Add(s);
                }
            }
            return list;
        }

        public static string? CurrentBranch(string root)
        {
            ProcResult r = TryRun(root, "symbolic-ref", "--quiet", "--short", "HEAD");
            return r.ExitCode == 0 ? r.Stdout.Trim() : null;
        }

        public static string? RevParse(string root, string rev)
        {
            ProcResult r = TryRun(root, "rev-parse", "--verify", "--quiet", rev + "^{commit}");
            return r.ExitCode == 0 ? r.Stdout.Trim() : null;
        }

        public static bool IsRepo(string root) => TryRun(root, "rev-parse", "--git-dir").ExitCode == 0;
    }
}

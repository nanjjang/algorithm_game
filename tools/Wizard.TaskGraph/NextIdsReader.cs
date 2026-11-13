using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Wizard.TaskGraph
{
    // next-ids 가 볼 곳을 git 에서 모은다(명령 줄 층만 부른다. T-65 6번):
    //   <base>(기본 main), refs/heads/task/* 의 끝(커밋한 것), `git worktree list` 의 모든 작업 트리(커밋하지 않은 것 포함)
    // 장부 파일이 없는 브랜치·작업 트리는 그 파일만 건너뛴다.
    public static class NextIdsReader
    {
        public static IReadOnlyList<NextIds.Source> Read(Git git, string baseRef)
        {
            var sources = new List<NextIds.Source>();
            var refs = new List<string>();
            if (git.RefExists(baseRef))
            {
                refs.Add(baseRef);
            }
            foreach (string line in git.RunChecked("for-each-ref", "--format=%(refname:short)", "refs/heads/task/").Split('\n'))
            {
                string r = line.Trim();
                if (r.Length > 0 && !refs.Contains(r))
                {
                    refs.Add(r);
                }
            }
            foreach (string r in refs)
            {
                var files = new Dictionary<string, string>(StringComparer.Ordinal);
                ProcResult ls = git.Run("ls-tree", "--name-only", r, "docs/dev/");
                if (ls.ExitCode == 0)
                {
                    foreach (string p in ls.Stdout.Split('\n').Select(x => x.Trim()).Where(x => x.Length > 0))
                    {
                        if (p == Ledger.TasksPath || p == Ledger.QuestionsPath || NextIds.IsDevMarkdown(p))
                        {
                            ProcResult show = git.Run("show", r + ":" + p);
                            if (show.ExitCode == 0)
                            {
                                files[p] = show.Stdout;
                            }
                        }
                    }
                }
                sources.Add(new NextIds.Source(r, files));
            }
            foreach (string line in git.RunChecked("worktree", "list", "--porcelain").Split('\n'))
            {
                if (!line.StartsWith("worktree ", StringComparison.Ordinal))
                {
                    continue;
                }
                string dir = line.Substring("worktree ".Length).Trim();
                string dev = Path.Combine(dir, "docs", "dev");
                var files = new Dictionary<string, string>(StringComparer.Ordinal);
                if (Directory.Exists(dev))
                {
                    foreach (string f in Directory.GetFiles(dev).OrderBy(x => x, StringComparer.Ordinal))
                    {
                        string rel = "docs/dev/" + Path.GetFileName(f);
                        if (rel == Ledger.TasksPath || NextIds.IsDevMarkdown(rel))
                        {
                            files[rel] = File.ReadAllText(f, Encoding.UTF8);
                        }
                    }
                }
                sources.Add(new NextIds.Source("작업 트리 " + dir, files));
            }
            return sources;
        }
    }
}

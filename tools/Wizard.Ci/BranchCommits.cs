using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Wizard.Ci
{
    // 브랜치에 쌓인 커밋의 메시지 형식(PIPELINE 7.1 G0 "커밋 메시지 형식", CLAUDE.md 10절). `ci commit-msgs` 가 부르고
    // fast 단계의 tools/ci/lib/task-branch.sh 가 그것을 부른다. commit-msg 훅이 없을 때(설치 전, --no-verify)도 잡으려는 것이다.
    //
    //   범위: 비교 기준(baseRef, 없으면 main)과 HEAD 의 갈림점 .. HEAD 의 커밋. 병합 커밋은 보지 않는다(git 이 만든 메시지다).
    //     baseRef 는 명령 줄 층(Program.Run)이 --base, 없으면 WIZARD_CI_BASE 에서 정해 넘긴다. 여기서는 프로세스 환경을 읽지 않는다(FIX-4).
    //   각 커밋: CommitMessage.Check(형식, type, tasks.json 의 작업 id, 본문, Co-Authored-By 줄)
    //   작업 id 를 주면(task/<id> 브랜치) 첫 줄의 작업 id 가 그 id 여야 한다(taskgraph reconcile 이 첫 줄의 id 로 끝난 작업을 찾는다).
    public static class BranchCommits
    {
        public sealed class Problem
        {
            public Problem(string commit, string subject, string message)
            {
                Commit = commit;
                Subject = subject;
                Message = message;
            }

            public string Commit { get; }
            public string Subject { get; }
            public string Message { get; }

            public override string ToString() => Commit.Substring(0, Math.Min(12, Commit.Length)) + " \"" + Subject + "\": " + Message;
        }

        // 본 커밋 수와 문제 목록(비면 통과)
        public static List<Problem> Check(string root, string? baseRef, string? taskId, ISet<string> taskIds, out int checkedCount, out string range)
        {
            baseRef ??= "main";
            if (Git.RevParse(root, baseRef) == null)
            {
                throw new CiException("비교 기준 '" + baseRef + "' 을 git 이 모른다(--base 또는 WIZARD_CI_BASE)");
            }
            if (Git.RevParse(root, "HEAD") == null)
            {
                throw new CiException(root + " 에 커밋이 없다");
            }
            ProcResult mb = Git.TryRun(root, "merge-base", baseRef, "HEAD");
            if (mb.ExitCode != 0)
            {
                throw new CiException("HEAD 와 '" + baseRef + "' 의 갈림점이 없다");
            }
            string mergeBase = mb.Stdout.Trim();
            range = mergeBase.Substring(0, Math.Min(12, mergeBase.Length)) + "(" + baseRef + " 와의 갈림점)..HEAD";

            var problems = new List<Problem>();
            string list = Git.Run(root, "rev-list", "--no-merges", "--reverse", mergeBase + "..HEAD");
            checkedCount = 0;
            foreach (string line in list.Split('\n'))
            {
                string sha = line.Trim();
                if (sha.Length == 0)
                {
                    continue;
                }
                checkedCount++;
                string message = Git.Run(root, "log", "-1", "--format=%B", sha);
                string subject = FirstLine(message);
                foreach (string p in CommitMessage.Check(message, taskIds))
                {
                    problems.Add(new Problem(sha, subject, p));
                }
                if (taskId != null)
                {
                    Match m = CommitMessage.Subject.Match(subject);
                    if (m.Success && m.Groups["id"].Value != taskId)
                    {
                        problems.Add(new Problem(sha, subject,
                            "브랜치 task/" + taskId + " 의 커밋인데 첫 줄의 작업 id 가 '" + m.Groups["id"].Value + "' 다(" + taskId + " 여야 한다)"));
                    }
                }
            }
            return problems;
        }

        private static string FirstLine(string message)
        {
            foreach (string l in message.Replace("\r\n", "\n").Split('\n'))
            {
                if (l.Trim().Length > 0 && !l.StartsWith("#", StringComparison.Ordinal))
                {
                    return l.TrimEnd();
                }
            }
            return "";
        }
    }
}

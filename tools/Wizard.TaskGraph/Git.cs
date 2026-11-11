using System.Collections.Generic;

namespace Wizard.TaskGraph
{
    // git 을 한 저장소(-C <경로>)에 대고 부른다. 작업 트리끼리 ref 를 같이 쓰므로 메인 저장소에 대고 부르면 된다.
    public sealed class Git
    {
        public Git(string repo)
        {
            Repo = repo;
        }

        public string Repo { get; }

        public ProcResult Run(params string[] args)
        {
            var all = new List<string> { "-C", Repo, "-c", "core.quotepath=off" };
            all.AddRange(args);
            return Proc.Run("git", all);
        }

        public string RunChecked(params string[] args)
        {
            ProcResult r = Run(args);
            if (r.ExitCode != 0)
            {
                throw new TaskGraphException($"git {string.Join(" ", args)} 이 실패했다(종료 {r.ExitCode}): {r.FirstErrorLine()}");
            }
            return r.Stdout;
        }

        public bool RefExists(string reference)
        {
            return Run("rev-parse", "--verify", "--quiet", reference + "^{commit}").ExitCode == 0;
        }

        public bool BranchExists(string branch)
        {
            return Run("rev-parse", "--verify", "--quiet", "refs/heads/" + branch).ExitCode == 0;
        }
    }
}

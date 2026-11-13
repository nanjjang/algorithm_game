namespace Wizard.TaskGraph
{
    // 명령 줄 층에서만 부른다(T-65 5번). 기록을 읽지 못하면 실패하지 않고 Unreadable 을 돌려준다(요약에 그 사실이 적힌다).
    // 기준을 환경 변수에서 읽지 않는다(FIX-4 에서 본 누출): --base 와 --no-history 만 본다.
    public static class MergeHistoryReader
    {
        public static MergeHistory Read(CliOptions opt, Git git, TaskDoc doc)
        {
            return opt.NoHistory ? MergeHistory.NotRead() : Read(git, opt.Base, doc);
        }

        public static MergeHistory Read(Git git, string baseRef, TaskDoc doc)
        {
            try
            {
                if (!git.RefExists(baseRef))
                {
                    return MergeHistory.Unreadable(baseRef);
                }
                ProcResult r = git.Run("log", "--format=%s", baseRef);
                if (r.ExitCode != 0)
                {
                    return MergeHistory.Unreadable(baseRef);
                }
                return MergeHistory.FromSubjects(baseRef, r.Stdout, doc.ById);
            }
            catch (TaskGraphException)
            {
                // git 을 띄우지 못했다
                return MergeHistory.Unreadable(baseRef);
            }
        }
    }
}

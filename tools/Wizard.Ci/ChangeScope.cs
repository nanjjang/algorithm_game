using System;
using System.Collections.Generic;

namespace Wizard.Ci
{
    // 이 체크아웃에서 바뀐 경로(PIPELINE 3.8 "경로 조건", 4.6 "바뀐 프로젝트의 테스트").
    //   비교 기준: baseRef 가 있으면 그것, 없으면 main 과의 갈림점(git merge-base HEAD main).
    //     baseRef 는 명령 줄 층(Program.Run)이 --base, 없으면 WIZARD_CI_BASE 에서 정해 넘긴다. 여기서는 프로세스 환경을 읽지 않는다
    //     (읽으면 바깥 게이트의 WIZARD_CI_BASE 가 임시 저장소를 보는 자체 시험에 샌다. FIX-4).
    //   바뀐 경로 = 기준과 작업 트리의 차이(커밋한 것, 올린 것, 고치고 아직 올리지 않은 것) + 추적하지 않는 새 파일(.gitignore 제외).
    //   경로를 줄이지 않고 전부 도는 경우(All): 게이트·--all(WIZARD_CI_FORCE_ALL=1), 지금 브랜치가 main 이다, main 이 없다,
    //   git 저장소가 아니다. 줄일 근거가 없으면 줄이지 않는다.
    public sealed class ChangeScope
    {
        private ChangeScope(bool all, string reason, string? baseCommit, IReadOnlyList<string> paths)
        {
            All = all;
            Reason = reason;
            BaseCommit = baseCommit;
            Paths = paths;
        }

        // true 면 "전부 바뀐 것으로 본다"
        public bool All { get; }

        public string Reason { get; }

        public string? BaseCommit { get; }

        // 저장소 기준 경로('/' 구분), 오름차순, 중복 없음. All 이면 비어 있다
        public IReadOnlyList<string> Paths { get; }

        public static ChangeScope Everything(string reason) => new ChangeScope(true, reason, null, Array.Empty<string>());

        public static ChangeScope Of(IEnumerable<string> paths, string? baseCommit, string reason)
        {
            var set = new SortedSet<string>(paths, StringComparer.Ordinal);
            return new ChangeScope(false, reason, baseCommit, new List<string>(set));
        }

        public static ChangeScope Detect(string root, string? baseRef, bool forceAll)
        {
            if (forceAll)
            {
                return Everything("WIZARD_CI_FORCE_ALL=1(게이트 또는 --all): 바뀐 경로로 줄이지 않는다");
            }
            if (!Git.IsRepo(root))
            {
                return Everything(root + " 은 git 저장소가 아니다: 전부 돈다");
            }
            string? baseCommit;
            if (baseRef != null)
            {
                baseCommit = Git.RevParse(root, baseRef);
                if (baseCommit == null)
                {
                    throw new CiException("비교 기준 '" + baseRef + "' 을 git 이 모른다(WIZARD_CI_BASE 또는 --base)");
                }
            }
            else
            {
                string? branch = Git.CurrentBranch(root);
                if (branch == "main")
                {
                    return Everything("지금 브랜치가 main 이다: 비교할 기준이 없어 전부 돈다");
                }
                if (Git.RevParse(root, "main") == null)
                {
                    return Everything("main 브랜치가 없다: 전부 돈다(WIZARD_CI_BASE 로 기준을 줄 수 있다)");
                }
                ProcResult mb = Git.TryRun(root, "merge-base", "HEAD", "main");
                if (mb.ExitCode != 0)
                {
                    return Everything("HEAD 와 main 의 갈림점이 없다: 전부 돈다");
                }
                baseCommit = mb.Stdout.Trim();
            }
            var paths = new List<string>();
            paths.AddRange(Git.Paths(root, "diff", "--name-only", "-z", "--no-renames", baseCommit));
            paths.AddRange(Git.Paths(root, "ls-files", "--others", "--exclude-standard", "-z"));
            return Of(paths, baseCommit, "기준 " + baseCommit.Substring(0, Math.Min(12, baseCommit.Length)) + "(" + (baseRef ?? "merge-base HEAD main") + ")과의 차이");
        }

        // 경로 하나라도 접두어들 중 하나로 시작하는가(All 이면 언제나 true)
        public bool Touches(IEnumerable<string> prefixes, out string? first)
        {
            first = null;
            if (All)
            {
                return true;
            }
            foreach (string p in Paths)
            {
                foreach (string prefix in prefixes)
                {
                    if (p.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        first = p;
                        return true;
                    }
                }
            }
            return false;
        }
    }
}

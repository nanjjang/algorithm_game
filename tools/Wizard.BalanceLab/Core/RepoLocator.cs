using System;
using System.IO;

namespace Wizard.BalanceLab.Core
{
    // 저장소 뿌리(Wizard.sln 과 data/ 가 있는 폴더). 현재 폴더는 믿지 않는다: 셸은 호출마다 메인 저장소로 돌아가므로(CLAUDE.md 9절)
    // 이 도구를 빌드한 체크아웃(실행 파일의 위치에서 위로)을 먼저 찾고, 그다음에 현재 폴더에서 찾는다. --repo 가 있으면 그것이 먼저다.
    internal static class RepoLocator
    {
        public static bool TryFind(string? explicitRoot, out string root)
        {
            root = string.Empty;
            if (explicitRoot != null)
            {
                return IsRoot(explicitRoot) && SetRoot(explicitRoot, out root);
            }
            return TryWalkUp(AppContext.BaseDirectory, out root) || TryWalkUp(Directory.GetCurrentDirectory(), out root);
        }

        private static bool TryWalkUp(string start, out string root)
        {
            root = string.Empty;
            string? dir = Path.GetFullPath(start);
            while (!string.IsNullOrEmpty(dir))
            {
                if (IsRoot(dir))
                {
                    root = dir;
                    return true;
                }
                dir = Path.GetDirectoryName(dir);
            }
            return false;
        }

        private static bool SetRoot(string dir, out string root)
        {
            root = Path.GetFullPath(dir);
            return true;
        }

        private static bool IsRoot(string dir) =>
            File.Exists(Path.Combine(dir, "Wizard.sln")) && Directory.Exists(Path.Combine(dir, "data"));
    }
}

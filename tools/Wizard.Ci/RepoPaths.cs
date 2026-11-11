using System;
using System.IO;

namespace Wizard.Ci
{
    // 저장소 뿌리. 현재 폴더에 기대지 않는다(PIPELINE 4.3): ci.sh 는 자기 위치에서 구한 뿌리를 --root 로 넘기고,
    // --root 가 없으면 이 도구가 놓인 곳(artifacts/ci-runner/bin/…)에서 위로 올라가 Wizard.sln 과 tools/ci/ci.sh 가 함께 있는 첫 폴더를 고른다.
    // 작업 트리(.claude/worktrees/<id>)의 도구는 그 작업 트리를 찾는다(메인 저장소를 읽어 거짓 통과가 나지 않게).
    public static class RepoPaths
    {
        public static string FindFrom(string start)
        {
            string? dir = Normalize(start);
            while (dir != null)
            {
                if (IsRoot(dir))
                {
                    return dir;
                }
                dir = Path.GetDirectoryName(dir);
            }
            throw new CiException("저장소 뿌리(Wizard.sln + tools/ci/ci.sh)를 " + start + " 위에서 찾지 못했다. --root <경로>로 알려 준다");
        }

        public static bool IsRoot(string dir) =>
            File.Exists(Path.Combine(dir, "Wizard.sln")) && File.Exists(Path.Combine(dir, "tools", "ci", "ci.sh"));

        public static string Resolve(string? rootOption)
        {
            if (rootOption == null)
            {
                return FindFrom(AppContext.BaseDirectory);
            }
            string root = Normalize(rootOption);
            if (!IsRoot(root))
            {
                throw new CiException("--root " + root + " 은 저장소 뿌리가 아니다(Wizard.sln 과 tools/ci/ci.sh 가 없다)");
            }
            return root;
        }

        public static string Normalize(string path)
        {
            string full = Path.GetFullPath(path);
            while (full.Length > 1 && full.EndsWith('/'))
            {
                full = full.Substring(0, full.Length - 1);
            }
            return full;
        }

        // 저장소 기준 상대 경로('/' 구분)
        public static string Relative(string root, string path) => Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/');

        public static string Combine(string root, string relative) => Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
    }
}

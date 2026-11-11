using System;
using System.IO;

namespace Wizard.TestKit.Shared
{
    // 저장소 뿌리를 찾는다. 현재 폴더가 아니라 시작 위치(테스트 어셈블리 경로, Unity 의 Application.dataPath)에서
    // 위로 올라가며 Wizard.sln 과 docs/spec 이 함께 있는 첫 폴더를 고른다. 작업 트리(.claude/worktrees/<id>)
    // 안에서 돌면 그 작업 트리가 먼저 걸린다(메인 저장소를 읽어 거짓 통과가 나지 않게, CLAUDE.md 9절).
    public static class RepoRoot
    {
        public static string Find(string startDirectory)
        {
            if (startDirectory == null)
            {
                throw new ArgumentNullException(nameof(startDirectory));
            }
            string? dir = Path.GetFullPath(startDirectory);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir, "Wizard.sln")) && Directory.Exists(Path.Combine(dir, "docs", "spec")))
                {
                    return dir;
                }
                dir = Path.GetDirectoryName(dir);
            }
            throw new DirectoryNotFoundException("repository root (Wizard.sln + docs/spec) not found above " + startDirectory);
        }

        // 저장소 기준 상대 경로('/' 구분)를 절대 경로로 바꾼다.
        public static string Resolve(string root, string relativePath)
        {
            string[] parts = relativePath.Split('/');
            string path = root;
            foreach (string p in parts)
            {
                if (p.Length > 0)
                {
                    path = Path.Combine(path, p);
                }
            }
            return path;
        }
    }
}

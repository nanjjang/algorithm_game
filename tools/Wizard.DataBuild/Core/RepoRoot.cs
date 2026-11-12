using System;
using System.IO;

namespace Wizard.DataBuild.Core
{
    // 저장소 뿌리: 이 도구의 위치에서 위로 올라가며 Wizard.sln 이 있는 첫 폴더. 현재 폴더는 보지 않는다(CLAUDE.md 9절).
    public static class RepoRoot
    {
        public static string Find() => FindFrom(AppContext.BaseDirectory);

        public static string FindFrom(string start)
        {
            string? dir = Path.GetFullPath(start).TrimEnd(Path.DirectorySeparatorChar);
            while (!string.IsNullOrEmpty(dir))
            {
                if (File.Exists(Path.Combine(dir, "Wizard.sln")))
                {
                    return dir;
                }
                dir = Path.GetDirectoryName(dir);
            }
            throw new DataBuildException("Wizard.sln 을 " + start + " 위에서 찾지 못했다(--root 로 저장소 뿌리를 준다)");
        }
    }
}

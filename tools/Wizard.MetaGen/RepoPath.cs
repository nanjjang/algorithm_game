using System;
using System.IO;
using System.Text;

namespace Wizard.MetaGen
{
    // 저장소 기준 상대 경로의 정규형: 구분자 '/', 앞뒤 '/' 없음, 유니코드 NFC.
    // NFC 는 계약(11.5)이 정하지 않은 부분을 메운 것이다. macOS 파일 시스템은 이름을 만든 꼴(NFC/NFD)대로 돌려주므로
    // 같은 이름이 기기마다 다른 바이트가 되지 않게 한 꼴로 모은다. ASCII 경로에서는 아무것도 바꾸지 않는다.
    public static class RepoPath
    {
        public static string Normalize(string path)
        {
            string p = path.Replace('\\', '/').Trim('/');
            return p.IsNormalized(NormalizationForm.FormC) ? p : p.Normalize(NormalizationForm.FormC);
        }

        public static string Relative(string root, string fullPath)
        {
            return Normalize(Path.GetRelativePath(root, fullPath));
        }

        // 저장소 뿌리는 현재 폴더가 아니라 이 도구의 위치에서 구한다(build-tools 규칙).
        // 실행 파일은 <뿌리>/artifacts/bin/Wizard.MetaGen/<구성>/ 에 있으므로 위로 올라가며 Wizard.sln 과 global.json 을 찾는다.
        public static string? FindRepoRoot(string startDirectory)
        {
            DirectoryInfo? dir = new DirectoryInfo(startDirectory);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "Wizard.sln")) &&
                    File.Exists(Path.Combine(dir.FullName, "global.json")))
                {
                    return dir.FullName;
                }
                dir = dir.Parent;
            }
            return null;
        }

        public static bool IsUnder(string relativePath, string prefixDirectory)
        {
            return relativePath == prefixDirectory ||
                   relativePath.StartsWith(prefixDirectory + "/", StringComparison.Ordinal);
        }
    }
}

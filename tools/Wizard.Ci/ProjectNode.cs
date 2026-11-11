using System.Collections.Generic;

namespace Wizard.Ci
{
    // 프로젝트 하나(ProjectGraph)
    public sealed class ProjectNode
    {
        public ProjectNode(string path, string dir, bool isTest, IReadOnlyCollection<string> references)
        {
            Path = path;
            Dir = dir;
            IsTest = isTest;
            References = new HashSet<string>(references, System.StringComparer.Ordinal);
        }

        // 저장소 기준 csproj 경로
        public string Path { get; }

        // 저장소 기준 폴더
        public string Dir { get; }

        public bool IsTest { get; }

        // 직접 참조하는 프로젝트(저장소 기준 csproj 경로)
        public HashSet<string> References { get; }
    }
}

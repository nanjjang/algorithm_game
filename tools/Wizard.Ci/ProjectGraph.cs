using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Wizard.Ci
{
    // dotnet 프로젝트의 참조 그래프. 프로젝트를 훑는 범위는 src/, server/, tests/, tools/ 다(ARCHITECTURE 3.4, T-43).
    //   간선: csproj 의 <ProjectReference Include="…">, 그리고 Directory.Build.props 가 공유(src/*, tests/Wizard.TestKit/Shared)·서버
    //   프로젝트에 거는 분석기(tools/Wizard.Analyzers). 테스트 프로젝트: <IsTestProject>true</IsTestProject> 또는 Microsoft.NET.Test.Sdk 참조.
    public sealed class ProjectGraph
    {
        public static readonly string[] Roots = { "src", "server", "tests", "tools" };
        public const string AnalyzerProject = "tools/Wizard.Analyzers/Wizard.Analyzers.csproj";

        private static readonly HashSet<string> SkippedDirs = new HashSet<string>(StringComparer.Ordinal)
        {
            "bin", "obj", "artifacts", "node_modules", ".git",
        };

        private readonly SortedDictionary<string, ProjectNode> _nodes;

        private ProjectGraph(SortedDictionary<string, ProjectNode> nodes)
        {
            _nodes = nodes;
        }

        // 저장소 기준 csproj 경로 → 프로젝트
        public IReadOnlyDictionary<string, ProjectNode> Nodes => _nodes;

        public static ProjectGraph Load(string root)
        {
            var files = new List<KeyValuePair<string, string>>();
            foreach (string r in Roots)
            {
                string dir = Path.Combine(root, r);
                if (Directory.Exists(dir))
                {
                    Collect(root, dir, files);
                }
            }
            return Parse(files);
        }

        // (저장소 기준 csproj 경로, 내용)
        public static ProjectGraph Parse(IReadOnlyList<KeyValuePair<string, string>> files)
        {
            var nodes = new SortedDictionary<string, ProjectNode>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> f in files)
            {
                XDocument doc;
                try
                {
                    doc = XDocument.Parse(f.Value);
                }
                catch (XmlException e)
                {
                    throw new CiException(f.Key + " 을 XML 로 읽지 못했다: " + e.Message);
                }
                string dir = DirOf(f.Key);
                var refs = new SortedSet<string>(StringComparer.Ordinal);
                bool isTest = false;
                foreach (XElement e in doc.Descendants())
                {
                    string n = e.Name.LocalName;
                    if (n == "ProjectReference")
                    {
                        string? inc = (string?)e.Attribute("Include");
                        if (inc != null)
                        {
                            refs.Add(Join(dir, inc.Replace('\\', '/')));
                        }
                    }
                    else if (n == "IsTestProject" && string.Equals(e.Value.Trim(), "true", StringComparison.OrdinalIgnoreCase))
                    {
                        isTest = true;
                    }
                    else if (n == "PackageReference" && (string?)e.Attribute("Include") == "Microsoft.NET.Test.Sdk")
                    {
                        isTest = true;
                    }
                }
                if (GetsAnalyzer(f.Key))
                {
                    refs.Add(AnalyzerProject);
                }
                nodes[f.Key] = new ProjectNode(f.Key, dir, isTest, refs);
            }
            return new ProjectGraph(nodes);
        }

        // Directory.Build.props 의 분석기 연결 조건과 같다(공유·서버 묶음, 분석기 자신은 빼고)
        public static bool GetsAnalyzer(string csproj) =>
            csproj != AnalyzerProject &&
            (csproj.StartsWith("src/", StringComparison.Ordinal) || csproj.StartsWith("server/", StringComparison.Ordinal) ||
             csproj.StartsWith("tests/Wizard.TestKit/Shared/", StringComparison.Ordinal));

        // 경로를 가진 프로젝트(폴더가 가장 깊게 맞는 것). 없으면 null
        public ProjectNode? Owner(string path)
        {
            ProjectNode? best = null;
            foreach (ProjectNode n in _nodes.Values)
            {
                if (path.StartsWith(n.Dir + "/", StringComparison.Ordinal) && (best == null || n.Dir.Length > best.Dir.Length))
                {
                    best = n;
                }
            }
            return best;
        }

        // 프로젝트들과, 그것을 (전이로) 참조하는 프로젝트 전부
        public SortedSet<string> WithDependents(IEnumerable<string> projects)
        {
            var result = new SortedSet<string>(StringComparer.Ordinal);
            var queue = new Queue<string>();
            foreach (string p in projects)
            {
                if (result.Add(p))
                {
                    queue.Enqueue(p);
                }
            }
            while (queue.Count > 0)
            {
                string cur = queue.Dequeue();
                foreach (ProjectNode n in _nodes.Values)
                {
                    if (n.References.Contains(cur) && result.Add(n.Path))
                    {
                        queue.Enqueue(n.Path);
                    }
                }
            }
            return result;
        }

        private static string DirOf(string rel)
        {
            int i = rel.LastIndexOf('/');
            return i < 0 ? "" : rel.Substring(0, i);
        }

        // 저장소 기준 폴더 + 상대 경로 → 저장소 기준 경로("a/b" + "../c/d.csproj" → "a/c/d.csproj")
        public static string Join(string dir, string relative)
        {
            var parts = new List<string>();
            if (!relative.StartsWith("/", StringComparison.Ordinal) && dir.Length > 0)
            {
                parts.AddRange(dir.Split('/'));
            }
            foreach (string p in relative.Split('/'))
            {
                if (p.Length == 0 || p == ".")
                {
                    continue;
                }
                if (p == "..")
                {
                    if (parts.Count > 0)
                    {
                        parts.RemoveAt(parts.Count - 1);
                    }
                    continue;
                }
                parts.Add(p);
            }
            return string.Join("/", parts);
        }

        private static void Collect(string root, string dir, List<KeyValuePair<string, string>> files)
        {
            foreach (string sub in Directory.GetDirectories(dir))
            {
                if (!SkippedDirs.Contains(Path.GetFileName(sub)))
                {
                    Collect(root, sub, files);
                }
            }
            foreach (string f in Directory.GetFiles(dir, "*.csproj"))
            {
                files.Add(new KeyValuePair<string, string>(RepoPaths.Relative(root, f), File.ReadAllText(f, Encoding.UTF8)));
            }
        }
    }
}

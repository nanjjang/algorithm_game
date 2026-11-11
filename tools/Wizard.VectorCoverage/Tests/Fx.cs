using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Wizard.TestKit.Shared;
using Wizard.TestKit.Shared.Vectors;

namespace Wizard.VectorCoverage.Tests
{
    // 테스트 도우미: 저장소의 실제 규칙(tests/vectors.config.json)을 명세 01 하나로 줄인 설정과, 그 규칙으로 읽는 작은 가짜 명세.
    internal static class Fx
    {
        public static readonly string RepoRoot = Wizard.TestKit.Shared.RepoRoot.Find(AppContext.BaseDirectory);

        public static VectorConfig RealConfig() => VectorConfig.LoadFromRepo(RepoRoot);

        // 명세 01 만 남긴 설정(다른 명세 파일이 없다는 문제가 나지 않게)
        public static VectorConfig ConfigOnly01()
        {
            JsonNode root = JsonNode.Parse(File.ReadAllText(Path.Combine(RepoRoot, "tests", "vectors.config.json"), Encoding.UTF8))!;
            JsonObject specs = root["specs"]!.AsObject();
            foreach (string k in specs.Select(p => p.Key).ToList())
            {
                if (k != "01")
                {
                    specs.Remove(k);
                }
            }
            return VectorConfig.Parse(root.ToJsonString());
        }

        // 14.1 절: T-H01~T-H03, 14.2 절: T-M01~T-M02, 벡터 장 밖(15장): T-Z01
        public const string Spec01 =
            "# 가짜 명세\n" +
            "\n" +
            "## 14. 테스트 벡터\n" +
            "\n" +
            "### 14.1 열\n" +
            "\n" +
            "| ID | given | then |\n" +
            "|---|---|---|\n" +
            "| T-H01 | a | b |\n" +
            "| T-H02 | a | b |\n" +
            "| T-H03 | a | b |\n" +
            "\n" +
            "### 14.2 이동\n" +
            "\n" +
            "| ID | given | then |\n" +
            "|---|---|---|\n" +
            "| T-M01 | a | b |\n" +
            "| T-M02 | a | b |\n" +
            "\n" +
            "## 15. 다른 장\n" +
            "\n" +
            "- **T-Z01** 장 밖에서 정의한 벡터\n";

        public static SpecVectorScan Scan01()
        {
            var files = new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>("01-fake.md", Spec01) };
            SpecVectorScan scan = SpecVectorScanner.Scan(files, ConfigOnly01());
            if (scan.Problems.Count != 0)
            {
                throw new InvalidOperationException("가짜 명세에 문제가 있다: " + string.Join("; ", scan.Problems));
            }
            return scan;
        }

        public static TaskEntry Task(string id, params string[] vectors) => new TaskEntry(id, "M1", vectors);

        public static IReadOnlyList<TaskEntry> Tasks(params TaskEntry[] tasks) => tasks;

        public static Regex TestNameRx => ConfigOnly01().TestName;

        public static TestIndex Index(params string[] sources)
        {
            var files = new List<KeyValuePair<string, string>>();
            for (int i = 0; i < sources.Length; i++)
            {
                files.Add(new KeyValuePair<string, string>("tests/T" + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".cs", sources[i]));
            }
            return TestIndex.Scan(files, TestNameRx);
        }

        // [Fact] 메서드들을 가진 테스트 클래스 소스
        public static string FactClass(params string[] methodNames)
        {
            var sb = new StringBuilder("using Xunit;\nnamespace Demo.Tests\n{\n    public sealed class DemoTests\n    {\n");
            foreach (string m in methodNames)
            {
                sb.Append("        [Fact]\n        public void ").Append(m).Append("() { }\n");
            }
            sb.Append("    }\n}\n");
            return sb.ToString();
        }

        public static ISet<string> Done(params string[] ids) => new HashSet<string>(ids, StringComparer.Ordinal);
    }
}

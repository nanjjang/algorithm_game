using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Wizard.TestKit.Shared.Vectors;

namespace Wizard.VectorCoverage
{
    // 테스트 소스에서 벡터 테스트 메서드를 모은다. 메서드 이름이 testName 규칙(tests/vectors.config.json)에 맞고
    // 테스트 특성([Fact], [Theory], NUnit 의 [Test], [TestCase], [TestCaseSource], [UnityTest])이 붙은 것만 센다.
    // 주석과 문자열 속의 이름은 세지 않는다(구문 나무로 본다).
    // 훑는 곳: tests/, tools/, unity/Assets/. 그 아래의 bin/, obj/, artifacts/, Library/, 이름이 '~' 로 끝나는 폴더(Unity 가 무시한다)는 건너뛴다.
    public sealed class TestIndex
    {
        public static readonly string[] ScanRoots = { "tests", "tools", "unity/Assets" };

        private static readonly HashSet<string> TestAttributes = new HashSet<string>(StringComparer.Ordinal)
        {
            "Fact", "Theory", "Test", "TestCase", "TestCaseSource", "UnityTest",
        };

        private static readonly HashSet<string> SkippedDirs = new HashSet<string>(StringComparer.Ordinal)
        {
            "bin", "obj", "artifacts", "Library", "Temp", "Logs", ".git", ".claude", "node_modules",
        };

        // 벡터 테스트처럼 보이는 이름(S<두 자리>_…): 규칙에 맞지 않으면 문제로 알린다
        private static readonly Regex LooksLikeVector = new Regex("^S[0-9]{2}_", RegexOptions.CultureInvariant);

        private TestIndex(IReadOnlyList<VectorTest> tests, IReadOnlyList<string> problems)
        {
            Tests = tests;
            Problems = problems;
        }

        // 경로 오름차순, 파일 안에서는 줄 순서
        public IReadOnlyList<VectorTest> Tests { get; }

        // 이름 규칙에 맞지 않는 S<두 자리>_ 테스트
        public IReadOnlyList<string> Problems { get; }

        public static TestIndex ScanRepo(string root, Regex testName)
        {
            var files = new List<KeyValuePair<string, string>>();
            foreach (string rel in ScanRoots)
            {
                string dir = Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar));
                if (Directory.Exists(dir))
                {
                    Collect(root, dir, files);
                }
            }
            files.Sort((x, y) => string.CompareOrdinal(x.Key, y.Key));
            return Scan(files, testName);
        }

        // (저장소 기준 경로, 내용) 목록을 훑는다
        public static TestIndex Scan(IReadOnlyList<KeyValuePair<string, string>> files, Regex testName)
        {
            var tests = new List<VectorTest>();
            var problems = new List<string>();
            var options = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
            foreach (KeyValuePair<string, string> f in files)
            {
                if (f.Value.IndexOf("S", StringComparison.Ordinal) < 0)
                {
                    continue;
                }
                SyntaxTree tree = CSharpSyntaxTree.ParseText(f.Value, options);
                foreach (MethodDeclarationSyntax m in tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
                {
                    string name = m.Identifier.ValueText;
                    if (!LooksLikeVector.IsMatch(name) || !HasTestAttribute(m))
                    {
                        continue;
                    }
                    int line = m.Identifier.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                    string where = f.Key + ":" + line.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    if (!VectorNames.TryParseTestName(testName, name, out string spec, out string id))
                    {
                        problems.Add(where + ": test '" + name + "' looks like a vector test but does not match the name rule S<spec>_<id>__<desc>");
                        continue;
                    }
                    tests.Add(new VectorTest(spec, id, QualifiedName(m), f.Key, line));
                }
            }
            return new TestIndex(tests, problems);
        }

        public List<VectorTest> For(SpecVector v)
        {
            var list = new List<VectorTest>();
            foreach (VectorTest t in Tests)
            {
                if (string.Equals(t.Spec, v.Spec, StringComparison.Ordinal) && string.Equals(t.Id, v.Id, StringComparison.Ordinal))
                {
                    list.Add(t);
                }
            }
            return list;
        }

        private static bool HasTestAttribute(MethodDeclarationSyntax m)
        {
            foreach (AttributeListSyntax list in m.AttributeLists)
            {
                foreach (AttributeSyntax a in list.Attributes)
                {
                    string n = LastIdentifier(a.Name);
                    if (n.EndsWith("Attribute", StringComparison.Ordinal))
                    {
                        n = n.Substring(0, n.Length - "Attribute".Length);
                    }
                    if (TestAttributes.Contains(n))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static string LastIdentifier(NameSyntax name)
        {
            switch (name)
            {
                case QualifiedNameSyntax q: return q.Right.Identifier.ValueText;
                case AliasQualifiedNameSyntax a: return a.Name.Identifier.ValueText;
                case SimpleNameSyntax s: return s.Identifier.ValueText;
                default: return name.ToString();
            }
        }

        private static string QualifiedName(MethodDeclarationSyntax m)
        {
            var parts = new List<string> { m.Identifier.ValueText };
            for (SyntaxNode? n = m.Parent; n != null; n = n.Parent)
            {
                switch (n)
                {
                    case BaseTypeDeclarationSyntax t: parts.Add(t.Identifier.ValueText); break;
                    case BaseNamespaceDeclarationSyntax ns: parts.Add(ns.Name.ToString()); break;
                }
            }
            parts.Reverse();
            return string.Join(".", parts);
        }

        private static void Collect(string root, string dir, List<KeyValuePair<string, string>> files)
        {
            foreach (string sub in Directory.GetDirectories(dir))
            {
                string name = Path.GetFileName(sub);
                if (SkippedDirs.Contains(name) || name.EndsWith("~", StringComparison.Ordinal))
                {
                    continue;
                }
                Collect(root, sub, files);
            }
            foreach (string file in Directory.GetFiles(dir, "*.cs"))
            {
                string rel = Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/');
                files.Add(new KeyValuePair<string, string>(rel, File.ReadAllText(file, Encoding.UTF8)));
            }
        }
    }
}

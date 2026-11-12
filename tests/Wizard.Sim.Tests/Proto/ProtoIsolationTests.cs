using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Wizard.TestKit;
using Xunit;

namespace Wizard.Sim.Tests.Proto
{
    // 수락 조건 "Proto 폴더를 지우면 나머지가 그대로 컴파일된다"의 정적 검사: 시제품의 두 폴더(src/Wizard.Sim/Proto/, tests/Wizard.Sim.Tests/Proto/)
    // 밖의 C# 파일이 시제품의 이름(네임스페이스 Wizard.Sim.Proto, Proto 로 시작하는 타입·멤버, _proto 필드)을 쓰지 않는다.
    // 시제품이 붙는 길은 뼈대가 선언한 부분 메서드(훅)의 구현뿐이고, 구현을 지우면 호출은 컴파일에서 빠진다.
    // (실제로 두 폴더를 뺀 사본을 빌드해 본 결과는 작업의 결과 보고에 적는다.)
    public sealed class ProtoIsolationTests
    {
        private static readonly Regex ProtoName = new Regex(@"Wizard\.Sim\.Proto\b|\b_?[pP]roto[A-Z]\w*", RegexOptions.CultureInvariant);

        private static readonly string[] Roots = { "src", "tests", "tools", "server", "unity/Assets" };

        private static readonly string[] ProtoFolders =
        {
            "src/Wizard.Sim/Proto/",
            "tests/Wizard.Sim.Tests/Proto/",
        };

        [Fact]
        public void NoSourceOutsideTheProtoFoldersNamesThePrototype()
        {
            var offenders = new List<string>();
            foreach (string root in Roots)
            {
                string dir = TestRepo.Path(root);
                if (!Directory.Exists(dir))
                {
                    continue;
                }
                foreach (string file in Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories))
                {
                    string rel = Path.GetRelativePath(TestRepo.Root, file).Replace('\\', '/');
                    if (IsUnderProto(rel) || rel.Contains("/bin/", StringComparison.Ordinal) || rel.Contains("/obj/", StringComparison.Ordinal))
                    {
                        continue;
                    }
                    string text = File.ReadAllText(file);
                    Match m = ProtoName.Match(text);
                    if (m.Success)
                    {
                        offenders.Add(rel + ": " + m.Value);
                    }
                }
            }
            Assert.True(offenders.Count == 0, string.Join("\n", offenders));
        }

        [Fact]
        public void PrototypeFilesOnlyImplementDeclaredHooks()
        {
            // 시제품의 SimWorld 부분 파일은 새 부분 메서드를 선언하지 않는다(훅을 더하는 것은 계약 변경, CONTRACTS 3.2).
            // 구현 줄("partial void 이름(")의 이름은 전부 뼈대의 선언 파일에 있어야 한다
            string hooks = File.ReadAllText(TestRepo.Path("src/Wizard.Sim/Runtime/Core/SimWorld.Hooks.cs"));
            var impl = new Regex(@"partial\s+void\s+(\w+)\s*\(", RegexOptions.CultureInvariant);
            int seen = 0;
            foreach (string file in Directory.EnumerateFiles(TestRepo.Path("src/Wizard.Sim/Proto"), "SimWorld.*.cs"))
            {
                foreach (Match m in impl.Matches(File.ReadAllText(file)))
                {
                    string name = m.Groups[1].Value;
                    Assert.True(Regex.IsMatch(hooks, @"partial\s+void\s+" + name + @"\s*\("), name + " is not a declared hook");
                    seen++;
                }
            }
            Assert.True(seen > 0);
        }

        private static bool IsUnderProto(string rel)
        {
            foreach (string p in ProtoFolders)
            {
                if (rel.StartsWith(p, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;
using Wizard.TestKit;
using Xunit;

namespace Wizard.Text.Tests
{
    // 수락 조건: 코어·규칙·프로토콜 어셈블리가 Wizard.Text 를 참조하지 않는다(ARCHITECTURE 3.2·8.3, 이름 표는 표시 계층만 읽는다).
    // dotnet 쪽(csproj 의 ProjectReference)과 Unity 쪽(asmdef 의 references)을 전이까지 따라간다.
    // 분석기 WZ0023(AssemblyRoles)이 컴파일에서 같은 것을 막는다. 이 검사는 프로젝트 파일 자체를 본다.
    public sealed class TextLayeringTests
    {
        public static readonly TheoryData<string> NonDisplayAssemblies = new TheoryData<string>
        {
            "Wizard.Core", "Wizard.Content", "Wizard.Sim", "Wizard.Rules", "Wizard.Protocol", "Wizard.Netcode",
        };

        [Theory]
        [MemberData(nameof(NonDisplayAssemblies))]
        public void CsprojClosureDoesNotReachText(string assembly)
        {
            string start = TestRepo.Path("src/" + assembly + "/" + assembly + ".csproj");
            Assert.True(File.Exists(start), start);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var stack = new Stack<string>();
            stack.Push(Path.GetFullPath(start));
            while (stack.Count > 0)
            {
                string proj = stack.Pop();
                if (!seen.Add(proj))
                {
                    continue;
                }
                Assert.NotEqual("Wizard.Text", Path.GetFileNameWithoutExtension(proj));
                string dir = Path.GetDirectoryName(proj)!;
                foreach (XElement r in XDocument.Load(proj).Descendants().Where(e => e.Name.LocalName == "ProjectReference"))
                {
                    string include = (string?)r.Attribute("Include") ?? string.Empty;
                    stack.Push(Path.GetFullPath(Path.Combine(dir, include.Replace('\\', '/'))));
                }
            }
            Assert.Contains(seen, p => p.EndsWith(assembly + ".csproj", StringComparison.Ordinal));
        }

        [Theory]
        [MemberData(nameof(NonDisplayAssemblies))]
        public void AsmdefClosureDoesNotReachText(string assembly)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var stack = new Stack<string>();
            stack.Push(assembly);
            while (stack.Count > 0)
            {
                string name = stack.Pop();
                if (!seen.Add(name))
                {
                    continue;
                }
                Assert.NotEqual("Wizard.Text", name);
                string asmdef = TestRepo.Path("src/" + name + "/" + name + ".asmdef");
                if (!File.Exists(asmdef))
                {
                    Assert.False(name.StartsWith("Wizard.", StringComparison.Ordinal), "missing asmdef for " + name);
                    continue;
                }
                using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(asmdef));
                if (doc.RootElement.TryGetProperty("references", out JsonElement refs))
                {
                    foreach (JsonElement r in refs.EnumerateArray())
                    {
                        stack.Push(r.GetString() ?? string.Empty);
                    }
                }
            }
        }

        [Fact]
        public void TextAsmdefReferencesOnlyCoreAndHasNoEngine()
        {
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(TestRepo.Path("src/Wizard.Text/Wizard.Text.asmdef")));
            string[] refs = doc.RootElement.GetProperty("references").EnumerateArray().Select(e => e.GetString()!).ToArray();
            Assert.Equal(new[] { "Wizard.Core" }, refs);
            Assert.True(doc.RootElement.GetProperty("noEngineReferences").GetBoolean());
        }
    }
}

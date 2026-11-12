using System.IO;
using System.Linq;
using System.Text.Json;
using Wizard.TestKit;
using Wizard.ViewLogic.Common;
using Wizard.ViewLogic.Stack;
using Xunit;

namespace Wizard.ViewLogic.Tests.Common
{
    // 수락 조건: UnityEngine 을 참조하지 않는다(noEngineReferences). 화면 문자열은 이름 표 열쇠로만 가리킨다
    public sealed class EngineBoundaryTests
    {
        [Fact]
        public void ViewLogicAssemblyDoesNotReferenceUnity()
        {
            var refs = typeof(ScreenStack).Assembly.GetReferencedAssemblies().Select(a => a.Name ?? string.Empty).ToArray();
            Assert.DoesNotContain(refs, n => n.StartsWith("UnityEngine") || n.StartsWith("UnityEditor"));
            Assert.Same(typeof(ScreenStack).Assembly, typeof(InteractableGroup).Assembly);
        }

        [Fact]
        public void AsmdefKeepsNoEngineReferences()
        {
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(TestRepo.Path("src/Wizard.ViewLogic/Wizard.ViewLogic.asmdef")));
            Assert.True(doc.RootElement.GetProperty("noEngineReferences").GetBoolean());
        }

        [Theory]
        [InlineData("src/Wizard.ViewLogic/Stack")]
        [InlineData("src/Wizard.ViewLogic/Common")]
        public void SourcesNeitherUseUnityNorHangulLiterals(string folder)
        {
            foreach (string file in Directory.GetFiles(TestRepo.Path(folder), "*.cs", SearchOption.AllDirectories))
            {
                string text = File.ReadAllText(file);
                Assert.DoesNotContain("UnityEngine", text);
                // 한글은 주석에만 있다(이름 표 밖 한글 문자열은 names-scan 이 CI 에서도 막는다)
                foreach (string line in File.ReadAllLines(file))
                {
                    int comment = line.IndexOf("//", System.StringComparison.Ordinal);
                    string code = comment < 0 ? line : line.Substring(0, comment);
                    Assert.False(code.Any(c => c >= '가' && c <= '힣'), file + ": " + line);
                }
            }
        }
    }
}

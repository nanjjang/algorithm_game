using System.Linq;
using Xunit;

namespace Wizard.Text.Tests
{
    // CONTRACTS 5, ARCHITECTURE 8.3: Wizard.Text 는 Core 에만 기대고 공유 코드에 JSON 파서를 들이지 않는다.
    public sealed class TextAssemblyTests
    {
        [Fact]
        public void DependsOnlyOnCore()
        {
            string[] refs = typeof(ITextResolver).Assembly.GetReferencedAssemblies().Select(a => a.Name!).ToArray();
            Assert.DoesNotContain(refs, r => r.Contains("Json"));
            Assert.All(refs.Where(r => r.StartsWith("Wizard.")), r => Assert.Equal("Wizard.Core", r));
        }
    }
}

using System.Reflection;
using Xunit;

namespace Wizard.AssetCheck.Tests
{
    // ARCHITECTURE 3.4: 실행 파일 프로젝트는 진입점을 가진다. 도구의 실제 검사는 그 도구의 작업이 테스트와 같이 더한다.
    public sealed class EntryPointTests
    {
        [Fact]
        public void HasEntryPoint()
        {
            MethodInfo? main = typeof(Wizard.AssetCheck.Program).Assembly.EntryPoint;
            Assert.NotNull(main);
        }
    }
}

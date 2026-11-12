using System.Collections.Generic;
using Wizard.Protocol.Tests.Crypto.Shared;
using Xunit;

namespace Wizard.Protocol.Tests.Crypto
{
    // 머리 없는 플레이어가 부를 묶음(AeadConformance.RunAll)을 dotnet 에서도 그대로 돌린다(같은 코드, 같은 씨앗).
    public sealed class AeadConformanceSuiteTests
    {
        [Theory]
        [MemberData(nameof(AeadImplementations.Names), MemberType = typeof(AeadImplementations))]
        public void RunAll_HasNoProblems(string implementation)
        {
            List<string> problems = AeadConformance.RunAll(implementation, AeadImplementations.Create(implementation), AeadImplementations.Vectors());
            Assert.True(problems.Count == 0, string.Join("\n", problems));
        }
    }
}

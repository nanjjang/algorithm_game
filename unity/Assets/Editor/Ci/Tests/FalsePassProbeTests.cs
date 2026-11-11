using System;
using NUnit.Framework;

namespace Wizard.Unity.Editor
{
    // 거짓 통과 방지 탐침(ARCHITECTURE 4.1). unity.sh false-pass-check 가 WIZARD_CI_FALSE_PASS_PROBE=1 로 테스트를 돌리면
    // 이 테스트가 실패하고, 테스트 실행의 종료 코드가 0이 아니어야 한다. 0이면 실행 방식(-quit 등)이 테스트를 돌리지 않은 것이다.
    // 변수가 없으면 아무것도 확인하지 않고 끝난다.
    public sealed class FalsePassProbeTests
    {
        public const string ProbeVariable = "WIZARD_CI_FALSE_PASS_PROBE";

        [Test]
        public void FalsePassProbe_FailsOnlyWhenRequested()
        {
            if (Environment.GetEnvironmentVariable(ProbeVariable) == "1")
            {
                Assert.Fail("false-pass probe: this failure is intentional (" + ProbeVariable + "=1)");
            }
        }
    }
}

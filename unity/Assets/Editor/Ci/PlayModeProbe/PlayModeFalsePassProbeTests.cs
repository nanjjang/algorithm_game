using System;
using NUnit.Framework;

namespace Wizard.Unity.Editor
{
    // 거짓 통과 방지 탐침(PlayMode). FalsePassProbeTests 와 같은 뜻이다.
    // 이 탐침이 있어 PlayMode 실행의 테스트 수가 0이 아니다(unity.sh 는 0개 실행을 실패로 본다).
    public sealed class PlayModeFalsePassProbeTests
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

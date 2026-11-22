using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Wizard.Solo.Tests.PlayMode
{
    /// <summary>PlayMode 어셈블리가 컴파일되고 테스트 러너가 도는지 보는 자리. 통합 작업이 실제 흐름 테스트를 더한다.</summary>
    public class PlayModeSmokeTests
    {
        [UnityTest]
        public IEnumerator OneFramePasses()
        {
            yield return null;
            Assert.Pass();
        }
    }
}

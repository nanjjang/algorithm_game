using Xunit;

namespace Wizard.Ci.Tests
{
    // 다른 시험과 동시에 돌면 안 되는 시험의 묶음. xunit 은 병렬 묶음을 다 돈 뒤에 이 묶음을 혼자 돌린다.
    // 쓰는 곳: WholeSuiteUnderGateEnvironmentTests(이 어셈블리를 자식 프로세스로 다시 돌리며 같은 산출물 폴더를 쓴다)
    [CollectionDefinition(Name, DisableParallelization = true)]
    public sealed class SerialCollection
    {
        public const string Name = "serial";
    }
}

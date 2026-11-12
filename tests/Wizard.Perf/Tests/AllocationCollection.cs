using Xunit;

namespace Wizard.Perf.Tests
{
    // 할당을 재는 시험의 묶음. 병렬로 돌지 않는다(xUnit 은 이런 묶음을 다른 시험이 다 끝난 뒤 혼자 돌린다).
    // GC 없는 구간(NoGcWindow)은 프로세스 전체에 걸리므로 다른 스레드의 시험이 할당하거나 구간을 열면 구간이 깨진다
    [CollectionDefinition(Name, DisableParallelization = true)]
    public sealed class AllocationCollection
    {
        public const string Name = "allocation";
    }
}

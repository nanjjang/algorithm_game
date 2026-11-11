// 결정론 가드 탐침: 공유 패키지는 Unity 가 컴파일하지 못하는 record·init 을 쓰지 않는다(WZ0021).
// netstandard2.1 에 없는 IsExternalInit 을 직접 정의해 컴파일러 오류 없이 넘어가려는 경우도 분석기가 막아야 한다.
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit
    {
    }
}

namespace Wizard.Probe
{
    public sealed record SharedProbe(int A);

    public sealed class SharedProbe2
    {
        public int B { get; init; }
    }
}

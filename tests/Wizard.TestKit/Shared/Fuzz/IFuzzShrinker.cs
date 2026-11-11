using System.Collections.Generic;

namespace Wizard.TestKit.Shared.Fuzz
{
    // 실패한 입력보다 "작은" 후보를 낸다. 후보의 순서는 고정이어야 한다(같은 입력이면 같은 줄임 결과).
    // 앞쪽 후보일수록 크게 줄인 것이다. 무한히 줄어들지 않게 후보는 원래보다 엄격히 작아야 한다.
    public interface IFuzzShrinker<T>
    {
        IEnumerable<T> Shrink(T value);
    }
}

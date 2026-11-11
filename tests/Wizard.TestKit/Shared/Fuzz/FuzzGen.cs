using Wizard.Core;

namespace Wizard.TestKit.Shared.Fuzz
{
    // 입력 하나를 만든다. 난수는 넘겨받은 Pcg32 만 쓴다(같은 사례 씨앗이면 같은 입력, 모든 런타임에서).
    public delegate T FuzzGen<T>(ref Pcg32 rng);
}

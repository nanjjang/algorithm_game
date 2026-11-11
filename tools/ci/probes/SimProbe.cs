// 결정론 가드 탐침: Wizard.Sim 은 규칙 쪽 정의와 오류 타입을 참조하지 못하고(WZ0017) 유료 재화 값을 읽지 못한다(WZ0020).
namespace Wizard.Probe
{
    internal static class SimProbe
    {
        internal static object Violate(Wizard.Content.Rules.NetRuleSet net) =>
            (net, Wizard.Core.ErrorCode.None, Wizard.Content.CurrencyCode.Starlight);
    }
}

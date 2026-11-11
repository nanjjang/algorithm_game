// 결정론 가드 탐침: Wizard.Rules 에는 실수형이 없고(WZ0001) 강화 코드는 유료 재화 값을 읽지 않는다(WZ0020).
namespace Wizard.Rules.Enhance
{
    internal static class RulesProbe
    {
        internal static double Violate() => (double)Wizard.Content.CurrencyBucket.Paid;
    }
}

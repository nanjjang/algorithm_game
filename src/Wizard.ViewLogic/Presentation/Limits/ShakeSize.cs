namespace Wizard.ViewLogic.Presentation.Limits
{
    // spec 13 14.3절 흔들림 단위(effects.md 0.2): 소 4px, 중 8px, 대 12px, 특대 16px(+13 도달 한 곳뿐). 번호는 더하기만 한다
    public enum ShakeSize : byte
    {
        Small = 0,
        Medium = 1,
        Large = 2,
        XLarge = 3,
    }
}

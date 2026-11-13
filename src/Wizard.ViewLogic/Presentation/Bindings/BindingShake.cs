namespace Wizard.ViewLogic.Presentation
{
    // event_bindings.json 의 "shake" 칸(none, small, medium, large, xlarge). effects.md 0.2절 흔들림 소·중·대·특대, spec 13 14.3절 ShakeSize.
    // 폭·길이·누적·줄이기 설정은 연출 한도(M9-PRS-02)가 집행한다. 이 값은 "어느 크기의 사건인가"만 전한다
    public enum BindingShake : byte
    {
        None = 0,
        Small = 1,
        Medium = 2,
        Large = 3,
        XLarge = 4,
    }
}

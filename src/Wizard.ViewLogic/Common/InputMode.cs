namespace Wizard.ViewLogic.Common
{
    // spec 13 1.3절 InputModeTracker, spec 14 4.3절 "입력 모드 Mode ∈ {Mouse, Keyboard}". 마지막으로 쓴 장치를 따른다
    public enum InputMode
    {
        Mouse = 0,
        Keyboard = 1,
    }
}

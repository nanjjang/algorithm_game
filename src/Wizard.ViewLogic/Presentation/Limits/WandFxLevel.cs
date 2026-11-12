namespace Wizard.ViewLogic.Presentation.Limits
{
    // spec 13 13.2 wand_fx_level, effects.md 18절 "지팡이 효과": 전부 / 줄임 / 최소. 번호는 더하기만 한다
    public enum WandFxLevel : byte
    {
        All = 0,
        Reduced = 1,
        Minimal = 2,
    }
}

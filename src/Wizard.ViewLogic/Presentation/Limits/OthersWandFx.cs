namespace Wizard.ViewLogic.Presentation.Limits
{
    // spec 13 13.2 others_wand_fx, effects.md 18절 "남의 지팡이 효과(광장)": 전부 / 줄임 / 끔.
    // 줄임·끔에서도 본체 모양과 단계 표식은 보인다(입자가 아니다). 번호는 더하기만 한다
    public enum OthersWandFx : byte
    {
        All = 0,
        Reduced = 1,
        Off = 2,
    }
}

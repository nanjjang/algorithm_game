namespace Wizard.ViewLogic.Presentation.Limits
{
    // 적중 장식의 양(effects.md 9.3: 내 탄 수 문턱, "지팡이 효과" 옵션, 결투 상대). 뒤로 갈수록 적다. 번호는 더하기만 한다
    public enum HitDecorMode : byte
    {
        Full = 0,
        Half = 1,       // 절반(올림)
        Minimal = 2,    // 최소형(입자 2개)
    }
}

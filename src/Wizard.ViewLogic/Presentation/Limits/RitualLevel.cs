namespace Wizard.ViewLogic.Presentation.Limits
{
    // spec 13 14.5 "강화 의식": 전체 / 줄임(어두워짐 절반, 균열은 마법진 안에만) / 결과 카드만. 번호는 더하기만 한다
    public enum RitualLevel : byte
    {
        Full = 0,
        Reduced = 1,
        CardOnly = 2,
    }
}

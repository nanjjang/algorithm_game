namespace Wizard.ViewLogic.Presentation.Limits
{
    // spec 13 14.5 "예고의 흰 번쩍": Full 은 구역 전체, Reduced·Minimal 은 테두리만 굵게. 번호는 더하기만 한다
    public enum TelegraphFlash : byte
    {
        Zone = 0,
        ThickBorder = 1,
    }
}

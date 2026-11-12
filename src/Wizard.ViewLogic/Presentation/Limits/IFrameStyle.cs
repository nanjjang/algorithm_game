namespace Wizard.ViewLogic.Presentation.Limits
{
    // spec 13 14.5 "무적 중 캐릭터 깜빡임": Full 은 8Hz 반투명, 그 밖은 깜빡임 없이 60% 투명 + 흰 테두리. 번호는 더하기만 한다
    public enum IFrameStyle : byte
    {
        Blink8Hz = 0,
        Translucent60 = 1,
    }
}

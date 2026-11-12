namespace Wizard.ViewLogic.Common
{
    // spec 14 4.3절 "그릴 바탕 그림"의 다섯 상태. MAN-ui 파일의 상태 토큰은 SkinStateToken 이 준다
    public enum SkinState
    {
        Normal = 0,
        Hover = 1,
        Pressed = 2,
        On = 3,
        Disabled = 4,
    }
}

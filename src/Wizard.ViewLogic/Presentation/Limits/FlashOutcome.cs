namespace Wizard.ViewLogic.Presentation.Limits
{
    // spec 13 14.2절. 한도를 넘는 요청은 버리지 않고 테두리 강조로 바꾼다
    public enum FlashOutcome : byte
    {
        Flash = 0,
        BorderHighlight = 1,
    }
}

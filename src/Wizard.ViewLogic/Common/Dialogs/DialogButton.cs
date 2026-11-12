namespace Wizard.ViewLogic.Common.Dialogs
{
    // 공용 창의 버튼 하나. 글은 이름 표 열쇠, 상태는 Control(누를 수 있는 것의 상태 기계)
    public sealed class DialogButton
    {
        public DialogButton(DialogButtonRole role, string labelKey, ButtonTone tone, Interactable control)
        {
            Role = role;
            LabelKey = labelKey;
            Tone = tone;
            Control = control;
        }

        public DialogButtonRole Role { get; }
        public string LabelKey { get; }
        public ButtonTone Tone { get; }
        public Interactable Control { get; }

        // 조심 버튼의 테 색 토큰. 보통 버튼은 null(바탕 그림 그대로)
        public string? ToneToken => Tone == ButtonTone.Caution ? UiColorTokens.AlertAmber : null;

        public bool ShowsWarningTriangle => Tone == ButtonTone.Caution;
    }
}

using UnityEngine;
using Wizard.Solo.Art;

namespace Wizard.Solo.UI
{
    /// <summary>
    /// 저장 되돌림 알림 창(ScreenId.SaveRestored, 640×360, 08 1.6절): 제목 str.save_restored.title, 본문 .message, [확인].
    /// Title이 ScreenArgKeys.ProfileRestored를 받으면 이 창을 연다. [확인]·Esc·J = Pop.
    /// </summary>
    public sealed class UiSaveRestoredDialog : ScreenBase
    {
        public const float Width = 640f, Height = 360f;

        public override ScreenId Id => ScreenId.SaveRestored;
        public override ScreenLayer Layer => ScreenLayer.Modal;
        public override bool BlocksInput => true;

        protected override void BuildContent(RectTransform content)
        {
            var panel = UiKit.Panel(Ctx, content, UiPanelKind.Paper, 0f, 0f, Width, Height);
            var prt = panel.rectTransform;
            UiKit.Center(prt, Width, Height);
            panel.name = "SaveRestored";
            const float Pad = 32f;
            float th = UiTheme.LineHeight(UiFontId.F24);
            UiKit.Label(Ctx, prt, T(UiNameKeys.SaveRestoredTitle), UiFontId.F24, Pad, Pad, Width - 2f * Pad, th, UiTheme.PaperText);
            UiKit.Label(Ctx, prt, T(UiNameKeys.SaveRestoredMessage), UiFontId.F16, Pad, Pad + th + 24f, Width - 2f * Pad,
                UiTheme.LineHeight(UiFontId.F16) * 4f, UiTheme.PaperText, TextAnchor.UpperLeft, wrap: true);
            const float BtnW = 200f, BtnH = 64f;
            UiKit.Button(Ctx, prt, T(UiNameKeys.CommonOk), (Width - BtnW) * 0.5f, Height - Pad - BtnH, BtnW, BtnH, () => Close(true),
                UiButtonStyle.Large, Focus);
        }

        public override bool OnBack()
        {
            Close(true);
            return true;
        }

        public override bool OnMenuKey(MenuKey key)
        {
            if (key == MenuKey.Cancel) { Close(true); return true; }
            base.OnMenuKey(key);
            return true;
        }
    }
}

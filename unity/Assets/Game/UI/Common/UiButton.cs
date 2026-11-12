#nullable enable
using UnityEngine;
using Wizard.ViewLogic.Common;
using Wizard.ViewLogic.Common.Dialogs;

namespace Wizard.Unity.UI.Common
{
    // 버튼(자리 표시). 글은 이름 표 열쇠. 크기는 32×32px 이상(spec 14 4.3 최소 크기)이어야 한다.
    // 조심 버튼(ButtonTone.Caution)은 빨강이 아니라 호박색 테 + 경고 삼각(spec 13 1.5, spec 14 4.2). 삼각 그림(K-24)이 없는
    // 지금은 왼쪽 안쪽의 호박색 네모로 자리를 표시한다 [임시].
    public class UiButton : UiInteractiveWidget
    {
        public const int WarningMarkPx = 16;    // [임시] 경고 삼각 자리 표시 한 변
        public const int PaddingPx = 8;          // 글자 좌우 여백(8px 격자)

        private readonly UiPanel? _warning;

        public UiButton(UiScreenView owner, UiWidget parent, PixelRect localRect, Interactable control, string labelKey,
            ButtonTone tone = ButtonTone.Normal, UiFontId font = UiFontId.F24)
            : base(owner, parent, localRect, control, "Button:" + control.Id)
        {
            Tone = tone;
            Label = new UiLabel(owner.Context, Skin, LabelRect(localRect), font, TextAnchor.MiddleCenter);
            Label.SetKey(labelKey);
            if (tone == ButtonTone.Caution)
            {
                _warning = new UiPanel("WarningMark", Skin, WarningRect(localRect), framed: false);
                _warning.SetFill(UiPalette.AlertAmber);
            }
        }

        public ButtonTone Tone { get; }

        public UiLabel Label { get; }

        public bool ShowsWarningMark => _warning != null;

        public void SetLabelKey(string labelKey) => Label.SetKey(labelKey);

        protected override void OnResized(PixelRect localRect)
        {
            Label?.SetRect(LabelRect(localRect));
            _warning?.SetRect(WarningRect(localRect));
        }

        protected override void ApplySkin(SkinState state)
        {
            base.ApplySkin(state);
            if (Tone == ButtonTone.Caution && state != SkinState.Disabled)
            {
                Skin.SetBorder(UiPalette.AlertAmber);
            }
            Label.SetColor(TextColorOf(state));
        }

        private static PixelRect LabelRect(PixelRect r) => new PixelRect(PaddingPx, 0, r.Width - PaddingPx - PaddingPx, r.Height);

        private static PixelRect WarningRect(PixelRect r) => new PixelRect(PaddingPx, (r.Height - WarningMarkPx) / 2, WarningMarkPx, WarningMarkPx);
    }
}

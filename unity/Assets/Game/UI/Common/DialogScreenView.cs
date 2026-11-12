#nullable enable
using System.Collections.Generic;
using Wizard.ViewLogic.Common;
using Wizard.ViewLogic.Common.Dialogs;
using Wizard.ViewLogic.Stack;

namespace Wizard.Unity.UI.Common
{
    // 공용 창(spec 13 1.5)의 뷰 뼈대. 모델(DialogScreen)이 쥔 버튼과 묶음을 그대로 그린다: 버튼을 끝까지 누르면 모델이 Raised 를 듣고
    // 스스로 고른다(DialogScreen.OnInteraction). 뷰는 고르는 일을 하지 않는다. Esc 는 ScreenStack.Back → 모델의 OnBack 이다.
    // 판은 가운데, 버튼은 아래 가운데에 한 줄로(조심 버튼은 호박색 테 + 경고 표시). 몸글은 하위 뷰가 BuildBody 로 채운다.
    public class DialogScreenView : UiScreenView
    {
        // 8px 격자에 놓이게 고른 크기 [임시]
        public const int PanelWidthPx = 800;
        public const int PanelHeightPx = 280;
        public const int PaddingPx = 48;
        public const int ButtonGapPx = 32;

        private readonly List<UiButton> _buttons = new List<UiButton>();

        public DialogScreenView(DialogScreen dialog, UiViewContext context)
            : base(dialog, context)
        {
            Dialog = dialog;
        }

        public DialogScreen Dialog { get; }

        public UiPanel? Panel { get; private set; }

        public IReadOnlyList<UiButton> Buttons => _buttons;

        public static PixelRect PanelRect => new PixelRect(
            (UiLayout.ReferenceWidthPx - PanelWidthPx) / 2, (UiLayout.ReferenceHeightPx - PanelHeightPx) / 2, PanelWidthPx, PanelHeightPx);

        // Modal 이면 무대 전체가 막히고(뒤를 어둡게 덮는다) 아니면 판만 막는다
        public override PixelRect CoverRect => Dialog.Layer == ScreenLayer.Modal ? Root.AbsoluteRect : PanelRect;

        public UiButton? ButtonOf(DialogButtonRole role)
        {
            DialogButton? b = Dialog.FindButton(role);
            return b == null ? null : WidgetOf(b.Control) as UiButton;
        }

        protected override void OnBuild()
        {
            if (Dialog.Layer == ScreenLayer.Modal)
            {
                var scrim = new UiPanel("Scrim", Root, Root.LocalRect, framed: false);
                scrim.SetFill(UiPalette.Scrim);
            }
            Panel = new UiPanel("Panel", Root, PanelRect);
            UseGroup(Dialog.Group);
            PixelRect inner = PixelRect.FromSize(PanelWidthPx, PanelHeightPx);
            int buttonsTop = PanelHeightPx - PaddingPx - DialogScreen.ButtonHeightPx;
            BuildBody(Panel, new PixelRect(PaddingPx, PaddingPx, inner.Width - PaddingPx - PaddingPx, buttonsTop - PaddingPx));

            IReadOnlyList<DialogButton> buttons = Dialog.Buttons;
            int n = buttons.Count;
            int total = n * DialogScreen.ButtonWidthPx + (n > 0 ? (n - 1) * ButtonGapPx : 0);
            int x = (PanelWidthPx - total) / 2;
            foreach (DialogButton b in buttons)
            {
                var rect = new PixelRect(x, buttonsTop, DialogScreen.ButtonWidthPx, DialogScreen.ButtonHeightPx);
                _buttons.Add(new UiButton(this, Panel, rect, b.Control, b.LabelKey, b.Tone));
                x += DialogScreen.ButtonWidthPx + ButtonGapPx;
            }
        }

        // 몸글 자리(판 왼쪽 위 기준). 기본은 비워 둔다
        protected virtual void BuildBody(UiPanel panel, PixelRect area)
        {
        }

        // 커서는 모델이 OnEnter 에서 정했다(DialogScreen.DefaultButton). 정하지 않았을 때만 첫 버튼
        protected override UiInteractiveWidget? DefaultCursor() => _buttons.Count == 0 ? null : _buttons[0];
    }
}

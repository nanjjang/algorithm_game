#nullable enable
using UnityEngine;
using Wizard.ViewLogic.Common;

namespace Wizard.Unity.UI.Common
{
    // 목록의 한 줄(자리 표시). 줄 높이는 48px 이상(spec 14 4.3). 글은 F-16 본문, 왼쪽 맞춤
    public sealed class UiListRow : UiInteractiveWidget
    {
        public UiListRow(UiScreenView owner, UiList list, PixelRect localRect, Interactable control, int index)
            : base(owner, list, localRect, control, "Row:" + control.Id)
        {
            List = list;
            Index = index;
            Label = new UiLabel(owner.Context, Skin, LabelRect(localRect), UiFontId.F16, TextAnchor.MiddleLeft);
        }

        public const int PaddingPx = 16;

        public UiList List { get; }

        public int Index { get; }

        public UiLabel Label { get; }

        protected override void OnResized(PixelRect localRect) => Label?.SetRect(LabelRect(localRect));

        protected override void ApplySkin(SkinState state)
        {
            base.ApplySkin(state);
            Label.SetColor(TextColorOf(state));
        }

        private static PixelRect LabelRect(PixelRect r) => new PixelRect(PaddingPx, 0, r.Width - PaddingPx - PaddingPx, r.Height);
    }
}

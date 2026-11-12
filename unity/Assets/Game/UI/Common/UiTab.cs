#nullable enable
using UnityEngine;
using Wizard.ViewLogic.Common;

namespace Wizard.Unity.UI.Common
{
    // 탭 하나(자리 표시). 켜짐(On)은 Interactable 의 토글 상태다(isToggle 이어야 한다). 켜짐 모습은 UiInteractiveWidget 이 그린다.
    public sealed class UiTab : UiInteractiveWidget
    {
        public UiTab(UiScreenView owner, UiWidget parent, PixelRect localRect, Interactable control, string labelKey, int index)
            : base(owner, parent, localRect, control, "Tab:" + control.Id)
        {
            if (!control.IsToggle)
            {
                throw new System.ArgumentException("a tab needs a toggle Interactable", nameof(control));
            }
            Index = index;
            Label = new UiLabel(owner.Context, Skin, PixelRect.FromSize(localRect.Width, localRect.Height), UiFontId.F24, TextAnchor.MiddleCenter);
            Label.SetKey(labelKey);
        }

        public int Index { get; }

        public UiLabel Label { get; }

        protected override void OnResized(PixelRect localRect) => Label?.SetRect(PixelRect.FromSize(localRect.Width, localRect.Height));

        protected override void ApplySkin(SkinState state)
        {
            base.ApplySkin(state);
            Label.SetColor(TextColorOf(state));
        }
    }
}

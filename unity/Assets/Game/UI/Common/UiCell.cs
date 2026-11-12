#nullable enable
using UnityEngine;
using Wizard.ViewLogic.Common;

namespace Wizard.Unity.UI.Common
{
    // 칸(자리 표시): 회로의 블록 칸, 가방 칸, 재료 칸처럼 무엇이 들어가는 네모. 아이콘(V-10)이 오기 전에는 가운데에 글자를 찍는다.
    // 들어 있는 것은 이름 표 열쇠(SetContentKey)나 모델이 만든 값(SetContentRaw: 수량 따위)으로 받는다. 비었으면 SetEmpty.
    public sealed class UiCell : UiInteractiveWidget
    {
        public UiCell(UiScreenView owner, UiWidget parent, PixelRect localRect, Interactable control, UiFontId font = UiFontId.F12)
            : base(owner, parent, localRect, control, "Cell:" + control.Id)
        {
            Content = new UiLabel(owner.Context, Skin, PixelRect.FromSize(localRect.Width, localRect.Height), font, TextAnchor.MiddleCenter);
            IsEmpty = true;
        }

        public UiLabel Content { get; }

        public bool IsEmpty { get; private set; }

        public void SetContentKey(string nameKey)
        {
            Content.SetKey(nameKey);
            IsEmpty = false;
        }

        public void SetContentRaw(string text)
        {
            Content.SetRaw(text);
            IsEmpty = false;
        }

        public void SetEmpty()
        {
            Content.SetRaw(string.Empty);
            IsEmpty = true;
        }

        protected override void OnResized(PixelRect localRect) => Content?.SetRect(PixelRect.FromSize(localRect.Width, localRect.Height));

        protected override void ApplySkin(SkinState state)
        {
            base.ApplySkin(state);
            if (IsEmpty && state == SkinState.Normal)
            {
                Skin.SetFill(UiPalette.WoodDark);            // 빈 칸은 한 단계 어둡게 [임시]
            }
            Content.SetColor(TextColorOf(state));
        }
    }
}

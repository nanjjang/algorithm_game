using UnityEngine;
using UImage = UnityEngine.UI.Image;

namespace Wizard.Solo.UI
{
    /// <summary>키트 그림 하나를 상태별 파일로 바꿔 끼우는 겉모습(KitIconButton). 버튼 바탕은 투명이고 이 그림이 상태를 보인다.</summary>
    public sealed class UiKitStateSkin : MonoBehaviour
    {
        GameContext _ctx;
        UiButton _button;
        string _id, _variant;
        UImage _img;
        UiVisualState _last = (UiVisualState)(-1);

        public void Init(GameContext ctx, UiButton b, string id, string variant)
        {
            _ctx = ctx; _button = b; _id = id; _variant = variant;
            var go = new GameObject("Skin", typeof(RectTransform), typeof(UImage));
            var rt = (RectTransform)go.transform;
            rt.SetParent(b.ContentRoot != null ? b.ContentRoot : b.Rect, false);
            UiKit.Stretch(rt);
            _img = go.GetComponent<UImage>();
            _img.raycastTarget = false;
            _img.preserveAspect = true;
            Apply();
        }

        void LateUpdate() { Apply(); }

        void Apply()
        {
            if (_button == null || _img == null) return;
            var s = _button.VisualState;
            if (s == _last) return;
            _last = s;
            var sp = UiKit.KitSprite(_ctx, _id, _variant, UiStateRules.StateToken(s));
            if (!sp.IsValid) sp = UiKit.KitSprite(_ctx, _id, _variant, null);
            if (sp.IsValid) { UiKit.SetSprite(_img, sp); _img.color = s == UiVisualState.Disabled ? UiTheme.DisabledTint : Color.white; }
            else { _img.sprite = null; _img.color = s == UiVisualState.Disabled ? UiTheme.SlateBlue : UiTheme.Chalk; }
        }
    }
}

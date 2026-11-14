using System;
using UnityEngine;
using Wizard.Solo.Art;
using UImage = UnityEngine.UI.Image;

namespace Wizard.Solo.UI
{
    /// <summary>
    /// 켬/끔 토글(K-27 48×24: toggle_off / toggle_on / toggle_disabled). J·클릭·좌우 키로 바꾼다.
    /// 켜면 ui.common.toggle_on, 끄면 ui.common.toggle_off. 값은 Value, 바뀌면 Changed(새 값).
    /// 겉모습의 On(켜짐 바탕)과 값은 따로다 — 토글은 그림이 값을 보이므로 위젯의 On은 쓰지 않는다.
    /// </summary>
    public sealed class UiToggle : UiWidget
    {
        public UImage Switch;
        public UImage Knob;
        public UiLabel Label;
        public Action<bool> Changed;
        bool _value;

        public bool Value
        {
            get => _value;
            set { if (_value == value) return; _value = value; Refresh(); }
        }

        protected override void OnInit() { PressSound = null; }

        protected override void OnActivated()
        {
            _value = !_value;
            UiSounds.Play(Ctx != null ? Ctx.Sound : null, _value ? UiSounds.ToggleOn : UiSounds.ToggleOff);
            Refresh();
            try { Changed?.Invoke(_value); } catch (Exception ex) { Debug.LogException(ex); }
        }

        public override bool HandleKey(MenuKey key)
        {
            if (key != MenuKey.Left && key != MenuKey.Right) return false;
            bool want = key == MenuKey.Right;
            if (want == _value) return true;
            Activate();
            return true;
        }

        protected override void ApplyVisual(UiVisualState s)
        {
            if (Label != null) TintLabel(Label.Text, s, UiTheme.Chalk);
            if (Switch == null) return;
            UiSprite sp = s == UiVisualState.Disabled
                ? UiKit.KitSprite(Ctx, "K-27", "toggle", "disabled")
                : _value ? UiKit.KitSprite(Ctx, "K-27", "toggle", "on") : UiKit.KitSprite(Ctx, "K-27", "toggle_off");
            if (sp.IsValid)
            {
                UiKit.SetSprite(Switch, sp);
                Switch.color = s == UiVisualState.Hover ? new Color(1.1f, 1.1f, 1.1f, 1f) : Color.white;
                if (Knob != null) Knob.gameObject.SetActive(false);
            }
            else
            {
                Switch.sprite = null;
                Switch.color = s == UiVisualState.Disabled ? UiTheme.Wood : _value ? UiTheme.Good : UiTheme.WoodDark;
                if (Knob != null)
                {
                    Knob.gameObject.SetActive(true);
                    Knob.color = s == UiVisualState.Disabled ? UiTheme.SlateBlue : UiTheme.Chalk;
                    var rt = Knob.rectTransform;
                    rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(_value ? 1f : 0f, 0.5f);
                    rt.anchoredPosition = new Vector2(_value ? -3f : 3f, 0f);
                }
            }
        }
    }
}

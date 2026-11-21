using System;
using UnityEngine;
using UnityEngine.EventSystems;
using Wizard.Solo.Art;
using UImage = UnityEngine.UI.Image;

namespace Wizard.Solo.UI
{
    /// <summary>
    /// 숫자 슬라이더 "◀ 막대 값 ▶"(K-30 rail/fill/thumb, K-32 단계 화살). 값은 Min~Max(기본 0~100) 정수, Step 단위.
    /// 커서가 있을 때 좌우 키로 Step씩(누르고 있으면 MenuInput의 키 반복으로 이어진다), 마우스는 화살 클릭·막대 클릭·끌기.
    /// 바뀌면 Changed(새 값). Steps가 있으면 그 값들 사이만 오간다(telegraph_fill 70/80/90).
    /// </summary>
    public sealed class UiSlider : UiWidget
    {
        public int Min = 0, Max = 100, Step = 1;
        public int[] Steps;
        public Action<int> Changed;
        public RectTransform Rail;
        public RectTransform FillRect;
        public RectTransform Thumb;
        public UImage RailImage, FillImage, ThumbImage, LeftArrow, RightArrow;
        public UiTriangle LeftTri, RightTri;
        public UiLabel ValueLabel;
        int _value;

        public int Value
        {
            get => _value;
            set => SetValue(value, false);
        }

        protected override void OnInit() { PressSound = null; }

        /// <summary>값을 넣는다(범위·단계로 고정). notify면 Changed를 부르고 소리를 낸다.</summary>
        public void SetValue(int v, bool notify)
        {
            v = Clamp(v);
            if (v == _value) { Layout(); return; }
            _value = v;
            Layout();
            UpdateLabel();
            if (!notify) return;
            UiSounds.Play(Ctx != null ? Ctx.Sound : null, UiSounds.CursorMove);
            try { Changed?.Invoke(_value); } catch (Exception ex) { Debug.LogException(ex); }
        }

        int Clamp(int v)
        {
            if (Steps != null && Steps.Length > 0)
            {
                int best = Steps[0];
                for (int i = 1; i < Steps.Length; i++) if (Mathf.Abs(Steps[i] - v) < Mathf.Abs(best - v)) best = Steps[i];
                return best;
            }
            if (v < Min) v = Min;
            if (v > Max) v = Max;
            return v;
        }

        /// <summary>한 칸 옮긴다(dir = −1 / +1).</summary>
        public void Nudge(int dir)
        {
            if (!Enabled) { Activate(); return; }
            if (Steps != null && Steps.Length > 0)
            {
                int idx = Array.IndexOf(Steps, _value);
                if (idx < 0) idx = 0;
                idx = Mathf.Clamp(idx + dir, 0, Steps.Length - 1);
                SetValue(Steps[idx], true);
                return;
            }
            SetValue(_value + dir * Mathf.Max(1, Step), true);
        }

        public override bool HandleKey(MenuKey key)
        {
            if (key == MenuKey.Left) { Nudge(-1); return true; }
            if (key == MenuKey.Right) { Nudge(1); return true; }
            return false;
        }

        /// <summary>J는 아무 일도 하지 않는다(값은 좌우로). 꺼져 있으면 이유를 보인다.</summary>
        public override void Activate()
        {
            if (!Enabled) base.Activate();
        }

        /// <summary>막대 위 화면 점 → 값.</summary>
        public void SetFromScreenPoint(Vector2 screenPos, Camera cam)
        {
            if (Rail == null || !Enabled) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(Rail, screenPos, cam, out var local)) return;
            var r = Rail.rect;
            float t = r.width > 0f ? Mathf.Clamp01((local.x - r.xMin) / r.width) : 0f;
            int raw = Mathf.RoundToInt(Min + t * (Max - Min));
            if (Step > 1) raw = Min + Mathf.RoundToInt((raw - Min) / (float)Step) * Step;
            SetValue(raw, true);
        }

        void UpdateLabel()
        {
            if (ValueLabel == null) return;
            ValueLabel.SetText(Ctx != null && Ctx.Names != null ? Ctx.Names.Number(_value) : UiTextFit.FormatThousands(_value));
        }

        void Layout()
        {
            if (FillRect == null || Rail == null) return;
            float t = Max > Min ? (_value - Min) / (float)(Max - Min) : 0f;
            t = Mathf.Clamp01(t);
            FillRect.anchorMax = new Vector2(t, 1f);
            if (Thumb != null)
            {
                Thumb.anchorMin = Thumb.anchorMax = new Vector2(t, 0.5f);
                Thumb.anchoredPosition = Vector2.zero;
            }
        }

        protected override void ApplyVisual(UiVisualState s)
        {
            Layout();
            UpdateLabel();
            bool off = s == UiVisualState.Disabled;
            if (ValueLabel != null) TintLabel(ValueLabel.Text, s, UiTheme.Chalk);
            ApplyPart(RailImage, "K-30", "slider_rail", null, UiTheme.WoodDark, off);
            ApplyPart(FillImage, "K-30", "slider_fill", null, UiTheme.Chalk, off);
            string thumbState = off ? null : s == UiVisualState.Pressed ? "pressed" : s == UiVisualState.Hover ? "hover" : null;
            ApplyPart(ThumbImage, "K-30", "slider_thumb", thumbState, UiTheme.Chalk, off);
            string stepState = off ? "disabled" : null;
            bool arrowsArt = ApplyPart(LeftArrow, "K-32", "step", stepState, new Color(0f, 0f, 0f, 0f), off);
            ApplyPart(RightArrow, "K-32", "step", stepState, new Color(0f, 0f, 0f, 0f), off);
            if (LeftTri != null) { LeftTri.gameObject.SetActive(!arrowsArt); LeftTri.color = off ? UiTheme.SlateBlue : UiTheme.Chalk; }
            if (RightTri != null) { RightTri.gameObject.SetActive(!arrowsArt); RightTri.color = off ? UiTheme.SlateBlue : UiTheme.Chalk; }
        }

        bool ApplyPart(UImage img, string id, string variant, string state, Color fallback, bool off)
        {
            if (img == null) return false;
            var sp = UiKit.KitSprite(Ctx, id, variant, state);
            if (!sp.IsValid && state != null) sp = UiKit.KitSprite(Ctx, id, variant, null);
            if (sp.IsValid)
            {
                UiKit.SetSprite(img, sp);
                img.color = off ? UiTheme.DisabledTint : Color.white;
                return true;
            }
            img.sprite = null;
            img.type = UImage.Type.Simple;
            img.color = off && fallback.a > 0f ? fallback * UiTheme.DisabledTint : fallback;
            return false;
        }
    }
}

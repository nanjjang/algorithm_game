using System;
using UnityEngine;
using UnityEngine.EventSystems;
using Wizard.Solo.Art;
using Wizard.Solo.UI;
using UImage = UnityEngine.UI.Image;

namespace Wizard.Solo.Screens.Settings
{
    /// <summary>
    /// 고르기 옵션 "◀ 값 ▶"(08 12.1절). 좌우 키·화살 클릭으로 앞뒤 값, J·클릭은 다음 값. 값 글자는 부르는 쪽이 이름 표로 만든다.
    /// UiWidget 위에 얹어 괄호·꺼짐 이유·커서 규약을 그대로 쓴다.
    /// </summary>
    public sealed class ChoiceStepper : UiWidget
    {
        public UImage Background;
        public UiLabel ValueLabel;
        public UiTriangle LeftTri, RightTri;
        /// <summary>값 순번이 바뀌었다(새 순번).</summary>
        public Action<int> Changed;
        /// <summary>순번 → 보일 글.</summary>
        public Func<int, string> Format;
        public int Count;
        int _index;

        public int Index => _index;

        public static ChoiceStepper Create(GameContext ctx, RectTransform parent, float x, float y, float w, float h, int count, int index,
            Func<int, string> format, Action<int> onChanged, UiFocusGroup group)
        {
            var go = new GameObject("ChoiceStepper", typeof(RectTransform), typeof(UImage));
            var rt = (RectTransform)go.transform;
            if (parent != null) rt.SetParent(parent, false);
            UiKit.Place(rt, x, y, w, h);
            var bg = go.GetComponent<UImage>();
            bg.color = UiTheme.WithAlpha(UiTheme.WoodDark, 0.6f);
            bg.raycastTarget = true;
            var s = go.AddComponent<ChoiceStepper>();
            s.Background = bg;
            s.Count = count;
            s._index = index;
            s.Format = format;
            const float Arrow = 24f;
            float cy = (h - Arrow) * 0.5f;
            var left = UiKit.Node(rt, "Left", 4f, cy, Arrow, Arrow);
            s.LeftTri = UiKit.Triangle(left, UiTriangleDir.Left, 4f, 4f, Arrow - 8f, Arrow - 8f, UiTheme.Chalk);
            AddArrow(left, s, -1);
            var right = UiKit.Node(rt, "Right", w - Arrow - 4f, cy, Arrow, Arrow);
            s.RightTri = UiKit.Triangle(right, UiTriangleDir.Right, 4f, 4f, Arrow - 8f, Arrow - 8f, UiTheme.Chalk);
            AddArrow(right, s, 1);
            float lh = UiTheme.LineHeight(UiFontId.F16);
            s.ValueLabel = UiKit.Label(ctx, rt, string.Empty, UiFontId.F16, Arrow + 8f, (h - lh) * 0.5f, Mathf.Max(1f, w - 2f * Arrow - 16f), lh,
                UiTheme.Chalk, TextAnchor.MiddleCenter);
            s.InitWidget(ctx);
            s.Changed = onChanged;
            s.RefreshValue();
            group?.Add(s);
            return s;
        }

        static void AddArrow(RectTransform rt, ChoiceStepper owner, int dir)
        {
            var hit = rt.gameObject.AddComponent<UImage>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            hit.raycastTarget = true;
            var a = rt.gameObject.AddComponent<ChoiceStepperArrow>();
            a.Owner = owner;
            a.Direction = dir;
        }

        protected override void OnInit() { PressSound = UiSounds.Press; }

        /// <summary>값 순번을 바꾼다(notify면 Changed).</summary>
        public void SetIndex(int i, bool notify)
        {
            if (Count <= 0) return;
            if (i < 0) i = 0;
            if (i >= Count) i = Count - 1;
            if (i == _index) { RefreshValue(); return; }
            _index = i;
            RefreshValue();
            if (notify)
            {
                try { Changed?.Invoke(_index); } catch (Exception ex) { Debug.LogException(ex); }
            }
        }

        /// <summary>앞뒤로 하나(끝에서 반대쪽으로). 꺼져 있으면 꺼짐 처리만.</summary>
        public void Nudge(int dir)
        {
            if (!Enabled) { Activate(); return; }
            if (Count <= 1) return;
            UiSounds.Play(Ctx != null ? Ctx.Sound : null, UiSounds.CursorMove);
            int n = (_index + (dir < 0 ? -1 : 1)) % Count;
            if (n < 0) n += Count;
            SetIndex(n, true);
        }

        protected override void OnActivated()
        {
            if (Count <= 1) return;
            SetIndex((_index + 1) % Count, true);
        }

        public override bool HandleKey(MenuKey key)
        {
            if (key == MenuKey.Left) { Nudge(-1); return true; }
            if (key == MenuKey.Right) { Nudge(1); return true; }
            return false;
        }

        public void RefreshValue()
        {
            if (ValueLabel != null) ValueLabel.SetText(Format != null ? Format(_index) : _index.ToString());
        }

        protected override void ApplyVisual(UiVisualState s)
        {
            if (ValueLabel != null) TintLabel(ValueLabel.Text, s, UiTheme.Chalk);
            Color arrow = s == UiVisualState.Disabled ? UiTheme.SlateBlue : UiTheme.Chalk;
            if (LeftTri != null) LeftTri.color = arrow;
            if (RightTri != null) RightTri.color = arrow;
            if (Background != null)
                Background.color = s == UiVisualState.Hover || s == UiVisualState.Pressed
                    ? UiTheme.WithAlpha(UiTheme.WoodLight, 0.8f) : UiTheme.WithAlpha(UiTheme.WoodDark, 0.6f);
        }
    }

    /// <summary>◀ ▶ 화살 클릭(포인터 이벤트를 위젯에 넘긴다).</summary>
    public sealed class ChoiceStepperArrow : MonoBehaviour, IPointerClickHandler
    {
        public ChoiceStepper Owner;
        public int Direction;

        public void OnPointerClick(PointerEventData e)
        {
            if (Owner == null) return;
            Owner.Group?.SetCursor(Owner, false);
            Owner.Nudge(Direction);
        }
    }
}

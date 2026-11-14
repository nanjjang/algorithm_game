using System;
using UnityEngine;
using UnityEngine.EventSystems;
using Wizard.Solo.Art;
using UText = UnityEngine.UI.Text;

namespace Wizard.Solo.UI
{
    /// <summary>
    /// 누를 수 있는 것의 바탕(버튼, 토글, 슬라이더, 탭, 목록 줄, 카드). 상태 규약(08 1.3절)을 한곳에서 지킨다:
    /// - 상태 = (Enabled, On, Hovered, Cursor, Picked, Pressed). 겉모습은 UiStateRules.Resolve(!Enabled → Disabled &gt; Pressed &gt; On &gt; Hover(마우스) &gt; Normal).
    /// - 괄호: Keyboard 모드 = 커서 + 고른 것, Mouse 모드 = 고른 것만. 꺼짐이면 회청.
    /// - 꺼짐: 커서는 간다. 커서가 있으면(또는 마우스가 위에 있으면) 아래 한 줄에 이유(DisabledReasonKey). 눌러도 아무 일 없고
    ///   ui.common.disabled_press를 내며 이유를 다시 보여 준다.
    /// - 마우스: 올림 = Hover(Mouse 모드에서만 보임), 누르는 동안 = Pressed, 왼쪽 클릭 = 커서를 옮기고 Activate.
    /// - 키보드: UiFocusGroup이 Cursor를 정하고 J로 Activate. 누름 모습은 PressFlashSeconds 동안 보인다.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public abstract class UiWidget : MonoBehaviour, IUiFocusable,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        /// <summary>[임시] 키보드로 눌렀을 때 누름 모습을 보이는 시간(초)의 기본값. 쓰는 값은 관리자의 Tuning(DataSolo/F3.json).</summary>
        public const float PressFlashSeconds = 0.1f;
        /// <summary>[임시] 꺼진 것을 눌렀을 때 이유를 다시 보여 주는 시간(초, 08 1.3절 U-15).</summary>
        public const float ReasonFlashSeconds = 2f;

        protected GameContext Ctx;
        bool _enabled = true, _on, _picked, _cursor, _hovered, _pressed;
        float _pressFlash, _reasonFlash;
        UiInputMode _lastMode = (UiInputMode)(-1);
        UiSelectionBracket _bracket;
        UiLabel _reasonLabel;
        string _reasonKey;
        bool _initialized;

        /// <summary>이 칸이 속한 커서 묶음(UiFocusGroup.Add가 넣는다).</summary>
        public UiFocusGroup Group;
        /// <summary>눌렀을 때(켜져 있을 때만).</summary>
        public Action OnClick;
        /// <summary>눌렀을 때 낼 소리 사건(ui.* 열쇠, null이면 소리 없음). 기본 ui.common.press.</summary>
        public string PressSound = UiSounds.Press;
        /// <summary>괄호를 그리지 않는다(괄호를 따로 그리는 칸).</summary>
        public bool NoBracket;

        public RectTransform Rect => (RectTransform)transform;
        public bool IsNavigable => this != null && enabled && gameObject.activeInHierarchy;

        /// <summary>UiKit이 만든 뒤 부른다.</summary>
        public void InitWidget(GameContext ctx)
        {
            Ctx = ctx;
            if (_initialized) return;
            _initialized = true;
            if (!NoBracket) _bracket = UiSelectionBracket.Attach(ctx, Rect);
            OnInit();
            Refresh();
        }

        protected virtual void OnInit() { }

        public bool Enabled { get => _enabled; set { if (_enabled == value) return; _enabled = value; if (!value) _pressed = false; Refresh(); } }
        public bool On { get => _on; set { if (_on == value) return; _on = value; Refresh(); } }
        public bool Picked { get => _picked; set { if (_picked == value) return; _picked = value; Refresh(); } }
        public bool Cursor { get => _cursor; set { if (_cursor == value) return; _cursor = value; Refresh(); } }
        public bool Hovered => _hovered;
        public bool Pressed => _pressed || _pressFlash > 0f;

        /// <summary>꺼짐 이유(이름 표 열쇠). Enabled = false와 같이 쓴다.</summary>
        public string DisabledReasonKey { get => _reasonKey; set { _reasonKey = value; Refresh(); } }

        /// <summary>꺼고 이유를 단다(한 번에).</summary>
        public void SetDisabled(string reasonKey)
        {
            _reasonKey = reasonKey;
            _enabled = false;
            _pressed = false;
            Refresh();
        }

        public void SetEnabled() { _enabled = true; Refresh(); }

        protected UiInputMode Mode
        {
            get
            {
                if (Group != null) return Group.Mode;
                var owner = GetComponentInParent<UiCanvasOwner>();
                return owner != null && owner.Manager != null ? owner.Manager.InputMode.Mode : UiInputMode.Keyboard;
            }
        }

        /// <summary>이 위젯이 놓인 캔버스의 관리자가 든 수치(관리자 없이 만든 위젯 — 테스트 — 이면 null → 기본값).</summary>
        UiTuning Tuning
        {
            get
            {
                var owner = GetComponentInParent<UiCanvasOwner>();
                return owner != null && owner.Manager != null ? owner.Manager.Tuning : null;
            }
        }

        /// <summary>지금 겉모습 상태.</summary>
        public UiVisualState VisualState => UiStateRules.Resolve(_enabled, Pressed, _on, _hovered, Mode);

        public virtual void Activate()
        {
            if (!_enabled)
            {
                UiSounds.Play(Ctx != null ? Ctx.Sound : null, UiSounds.DisabledPress);
                _reasonFlash = Tuning != null ? Tuning.DisabledReasonSeconds : ReasonFlashSeconds;
                Refresh();
                return;
            }
            _pressFlash = Tuning != null ? Tuning.PressFlashSeconds : PressFlashSeconds;
            UiSounds.Play(Ctx != null ? Ctx.Sound : null, PressSound);
            Refresh();
            OnActivated();
            try { OnClick?.Invoke(); }
            catch (Exception ex) { Debug.LogException(ex); }
        }

        /// <summary>Activate가 켜진 상태에서 불릴 때 OnClick 전에(토글이 값을 바꾸는 자리).</summary>
        protected virtual void OnActivated() { }

        public virtual bool HandleKey(MenuKey key) => false;

        public virtual void OnPointerEnter(PointerEventData e)
        {
            _hovered = true;
            if (Mode == UiInputMode.Mouse) UiSounds.Play(Ctx != null ? Ctx.Sound : null, UiSounds.HoverEnter);
            Group?.OnPointerHover(this);
            Refresh();
        }

        public virtual void OnPointerExit(PointerEventData e)
        {
            _hovered = false;
            _pressed = false;
            Refresh();
        }

        public virtual void OnPointerDown(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left || !_enabled) return;
            _pressed = true;
            Refresh();
        }

        public virtual void OnPointerUp(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left) return;
            _pressed = false;
            Refresh();
        }

        public virtual void OnPointerClick(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left) return;
            Group?.SetCursor(this, false);
            _pressFlash = 0f;
            Activate();
        }

        protected virtual void Update()
        {
            bool dirty = false;
            var m = Mode;
            if (m != _lastMode) { _lastMode = m; dirty = true; }
            if (_pressFlash > 0f) { _pressFlash -= Time.unscaledDeltaTime; if (_pressFlash <= 0f) { _pressFlash = 0f; dirty = true; } }
            if (_reasonFlash > 0f) { _reasonFlash -= Time.unscaledDeltaTime; if (_reasonFlash <= 0f) { _reasonFlash = 0f; dirty = true; } }
            if (dirty) Refresh();
        }

        /// <summary>상태가 바뀌었으면 겉모습을 다시 맞춘다.</summary>
        public void Refresh()
        {
            if (!_initialized) return;
            var mode = Mode;
            ApplyVisual(UiStateRules.Resolve(_enabled, Pressed, _on, _hovered, mode));
            if (_bracket != null) _bracket.Set(UiStateRules.Bracket(_enabled, _cursor, _picked, mode));
            bool showReason = (_reasonFlash > 0f && !_enabled && !string.IsNullOrEmpty(_reasonKey))
                || UiStateRules.ShowsDisabledReason(_enabled, _cursor, _hovered, mode, !string.IsNullOrEmpty(_reasonKey));
            UpdateReason(showReason);
        }

        void UpdateReason(bool show)
        {
            if (!show)
            {
                if (_reasonLabel != null) _reasonLabel.gameObject.SetActive(false);
                return;
            }
            if (_reasonLabel == null)
            {
                var size = Rect.rect.size;
                float w = Mathf.Max(size.x, 240f);
                _reasonLabel = UiKit.Label(Ctx, Rect, string.Empty, UiFontId.F12, 0f, 0f, w, UiTheme.LineHeight(UiFontId.F12),
                    UiTheme.SlateBlue, TextAnchor.UpperLeft);
                var rt = _reasonLabel.Rect;
                // 대상 바로 아래(괄호 바깥까지 띄운다).
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(0f, -(UiBrackets.GapPx + UiBrackets.LineWidthPx + UiBrackets.OutlinePx + UiBrackets.PulseExtraPx));
                _reasonLabel.name = "DisabledReason";
            }
            _reasonLabel.gameObject.SetActive(true);
            _reasonLabel.SetText(Ctx != null && Ctx.Names != null ? Ctx.Names.Get(_reasonKey) : _reasonKey);
        }

        /// <summary>겉모습 상태를 그림에 입힌다.</summary>
        protected abstract void ApplyVisual(UiVisualState state);

        /// <summary>글자 색 규칙: 꺼짐 = 회청, 그 밖 = 기본색.</summary>
        protected static void TintLabel(UText t, UiVisualState s, Color normal)
        {
            if (t == null) return;
            t.color = s == UiVisualState.Disabled ? UiTheme.SlateBlue : normal;
        }
    }
}

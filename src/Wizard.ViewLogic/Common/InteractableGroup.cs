using System;
using System.Collections.Generic;

namespace Wizard.ViewLogic.Common
{
    // spec 14 4.3절 "상태 규약과 상태 기계"의 의사코드, spec 13 1.3절. 한 화면(또는 한 창)의 누를 수 있는 것들과 키보드 커서 하나.
    //
    //   KeyAny            → Mode = Keyboard; 모든 Hovered 표시를 지운다; 커서 대상에 괄호
    //   MouseMove         → Mode = Mouse; 괄호는 Picked 인 것에만 남긴다; 포인터 아래 대상 Hovered = true
    //   PointerDown(t)    → if t.Enabled: t.Pressed = true
    //   PointerUp(t)      → if t.Pressed and 포인터가 t 위: Activate(t); t.Picked = true;  t.Pressed = false
    //   KeyConfirm(J)     → if cursor.Enabled: cursor.Pressed = true (키 누르는 동안) → 뗄 때 Activate
    //   Disable/Enable    → Enabled 갱신. Disabled 면 Hovered·Pressed 무시
    //
    // 명세가 정하지 않아 정한 것 [임시]
    //   - 꺼진 것을 누르면(포인터·J) 아무 일 없이 DisabledPress(이유 키)를 낸다(spec 13 U-15).
    //   - 포인터로 끝까지 누른 것에 커서도 옮긴다("지금 고른 것"에도 괄호, spec 13 1.3). 이때 CursorMove 는 내지 않는다.
    //   - Picked 는 PickPolicy.Single 이면 새로 고른 하나만 남긴다.
    //   - 커서를 옮기면 J 로 누르던 것은 풀린다(Activate 없음). 누르던 것이 꺼지면 소리 없이 풀린다.
    //   - 꺼진 것의 이유 한 줄은 키보드 커서가 있을 때, 마우스 모드에서는 포인터가 위에 있을 때 보인다.
    //   - 확정 괄호의 색은 꺼진 것이면 회청이다(명세는 커서 괄호의 색만 정했다).
    // 입력 모드는 화면 전체가 같은 InputModeTracker 를 쓴다. 모드가 Keyboard 로 바뀌면 모든 묶음이 자기 Hovered 를 지운다.
    public sealed class InteractableGroup : IDisposable
    {
        private enum PressSource
        {
            None = 0,
            Pointer = 1,
            Key = 2,
        }

        private readonly InputModeTracker _mode;
        private readonly List<Interactable> _items = new List<Interactable>();
        private Interactable? _cursor;
        private Interactable? _hovered;
        private Interactable? _pressed;
        private PressSource _pressSource;
        private long _ticksSinceCursorMove;
        private bool _disposed;

        public InteractableGroup(InputModeTracker mode, PickPolicy pickPolicy = PickPolicy.Single)
        {
            _mode = mode ?? throw new ArgumentNullException(nameof(mode));
            PickPolicy = pickPolicy;
            _mode.Changed += OnModeChanged;
        }

        public event Action<InteractionEvent>? Raised;

        public PickPolicy PickPolicy { get; }

        public InputMode Mode => _mode.Mode;

        public IReadOnlyList<Interactable> Items => _items;

        public Interactable? CursorTarget => _cursor;

        public Interactable? HoveredTarget => _hovered;

        public Interactable? PressedTarget => _pressed;

        // 커서 괄호 애니메이션의 지금 프레임(커서가 옮겨 온 뒤 지난 화면 틱으로 센다)
        public int CursorPulseFrame => Brackets.PulseFrame(_ticksSinceCursorMove);

        public long UiTicksSinceCursorMove => _ticksSinceCursorMove;

        public Interactable Add(Interactable item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }
            if (_items.Contains(item))
            {
                throw new InvalidOperationException("already in the group: " + item.Id);
            }
            _items.Add(item);
            return item;
        }

        public bool Remove(Interactable item)
        {
            if (!_items.Remove(item))
            {
                return false;
            }
            if (ReferenceEquals(_cursor, item))
            {
                item.Cursor = false;
                _cursor = null;
            }
            if (ReferenceEquals(_hovered, item))
            {
                item.Hovered = false;
                _hovered = null;
            }
            if (ReferenceEquals(_pressed, item))
            {
                item.Pressed = false;
                _pressed = null;
                _pressSource = PressSource.None;
            }
            return true;
        }

        // 화면 틱 하나(spec 13 0.2 UiTick 60Hz)
        public void Tick()
        {
            if (_ticksSinceCursorMove < long.MaxValue)
            {
                _ticksSinceCursorMove++;
            }
        }

        // ── 입력 ──

        public void KeyAny()
        {
            _mode.OnKeyAny();
            ClearHover();
        }

        // under = 포인터 아래의 대상(없으면 null). 움직임이 0이면 모드를 바꾸지 않고, 마우스 모드일 때만 올림을 고친다
        public void MouseMove(int dxPx, int dyPx, Interactable? under)
        {
            Check(under);
            _mode.OnMouseMove(dxPx, dyPx);
            if (_mode.Mode != InputMode.Mouse)
            {
                return;
            }
            SetHover(under);
        }

        public void PointerDown(Interactable? under)
        {
            Check(under);
            if (under == null || _pressSource != PressSource.None)
            {
                return;
            }
            if (!under.Enabled)
            {
                Raise(InteractionEventKind.DisabledPress, under, under.DisabledReasonKey);
                return;
            }
            Press(under, PressSource.Pointer);
        }

        public void PointerUp(Interactable? under)
        {
            Check(under);
            if (_pressSource != PressSource.Pointer)
            {
                return;
            }
            Interactable p = Release();
            if (ReferenceEquals(p, under))
            {
                MoveCursorSilently(p);
                Activate(p);
            }
        }

        // 확인 키(J) 누름. 어떤 키든 키보드 모드로 바꾼다
        public void KeyConfirmDown()
        {
            KeyAny();
            Interactable? c = _cursor;
            if (c == null || _pressSource != PressSource.None)
            {
                return;
            }
            if (!c.Enabled)
            {
                Raise(InteractionEventKind.DisabledPress, c, c.DisabledReasonKey);
                return;
            }
            Press(c, PressSource.Key);
        }

        public void KeyConfirmUp()
        {
            if (_pressSource != PressSource.Key)
            {
                return;
            }
            Interactable p = Release();
            if (ReferenceEquals(p, _cursor))
            {
                Activate(p);
            }
        }

        // 방향키 따위로 커서를 옮긴다(키보드 입력이므로 키보드 모드가 된다). 꺼진 것에도 갈 수 있다
        public void MoveCursor(Interactable? target)
        {
            Check(target);
            KeyAny();
            SetCursor(target);
        }

        // 묶음의 순서로 delta 칸 옮긴다. 끝에서 멈춘다 [임시]. 커서가 없으면 첫 칸으로 간다
        public void MoveCursorBy(int delta)
        {
            KeyAny();
            if (_items.Count == 0)
            {
                return;
            }
            int i = _cursor == null ? 0 : _items.IndexOf(_cursor) + delta;
            SetCursor(_items[Math.Max(0, Math.Min(_items.Count - 1, i))]);
        }

        // 화면이 커서를 정한다(처음 열 때 기본 칸). 모드는 바꾸지 않는다
        public void SetCursor(Interactable? target)
        {
            Check(target);
            if (ReferenceEquals(target, _cursor))
            {
                return;
            }
            if (_pressSource == PressSource.Key)
            {
                Release();
            }
            MoveCursorSilently(target);
            if (target != null)
            {
                Raise(InteractionEventKind.CursorMove, target);
            }
        }

        public void SetEnabled(Interactable target, bool enabled, string? disabledReasonKey = null)
        {
            Check(target);
            target.Enabled = enabled;
            target.DisabledReasonKey = enabled ? null : disabledReasonKey;
            if (!enabled && ReferenceEquals(_pressed, target))
            {
                target.Pressed = false;
                _pressed = null;
                _pressSource = PressSource.None;
            }
        }

        // 화면이 켜짐을 정한다(처음 값, 다른 탭을 켜서 이것이 꺼질 때). 일을 내지 않는다
        public void SetToggled(Interactable target, bool on)
        {
            Check(target);
            target.ToggledOn = on;
        }

        // 화면이 확정을 정한다(이미 고른 캐릭터 따위). 일을 내지 않는다. Single 이면 다른 것의 확정을 푼다
        public void SetPicked(Interactable target, bool picked)
        {
            Check(target);
            if (picked && PickPolicy == PickPolicy.Single)
            {
                UnpickOthers(target);
            }
            target.Picked = picked;
        }

        // ── 그림 ──

        // spec 14 4.3 "그릴 바탕 그림 선택 (우선순위)"
        public SkinState SkinOf(Interactable t)
        {
            if (!t.Enabled)
            {
                return SkinState.Disabled;
            }
            if (t.Pressed)
            {
                return SkinState.Pressed;
            }
            if (t.On && t.HasOnSkin)
            {
                return SkinState.On;
            }
            if (t.Hovered && _mode.Mode == InputMode.Mouse)
            {
                return SkinState.Hover;
            }
            return SkinState.Normal;
        }

        // spec 14 4.3 "괄호 겹그림"
        public BracketOverlay OverlayOf(Interactable t)
        {
            bool keyboard = _mode.Mode == InputMode.Keyboard;
            BracketMotion motion = BracketMotion.None;
            if (keyboard && t.Cursor)
            {
                motion = BracketMotion.Moving;
            }
            else if (t.Picked)
            {
                motion = BracketMotion.Still;
            }
            BracketTone tone = t.Enabled ? BracketTone.Select : BracketTone.Muted;
            bool showReason = !t.Enabled && (keyboard ? t.Cursor : t.Hovered);
            return new BracketOverlay(motion, tone, t.Picked, showReason ? t.DisabledReasonKey : null, t.BracketSize);
        }

        public InteractVisual VisualOf(Interactable t)
        {
            InteractVisual v = InteractVisual.None;
            switch (SkinOf(t))
            {
                case SkinState.Disabled: v |= InteractVisual.Disabled; break;
                case SkinState.Pressed: v |= InteractVisual.Pressed; break;
                case SkinState.Hover: v |= InteractVisual.Hover; break;
            }
            if (t.On)
            {
                v |= InteractVisual.On;
            }
            if (OverlayOf(t).Motion == BracketMotion.Moving)
            {
                v |= InteractVisual.Cursor;
            }
            if (t.Picked)
            {
                v |= InteractVisual.Confirmed;
            }
            return v;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            _mode.Changed -= OnModeChanged;
        }

        // ── 안쪽 ──

        private void OnModeChanged(InputMode mode)
        {
            if (mode == InputMode.Keyboard)
            {
                ClearHover();
            }
        }

        private void ClearHover() => SetHover(null);

        private void SetHover(Interactable? under)
        {
            if (ReferenceEquals(under, _hovered))
            {
                return;
            }
            Interactable? old = _hovered;
            _hovered = under;
            if (old != null)
            {
                old.Hovered = false;
                if (old.Enabled)
                {
                    Raise(InteractionEventKind.HoverExit, old);
                }
            }
            if (under != null)
            {
                under.Hovered = true;
                if (under.Enabled)
                {
                    Raise(InteractionEventKind.HoverEnter, under);
                }
            }
        }

        private void Press(Interactable t, PressSource source)
        {
            t.Pressed = true;
            _pressed = t;
            _pressSource = source;
            Raise(InteractionEventKind.Press, t);
        }

        private Interactable Release()
        {
            Interactable p = _pressed!;
            p.Pressed = false;
            _pressed = null;
            _pressSource = PressSource.None;
            Raise(InteractionEventKind.Release, p);
            return p;
        }

        private void MoveCursorSilently(Interactable? target)
        {
            if (ReferenceEquals(target, _cursor))
            {
                return;
            }
            if (_cursor != null)
            {
                _cursor.Cursor = false;
            }
            _cursor = target;
            if (target != null)
            {
                target.Cursor = true;
            }
            _ticksSinceCursorMove = 0;
        }

        private void Activate(Interactable t)
        {
            if (t.IsToggle)
            {
                t.ToggledOn = !t.ToggledOn;
                Raise(t.ToggledOn ? InteractionEventKind.ToggleOn : InteractionEventKind.ToggleOff, t);
            }
            if (!t.Picked)
            {
                if (PickPolicy == PickPolicy.Single)
                {
                    UnpickOthers(t);
                }
                t.Picked = true;
                Raise(InteractionEventKind.ConfirmCheck, t);
            }
            Raise(InteractionEventKind.Activated, t);
        }

        private void UnpickOthers(Interactable keep)
        {
            foreach (Interactable i in _items)
            {
                if (!ReferenceEquals(i, keep))
                {
                    i.Picked = false;
                }
            }
        }

        private void Check(Interactable? t)
        {
            if (t != null && !_items.Contains(t))
            {
                throw new ArgumentException("not a member of this group: " + t.Id, nameof(t));
            }
        }

        private void Raise(InteractionEventKind kind, Interactable target, string? reasonKey = null) =>
            Raised?.Invoke(new InteractionEvent(kind, target, reasonKey));
    }
}

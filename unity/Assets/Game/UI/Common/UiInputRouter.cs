#nullable enable
using System;
using System.Collections.Generic;
using Wizard.Client;
using Wizard.ViewLogic.Common;
using Wizard.ViewLogic.Stack;

namespace Wizard.Unity.UI.Common
{
    // 화면 입력의 길잡이. 물리 키와 포인터를 받아 스택의 알맞은 화면으로 보내고, 그 화면의 InteractableGroup(ViewLogic 상태 기계)에 넘긴다.
    // 입력 모드 전환(spec 14 4.3, spec 13 1.3)은 상태 기계가 한다:
    //   어떤 키든     → 키보드 모드. 모든 올림 표시를 지우고 커서 대상에 괄호(InputModeTracker.Changed → 각 묶음)
    //   마우스 움직임 → 움직임이 0이 아니면 마우스 모드. 괄호는 확정된 것에만, 포인터 아래 대상이 올림
    //   포인터 누름   → 켜진 것이면 누름. 뗄 때 포인터가 그 위면 Activate + 확정
    //   확인 키(J)    → 커서 대상이 켜진 것이면 누름, 떼면 Activate
    // 보내는 곳(ScreenStack 의 규칙):
    //   키보드      포커스 화면(맨 위) 하나. 방향키 = 커서 이동(UiScreenView.Neighbor), 확인 = 누름/뗌, Esc = ScreenStack.Back,
    //               그 밖의 키 = 그 화면의 OnKey(K 빼기 따위)
    //   포인터      입력을 받는 화면(GetInputReceivers: 위 → 아래, 가로막는 화면에서 멈춤) 가운데 포인터 아래에 위젯이 있는 맨 위의 것.
    //               위젯이 없어도 그 화면의 판(CoverRect) 위면 거기서 멈춘다. 뗌은 누른 화면의 묶음으로 간다.
    //   토스트      입력을 받지 않는다
    // 이 클래스는 엔진 입력 장치를 모른다. Input System 의 저수준 이벤트(키는 물리 키 자리, T-28)를 이것으로 옮기는 일은
    // 조립 루트가 한다(키 반복은 넘기지 않는다: 상태가 바뀐 키만).
    public sealed class UiInputRouter
    {
        private readonly ScreenHost _host;
        private readonly InputModeTracker _mode;
        private readonly UiCanvas _canvas;
        private readonly List<IScreen> _receivers = new List<IScreen>();

        private PixelPoint? _pointer;
        private UiScreenView? _pointerPressView;
        private UiScreenView? _confirmView;
        private PhysicalKey _confirmKey;

        public UiInputRouter(ScreenHost host, UiCanvas canvas, UiKeyMap? keys = null)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
            _mode = host.Context.Mode;
            Keys = keys ?? UiKeyMap.Default;
        }

        public UiKeyMap Keys { get; set; }

        public InputMode Mode => _mode.Mode;

        // 마지막 포인터 자리(무대 좌표). 아직 움직인 적이 없으면 null
        public PixelPoint? Pointer => _pointer;

        // ── 키보드 ──

        public void KeyDown(PhysicalKey key)
        {
            UiScreenView? view = FocusView();
            _mode.OnKeyAny();
            if (view == null)
            {
                return;
            }
            InteractableGroup? group = view.Group;
            group?.KeyAny();
            UiKeyAction action = Keys.ActionOf(key);
            switch (action)
            {
                case UiKeyAction.Up:
                    Move(view, UiDirection.Up);
                    break;
                case UiKeyAction.Down:
                    Move(view, UiDirection.Down);
                    break;
                case UiKeyAction.Left:
                    Move(view, UiDirection.Left);
                    break;
                case UiKeyAction.Right:
                    Move(view, UiDirection.Right);
                    break;
                case UiKeyAction.Confirm:
                    if (group != null && _confirmView == null)
                    {
                        _confirmView = view;
                        _confirmKey = key;
                        group.KeyConfirmDown();
                    }
                    break;
                case UiKeyAction.Back:
                    if (!view.OnKey(key, action))
                    {
                        _host.Stack.Back();
                    }
                    break;
                default:
                    view.OnKey(key, action);
                    break;
            }
            _host.Sync();
        }

        public void KeyUp(PhysicalKey key)
        {
            if (_confirmView == null || key != _confirmKey)
            {
                return;
            }
            UiScreenView view = _confirmView;
            _confirmView = null;
            _confirmKey = PhysicalKey.None;
            if (!view.IsDisposed)
            {
                view.Group?.KeyConfirmUp();
            }
            _host.Sync();
        }

        // ── 포인터(창 px, Unity 화면 좌표: 왼쪽 아래 원점) ──

        public void PointerMove(int screenX, int screenY) => PointerMoveStage(_canvas.ScreenToStage(screenX, screenY));

        public void PointerDown(int screenX, int screenY) => PointerDownStage(_canvas.ScreenToStage(screenX, screenY));

        public void PointerUp(int screenX, int screenY) => PointerUpStage(_canvas.ScreenToStage(screenX, screenY));

        // 바퀴. + 아래로(목록을 아래로 민다)
        public void Scroll(int steps)
        {
            if (steps == 0 || _pointer == null)
            {
                return;
            }
            PixelPoint p = _pointer.Value;
            _host.Stack.GetInputReceivers(_receivers);
            foreach (IScreen s in _receivers)
            {
                UiScreenView? v = _host.ViewOf(s);
                if (v == null || !v.IsBuilt)
                {
                    continue;
                }
                if (v.OnScroll(p, steps) || v.Covers(p))
                {
                    break;
                }
            }
            _host.Sync();
        }

        // ── 포인터(무대 좌표) ──

        public void PointerMoveStage(PixelPoint p)
        {
            int dx = _pointer == null ? 1 : p.X - _pointer.Value.X;    // 처음 알게 된 자리는 움직임으로 친다
            int dy = _pointer == null ? 0 : p.Y - _pointer.Value.Y;
            _pointer = p;
            DeliverMove(dx, dy, p);
            _host.Sync();
        }

        public void PointerDownStage(PixelPoint p)
        {
            if (_pointer == null || _pointer.Value.X != p.X || _pointer.Value.Y != p.Y)
            {
                PointerMoveStage(p);
            }
            if (_pointerPressView != null)
            {
                return;                                    // 다른 단추로 이미 누르는 중
            }
            UiInteractiveWidget? under = Under(p, out UiScreenView? view);
            if (under == null || view == null || view.Group == null)
            {
                return;
            }
            _pointerPressView = view;
            view.Group.PointerDown(under.Control);
            _host.Sync();
        }

        public void PointerUpStage(PixelPoint p)
        {
            if (_pointer == null || _pointer.Value.X != p.X || _pointer.Value.Y != p.Y)
            {
                PointerMoveStage(p);
            }
            UiScreenView? pressed = _pointerPressView;
            _pointerPressView = null;
            if (pressed == null || pressed.IsDisposed || pressed.Group == null)
            {
                return;
            }
            UiInteractiveWidget? under = Under(p, out UiScreenView? view);
            pressed.Group.PointerUp(ReferenceEquals(view, pressed) ? under?.Control : null);
            _host.Sync();
        }

        // 화면이 바뀐 뒤(새 창이 떴다) 멈춰 있는 포인터 아래의 올림을 다시 맞춘다. 모드는 바꾸지 않는다. 프레임마다 불러도 된다
        public void SyncHover()
        {
            if (_pointer != null)
            {
                DeliverMove(0, 0, _pointer.Value);
            }
        }

        // 무대 좌표 p 아래의 위젯과 그 화면(입력을 받는 화면 가운데 맨 위)
        public UiInteractiveWidget? Under(PixelPoint p, out UiScreenView? view)
        {
            _host.Stack.GetInputReceivers(_receivers);
            foreach (IScreen s in _receivers)
            {
                UiScreenView? v = _host.ViewOf(s);
                if (v == null || !v.IsBuilt)
                {
                    continue;
                }
                UiInteractiveWidget? w = v.HitTest(p);
                if (w != null)
                {
                    view = v;
                    return w;
                }
                if (v.Covers(p))
                {
                    break;
                }
            }
            view = null;
            return null;
        }

        private UiScreenView? FocusView()
        {
            UiScreenView? v = _host.ViewOf(_host.Stack.Focus);
            return v != null && v.IsBuilt ? v : null;
        }

        private void Move(UiScreenView view, UiDirection direction)
        {
            InteractableGroup? group = view.Group;
            if (group == null)
            {
                return;
            }
            UiInteractiveWidget? target = view.Neighbor(direction);
            if (target != null)
            {
                group.MoveCursor(target.Control);
            }
        }

        // 움직임을 모든 화면의 묶음에 알린다: 포인터 아래 위젯이 있는 묶음에는 그 대상, 나머지는 대상 없음(올림이 풀린다)
        private void DeliverMove(int dx, int dy, PixelPoint p)
        {
            _mode.OnMouseMove(dx, dy);
            UiInteractiveWidget? under = Under(p, out UiScreenView? hit);
            IReadOnlyList<UiScreenView> views = _host.Views;
            for (int i = 0; i < views.Count; i++)
            {
                UiScreenView v = views[i];
                InteractableGroup? g = v.Group;
                if (g == null || v.IsDisposed)
                {
                    continue;
                }
                g.MouseMove(dx, dy, ReferenceEquals(v, hit) ? under?.Control : null);
            }
        }
    }
}

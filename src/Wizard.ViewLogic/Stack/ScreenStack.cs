using System;
using System.Collections.Generic;

namespace Wizard.ViewLogic.Stack
{
    // spec 13 1.1절의 화면 관리자. 장면이 아니라 이 스택으로 화면을 바꾼다(ARCHITECTURE 4.3절, Unity 쪽 ScreenHost 가 따라 그린다).
    //
    // 규칙
    //   - Push 는 맨 위에 쌓고, Replace 는 맨 위를 바꾸고, Pop 은 맨 위를 닫고 "들어온 곳으로 돌아간다"(fx 1.1).
    //   - 돌아갈 곳(ReturnTarget)은 바로 아래 화면이다. 진입점이 여럿이라 아래 화면이 아닌 곳으로 돌아가야 하면
    //     ScreenArgs.ReturnTarget 으로 따로 정한다(광장 메뉴 → 강화 → 광장, 정산 → 강화 → 메인 메뉴).
    //     따로 정한 칸을 Pop 하면: 그 화면이 스택 안에 있으면 그 화면까지 닫아 내려가고, 없으면 그 화면을 새로 연다.
    //     따로 정하지 않은 칸의 Pop 은 그 칸만 닫는다(드러난 화면이 곧 돌아갈 곳이다).
    //   - Toast 층 화면은 따로 쌓인다. 입력·포커스·Back 을 받지 않고 Pop·Replace 의 대상이 아니다. DismissToast 로 닫는다.
    //   - 보이는 화면: 맨 위에서 아래로 첫 Fullscreen 까지(그 아래는 덮인다). 토스트는 그 위에 언제나 보인다.
    //   - 입력을 받는 화면: 맨 위에서 아래로 내려가며, 입력을 가로막는 화면(Modal, Fullscreen, BlocksInput)을 만나면 거기서 멈춘다.
    //     키보드 포커스(Focus)는 맨 위 화면 하나다.
    //   - 수명: 열 때 Opened → OnEnter, 닫을 때 OnExit → Closed. 덮이거나 드러날 때는 부르지 않는다(덮인 화면은 열린 채로 있다).
    //     스택의 상태는 이 호출들보다 먼저 바뀐다. 그래서 OnEnter·OnExit 안에서 스택을 다시 바꿔도 된다.
    public sealed class ScreenStack
    {
        private readonly IScreenFactory _factory;
        private readonly List<ScreenStackEntry> _stack = new List<ScreenStackEntry>();
        private readonly List<ScreenStackEntry> _toasts = new List<ScreenStackEntry>();

        public ScreenStack(IScreenFactory factory)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        // ui.common.screen_open / screen_close(spec 13 21절)를 낼 자리. 바인딩은 표시 계층(M9-PRS-01)이 잇는다
        public event Action<IScreen>? Opened;
        public event Action<IScreen>? Closed;

        public int Count => _stack.Count;

        // 아래 → 위
        public IReadOnlyList<ScreenStackEntry> Entries => _stack;

        // 먼저 뜬 것 → 나중에 뜬 것
        public IReadOnlyList<ScreenStackEntry> Toasts => _toasts;

        public IScreen? Top => _stack.Count == 0 ? null : _stack[_stack.Count - 1].Screen;

        public ScreenId TopId => _stack.Count == 0 ? ScreenId.None : _stack[_stack.Count - 1].Id;

        // 맨 위 화면이 나갈 때 돌아갈 곳(spec 13 1.1절 ReturnTarget)
        public ScreenId ReturnTarget => ReturnTargetAt(_stack.Count - 1);

        // 키보드 입력과 Esc 를 받는 화면
        public IScreen? Focus => Top;

        public void Push(ScreenId id, ScreenArgs args)
        {
            IScreen screen = Create(id);
            if (screen.Layer == ScreenLayer.Toast)
            {
                _toasts.Add(new ScreenStackEntry(screen, args));
            }
            else
            {
                _stack.Add(new ScreenStackEntry(screen, args));
            }
            Enter(screen, args);
        }

        public void Replace(ScreenId id, ScreenArgs args)
        {
            if (_stack.Count == 0)
            {
                Push(id, args);
                return;
            }
            IScreen screen = Create(id);
            if (screen.Layer == ScreenLayer.Toast)
            {
                throw new InvalidOperationException("toast screen " + id + " can only be opened with Push");
            }
            int top = _stack.Count - 1;
            ScreenStackEntry old = _stack[top];
            _stack[top] = new ScreenStackEntry(screen, args);
            Exit(old.Screen);
            Enter(screen, args);
        }

        public void Pop()
        {
            if (_stack.Count == 0)
            {
                throw new InvalidOperationException("Pop on an empty screen stack");
            }
            ScreenStackEntry popped = RemoveTop();
            ScreenId target = popped.ExplicitReturnTarget;
            if (target == ScreenId.None || TopId == target)
            {
                Exit(popped.Screen);
                return;
            }
            int found = IndexOf(target);
            if (found < 0)
            {
                Exit(popped.Screen);
                Push(target, ScreenArgs.None);
                return;
            }
            // 돌아갈 화면 위의 칸을 모두 뗀 뒤(상태 먼저) 위에서부터 닫는다
            var unwound = new List<IScreen>(_stack.Count - found);
            unwound.Add(popped.Screen);
            while (_stack.Count - 1 > found)
            {
                unwound.Add(RemoveTop().Screen);
            }
            foreach (IScreen s in unwound)
            {
                Exit(s);
            }
        }

        // 스택을 비우고 화면 하나로 시작한다(타이틀 → 메인 메뉴, 광장 메뉴의 "메인 메뉴로"). 토스트는 남긴다
        public void ResetTo(ScreenId id, ScreenArgs args)
        {
            var closed = new List<IScreen>(_stack.Count);
            while (_stack.Count > 0)
            {
                closed.Add(RemoveTop().Screen);
            }
            foreach (IScreen s in closed)
            {
                Exit(s);
            }
            Push(id, args);
        }

        // 화면 하나를 그 자리에서 닫는다(확인 창이 스스로 닫을 때). 맨 위면 Pop 과 같다(ReturnTarget 을 따른다).
        // 맨 위가 아니면 그 칸만 뗀다. 토스트면 DismissToast 와 같다. 스택에 없으면 false
        public bool Close(IScreen screen)
        {
            if (_stack.Count > 0 && ReferenceEquals(_stack[_stack.Count - 1].Screen, screen))
            {
                Pop();
                return true;
            }
            for (int i = _stack.Count - 2; i >= 0; i--)
            {
                if (ReferenceEquals(_stack[i].Screen, screen))
                {
                    _stack.RemoveAt(i);
                    Exit(screen);
                    return true;
                }
            }
            for (int i = _toasts.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(_toasts[i].Screen, screen))
                {
                    _toasts.RemoveAt(i);
                    Exit(screen);
                    return true;
                }
            }
            return false;
        }

        // 그 id 의 토스트 가운데 나중에 뜬 것 하나를 닫는다
        public bool DismissToast(ScreenId id)
        {
            for (int i = _toasts.Count - 1; i >= 0; i--)
            {
                if (_toasts[i].Id == id)
                {
                    IScreen s = _toasts[i].Screen;
                    _toasts.RemoveAt(i);
                    Exit(s);
                    return true;
                }
            }
            return false;
        }

        // Esc. 포커스 화면이 소비하지 않으면 그 화면을 닫는다(돌아갈 곳이 없는 맨 아래 화면은 닫지 않는다).
        // 무엇이든 일어났으면 true
        public bool Back()
        {
            if (_stack.Count == 0)
            {
                return false;
            }
            ScreenStackEntry top = _stack[_stack.Count - 1];
            if (top.Screen.OnBack())
            {
                return true;
            }
            if (_stack.Count > 1 || top.ExplicitReturnTarget != ScreenId.None)
            {
                Pop();
                return true;
            }
            return false;
        }

        public bool Contains(ScreenId id) => IndexOf(id) >= 0 || ToastIndexOf(id) >= 0;

        // 그릴 화면을 아래 → 위 순서로 채운다(토스트가 맨 뒤). into 는 먼저 비운다
        public void GetVisible(List<IScreen> into)
        {
            into.Clear();
            int first = 0;
            for (int i = _stack.Count - 1; i >= 0; i--)
            {
                if (_stack[i].Layer == ScreenLayer.Fullscreen)
                {
                    first = i;
                    break;
                }
            }
            for (int i = first; i < _stack.Count; i++)
            {
                into.Add(_stack[i].Screen);
            }
            foreach (ScreenStackEntry t in _toasts)
            {
                into.Add(t.Screen);
            }
        }

        // 입력을 받는 화면을 위 → 아래 순서로 채운다. into 는 먼저 비운다
        public void GetInputReceivers(List<IScreen> into)
        {
            into.Clear();
            for (int i = _stack.Count - 1; i >= 0; i--)
            {
                IScreen s = _stack[i].Screen;
                into.Add(s);
                if (Blocks(s))
                {
                    return;
                }
            }
        }

        public bool IsVisible(IScreen screen)
        {
            for (int i = _stack.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(_stack[i].Screen, screen))
                {
                    return true;
                }
                if (_stack[i].Layer == ScreenLayer.Fullscreen)
                {
                    break;
                }
            }
            return ToastIndexOf(screen) >= 0;
        }

        public bool ReceivesInput(IScreen screen)
        {
            for (int i = _stack.Count - 1; i >= 0; i--)
            {
                IScreen s = _stack[i].Screen;
                if (ReferenceEquals(s, screen))
                {
                    return true;
                }
                if (Blocks(s))
                {
                    return false;
                }
            }
            return false;
        }

        // 그 화면 아래로 입력이 내려가지 못하는가. Modal 은 언제나 가로막는다(spec 13 1.1 "Modal = true").
        // Fullscreen 은 아래를 덮으므로 가로막는다. Toast 는 스택에 없으므로 여기에 오지 않는다
        public static bool Blocks(IScreen screen) =>
            screen.BlocksInput || screen.Layer == ScreenLayer.Modal || screen.Layer == ScreenLayer.Fullscreen;

        private ScreenId ReturnTargetAt(int index)
        {
            if (index < 0)
            {
                return ScreenId.None;
            }
            ScreenId explicitTarget = _stack[index].ExplicitReturnTarget;
            if (explicitTarget != ScreenId.None)
            {
                return explicitTarget;
            }
            return index == 0 ? ScreenId.None : _stack[index - 1].Id;
        }

        private IScreen Create(ScreenId id)
        {
            if (id == ScreenId.None)
            {
                throw new ArgumentException("ScreenId.None cannot be opened", nameof(id));
            }
            IScreen screen = _factory.Create(id) ?? throw new InvalidOperationException("screen factory returned null for " + id);
            if (screen.Id != id)
            {
                throw new InvalidOperationException("screen factory returned " + screen.Id + " for " + id);
            }
            return screen;
        }

        private ScreenStackEntry RemoveTop()
        {
            int top = _stack.Count - 1;
            ScreenStackEntry e = _stack[top];
            _stack.RemoveAt(top);
            return e;
        }

        private int IndexOf(ScreenId id)
        {
            for (int i = _stack.Count - 1; i >= 0; i--)
            {
                if (_stack[i].Id == id)
                {
                    return i;
                }
            }
            return -1;
        }

        private int ToastIndexOf(ScreenId id)
        {
            for (int i = _toasts.Count - 1; i >= 0; i--)
            {
                if (_toasts[i].Id == id)
                {
                    return i;
                }
            }
            return -1;
        }

        private int ToastIndexOf(IScreen screen)
        {
            for (int i = _toasts.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(_toasts[i].Screen, screen))
                {
                    return i;
                }
            }
            return -1;
        }

        private void Enter(IScreen screen, ScreenArgs args)
        {
            Opened?.Invoke(screen);
            screen.OnEnter(args);
        }

        private void Exit(IScreen screen)
        {
            screen.OnExit();
            Closed?.Invoke(screen);
        }
    }
}

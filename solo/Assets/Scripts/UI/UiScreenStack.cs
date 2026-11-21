using System;
using System.Collections.Generic;

namespace Wizard.Solo.UI
{
    /// <summary>스택의 칸 하나.</summary>
    public sealed class UiScreenEntry
    {
        public readonly IScreen Screen;
        public readonly ScreenArgs Args;
        public UiScreenEntry(IScreen screen, ScreenArgs args) { Screen = screen; Args = args; }
        public ScreenId Id => Screen.Id;
        public ScreenLayer Layer => Screen.Layer;
        /// <summary>args의 ScreenArgKeys.ReturnTo(없으면 None).</summary>
        public ScreenId ReturnTo => Args != null ? Args.Get(ScreenArgKeys.ReturnTo, ScreenId.None) : ScreenId.None;
    }

    /// <summary>
    /// 화면 스택 규칙(캔버스 없는 순수 클래스, 08 요약서 1.1절). ScreenManager가 이것을 따라 그린다.
    ///
    /// - Push는 맨 위에 쌓고, Replace는 맨 위를 바꾸고, Pop은 맨 위를 닫고 "들어온 곳으로" 돌아간다.
    /// - 돌아갈 곳은 바로 아래 화면이다. 닫힌 칸의 args에 ScreenArgKeys.ReturnTo가 있으면 그 화면으로 간다:
    ///   스택 안에 있으면 그 위를 모두 닫고, 없으면 전부 닫고 그 화면을 새로 연다(정산 → 강화 → Esc = 메인 메뉴).
    /// - Toast 층 화면(Toast, Tooltip)은 따로 쌓인다. Top·입력·Back·Pop·Replace의 대상이 아니다. Dismiss로 닫는다.
    /// - 등록되지 않은 화면(provider가 null)은 경고만 남기고 스택을 바꾸지 않는다.
    /// - 수명: 스택 상태를 먼저 바꾸고 OnExit/OnEnter를 부른다(그 안에서 스택을 다시 바꿔도 된다).
    ///   덮이거나 드러날 때는 부르지 않는다. 닫힌 칸의 args.OnResult는 닫기(와 돌아가기)가 끝난 뒤 부른다.
    /// </summary>
    public sealed class UiScreenStack
    {
        readonly Func<ScreenId, IScreen> _provider;
        readonly Action<string> _warn;
        readonly List<UiScreenEntry> _stack = new List<UiScreenEntry>();
        readonly List<UiScreenEntry> _toasts = new List<UiScreenEntry>();

        /// <param name="provider">id → 화면(이미 Build된 것). 등록이 없으면 null.</param>
        /// <param name="warn">경고 출력(없으면 버린다).</param>
        public UiScreenStack(Func<ScreenId, IScreen> provider, Action<string> warn = null)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _warn = warn;
        }

        /// <summary>화면이 열렸다(OnEnter 직전). 관리자는 여기서 그 화면의 루트를 켜고 맨 앞으로 올린다.</summary>
        public event Action<UiScreenEntry> Entering;
        /// <summary>화면이 닫혔다(OnExit 직후).</summary>
        public event Action<UiScreenEntry> Exited;
        /// <summary>스택이 바뀌었다(보이는 화면을 다시 정할 때).</summary>
        public event Action Changed;

        public int Count => _stack.Count;
        /// <summary>아래 → 위.</summary>
        public IReadOnlyList<UiScreenEntry> Entries => _stack;
        /// <summary>먼저 뜬 것 → 나중에 뜬 것.</summary>
        public IReadOnlyList<UiScreenEntry> Toasts => _toasts;

        /// <summary>맨 위 화면의 id(Toast 층은 세지 않는다. 없으면 None).</summary>
        public ScreenId Top => _stack.Count == 0 ? ScreenId.None : _stack[_stack.Count - 1].Id;
        public UiScreenEntry TopEntry => _stack.Count == 0 ? null : _stack[_stack.Count - 1];
        public IScreen TopScreen => _stack.Count == 0 ? null : _stack[_stack.Count - 1].Screen;

        /// <summary>맨 위 화면이 Pop 될 때 돌아갈 곳(ReturnTo가 있으면 그것, 없으면 바로 아래, 맨 아래면 None).</summary>
        public ScreenId ReturnTarget
        {
            get
            {
                int i = _stack.Count - 1;
                if (i < 0) return ScreenId.None;
                var rt = _stack[i].ReturnTo;
                if (rt != ScreenId.None) return rt;
                return i == 0 ? ScreenId.None : _stack[i - 1].Id;
            }
        }

        public bool IsOpen(ScreenId id) => IndexOf(id) >= 0 || ToastIndexOf(id) >= 0;

        /// <summary>그 화면 객체가 지금 스택(또는 토스트)에 있는가.</summary>
        public bool Contains(IScreen screen)
        {
            for (int i = 0; i < _stack.Count; i++) if (ReferenceEquals(_stack[i].Screen, screen)) return true;
            for (int i = 0; i < _toasts.Count; i++) if (ReferenceEquals(_toasts[i].Screen, screen)) return true;
            return false;
        }

        public void Push(ScreenId id, ScreenArgs args = null)
        {
            var screen = Create(id);
            if (screen == null) return;
            PushScreen(screen, args);
        }

        /// <summary>등록 없이 화면 객체를 바로 쌓는다(관리자의 Toast처럼 ScreenId가 없는 화면). Toast 층이면 토스트 목록으로.</summary>
        public void PushScreen(IScreen screen, ScreenArgs args = null)
        {
            if (screen == null) return;
            if (args == null) args = ScreenArgs.From(Top);
            var e = new UiScreenEntry(screen, args);
            if (screen.Layer == ScreenLayer.Toast) _toasts.Add(e);
            else _stack.Add(e);
            Changed?.Invoke();
            Enter(e);
        }

        public void Replace(ScreenId id, ScreenArgs args = null)
        {
            if (_stack.Count == 0) { Push(id, args); return; }
            var screen = Create(id);
            if (screen == null) return;
            if (screen.Layer == ScreenLayer.Toast) { PushScreen(screen, args); return; }
            int top = _stack.Count - 1;
            var old = _stack[top];
            if (args == null) args = ScreenArgs.From(old.Args != null ? old.Args.Entry : ScreenId.None);
            var e = new UiScreenEntry(screen, args);
            _stack[top] = e;
            Changed?.Invoke();
            Exit(old);
            Enter(e);
        }

        /// <summary>맨 위를 닫고 들어온 곳으로. result는 닫힌 화면의 args.OnResult로 간다. 비었으면 아무 일 없다.</summary>
        public void Pop(object result = null)
        {
            if (_stack.Count == 0) { _warn?.Invoke("UiScreenStack.Pop on empty stack"); return; }
            var popped = RemoveTop();
            var target = popped.ReturnTo;
            var closed = new List<UiScreenEntry> { popped };
            bool openTarget = false;
            if (target != ScreenId.None && Top != target)
            {
                int found = IndexOf(target);
                if (found >= 0) { while (_stack.Count - 1 > found) closed.Add(RemoveTop()); }
                else { while (_stack.Count > 0) closed.Add(RemoveTop()); openTarget = true; }
            }
            Changed?.Invoke();
            for (int i = 0; i < closed.Count; i++) Exit(closed[i]);
            if (openTarget) Push(target, ScreenArgs.From(popped.Id));
            popped.Args?.OnResult?.Invoke(result);
        }

        /// <summary>id 화면이 맨 위가 될 때까지 닫는다. 쌓여 있지 않으면 전부 닫고 id를 연다(args는 새로 열 때만 쓴다).</summary>
        public void PopTo(ScreenId id, ScreenArgs args = null)
        {
            int found = IndexOf(id);
            var closed = new List<UiScreenEntry>();
            if (found >= 0) { while (_stack.Count - 1 > found) closed.Add(RemoveTop()); }
            else { while (_stack.Count > 0) closed.Add(RemoveTop()); }
            if (closed.Count > 0) Changed?.Invoke();
            for (int i = 0; i < closed.Count; i++) Exit(closed[i]);
            if (found < 0) Push(id, args);
        }

        /// <summary>
        /// Esc: 맨 위 화면의 OnBack. false면 Pop(돌아갈 곳이 없는 맨 아래 화면은 닫지 않는다). 무엇이든 일어났으면 true.
        /// </summary>
        public bool Back()
        {
            var top = TopEntry;
            if (top == null) return false;
            if (top.Screen.OnBack()) return true;
            // OnBack 안에서 스택을 바꿨으면(스스로 닫음 등) 그것으로 끝.
            if (!ReferenceEquals(TopEntry, top)) return true;
            if (_stack.Count > 1 || top.ReturnTo != ScreenId.None) { Pop(); return true; }
            return false;
        }

        /// <summary>그 화면을 닫는다: 맨 위면 Pop과 같다(result 전달). 아래 칸이면 그 칸만, 토스트면 토스트 목록에서.</summary>
        public bool Dismiss(IScreen screen, object result = null)
        {
            if (screen == null) return false;
            if (_stack.Count > 0 && ReferenceEquals(_stack[_stack.Count - 1].Screen, screen)) { Pop(result); return true; }
            for (int i = _stack.Count - 2; i >= 0; i--)
            {
                if (!ReferenceEquals(_stack[i].Screen, screen)) continue;
                var e = _stack[i];
                _stack.RemoveAt(i);
                Changed?.Invoke();
                Exit(e);
                e.Args?.OnResult?.Invoke(result);
                return true;
            }
            for (int i = _toasts.Count - 1; i >= 0; i--)
            {
                if (!ReferenceEquals(_toasts[i].Screen, screen)) continue;
                var e = _toasts[i];
                _toasts.RemoveAt(i);
                Changed?.Invoke();
                Exit(e);
                e.Args?.OnResult?.Invoke(result);
                return true;
            }
            return false;
        }

        /// <summary>그 id의 토스트 층 화면 가운데 나중에 뜬 것 하나를 닫는다.</summary>
        public bool DismissToast(ScreenId id)
        {
            int i = ToastIndexOf(id);
            if (i < 0) return false;
            return Dismiss(_toasts[i].Screen);
        }

        /// <summary>모든 화면을 위에서부터 닫는다(관리자를 걷을 때). OnResult는 부르지 않는다.</summary>
        public void Clear()
        {
            var closed = new List<UiScreenEntry>();
            for (int i = _toasts.Count - 1; i >= 0; i--) closed.Add(_toasts[i]);
            for (int i = _stack.Count - 1; i >= 0; i--) closed.Add(_stack[i]);
            _toasts.Clear();
            _stack.Clear();
            if (closed.Count > 0) Changed?.Invoke();
            for (int i = 0; i < closed.Count; i++) Exit(closed[i]);
        }

        /// <summary>
        /// 그 화면 아래로 메뉴 키가 내려가지 못하는가: BlocksInput(Modal은 언제나), 또는 Fullscreen(아래를 덮는다).
        /// </summary>
        public static bool BlocksKeys(IScreen s) => s.BlocksInput || s.Layer == ScreenLayer.Modal || s.Layer == ScreenLayer.Fullscreen;

        /// <summary>메뉴 키를 받는 화면(위 → 아래). 가로막는 화면에서 멈춘다. into는 먼저 비운다.</summary>
        public void GetInputReceivers(List<IScreen> into)
        {
            into.Clear();
            for (int i = _stack.Count - 1; i >= 0; i--)
            {
                var s = _stack[i].Screen;
                into.Add(s);
                if (BlocksKeys(s)) return;
            }
        }

        /// <summary>
        /// Tick 받을 화면(위 → 아래, 그다음 토스트): 맨 위와, 그 위 화면이 BlocksInput이 아닌 동안 아래 화면(IScreen.Tick 약속).
        /// </summary>
        public void GetTickReceivers(List<IScreen> into)
        {
            into.Clear();
            for (int i = _stack.Count - 1; i >= 0; i--)
            {
                var s = _stack[i].Screen;
                into.Add(s);
                if (s.BlocksInput || s.Layer == ScreenLayer.Modal) break;
            }
            for (int i = 0; i < _toasts.Count; i++) into.Add(_toasts[i].Screen);
        }

        /// <summary>그 칸이 보이는가: 맨 위에서 아래로 첫 Fullscreen까지(그 아래는 덮인다). 토스트는 언제나.</summary>
        public bool IsVisibleAt(int index)
        {
            for (int i = _stack.Count - 1; i > index; i--)
                if (_stack[i].Layer == ScreenLayer.Fullscreen) return false;
            return index >= 0 && index < _stack.Count;
        }

        public bool IsVisible(IScreen screen)
        {
            // 토스트 층(토스트·툴팁)은 언제나 보인다 — 맨 위가 Fullscreen이어도 그 위에 뜬다.
            if (ToastIndexOf(screen) >= 0) return true;
            for (int i = _stack.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(_stack[i].Screen, screen)) return true;
                if (_stack[i].Layer == ScreenLayer.Fullscreen) return false;
            }
            return false;
        }

        IScreen Create(ScreenId id)
        {
            if (id == ScreenId.None) { _warn?.Invoke("UiScreenStack: ScreenId.None cannot be opened"); return null; }
            IScreen s = null;
            try { s = _provider(id); }
            catch (Exception ex) { _warn?.Invoke("UiScreenStack: creating " + id + " failed: " + ex); return null; }
            if (s == null) { _warn?.Invoke("UiScreenStack: screen " + id + " is not registered (ignored)"); return null; }
            return s;
        }

        UiScreenEntry RemoveTop()
        {
            int top = _stack.Count - 1;
            var e = _stack[top];
            _stack.RemoveAt(top);
            return e;
        }

        int IndexOf(ScreenId id)
        {
            for (int i = _stack.Count - 1; i >= 0; i--) if (_stack[i].Id == id) return i;
            return -1;
        }

        int ToastIndexOf(ScreenId id)
        {
            for (int i = _toasts.Count - 1; i >= 0; i--) if (_toasts[i].Id == id) return i;
            return -1;
        }

        int ToastIndexOf(IScreen screen)
        {
            for (int i = _toasts.Count - 1; i >= 0; i--) if (ReferenceEquals(_toasts[i].Screen, screen)) return i;
            return -1;
        }

        void Enter(UiScreenEntry e)
        {
            Entering?.Invoke(e);
            e.Screen.OnEnter(e.Args);
        }

        void Exit(UiScreenEntry e)
        {
            e.Screen.OnExit();
            Exited?.Invoke(e);
        }
    }
}

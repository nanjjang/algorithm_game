#nullable enable
using System;
using System.Collections.Generic;
using Wizard.ViewLogic.Stack;

namespace Wizard.Unity.UI.Common
{
    // ScreenStack(ViewLogic)을 따라 화면 뷰를 만들고 없앤다(spec 13 1.1, ARCHITECTURE 4.3: 화면은 장면이 아니라 스택으로 바꾼다).
    //   - 스택이 화면을 열면(Opened) 뷰를 만든다(숨김). 모델의 OnEnter 가 끝난 뒤의 첫 Sync 에서 Build 한다.
    //   - 스택이 화면을 닫으면(Closed) 그 뷰를 없앤다(uGUI 개체째).
    //   - Sync: 보이는 화면(ScreenStack.GetVisible: 맨 위에서 첫 Fullscreen 까지 + 토스트)만 켜고, 그 순서(아래 → 위, 토스트 맨 위)로 그린다.
    //   - Frame: 화면 틱을 세어 뷰에 돌리고 Sync 한다. 렌더 프레임마다 ScreenHostDriver 가 부른다.
    // 뷰는 모델을 읽기만 한다. 스택을 바꾸는 것은 모델과 UiInputRouter(Esc → Back)다.
    public sealed class ScreenHost : IDisposable
    {
        private readonly IScreenViewFactory _factory;
        private readonly List<UiScreenView> _views = new List<UiScreenView>();
        private readonly List<IScreen> _visible = new List<IScreen>();
        private readonly UiTickClock _clock = new UiTickClock();
        private bool _disposed;

        public ScreenHost(ScreenStack stack, IScreenViewFactory factory, UiViewContext context)
        {
            Stack = stack ?? throw new ArgumentNullException(nameof(stack));
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            Context = context ?? throw new ArgumentNullException(nameof(context));
            Stack.Opened += OnOpened;
            Stack.Closed += OnClosed;
            foreach (ScreenStackEntry e in Stack.Entries)
            {
                OnOpened(e.Screen);
            }
            foreach (ScreenStackEntry e in Stack.Toasts)
            {
                OnOpened(e.Screen);
            }
            Sync();
        }

        public ScreenStack Stack { get; }

        public UiViewContext Context { get; }

        // 만든 순서
        public IReadOnlyList<UiScreenView> Views => _views;

        public long UiTicks => _clock.TotalTicks;

        public UiScreenView? ViewOf(IScreen? screen)
        {
            if (screen == null)
            {
                return null;
            }
            foreach (UiScreenView v in _views)
            {
                if (ReferenceEquals(v.Screen, screen))
                {
                    return v;
                }
            }
            return null;
        }

        public UiScreenView? ViewOf(ScreenId id)
        {
            for (int i = _views.Count - 1; i >= 0; i--)
            {
                if (_views[i].Id == id)
                {
                    return _views[i];
                }
            }
            return null;
        }

        // 렌더 프레임 하나. nowSeconds 는 단조 시계(초)
        public void Frame(double nowSeconds)
        {
            ThrowIfDisposed();
            int ticks = _clock.Advance(nowSeconds);
            for (int t = 0; t < ticks; t++)
            {
                for (int i = 0; i < _views.Count; i++)
                {
                    if (_views[i].Shown)
                    {
                        _views[i].Tick();
                    }
                }
            }
            Sync();
        }

        // 스택과 뷰를 맞춘다: 아직 짓지 않은 뷰를 짓고, 보이는 것만 켜고, 그리는 순서를 맞추고, 다시 그린다
        public void Sync()
        {
            ThrowIfDisposed();
            Stack.GetVisible(_visible);
            for (int i = 0; i < _views.Count; i++)
            {
                UiScreenView v = _views[i];
                if (!v.IsBuilt && !v.IsDisposed)
                {
                    v.Build();
                }
            }
            // Build 가 모델을 거쳐 스택을 바꿨을 수 있으니 다시 읽는다
            Stack.GetVisible(_visible);
            for (int i = 0; i < _views.Count; i++)
            {
                _views[i].SetShown(_visible.Contains(_views[i].Screen));
            }
            foreach (IScreen s in _visible)
            {
                ViewOf(s)?.Root.Transform.SetAsLastSibling();
            }
            RefreshShown();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            Stack.Opened -= OnOpened;
            Stack.Closed -= OnClosed;
            foreach (UiScreenView v in _views)
            {
                v.Dispose();
            }
            _views.Clear();
        }

        private void RefreshShown()
        {
            for (int i = 0; i < _views.Count; i++)
            {
                if (_views[i].Shown)
                {
                    _views[i].Refresh();
                }
            }
        }

        private void OnOpened(IScreen screen)
        {
            if (ViewOf(screen) != null)
            {
                return;
            }
            UiScreenView view = _factory.Create(screen, Context) ?? throw new InvalidOperationException("no view for " + screen.Id);
            _views.Add(view);
        }

        private void OnClosed(IScreen screen)
        {
            for (int i = _views.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(_views[i].Screen, screen))
                {
                    UiScreenView v = _views[i];
                    _views.RemoveAt(i);
                    v.Dispose();
                    return;
                }
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ScreenHost));
            }
        }
    }
}

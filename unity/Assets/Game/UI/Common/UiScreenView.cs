#nullable enable
using System;
using System.Collections.Generic;
using Wizard.Client;
using Wizard.ViewLogic.Common;
using Wizard.ViewLogic.Stack;

namespace Wizard.Unity.UI.Common
{
    // 화면 모델(IScreen) 하나의 뷰(T-32, CONTRACTS 13). ScreenHost 가 스택을 따라 만들고 없앤다.
    // 뷰는 모델을 읽어 위젯을 그리고, 입력은 InteractableGroup(ViewLogic 의 상태 기계)에 넘기기만 한다. 규칙을 두지 않는다.
    //
    // 수명: 만든다(뿌리 개체만, 숨김) → Build(첫 Sync 때. 모델의 OnEnter 가 끝난 뒤라 모델의 묶음을 읽을 수 있다)
    //       → Refresh(프레임마다) → Dispose(모델이 닫힐 때).
    // 누를 수 있는 것의 묶음: 모델이 묶음을 쥐면 UseGroup 으로 그것을 쓰고(모델이 Raised 를 듣는다),
    //       모델이 쥐지 않으면 CreateGroup 으로 뷰가 만든다(그래도 상태 기계는 ViewLogic 의 것이다).
    public abstract class UiScreenView : IDisposable
    {
        private readonly List<UiInteractiveWidget> _widgets = new List<UiInteractiveWidget>();
        private readonly List<UiList> _lists = new List<UiList>();
        private readonly List<PixelRect> _rectScratch = new List<PixelRect>();
        private readonly List<UiInteractiveWidget> _candidateScratch = new List<UiInteractiveWidget>();
        private InteractableGroup? _group;
        private bool _ownsGroup;
        private bool _disposed;

        protected UiScreenView(IScreen screen, UiViewContext context)
        {
            Screen = screen ?? throw new ArgumentNullException(nameof(screen));
            Context = context ?? throw new ArgumentNullException(nameof(context));
            Root = new UiWidget("Screen:" + screen.Id, context.Stage, new PixelRect(0, 0, UiLayout.ReferenceWidthPx, UiLayout.ReferenceHeightPx));
            Root.Visible = false;
        }

        public IScreen Screen { get; }

        public ScreenId Id => Screen.Id;

        public UiViewContext Context { get; }

        // 무대 전체(1920×1080). 화면은 이 아래에 위젯을 놓는다
        public UiWidget Root { get; }

        public InteractableGroup? Group => _group;

        public bool IsBuilt { get; private set; }

        public bool IsDisposed => _disposed;

        public IReadOnlyList<UiInteractiveWidget> Interactives => _widgets;

        // 포인터를 이 화면에서 멈추게 하는 곳(무대 좌표). 이 안을 누르면 아래 화면으로 내려가지 않는다.
        // 기본은 무대 전체. 판 하나만 띄우는 화면(Panel)은 그 판의 사각형으로 좁힌다
        public virtual PixelRect CoverRect => Root.AbsoluteRect;

        public bool Shown => Root.Visible;

        public void Build()
        {
            ThrowIfDisposed();
            if (IsBuilt)
            {
                return;
            }
            OnBuild();
            IsBuilt = true;
            InteractableGroup? group = _group;
            if (group != null && group.CursorTarget == null)
            {
                UiInteractiveWidget? first = DefaultCursor();
                if (first != null)
                {
                    group.SetCursor(first.Control);    // 처음 열 때 기본 칸(모드는 바꾸지 않는다)
                }
            }
            Refresh();
        }

        // ScreenHost 가 스택의 보이는 화면을 따라 켜고 끈다
        public void SetShown(bool shown) => Root.Visible = shown;

        // 화면 틱 하나(spec 13 0.2 UiTick 60Hz). 커서 괄호의 움직임이 이것으로 간다.
        // 묶음의 Tick 은 표시용(괄호 프레임)이라 모델이 쥔 묶음도 뷰가 돌린다. 모델은 묶음을 틱하지 않는다
        public void Tick()
        {
            _group?.Tick();
            OnTick();
        }

        // 모델을 다시 읽어 그린다
        public void Refresh()
        {
            if (!IsBuilt || _disposed)
            {
                return;
            }
            OnRefresh();
            foreach (UiList list in _lists)
            {
                list.FollowCursor();
            }
            for (int i = 0; i < _widgets.Count; i++)
            {
                _widgets[i].Refresh();
            }
        }

        // ── 입력(UiInputRouter 가 부른다) ──

        // 무대 좌표 p 아래의 누를 수 있는 위젯. 나중에 더한 것이 위에 그려지므로 뒤에서부터 본다. 숨은 것은 빼다
        public UiInteractiveWidget? HitTest(PixelPoint p)
        {
            for (int i = _widgets.Count - 1; i >= 0; i--)
            {
                UiInteractiveWidget w = _widgets[i];
                if (IsShownInView(w) && w.AbsoluteRect.Contains(p))
                {
                    return w;
                }
            }
            return null;
        }

        public bool Covers(PixelPoint p) => CoverRect.Contains(p);

        public UiInteractiveWidget? WidgetOf(Interactable? control)
        {
            if (control == null)
            {
                return null;
            }
            foreach (UiInteractiveWidget w in _widgets)
            {
                if (ReferenceEquals(w.Control, control))
                {
                    return w;
                }
            }
            return null;
        }

        // 지금 커서에서 그 방향의 다음 위젯. 커서가 없으면 기본 칸
        public UiInteractiveWidget? Neighbor(UiDirection direction)
        {
            UiInteractiveWidget? from = WidgetOf(_group?.CursorTarget);
            if (from == null)
            {
                return DefaultCursor();
            }
            UiInteractiveWidget? forced = from.NavigationOverride?.Invoke(direction);
            if (forced != null)
            {
                return forced;
            }
            _rectScratch.Clear();
            _candidateScratch.Clear();
            foreach (UiInteractiveWidget w in _widgets)
            {
                if (!ReferenceEquals(w, from) && IsShownInView(w))
                {
                    _candidateScratch.Add(w);
                    _rectScratch.Add(w.AbsoluteRect);
                }
            }
            int i = UiNavigator.FindNeighbor(from.AbsoluteRect, direction, _rectScratch);
            return i < 0 ? null : _candidateScratch[i];
        }

        // 메뉴 키 가운데 화면마다 뜻이 다른 것(K 빼기, 그 밖의 키). 소비했으면 true.
        // 방향·확인·나가기는 UiInputRouter 가 공통으로 처리하므로 여기에 오지 않는다
        public virtual bool OnKey(PhysicalKey key, UiKeyAction action) => false;

        // 마우스 바퀴(+ 아래로). 포인터 아래의 목록을 민다. 소비했으면 true
        public virtual bool OnScroll(PixelPoint p, int steps)
        {
            for (int i = _lists.Count - 1; i >= 0; i--)
            {
                UiList list = _lists[i];
                if (IsShownInView(list) && list.AbsoluteRect.Contains(p))
                {
                    list.Scroll(steps);
                    return true;
                }
            }
            return false;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            OnDispose();
            if (_group != null)
            {
                _group.Raised -= OnGroupRaised;
                if (_ownsGroup)
                {
                    _group.Dispose();
                }
                _group = null;
            }
            _widgets.Clear();
            _lists.Clear();
            Root.Destroy();
        }

        // ── 화면이 쓰는 것 ──

        protected abstract void OnBuild();

        protected virtual void OnRefresh()
        {
        }

        protected virtual void OnTick()
        {
        }

        protected virtual void OnDispose()
        {
        }

        // 처음 커서가 갈 곳. 기본은 처음 더한 누를 수 있는 위젯
        protected virtual UiInteractiveWidget? DefaultCursor()
        {
            foreach (UiInteractiveWidget w in _widgets)
            {
                if (IsShownInView(w))
                {
                    return w;
                }
            }
            return null;
        }

        // 모델이 쥔 묶음을 쓴다(모델이 닫힐 때 모델이 Dispose 한다)
        protected void UseGroup(InteractableGroup group)
        {
            SetGroup(group ?? throw new ArgumentNullException(nameof(group)), owns: false);
        }

        // 모델이 묶음을 쥐지 않는 화면: 뷰가 만든다(화면 전체가 같은 입력 모드를 쓴다)
        protected InteractableGroup CreateGroup(PickPolicy pickPolicy = PickPolicy.Single)
        {
            var group = new InteractableGroup(Context.Mode, pickPolicy);
            SetGroup(group, owns: true);
            return group;
        }

        // 화면 묶음에 새 누를 수 있는 것을 만든다(모델이 쥐지 않는 버튼·탭·칸)
        protected Interactable NewControl(string id, bool isToggle = false)
        {
            InteractableGroup group = _group ?? throw new InvalidOperationException("UseGroup or CreateGroup first");
            var c = new Interactable(id, UiLayout.MinHitSizePx, UiLayout.MinHitSizePx, isToggle);
            group.Add(c);
            return c;
        }

        // 화면 안에서 보이는가(뿌리의 켜짐은 보지 않는다: 숨겨진 채 지을 때도 기본 칸과 이웃을 찾는다)
        public bool IsShownInView(UiWidget widget)
        {
            for (UiWidget? w = widget; w != null && !ReferenceEquals(w, Root); w = w.Parent)
            {
                if (!w.Visible)
                {
                    return false;
                }
            }
            return true;
        }

        internal void Register(UiInteractiveWidget widget) => _widgets.Add(widget);

        internal void Unregister(UiInteractiveWidget widget) => _widgets.Remove(widget);

        internal void RegisterList(UiList list) => _lists.Add(list);

        private void SetGroup(InteractableGroup group, bool owns)
        {
            if (_group != null)
            {
                throw new InvalidOperationException("the screen view already has an InteractableGroup");
            }
            _group = group;
            _ownsGroup = owns;
            group.Raised += OnGroupRaised;
        }

        private void OnGroupRaised(InteractionEvent e)
        {
            if (e.Kind != InteractionEventKind.Activated || _disposed)
            {
                return;
            }
            WidgetOf(e.Target)?.RaiseActivated();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }
    }
}

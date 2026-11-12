#nullable enable
using System;
using System.Collections.Generic;
using Wizard.ViewLogic.Common;

namespace Wizard.Unity.UI.Common
{
    // 탭 줄(자리 표시). 탭을 왼쪽부터 같은 폭으로 놓는다. 어느 탭이 켜져 있는지는 화면(모델)이 SetSelected 로 정한다.
    // 탭을 끝까지 누르면 TabActivated(번호)를 낸다. AutoSelect 면(기본) 그 탭 하나만 켜진 상태로 맞춘다.
    // 모델이 고를지 말지 정하는 화면은 AutoSelect 를 끄고 모델의 답으로 SetSelected 를 부른다(뷰에는 규칙이 없다).
    public sealed class UiTabBar : UiWidget
    {
        private readonly UiScreenView _owner;
        private readonly List<UiTab> _tabs = new List<UiTab>();
        private readonly int _tabWidthPx;

        public UiTabBar(UiScreenView owner, UiWidget parent, PixelRect localRect, int tabWidthPx)
            : base("TabBar", parent, localRect)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            if (tabWidthPx < UiLayout.MinHitSizePx)
            {
                throw new ArgumentOutOfRangeException(nameof(tabWidthPx), tabWidthPx, "smaller than the minimum hit size");
            }
            _tabWidthPx = tabWidthPx;
        }

        public IReadOnlyList<UiTab> Tabs => _tabs;

        public int SelectedIndex { get; private set; } = -1;

        public bool AutoSelect { get; set; } = true;

        public event Action<int>? TabActivated;

        // control 은 isToggle 이어야 한다. 모델이 쥔 것을 주거나 화면 묶음에 새로 만든 것을 준다
        public UiTab AddTab(Interactable control, string labelKey)
        {
            int index = _tabs.Count;
            var tab = new UiTab(_owner, this, new PixelRect(index * _tabWidthPx, 0, _tabWidthPx, LocalRect.Height), control, labelKey, index);
            tab.Activated += OnTabActivated;
            _tabs.Add(tab);
            return tab;
        }

        // -1 이면 모두 끈다
        public void SetSelected(int index)
        {
            if (index < -1 || index >= _tabs.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, null);
            }
            SelectedIndex = index;
            InteractableGroup group = _owner.Group!;
            for (int i = 0; i < _tabs.Count; i++)
            {
                group.SetToggled(_tabs[i].Control, i == index);
            }
        }

        private void OnTabActivated(UiInteractiveWidget w)
        {
            var tab = (UiTab)w;
            if (AutoSelect)
            {
                SetSelected(tab.Index);
            }
            TabActivated?.Invoke(tab.Index);
        }
    }
}

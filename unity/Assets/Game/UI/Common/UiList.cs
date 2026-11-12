#nullable enable
using System;
using System.Collections.Generic;
using Wizard.ViewLogic.Common;

namespace Wizard.Unity.UI.Common
{
    // 세로 목록(자리 표시). 줄 높이는 48px 이상(spec 14 4.3). 판 높이에 드는 줄만 보이고 나머지는 숨긴다(창).
    // 목록 판에는 테가 없다: 줄이 판의 폭 그대로 8px 격자에 놓이게 하고, 테는 줄마다 그린다.
    //   - 키보드: 위·아래는 이웃 줄로 간다(창 밖의 줄이어도). 커서가 간 줄이 창 밖이면 창을 그 줄까지 민다.
    //     맨 위에서 위, 맨 아래에서 아래는 목록 밖의 위젯을 자리로 찾는다.
    //   - 마우스: 바퀴로 창을 민다(Scroll). 보이는 줄만 누를 수 있다.
    // 무엇을 줄로 둘지(정렬, 거르기)는 모델이 정해 SetItems 로 준다.
    public sealed class UiList : UiPanel
    {
        private readonly UiScreenView _owner;
        private readonly List<UiListRow> _rows = new List<UiListRow>();
        private readonly List<Interactable> _ownedControls = new List<Interactable>();

        public UiList(UiScreenView owner, UiWidget parent, PixelRect localRect, int rowHeightPx = UiLayout.MinListRowHeightPx)
            : base("List", parent, localRect, framed: false)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            if (rowHeightPx < UiLayout.MinListRowHeightPx)
            {
                throw new ArgumentOutOfRangeException(nameof(rowHeightPx), rowHeightPx, "list rows must be at least 48px high (spec 14 4.3)");
            }
            RowHeightPx = rowHeightPx;
            SetFill(UiPalette.WoodDark);
            owner.RegisterList(this);
        }

        public int RowHeightPx { get; }

        public IReadOnlyList<UiListRow> Rows => _rows;

        // 창에 한꺼번에 드는 줄 수
        public int WindowRows => Math.Max(1, InnerRect.Height / RowHeightPx);

        // 창의 첫 줄 번호
        public int ScrollOffset { get; private set; }

        public int MaxScrollOffset => Math.Max(0, _rows.Count - WindowRows);

        public void SetItems(IReadOnlyList<UiListItem> items)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }
            if (_owner.Group == null)
            {
                throw new InvalidOperationException("the screen view has no InteractableGroup yet");
            }
            Clear();
            PixelRect inner = InnerRect;
            for (int i = 0; i < items.Count; i++)
            {
                UiListItem item = items[i];
                Interactable control = item.Control ?? NewControl(item.Id, inner.Width);
                var row = new UiListRow(_owner, this, new PixelRect(inner.X, inner.Y, inner.Width, RowHeightPx), control, i);
                if (item.TextKey != null)
                {
                    row.Label.SetKey(item.TextKey);
                }
                else
                {
                    row.Label.SetRaw(item.RawText ?? string.Empty);
                }
                row.NavigationOverride = d => Neighbor(row, d);
                _rows.Add(row);
            }
            ScrollOffset = 0;
            LayoutRows();
        }

        // 바퀴 따위로 창을 민다(+ 아래로). 끝에서 멈춘다
        public void Scroll(int rows)
        {
            int next = Math.Max(0, Math.Min(MaxScrollOffset, ScrollOffset + rows));
            if (next != ScrollOffset)
            {
                ScrollOffset = next;
                LayoutRows();
            }
        }

        // 그 줄이 창 안에 들게 민다
        public void EnsureVisible(int index)
        {
            if (index < 0 || index >= _rows.Count)
            {
                return;
            }
            int next = ScrollOffset;
            if (index < next)
            {
                next = index;
            }
            else if (index >= next + WindowRows)
            {
                next = index - WindowRows + 1;
            }
            if (next != ScrollOffset)
            {
                ScrollOffset = next;
                LayoutRows();
            }
        }

        // 화면 뷰가 Refresh 때 부른다: 커서가 이 목록의 줄에 있으면 그 줄이 보이게 한다
        public void FollowCursor()
        {
            Interactable? cursor = _owner.Group?.CursorTarget;
            if (cursor == null)
            {
                return;
            }
            for (int i = 0; i < _rows.Count; i++)
            {
                if (ReferenceEquals(_rows[i].Control, cursor))
                {
                    EnsureVisible(i);
                    return;
                }
            }
        }

        public void Clear()
        {
            InteractableGroup? group = _owner.Group;
            foreach (UiListRow row in _rows)
            {
                _owner.Unregister(row);
                if (group != null && _ownedControls.Contains(row.Control))
                {
                    group.Remove(row.Control);
                }
                row.Destroy();
            }
            _rows.Clear();
            _ownedControls.Clear();
            ScrollOffset = 0;
        }

        private Interactable NewControl(string id, int widthPx)
        {
            var c = new Interactable(id, widthPx, RowHeightPx);
            _ownedControls.Add(c);
            return c;
        }

        private UiInteractiveWidget? Neighbor(UiListRow row, UiDirection direction)
        {
            int i = row.Index;
            if (direction == UiDirection.Up && i > 0)
            {
                return _rows[i - 1];
            }
            if (direction == UiDirection.Down && i + 1 < _rows.Count)
            {
                return _rows[i + 1];
            }
            return null;
        }

        private void LayoutRows()
        {
            PixelRect inner = InnerRect;
            int window = WindowRows;
            for (int i = 0; i < _rows.Count; i++)
            {
                int slot = i - ScrollOffset;
                bool shown = slot >= 0 && slot < window;
                _rows[i].Visible = shown;
                if (shown)
                {
                    _rows[i].SetRect(new PixelRect(inner.X, inner.Y + slot * RowHeightPx, inner.Width, RowHeightPx));
                }
            }
        }
    }
}

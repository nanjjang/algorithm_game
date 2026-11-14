using System.Collections.Generic;
using UnityEngine;

namespace Wizard.Solo.UI
{
    /// <summary>키보드 커서가 갈 수 있는 것(버튼, 탭, 칸, 목록 줄, 카드, 슬라이더, 토글). UiWidget이 구현한다.</summary>
    public interface IUiFocusable
    {
        RectTransform Rect { get; }
        /// <summary>꺼져 있어도 커서는 간다(08 1.3절). 결정하면 이유를 다시 보여 준다.</summary>
        bool Enabled { get; }
        /// <summary>커서가 여기 있는가(그룹이 정한다).</summary>
        bool Cursor { get; set; }
        /// <summary>J(결정). 꺼져 있으면 아무 일 없이 이유를 보여 준다.</summary>
        void Activate();
        /// <summary>커서가 있을 때 먼저 받는 키(슬라이더의 좌우 등). true면 소비.</summary>
        bool HandleKey(MenuKey key);
        /// <summary>보이고 켜진 GameObject인가(꺼진 줄은 건너뛴다).</summary>
        bool IsNavigable { get; }
    }

    /// <summary>
    /// 칸 사이 커서 이동(순수 함수). 중심점은 y 위가 +인 좌표(월드·캔버스 좌표 그대로).
    /// 방향 쪽 반평면에 있는 것 가운데 (주축 거리 + 2 × 옆 거리)가 가장 작은 것. 없으면 wrap일 때 반대쪽 끝, 아니면 그대로.
    /// </summary>
    public static class UiFocusNav
    {
        const float Epsilon = 0.5f;

        public static int Next(IReadOnlyList<Vector2> centers, int current, MenuKey dir, bool wrap)
        {
            if (centers == null || centers.Count == 0) return -1;
            if (current < 0 || current >= centers.Count) return 0;
            Vector2 axis;
            switch (dir)
            {
                case MenuKey.Up: axis = Vector2.up; break;
                case MenuKey.Down: axis = Vector2.down; break;
                case MenuKey.Left: axis = Vector2.left; break;
                case MenuKey.Right: axis = Vector2.right; break;
                default: return current;
            }
            Vector2 c = centers[current];
            int best = -1;
            float bestScore = float.MaxValue;
            for (int i = 0; i < centers.Count; i++)
            {
                if (i == current) continue;
                Vector2 d = centers[i] - c;
                float primary = Vector2.Dot(d, axis);
                if (primary <= Epsilon) continue;
                float side = Mathf.Abs(d.x * axis.y - d.y * axis.x);
                float score = primary + 2f * side;
                if (score < bestScore) { bestScore = score; best = i; }
            }
            if (best >= 0 || !wrap) return best >= 0 ? best : current;
            // 반대쪽 끝: 옆 거리가 작고 반대 방향으로 가장 먼 것.
            for (int i = 0; i < centers.Count; i++)
            {
                if (i == current) continue;
                Vector2 d = centers[i] - c;
                float primary = Vector2.Dot(d, axis);
                if (primary >= -Epsilon) continue;
                float side = Mathf.Abs(d.x * axis.y - d.y * axis.x);
                float score = 2f * side + primary;
                if (score < bestScore) { bestScore = score; best = i; }
            }
            return best >= 0 ? best : current;
        }

        /// <summary>격자(columns열) 이동. 끝에서 멈추거나(wrap=false) 같은 줄·열의 반대쪽 끝으로.</summary>
        public static int NextInGrid(int count, int columns, int current, MenuKey dir, bool wrap)
        {
            if (count <= 0) return -1;
            if (columns <= 0) columns = 1;
            if (current < 0 || current >= count) return 0;
            int row = current / columns, col = current % columns;
            int rows = (count + columns - 1) / columns;
            switch (dir)
            {
                case MenuKey.Left:
                    if (col > 0) return current - 1;
                    if (!wrap) return current;
                    return Mathf.Min(count - 1, row * columns + columns - 1);
                case MenuKey.Right:
                    if (col < columns - 1 && current + 1 < count) return current + 1;
                    if (!wrap) return current;
                    return row * columns;
                case MenuKey.Up:
                    if (row > 0) return current - columns;
                    if (!wrap) return current;
                    { int t = (rows - 1) * columns + col; while (t >= count) t -= columns; return t; }
                case MenuKey.Down:
                    if (current + columns < count) return current + columns;
                    if (row < rows - 1) return count - 1; // 마지막 줄이 짧을 때: 그 줄의 끝으로
                    if (!wrap) return current;
                    return col;
                default: return current;
            }
        }
    }

    /// <summary>
    /// 한 화면(또는 그 안의 한 구역)의 키보드 커서. 방향 키로 칸 사이를 옮기고, J로 커서의 것을 누른다.
    /// 마우스가 올라가면(Mouse 모드) 커서를 그 칸으로 옮겨 키보드로 바꿀 때 그 자리에서 이어진다(괄호는 Mouse 모드에서 고른 것에만).
    /// 커서를 옮기면 ui.common.cursor_move 소리 사건을 낸다. 커서가 있는 칸에 툴팁이 붙어 있으면 Keyboard 모드에서 그 툴팁을 띄운다.
    /// </summary>
    public sealed class UiFocusGroup
    {
        readonly List<IUiFocusable> _items = new List<IUiFocusable>();
        readonly List<Vector2> _centers = new List<Vector2>();
        readonly List<int> _map = new List<int>();
        readonly UiInputModeTracker _mode;
        readonly ISoundPlayer _sound;
        int _index = -1;

        /// <summary>0이면 위치로 찾기, 1 이상이면 그 열 수의 격자(추가한 순서 = 왼쪽 위부터 줄 순).</summary>
        public int Columns;
        /// <summary>끝에서 반대쪽으로 넘어가는가(세로 목록 메뉴 등).</summary>
        public bool Wrap;
        /// <summary>커서가 바뀌었다(새 칸, 앞 칸 순번).</summary>
        public System.Action<int> CursorChanged;
        /// <summary>커서 칸의 툴팁을 띄우고 내리는 곳(관리자가 넣는다. null이면 툴팁 없음).</summary>
        public ScreenManager Tooltips;

        public UiFocusGroup(UiInputModeTracker mode = null, ISoundPlayer sound = null)
        {
            _mode = mode ?? new UiInputModeTracker();
            _sound = sound;
        }

        public UiInputModeTracker ModeTracker => _mode;
        public UiInputMode Mode => _mode.Mode;
        public IReadOnlyList<IUiFocusable> Items => _items;
        public int Index => _index;
        public IUiFocusable Current => _index >= 0 && _index < _items.Count ? _items[_index] : null;

        public void Add(IUiFocusable item)
        {
            if (item == null || _items.Contains(item)) return;
            _items.Add(item);
            if (item is UiWidget w) w.Group = this;
            if (_index < 0) SetCursor(0, false);
        }

        public void Remove(IUiFocusable item)
        {
            int i = _items.IndexOf(item);
            if (i < 0) return;
            item.Cursor = false;
            _items.RemoveAt(i);
            if (item is UiWidget w && w.Group == this) w.Group = null;
            if (_items.Count == 0) { _index = -1; return; }
            if (_index >= _items.Count) _index = _items.Count - 1;
            else if (i < _index) _index--;
            if (_index >= 0) _items[_index].Cursor = true;
        }

        public void Clear()
        {
            for (int i = 0; i < _items.Count; i++)
            {
                _items[i].Cursor = false;
                if (_items[i] is UiWidget w && w.Group == this) w.Group = null;
            }
            _items.Clear();
            _index = -1;
        }

        public int IndexOf(IUiFocusable item) => _items.IndexOf(item);

        /// <summary>커서를 i번째로(소리는 playSound일 때만).</summary>
        public void SetCursor(int i, bool playSound = true)
        {
            if (i < 0 || i >= _items.Count) return;
            int prev = _index;
            if (prev == i) { _items[i].Cursor = true; return; }
            if (prev >= 0 && prev < _items.Count) _items[prev].Cursor = false;
            _index = i;
            _items[i].Cursor = true;
            if (playSound) _sound?.Play(UiSounds.CursorMove, SoundBus.Ui);
            CursorChanged?.Invoke(prev);
            UpdateTooltip();
        }

        public void SetCursor(IUiFocusable item, bool playSound = true) => SetCursor(_items.IndexOf(item), playSound);

        /// <summary>위젯이 포인터가 올라왔다고 알린다. Mouse 모드면 커서를 그리로(소리 없음).</summary>
        public void OnPointerHover(IUiFocusable item)
        {
            if (_mode.Mode != UiInputMode.Mouse) return;
            int i = _items.IndexOf(item);
            if (i >= 0 && i != _index) SetCursor(i, false);
        }

        /// <summary>메뉴 키 처리. 커서 칸이 먼저 받고, 방향은 커서 이동, Confirm은 커서 칸 누름. 소비하면 true.</summary>
        public bool HandleKey(MenuKey key)
        {
            var cur = Current;
            if (cur != null && cur.HandleKey(key)) return true;
            switch (key)
            {
                case MenuKey.Up:
                case MenuKey.Down:
                case MenuKey.Left:
                case MenuKey.Right:
                    return Move(key);
                case MenuKey.Confirm:
                    if (cur == null) return false;
                    cur.Activate();
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>방향으로 커서를 옮긴다(보이지 않는 칸은 건너뛴다). 옮겼거나 칸이 있으면 true.</summary>
        public bool Move(MenuKey dir)
        {
            if (_items.Count == 0) return false;
            int next;
            if (Columns > 0)
            {
                next = _index;
                int guard = _items.Count;
                do { next = UiFocusNav.NextInGrid(_items.Count, Columns, next, dir, Wrap); guard--; }
                while (guard > 0 && next >= 0 && next != _index && !_items[next].IsNavigable);
                if (next >= 0 && !_items[next].IsNavigable) next = _index;
            }
            else
            {
                _centers.Clear();
                _map.Clear();
                int curMapped = -1;
                var corners = new Vector3[4];
                for (int i = 0; i < _items.Count; i++)
                {
                    if (!_items[i].IsNavigable && i != _index) continue;
                    var rt = _items[i].Rect;
                    if (rt == null) continue;
                    rt.GetWorldCorners(corners);
                    _centers.Add((Vector2)((corners[0] + corners[2]) * 0.5f));
                    if (i == _index) curMapped = _map.Count;
                    _map.Add(i);
                }
                int m = UiFocusNav.Next(_centers, curMapped, dir, Wrap);
                next = m >= 0 && m < _map.Count ? _map[m] : _index;
            }
            if (next >= 0 && next != _index) SetCursor(next, true);
            return true;
        }

        /// <summary>입력 장치 모드가 바뀌었을 때 관리자·화면이 부른다(툴팁 갱신).</summary>
        public void UpdateTooltip()
        {
            if (Tooltips == null) return;
            var cur = Current;
            if (_mode.Mode != UiInputMode.Keyboard || cur == null || cur.Rect == null) return;
            var trig = cur.Rect.GetComponent<UiTooltipTrigger>();
            if (trig != null && trig.isActiveAndEnabled) trig.ShowNow();
            else Tooltips.HideTooltip();
        }
    }
}

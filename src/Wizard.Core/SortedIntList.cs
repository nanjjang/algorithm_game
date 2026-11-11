using System;

namespace Wizard.Core
{
    // CONTRACTS 1.5. 오름차순 유지, 중복 없음. 순회 순서가 언제나 값 순서라 결정적이다.
    public sealed class SortedIntList
    {
        private int[] _items;
        private int _count;

        public SortedIntList() : this(8)
        {
        }

        public SortedIntList(int capacity)
        {
            _items = new int[capacity < 1 ? 1 : capacity];
            _count = 0;
        }

        public int Count => _count;

        public int this[int index]
        {
            get
            {
                if ((uint)index >= (uint)_count)
                {
                    throw new SimInvariantException("SortedIntList: index out of range");
                }
                return _items[index];
            }
        }

        public bool Add(int value)
        {
            int i = Search(value);
            if (i >= 0)
            {
                return false;
            }
            i = ~i;
            if (_count == _items.Length)
            {
                int[] next = new int[_items.Length * 2];
                new ReadOnlySpan<int>(_items, 0, _count).CopyTo(next);
                _items = next;
            }
            new Span<int>(_items, i, _count - i).CopyTo(new Span<int>(_items, i + 1, _count - i));
            _items[i] = value;
            _count++;
            return true;
        }

        public bool Contains(int value) => Search(value) >= 0;

        public bool Remove(int value)
        {
            int i = Search(value);
            if (i < 0)
            {
                return false;
            }
            new Span<int>(_items, i + 1, _count - i - 1).CopyTo(new Span<int>(_items, i, _count - i - 1));
            _count--;
            return true;
        }

        public void Clear() => _count = 0;

        // 스냅숏용 값 복사
        public void CopyFrom(SortedIntList other)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }
            if (_items.Length < other._count)
            {
                _items = new int[other._items.Length];
            }
            new ReadOnlySpan<int>(other._items, 0, other._count).CopyTo(_items);
            _count = other._count;
        }

        // 찾으면 색인, 없으면 넣을 자리의 비트 반전
        private int Search(int value)
        {
            int lo = 0, hi = _count - 1;
            while (lo <= hi)
            {
                int mid = lo + ((hi - lo) >> 1);
                int v = _items[mid];
                if (v == value)
                {
                    return mid;
                }
                if (v < value)
                {
                    lo = mid + 1;
                }
                else
                {
                    hi = mid - 1;
                }
            }
            return ~lo;
        }
    }
}

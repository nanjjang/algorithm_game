using System;

namespace Wizard.Core
{
    // CONTRACTS 0.1·3.2("배열, SortedIntList, 고정 크기 풀"), spec 01 3.1 D7. 고정 용량의 id → 값 표.
    // 칸은 늘 id 오름차순으로 붙어 있다. 그래서 색인 0..Count-1 의 순회가 곧 id 오름차순이다(개체 처리 순서, CLAUDE.md 3절).
    // 개체 id 는 생성 순서대로 1씩 늘어나므로(EntityId) 보통의 Add 는 끝에 붙고 옮김이 없다.
    // 생성자 말고는 할당하지 않는다(Step 의 할당 0). 스냅숏은 CopyFrom(깊은 복사)으로 한다. 대입으로 복사하지 않는다.
    // [임시] 이 타입의 시그니처는 CONTRACTS 1.5에 아직 없다. 결과 보고에 계약 추가로 올린다.
    public sealed class FixedPool<T> where T : struct
    {
        private readonly int[] _ids;
        private readonly T[] _items;
        private int _count;

        public FixedPool(int capacity)
        {
            if (capacity < 1)
            {
                throw new SimInvariantException("FixedPool: capacity < 1");
            }
            _ids = new int[capacity];
            _items = new T[capacity];
        }

        public int Count => _count;

        public int Capacity => _ids.Length;

        public bool IsFull => _count == _ids.Length;

        // 색인 순(= id 오름차순)의 값. 제자리에서 고칠 수 있게 ref 로 준다
        public ref T this[int index]
        {
            get
            {
                CheckIndex(index);
                return ref _items[index];
            }
        }

        public int IdAt(int index)
        {
            CheckIndex(index);
            return _ids[index];
        }

        // 가득 차 있으면 false(상한은 규칙상의 "안 됨"이라 예외가 아니다, CONTRACTS 0.2).
        // id ≤ 0 과 이미 있는 id 는 상태가 깨진 것이라 SimInvariantException.
        public bool TryAdd(int id, in T value)
        {
            if (id <= 0)
            {
                throw new SimInvariantException("FixedPool: id must be > 0");
            }
            int i = Search(id);
            if (i >= 0)
            {
                throw new SimInvariantException("FixedPool: duplicate id");
            }
            if (_count == _ids.Length)
            {
                return false;
            }
            i = ~i;
            if (i < _count)
            {
                new Span<int>(_ids, i, _count - i).CopyTo(new Span<int>(_ids, i + 1, _count - i));
                new Span<T>(_items, i, _count - i).CopyTo(new Span<T>(_items, i + 1, _count - i));
            }
            _ids[i] = id;
            _items[i] = value;
            _count++;
            return true;
        }

        // 찾으면 색인, 없으면 −1
        public int IndexOf(int id)
        {
            int i = Search(id);
            return i >= 0 ? i : -1;
        }

        public bool Contains(int id) => Search(id) >= 0;

        public bool Remove(int id)
        {
            int i = Search(id);
            if (i < 0)
            {
                return false;
            }
            RemoveAt(i);
            return true;
        }

        // 뒤의 칸이 한 칸씩 당겨진다. 앞에서부터 돌며 지울 때는 같은 색인을 다시 본다
        public void RemoveAt(int index)
        {
            CheckIndex(index);
            int tail = _count - index - 1;
            if (tail > 0)
            {
                new Span<int>(_ids, index + 1, tail).CopyTo(new Span<int>(_ids, index, tail));
                new Span<T>(_items, index + 1, tail).CopyTo(new Span<T>(_items, index, tail));
            }
            _count--;
            _ids[_count] = 0;
            _items[_count] = default;
        }

        public void Clear()
        {
            new Span<int>(_ids, 0, _count).Clear();
            new Span<T>(_items, 0, _count).Clear();
            _count = 0;
        }

        // 스냅숏용 깊은 복사. 용량이 다르면 상태가 깨진 것이다(세계마다 용량은 SimConfig 로 고정된다)
        public void CopyFrom(FixedPool<T> other)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }
            if (other._ids.Length != _ids.Length)
            {
                throw new SimInvariantException("FixedPool.CopyFrom: capacity differs");
            }
            if (ReferenceEquals(other, this))
            {
                return;
            }
            other._ids.AsSpan().CopyTo(_ids);
            other._items.AsSpan().CopyTo(_items);
            _count = other._count;
        }

        private void CheckIndex(int index)
        {
            if ((uint)index >= (uint)_count)
            {
                throw new SimInvariantException("FixedPool: index out of range");
            }
        }

        // 찾으면 색인, 없으면 넣을 자리의 비트 반전. 끝에 붙는 경우(새 id 가 가장 큼)를 먼저 본다
        private int Search(int id)
        {
            if (_count == 0 || _ids[_count - 1] < id)
            {
                return ~_count;
            }
            int lo = 0, hi = _count - 1;
            while (lo <= hi)
            {
                int mid = lo + ((hi - lo) >> 1);
                int v = _ids[mid];
                if (v == id)
                {
                    return mid;
                }
                if (v < id)
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

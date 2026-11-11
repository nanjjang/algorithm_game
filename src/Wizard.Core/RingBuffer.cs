using System;

namespace Wizard.Core
{
    // CONTRACTS 1.5. 고정 용량. C# 9에는 인라인 고정 배열이 없어 내부가 배열 참조라 클래스다.
    // 스냅숏은 CopyFrom(깊은 복사)으로 한다. 대입으로 복사하지 않는다.
    public sealed class RingBuffer<T> where T : struct
    {
        private readonly T[] _items;
        private int _start;
        private int _count;

        public RingBuffer(int capacity)
        {
            if (capacity < 1)
            {
                throw new SimInvariantException("RingBuffer: capacity < 1");
            }
            _items = new T[capacity];
        }

        public int Count => _count;

        public int Capacity => _items.Length;

        // 0 = 가장 오래된 것
        public ref readonly T this[int index]
        {
            get
            {
                if ((uint)index >= (uint)_count)
                {
                    throw new SimInvariantException("RingBuffer: index out of range");
                }
                return ref _items[Wrap(_start + index)];
            }
        }

        // 가득 차면 가장 오래된 것을 덮는다
        public void Push(in T value)
        {
            if (_count < _items.Length)
            {
                _items[Wrap(_start + _count)] = value;
                _count++;
                return;
            }
            _items[_start] = value;
            _start = Wrap(_start + 1);
        }

        public void Clear()
        {
            _start = 0;
            _count = 0;
        }

        public void CopyFrom(RingBuffer<T> other)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }
            if (other._items.Length != _items.Length)
            {
                throw new SimInvariantException("RingBuffer.CopyFrom: capacity differs");
            }
            other._items.AsSpan().CopyTo(_items);
            _start = other._start;
            _count = other._count;
        }

        private int Wrap(int i) => i >= _items.Length ? i - _items.Length : i;
    }
}

using System;

namespace Wizard.Sim
{
    // 목록 뷰(ISimView.Enemies 등)를 채우는 버퍼(CONTRACTS 3.3 "뷰 훅", T-54). 세계의 뷰(SimWorldView)가 가진다. 정적 버퍼를 두지 않는다.
    // 뷰 훅이 Add 로 채운다. 자리가 모자라면 두 배로 늘린다: 할당은 뷰를 읽는 쪽(표시 계층)의 일이고 틱의 결과와 무관하다.
    // 세계 상태가 아니다(해시·직렬화·스냅숏에 들어가지 않는다).
    internal sealed class ViewBuffer<T> where T : struct
    {
        private const int FirstCapacity = 64;             // 자료구조 크기

        private T[] _items = Array.Empty<T>();
        private int _count;

        public int Count => _count;

        public int Capacity => _items.Length;

        public void Clear() => _count = 0;

        public void Add(in T item)
        {
            if (_count == _items.Length)
            {
                var grown = new T[_items.Length == 0 ? FirstCapacity : _items.Length * 2];
                for (int i = 0; i < _count; i++)
                {
                    grown[i] = _items[i];
                }
                _items = grown;
            }
            _items[_count++] = item;
        }

        public ReadOnlySpan<T> AsSpan() => new ReadOnlySpan<T>(_items, 0, _count);
    }
}

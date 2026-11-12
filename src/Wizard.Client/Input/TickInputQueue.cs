using System;
using System.Collections.Generic;
using Wizard.Sim.Input;

namespace Wizard.Client
{
    // CONTRACTS 6.5, 3.1. 세계에 넣을 때 쓴다(무한 웨이브·연습의 RunSession). 양자화된 키를 유효 틱별로 꺼낸다.
    // 순번은 매기지 않는다. 꺼낸 열에 InputRecorder가 순번을 매긴다(명령 다음, (Offset, KeyClass) 순).
    //
    // - TakeForTick(t)는 유효 틱이 t인 키를 (Offset, KeyClass) 순으로 꺼낸다. 동률은 넣은 순서(안정 정렬).
    //   그래서 같은 물리 이벤트 열을 프레임 몇 장으로 나눠 넣어도 꺼낸 열이 같다.
    // - 한 틱의 아홉 번째 키부터는 버리고 DroppedOverflowCount에 센다(spec 01 4.3 [임시]: 다음 틱으로 미루지 않고 진단을 남긴다).
    // - 유효 틱이 t보다 앞선 키(이미 지나간 틱의 키)는 TakeForTick(t)에서 버리고 DroppedLateCount에 센다.
    //   CONTRACTS 6.5의 "유효 틱이 tick인 것"을 지키려는 것이다. 키 상태를 맞추는 일은 세션(M1-CLI-02)이 맡는다.
    // 버퍼는 이 객체가 가진다. 늘어날 때만 할당한다.
    public sealed class TickInputQueue
    {
        private QuantizedKey[] _items = new QuantizedKey[InputOrder.MaxEventsPerTickPerPlayer * 2];
        private QuantizedKey[] _scratch = new QuantizedKey[InputOrder.MaxEventsPerTickPerPlayer * 2];
        private int _count;

        public int Count => _count;

        public int DroppedOverflowCount { get; private set; }

        public int DroppedLateCount { get; private set; }

        public void Enqueue(ReadOnlySpan<QuantizedKey> keys)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                int k = (int)keys[i].Key;
                if (k < 0 || k >= KeyState.KeyCount)
                {
                    throw new ArgumentException("unknown LogicalKey", nameof(keys));
                }
            }
            EnsureCapacity(ref _items, _count + keys.Length);
            keys.CopyTo(_items.AsSpan(_count));
            _count += keys.Length;
        }

        // 돌려주는 값 = output에 쓴 개수(0~8). output은 8칸 이상이어야 한다
        public int TakeForTick(uint tick, Span<QuantizedKey> output)
        {
            if (output.Length < InputOrder.MaxEventsPerTickPerPlayer)
            {
                throw new ArgumentException("output must hold MaxEventsPerTickPerPlayer keys", nameof(output));
            }
            EnsureCapacity(ref _scratch, _count);
            int taken = 0, kept = 0;
            for (int i = 0; i < _count; i++)
            {
                QuantizedKey q = _items[i];
                if (q.Tick == tick)
                {
                    _scratch[taken++] = q;
                }
                else if (q.Tick < tick)
                {
                    DroppedLateCount++;
                }
                else
                {
                    _items[kept++] = q;
                }
            }
            _count = kept;

            Span<QuantizedKey> found = _scratch.AsSpan(0, taken);
            Wizard.Core.DetSort.InsertionStable(found, default(OffsetClassOrder));
            int n = taken;
            if (n > InputOrder.MaxEventsPerTickPerPlayer)
            {
                DroppedOverflowCount += n - InputOrder.MaxEventsPerTickPerPlayer;
                n = InputOrder.MaxEventsPerTickPerPlayer;
            }
            found.Slice(0, n).CopyTo(output);
            return n;
        }

        private static void EnsureCapacity(ref QuantizedKey[] buffer, int needed)
        {
            if (buffer.Length >= needed)
            {
                return;
            }
            int size = buffer.Length;
            while (size < needed)
            {
                size *= 2;
            }
            QuantizedKey[] grown = new QuantizedKey[size];
            buffer.AsSpan().CopyTo(grown);
            buffer = grown;
        }

        // (Offset, KeyClass). 같은 틱 안에서만 쓴다
        private readonly struct OffsetClassOrder : IComparer<QuantizedKey>
        {
            public int Compare(QuantizedKey a, QuantizedKey b)
            {
                if (a.Offset != b.Offset)
                {
                    return a.Offset < b.Offset ? -1 : 1;
                }
                int ca = InputOrder.KeyClass(a.Key), cb = InputOrder.KeyClass(b.Key);
                return ca == cb ? 0 : (ca < cb ? -1 : 1);
            }
        }
    }
}

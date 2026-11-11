using System;
using System.Collections.Generic;
using Wizard.Core;

namespace Wizard.Sim.Input
{
    // CONTRACTS 3.1, 0.3, T-12. 입력 정렬 키는 (틱, 오프셋, 키 분류, 슬롯, 순번) 하나다. 동률이 남지 않는다.
    // 키 분류: 자세·반전 0, 방향 1, J 2, K 3, 일시정지 4. 그래서 같은 오프셋이면 방향이 K보다 먼저다.
    public static class InputOrder
    {
        public const int MaxEventsPerTickPerPlayer = 8;

        public static int KeyClass(LogicalKey key)
        {
            switch (key)
            {
                case LogicalKey.Stance:
                case LogicalKey.Reverse:
                    return 0;
                case LogicalKey.Up:
                case LogicalKey.Left:
                case LogicalKey.Down:
                case LogicalKey.Right:
                    return 1;
                case LogicalKey.Fire:
                    return 2;
                case LogicalKey.Skill:
                    return 3;
                case LogicalKey.Pause:
                    return 4;
                default:
                    throw new SimInvariantException("unknown LogicalKey");
            }
        }

        public static int Compare(in PlayerInput a, in PlayerInput b)
        {
            InputEvent x = a.Event, y = b.Event;
            if (x.Tick != y.Tick)
            {
                return x.Tick < y.Tick ? -1 : 1;
            }
            if (x.Offset != y.Offset)
            {
                return x.Offset < y.Offset ? -1 : 1;
            }
            int cx = KeyClass(x.Key), cy = KeyClass(y.Key);
            if (cx != cy)
            {
                return cx < cy ? -1 : 1;
            }
            if (a.Slot.Value != b.Slot.Value)
            {
                return a.Slot.Value < b.Slot.Value ? -1 : 1;
            }
            if (x.Seq != y.Seq)
            {
                return x.Seq < y.Seq ? -1 : 1;
            }
            return 0;
        }

        // DetSort(안정). 한 틱의 입력은 플레이어당 8개 이하라 삽입 정렬로 충분하다.
        public static void Sort(Span<PlayerInput> inputs) => DetSort.InsertionStable(inputs, default(Comparer));

        public readonly struct Comparer : IComparer<PlayerInput>
        {
            public int Compare(PlayerInput a, PlayerInput b) => InputOrder.Compare(a, b);
        }
    }
}

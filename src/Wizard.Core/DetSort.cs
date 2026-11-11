using System;
using System.Collections.Generic;

namespace Wizard.Core
{
    // CONTRACTS 1.5, T-11. 안정 정렬. Array.Sort·List<T>.Sort(불안정)는 코어에서 쓰지 않는다.
    // 비교자는 구조체 제네릭 매개변수로 받는다(인터페이스로 받으면 박싱되어 Step 의 할당 0과 부딪친다).
    public static class DetSort
    {
        private const int RunLength = 16;

        // 병합 정렬. 안정. scratch 는 호출자가 가진 임시 버퍼(길이 ≥ count)
        public static void Stable<T, TComparer>(T[] items, int start, int count, TComparer comparer, T[] scratch)
            where TComparer : struct, IComparer<T>
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }
            if (start < 0 || count < 0 || start > items.Length - count)
            {
                throw new SimInvariantException("DetSort.Stable: range out of bounds");
            }
            if (count < 2)
            {
                return;
            }
            Span<T> a = new Span<T>(items, start, count);
            if (count <= RunLength)
            {
                InsertionStable(a, comparer);
                return;
            }
            if (scratch == null || scratch.Length < count)
            {
                throw new SimInvariantException("DetSort.Stable: scratch too small");
            }
            Span<T> b = new Span<T>(scratch, 0, count);
            for (int i = 0; i < count; i += RunLength)
            {
                int len = count - i < RunLength ? count - i : RunLength;
                InsertionStable(a.Slice(i, len), comparer);
            }
            bool inA = true;
            for (int width = RunLength; width < count; width *= 2)
            {
                Span<T> src = inA ? a : b;
                Span<T> dst = inA ? b : a;
                for (int lo = 0; lo < count; lo += 2 * width)
                {
                    int mid = lo + width < count ? lo + width : count;
                    int hi = lo + 2 * width < count ? lo + 2 * width : count;
                    int i = lo, j = mid, k = lo;
                    while (i < mid && j < hi)
                    {
                        // 같으면 왼쪽(앞의 것)을 먼저 둔다: 안정성
                        if (comparer.Compare(src[i], src[j]) <= 0)
                        {
                            dst[k++] = src[i++];
                        }
                        else
                        {
                            dst[k++] = src[j++];
                        }
                    }
                    while (i < mid)
                    {
                        dst[k++] = src[i++];
                    }
                    while (j < hi)
                    {
                        dst[k++] = src[j++];
                    }
                }
                inA = !inA;
            }
            if (!inA)
            {
                b.CopyTo(a);
            }
        }

        public static void InsertionStable<T, TComparer>(Span<T> items, TComparer comparer)
            where TComparer : struct, IComparer<T>
        {
            for (int i = 1; i < items.Length; i++)
            {
                T x = items[i];
                int j = i - 1;
                while (j >= 0 && comparer.Compare(items[j], x) > 0)
                {
                    items[j + 1] = items[j];
                    j--;
                }
                items[j + 1] = x;
            }
        }
    }
}

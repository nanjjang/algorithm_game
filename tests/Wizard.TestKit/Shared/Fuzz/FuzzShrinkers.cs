using System.Collections.Generic;

namespace Wizard.TestKit.Shared.Fuzz
{
    // 기본 줄이기. 후보 순서: 크게 지우기 → 작게 지우기 → 값 줄이기(0 쪽으로, 차이를 반씩 줄이며).
    public static class FuzzShrinkers
    {
        public static readonly IFuzzShrinker<byte[]> Bytes = new ByteArrayShrinker();
        public static readonly IFuzzShrinker<long> Int64 = new Int64Shrinker();
        public static readonly IFuzzShrinker<int> Int32 = new Int32Shrinker();

        public static IFuzzShrinker<List<T>> ListOf<T>(IFuzzShrinker<T>? element) => new ListShrinker<T>(element);

        // v 에서 0 쪽으로: 0, v - v/2, v - v/4, …, v - 1 (부호 그대로)
        internal static IEnumerable<long> TowardZero(long v)
        {
            long d = v;
            while (d != 0)
            {
                yield return v - d;
                d /= 2;
            }
        }

        // 길이 n 에서 지울 구간들: 전체, 반, 4분의 1 … 한 칸
        internal static IEnumerable<KeyValuePair<int, int>> Removals(int n)
        {
            for (int size = n; size >= 1; size /= 2)
            {
                for (int start = 0; start + size <= n; start += size)
                {
                    yield return new KeyValuePair<int, int>(start, size);
                }
            }
        }

        private sealed class ByteArrayShrinker : IFuzzShrinker<byte[]>
        {
            public IEnumerable<byte[]> Shrink(byte[] value)
            {
                foreach (KeyValuePair<int, int> r in Removals(value.Length))
                {
                    var b = new byte[value.Length - r.Value];
                    System.Array.Copy(value, 0, b, 0, r.Key);
                    System.Array.Copy(value, r.Key + r.Value, b, r.Key, value.Length - r.Key - r.Value);
                    yield return b;
                }
                for (int i = 0; i < value.Length; i++)
                {
                    foreach (long c in TowardZero(value[i]))
                    {
                        var b = (byte[])value.Clone();
                        b[i] = (byte)c;
                        yield return b;
                    }
                }
            }
        }

        private sealed class Int64Shrinker : IFuzzShrinker<long>
        {
            public IEnumerable<long> Shrink(long value) => TowardZero(value);
        }

        private sealed class Int32Shrinker : IFuzzShrinker<int>
        {
            public IEnumerable<int> Shrink(int value)
            {
                foreach (long c in TowardZero(value))
                {
                    yield return (int)c;
                }
            }
        }

        private sealed class ListShrinker<T> : IFuzzShrinker<List<T>>
        {
            private readonly IFuzzShrinker<T>? _element;

            public ListShrinker(IFuzzShrinker<T>? element)
            {
                _element = element;
            }

            public IEnumerable<List<T>> Shrink(List<T> value)
            {
                foreach (KeyValuePair<int, int> r in Removals(value.Count))
                {
                    var l = new List<T>(value);
                    l.RemoveRange(r.Key, r.Value);
                    yield return l;
                }
                if (_element == null)
                {
                    yield break;
                }
                for (int i = 0; i < value.Count; i++)
                {
                    foreach (T c in _element.Shrink(value[i]))
                    {
                        var l = new List<T>(value);
                        l[i] = c;
                        yield return l;
                    }
                }
            }
        }
    }
}

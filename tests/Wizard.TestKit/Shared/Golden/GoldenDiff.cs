using System.Collections.Generic;
using System.Globalization;

namespace Wizard.TestKit.Shared.Golden
{
    // 두 해시 목록의 처음 갈라진 곳. 같으면 Compare 가 null 을 돌려준다.
    // 틱 목록이 다르면(기대에 없는 틱을 냈다, 기대의 틱을 내지 않았다) 그것도 갈라진 것이다.
    public sealed class GoldenDiff
    {
        private GoldenDiff(int index, GoldenHash? expected, GoldenHash? actual)
        {
            Index = index;
            Expected = expected;
            Actual = actual;
        }

        // 처음 다른 칸의 번호(0부터)
        public int Index { get; }

        // 그 칸의 기대값. 실제가 더 길면 null
        public GoldenHash? Expected { get; }

        // 그 칸의 실제값. 실제가 더 짧으면 null
        public GoldenHash? Actual { get; }

        public static GoldenDiff? Compare(IReadOnlyList<GoldenHash> expected, IReadOnlyList<GoldenHash> actual)
        {
            int n = expected.Count < actual.Count ? expected.Count : actual.Count;
            for (int i = 0; i < n; i++)
            {
                if (!expected[i].Equals(actual[i]))
                {
                    return new GoldenDiff(i, expected[i], actual[i]);
                }
            }
            if (expected.Count == actual.Count)
            {
                return null;
            }
            return expected.Count > n
                ? new GoldenDiff(n, expected[n], null)
                : new GoldenDiff(n, null, actual[n]);
        }

        public override string ToString()
        {
            string at = "hash #" + Index.ToString(CultureInfo.InvariantCulture);
            if (Expected.HasValue && Actual.HasValue)
            {
                GoldenHash e = Expected.Value;
                GoldenHash a = Actual.Value;
                if (e.Tick != a.Tick)
                {
                    return at + ": expected " + e + " but got " + a + " (tick lists differ)";
                }
                return at + ": first divergence at tick " + e.Tick.ToString(CultureInfo.InvariantCulture) +
                       ": expected " + HashText.Format(e.Hash) + ", got " + HashText.Format(a.Hash);
            }
            if (Expected.HasValue)
            {
                return at + ": expected " + Expected.Value + " but the run produced no more hashes";
            }
            return at + ": unexpected extra hash " + Actual!.Value;
        }
    }
}

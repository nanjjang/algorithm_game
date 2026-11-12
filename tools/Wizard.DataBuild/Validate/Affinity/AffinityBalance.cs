using System;
using System.Collections.Generic;

namespace Wizard.DataBuild.Validate.Affinity
{
    // "속성별 개수 차이 1 이하"(DECISIONS 2.5, spec 05 1.3 A7)의 셈.
    // 다 찬 표(줄 수 = 로스터)에서는 최대 − 최소 ≤ 1 이다. 아직 덜 찬 표(적 12종 가운데 일부만 데이터에 있을 때)에서는
    // "남은 종을 어떻게든 더해 차이 ≤ 1 을 만들 수 있는가"를 본다: 다 찼을 때 속성마다 개수는 ⌊N/E⌋ 또는 ⌈N/E⌉ 이어야 하므로
    //   모든 속성의 개수 ≤ ⌈N/E⌉  그리고  Σ max(0, ⌊N/E⌋ − 개수) ≤ N − 지금 줄 수
    // 다 찬 표에서는 이 조건이 최대 − 최소 ≤ 1 과 같다.
    public static class AffinityBalance
    {
        public static bool CanBalance(IReadOnlyList<int> counts, int roster)
        {
            int e = counts.Count;
            if (e == 0)
            {
                return true;
            }
            int k = 0;
            foreach (int c in counts)
            {
                k += c;
            }
            int n = Math.Max(roster, k);
            int lo = n / e;
            int hi = n % e == 0 ? lo : lo + 1;
            int deficit = 0;
            foreach (int c in counts)
            {
                if (c > hi)
                {
                    return false;
                }
                if (c < lo)
                {
                    deficit += lo - c;
                }
            }
            return deficit <= n - k;
        }

        public static int Spread(IReadOnlyList<int> counts)
        {
            if (counts.Count == 0)
            {
                return 0;
            }
            int max = int.MinValue;
            int min = int.MaxValue;
            foreach (int c in counts)
            {
                max = Math.Max(max, c);
                min = Math.Min(min, c);
            }
            return max - min;
        }
    }
}

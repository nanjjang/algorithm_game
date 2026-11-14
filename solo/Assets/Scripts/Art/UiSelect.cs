using System.Collections.Generic;

namespace Wizard.Solo.Art
{
    /// <summary>
    /// 같은 UI id 안의 항목 고르기(09 요약서 6.1절). 변형이 맞는 것 중 상태를 UiStates.Fallback 순서로 찾는다.
    /// sizePx &gt; 0이면 같은 상태 안에서 높이가 sizePx와 같은 것 → sizePx 이상 중 가장 작은 것 → 가장 큰 것 순(K-40 16/24/32 같은 크기 갈래).
    /// </summary>
    public static class UiSelect
    {
        public static UiEntry Pick(IList<UiEntry> entries, string variant, string state, int sizePx = 0)
        {
            if (entries == null || entries.Count == 0) return null;
            var cands = new List<UiEntry>();
            for (int i = 0; i < entries.Count; i++)
                if (variant == null || entries[i].Variant == variant) cands.Add(entries[i]);
            if (cands.Count == 0) return null;
            string s = string.IsNullOrEmpty(state) ? UiStates.Normal : state;
            while (s != null)
            {
                var hit = BySize(cands, s, sizePx);
                if (hit != null) return hit;
                s = UiStates.Fallback(s);
            }
            return BySize(cands, null, sizePx);
        }

        /// <summary>state null = 상태 무관.</summary>
        public static UiEntry BySize(IList<UiEntry> cands, string state, int sizePx)
        {
            UiEntry first = null, exact = null, bestAbove = null, largest = null;
            for (int i = 0; i < cands.Count; i++)
            {
                var e = cands[i];
                if (state != null && e.State != state) continue;
                if (first == null) first = e;
                if (sizePx <= 0) return e;
                int h = e.Height;
                if (h == sizePx && exact == null) exact = e;
                if (h >= sizePx && (bestAbove == null || h < bestAbove.Height)) bestAbove = e;
                if (largest == null || h > largest.Height) largest = e;
            }
            return exact ?? bestAbove ?? largest ?? first;
        }
    }
}

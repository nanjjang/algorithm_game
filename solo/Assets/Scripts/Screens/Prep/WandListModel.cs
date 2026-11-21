using System;
using System.Collections.Generic;
using System.Globalization;
using Wizard.Solo.Meta;

namespace Wizard.Solo.Screens.Prep
{
    /// <summary>
    /// 지팡이 목록의 순서 규칙(08 요약서 5.3절 SortRows). 엔진 없음.
    /// 든 지팡이는 어느 정렬에서든 맨 위 고정, 나머지는 정렬 키로. 동률 깨기 [임시 U-50]: 단계 desc → 얻은 시각 desc → id asc
    /// (IL은 솔로에 없다). 최근 얻은 순은 얻은 시각 desc → id desc.
    /// IWandService.List가 이미 같은 순서를 주지만, 화면은 받은 목록에 이 규칙을 한 번 더 건다(가짜·다른 구현이어도 순서가 같게).
    /// </summary>
    public static class WandListModel
    {
        public static List<WandListEntry> SortRows(IReadOnlyList<WandListEntry> rows, WandSort sort)
        {
            var held = new List<WandListEntry>();
            var rest = new List<WandListEntry>();
            if (rows == null) return held;
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                if (r == null || r.Wand == null) continue;
                (r.Equipped ? held : rest).Add(r);
            }
            StableSort(held, (a, b) => a.Wand.Id.CompareTo(b.Wand.Id));
            StableSort(rest, Comparer(sort));
            held.AddRange(rest);
            return held;
        }

        static Comparison<WandListEntry> Comparer(WandSort sort)
        {
            switch (sort)
            {
                case WandSort.StageAsc:
                    return (a, b) =>
                    {
                        int c = a.Wand.Stage.CompareTo(b.Wand.Stage);
                        if (c != 0) return c;
                        c = AcquiredTicks(b.Wand).CompareTo(AcquiredTicks(a.Wand));
                        return c != 0 ? c : a.Wand.Id.CompareTo(b.Wand.Id);
                    };
                case WandSort.Recent:
                    return (a, b) =>
                    {
                        int c = AcquiredTicks(b.Wand).CompareTo(AcquiredTicks(a.Wand));
                        return c != 0 ? c : b.Wand.Id.CompareTo(a.Wand.Id);
                    };
                default:
                    return (a, b) =>
                    {
                        int c = b.Wand.Stage.CompareTo(a.Wand.Stage);
                        if (c != 0) return c;
                        c = AcquiredTicks(b.Wand).CompareTo(AcquiredTicks(a.Wand));
                        return c != 0 ? c : a.Wand.Id.CompareTo(b.Wand.Id);
                    };
            }
        }

        /// <summary>acquired_at(ISO 8601)의 시각. 읽을 수 없으면 0(가장 오래된 것으로 본다).</summary>
        public static long AcquiredTicks(WandData w)
        {
            if (w == null || string.IsNullOrEmpty(w.AcquiredAt)) return 0;
            DateTime t;
            return DateTime.TryParse(w.AcquiredAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out t) ? t.ToUniversalTime().Ticks : 0;
        }

        static void StableSort(List<WandListEntry> list, Comparison<WandListEntry> cmp)
        {
            // 삽입 정렬(목록이 짧고 List.Sort는 안정 정렬이 아니다).
            for (int i = 1; i < list.Count; i++)
            {
                var x = list[i];
                int j = i - 1;
                while (j >= 0 && cmp(list[j], x) > 0) { list[j + 1] = list[j]; j--; }
                list[j + 1] = x;
            }
        }
    }
}

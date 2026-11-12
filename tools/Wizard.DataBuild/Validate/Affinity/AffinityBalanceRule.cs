using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Wizard.Content;
using Wizard.DataBuild.Core;

namespace Wizard.DataBuild.Validate.Affinity
{
    // 속성별 약점 수·저항 수의 차이 ≤ 1(DECISIONS 2.5, spec 05 1.3 A7·5.2, TV-AFF-06~09·15).
    // 일반 적만, 보스만, 둘을 합친 것을 따로 센다. 약점과 저항도 따로 센다. 덜 찬 표는 AffinityBalance.CanBalance 로 본다.
    public sealed class AffinityBalanceRule : IAffinityRule
    {
        public void Check(AffinityTable table, Diagnostics diagnostics)
        {
            CheckGroup(table, AffinityGroup.Enemy, "일반 적", diagnostics);
            CheckGroup(table, AffinityGroup.Boss, "보스", diagnostics);
            CheckGroup(table, null, "일반 적 + 보스", diagnostics);
        }

        private static void CheckGroup(AffinityTable table, AffinityGroup? group, string groupName, Diagnostics diagnostics)
        {
            var rows = new List<AffinityRow>();
            foreach (AffinityRow r in table.Rows)
            {
                if (group == null || r.Group == group.Value)
                {
                    rows.Add(r);
                }
            }
            if (rows.Count == 0)
            {
                return;
            }
            int roster = group == null ? table.EnemyRoster + table.BossRoster : table.Roster(group.Value);
            CheckSide(rows, roster, groupName, "약점", true, diagnostics);
            CheckSide(rows, roster, groupName, "저항", false, diagnostics);
        }

        private static void CheckSide(List<AffinityRow> rows, int roster, string groupName, string side, bool weak, Diagnostics diagnostics)
        {
            IReadOnlyList<Element> elements = AffinityTable.Elements();
            var counts = new int[elements.Count];
            foreach (AffinityRow r in rows)
            {
                Element e = weak ? r.Weak : r.Resist;
                for (int i = 0; i < elements.Count; i++)
                {
                    if (elements[i] == e)
                    {
                        counts[i]++;
                    }
                }
            }
            if (AffinityBalance.CanBalance(counts, roster))
            {
                return;
            }
            var sb = new StringBuilder();
            sb.Append("상성 분배(").Append(groupName).Append(' ').Append(side).Append(", ")
              .Append(rows.Count.ToString(CultureInfo.InvariantCulture)).Append('/').Append(roster.ToString(CultureInfo.InvariantCulture)).Append("종): ");
            for (int i = 0; i < elements.Count; i++)
            {
                sb.Append(i == 0 ? "" : ", ").Append(ContentIds.ToId(elements[i])).Append(' ').Append(counts[i].ToString(CultureInfo.InvariantCulture));
            }
            if (rows.Count >= roster)
            {
                sb.Append(" — 속성별 개수 차이 ").Append(AffinityBalance.Spread(counts).ToString(CultureInfo.InvariantCulture)).Append(" > 1");
            }
            else
            {
                sb.Append(" — 남은 ").Append((roster - rows.Count).ToString(CultureInfo.InvariantCulture))
                  .Append("종을 어떻게 더해도 속성별 개수 차이가 1 이하가 될 수 없다");
            }
            sb.Append("(DECISIONS 2.5, spec 05 1.3 A7)");
            AffinityRow first = rows[0];
            diagnostics.Error(first.File, sb.ToString());
        }
    }
}

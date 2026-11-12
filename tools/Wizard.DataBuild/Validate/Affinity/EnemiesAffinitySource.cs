using System.Globalization;
using System.Text.Json;
using Wizard.Content;
using Wizard.DataBuild.Core;

namespace Wizard.DataBuild.Validate.Affinity
{
    // data/sim/enemies.json 의 일반 적(졸개 번호 앞의 EnemyTypeId)을 상성 줄로 옮긴다. 읽지 못하는 id·속성은 변환기가 알리므로 건너뛴다
    public sealed class EnemiesAffinitySource : IAffinitySource
    {
        public const string EnemiesFile = "sim/enemies.json";

        public void Collect(DataSet data, AffinityTable table, Diagnostics diagnostics)
        {
            foreach (DataFile f in data.Files)
            {
                if (f.DataPath != EnemiesFile || !f.Root.TryGetProperty("enemies", out JsonElement enemies) || enemies.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }
                int i = 0;
                foreach (JsonElement row in enemies.EnumerateArray())
                {
                    AffinityRow? r = Read(row);
                    if (r != null)
                    {
                        r.File = f.RepoPath;
                        r.Pointer = "/enemies/" + i.ToString(CultureInfo.InvariantCulture);
                        table.Rows.Add(r);
                    }
                    i++;
                }
            }
        }

        private static AffinityRow? Read(JsonElement row)
        {
            string id = Str(row, "id");
            if (!ContentIds.TryParse(id, out EnemyTypeId type) || type == EnemyTypeId.None || (ushort)type >= AffinityTable.MinionCodeStart)
            {
                return null;
            }
            if (!ContentIds.TryParse(Str(row, "weak"), out Element weak) || weak == Element.None
                || !ContentIds.TryParse(Str(row, "resist"), out Element resist) || resist == Element.None)
            {
                return null;
            }
            int firstWave = row.TryGetProperty("first_wave", out JsonElement fw) && fw.ValueKind == JsonValueKind.Number && fw.TryGetInt32(out int w) ? w : 0;
            return new AffinityRow { Id = id, Group = AffinityGroup.Enemy, Weak = weak, Resist = resist, FirstWave = firstWave, Own = Element.None };
        }

        private static string Str(JsonElement o, string name) =>
            o.TryGetProperty(name, out JsonElement e) && e.ValueKind == JsonValueKind.String ? e.GetString() ?? "" : "";
    }
}

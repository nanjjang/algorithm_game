using System.Globalization;
using System.Text.Json;
using Wizard.Content;
using Wizard.DataBuild.Core;

namespace Wizard.DataBuild.Validate.Affinity.BossRules
{
    // data/sim/bosses.json 의 보스 여섯(spec 06 1.3, spec 14 3.8)을 상성 줄로 옮긴다(묶음 Boss, 자기 속성 Own).
    // 첫 만남의 허상은 같은 보스(boss.mirror_phantom + 변형)라 줄이 따로 없다(spec 14 3.8, spec 06 T092).
    // 읽지 못하는 id·속성은 변환기(BossesConverter)가 알리므로 건너뛴다
    public sealed class BossesAffinitySource : IAffinitySource
    {
        public const string BossesFile = "sim/bosses.json";

        public void Collect(DataSet data, AffinityTable table, Diagnostics diagnostics)
        {
            foreach (DataFile f in data.Files)
            {
                if (f.DataPath != BossesFile || !f.Root.TryGetProperty("bosses", out JsonElement bosses) || bosses.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }
                int i = 0;
                foreach (JsonElement row in bosses.EnumerateArray())
                {
                    AffinityRow? r = Read(row);
                    if (r != null)
                    {
                        r.File = f.RepoPath;
                        r.Pointer = "/bosses/" + i.ToString(CultureInfo.InvariantCulture);
                        table.Rows.Add(r);
                    }
                    i++;
                }
            }
        }

        private static AffinityRow? Read(JsonElement row)
        {
            string id = Str(row, "id");
            if (!ContentIds.TryParse(id, out BossId boss) || boss == BossId.None)
            {
                return null;
            }
            if (!ContentIds.TryParse(Str(row, "weak"), out Element weak) || weak == Element.None
                || !ContentIds.TryParse(Str(row, "resist"), out Element resist) || resist == Element.None
                || !ContentIds.TryParse(Str(row, "element"), out Element own))
            {
                return null;
            }
            return new AffinityRow { Id = id, Group = AffinityGroup.Boss, Weak = weak, Resist = resist, FirstWave = 0, Own = own };
        }

        private static string Str(JsonElement o, string name) =>
            o.TryGetProperty(name, out JsonElement e) && e.ValueKind == JsonValueKind.String ? e.GetString() ?? "" : "";
    }
}

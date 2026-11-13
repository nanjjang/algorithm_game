using Wizard.Content;
using Wizard.DataBuild.Core;

namespace Wizard.DataBuild.Validate.Affinity.BossRules
{
    // 처음 만나는 적(웨이브 1~4 에 처음 나오는 종)은 비전에 저항하지 않는다: 시작 블록이 마력탄(비전)이라
    // 첫 판이 저항으로 막히지 않게 한다(spec 02 AF-5, spec 14 TV-036, spec 02 TV-A14, M 1.3)
    public sealed class EarlyEnemyResistRule : IAffinityRule
    {
        // "웨이브 1~4"(spec 02 AF-5, spec 14 TV-036의 다섯 종: 슬라임, 벌레, 늑대, 술사, 고슴도치)
        public const int LastEarlyWave = 4;

        // 시작 블록 마력탄의 속성(spec 02 AF-5, spec 14 3.3 block.magic_bolt)
        public const Element StarterElement = Element.Arcane;

        public void Check(AffinityTable table, Diagnostics diagnostics)
        {
            foreach (AffinityRow r in table.Rows)
            {
                if (r.Group == AffinityGroup.Enemy && r.FirstWave >= 1 && r.FirstWave <= LastEarlyWave && r.Resist == StarterElement)
                {
                    diagnostics.Error(r.File, r.Pointer, r.Id + ": 웨이브 " + r.FirstWave.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        + " 에 처음 나오는 적이 " + ContentIds.ToId(StarterElement) + " 에 저항한다. 웨이브 1~4 의 적은 시작 블록(마력탄)의 속성에 저항하지 않는다(spec 02 AF-5, spec 14 TV-036)");
                }
            }
        }
    }
}

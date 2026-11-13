using Wizard.Content;
using Wizard.DataBuild.Core;

namespace Wizard.DataBuild.Validate.Affinity.BossRules
{
    // 속성이 있는 보스는 자기 속성에 저항한다(spec 02 AF-3, spec 14 TV-035, spec 02 TV-A13, M 2.5). 속성 없는 보스(천 갓)는 보지 않는다
    public sealed class BossResistOwnElementRule : IAffinityRule
    {
        public void Check(AffinityTable table, Diagnostics diagnostics)
        {
            foreach (AffinityRow r in table.Rows)
            {
                if (r.Group == AffinityGroup.Boss && r.Own != Element.None && r.Resist != r.Own)
                {
                    diagnostics.Error(r.File, r.Pointer, r.Id + ": 속성 보스의 저항은 자기 속성 " + ContentIds.ToId(r.Own) + " 이다. 지금 저항은 "
                        + ContentIds.ToId(r.Resist) + " 이다(spec 02 AF-3, spec 14 TV-035)");
                }
            }
        }
    }
}

using Wizard.Content;
using Wizard.DataBuild.Core;

namespace Wizard.DataBuild.Validate.Affinity
{
    // 어떤 종도 약점과 저항이 같은 속성이 아니다(spec 05 2.2 weak ≠ resist, 5.2, TV-AFF-10, spec 14 TV-034)
    public sealed class AffinityDistinctRule : IAffinityRule
    {
        public void Check(AffinityTable table, Diagnostics diagnostics)
        {
            foreach (AffinityRow r in table.Rows)
            {
                if (r.Weak != Element.None && r.Weak == r.Resist)
                {
                    diagnostics.Error(r.File, r.Pointer, r.Id + ": 약점과 저항이 같은 속성 " + ContentIds.ToId(r.Weak) + " 이다(spec 05 2.2·5.2)");
                }
            }
        }
    }
}

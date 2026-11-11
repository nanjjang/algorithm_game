using System.Collections.Generic;
using Wizard.Content.Rules;

namespace Wizard.Content
{
    // CONTRACTS 2.3. 규칙 쪽 접근자(해시 밖, src/Wizard.Content/Rules/). Wizard.Sim 은 부르지 못한다(분석기 WZ0017)
    public sealed partial class ContentDb
    {
        public EnhanceTable Enhance => throw Missing("enhance");
        public EconomyTable Economy => throw Missing("economy");
        public DropTable Drops => throw Missing("drops");
        public LeagueTable Leagues => throw Missing("leagues");
        public SeasonTable Season => throw Missing("season");
        public ShopCatalog Shop => throw Missing("shop");
        public BagTable Bag => throw Missing("bag");
        public RunLimitsTable RunLimits => throw Missing("run_limits");
        public IReadOnlyList<TitleDef> Titles => throw Missing("titles");
        public IReadOnlyList<CosmeticDef> Cosmetics => throw Missing("cosmetics");
        public PlazaDef Plaza => throw Missing("plaza");
        public NetRuleSet Net => throw Missing("net");
        public ItemDef Item(ItemCode code) => throw Missing("items");
    }
}

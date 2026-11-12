using System.Collections.Generic;
using Wizard.Content.Rules;

namespace Wizard.Content
{
    // CONTRACTS 2.3. 규칙 쪽 접근자(해시 밖, src/Wizard.Content/Rules/). Wizard.Sim 은 부르지 못한다(분석기 WZ0017)
    //
    // 규칙 쪽 조회 훅은 규칙 타입을 쓰므로 해시 안(Pack/Core/)에 둘 수 없어 여기에 선언한다(T-47).
    // 절을 읽는 훅(Load<절>)은 바이트만 받으므로 전투 쪽과 같이 Pack/Core/ContentDb.Core.cs 에 있다.
    // 절 파일은 Rules/Sections/ContentDb.<절>.cs 이고 Load<절> 과 아래의 Find 훅을 구현하기만 한다.
    public sealed partial class ContentDb
    {
        public EnhanceTable Enhance
        {
            get
            {
                EnhanceTable? def = null;
                FindEnhance(ref def);
                return Require(def, ContentSection.Enhance);
            }
        }

        public EconomyTable Economy
        {
            get
            {
                EconomyTable? def = null;
                FindEconomy(ref def);
                return Require(def, ContentSection.Economy);
            }
        }

        public DropTable Drops
        {
            get
            {
                DropTable? def = null;
                FindDrops(ref def);
                return Require(def, ContentSection.Drops);
            }
        }

        public LeagueTable Leagues
        {
            get
            {
                LeagueTable? def = null;
                FindLeagues(ref def);
                return Require(def, ContentSection.Leagues);
            }
        }

        public SeasonTable Season
        {
            get
            {
                SeasonTable? def = null;
                FindSeason(ref def);
                return Require(def, ContentSection.Season);
            }
        }

        public ShopCatalog Shop
        {
            get
            {
                ShopCatalog? def = null;
                FindShop(ref def);
                return Require(def, ContentSection.Shop);
            }
        }

        public BagTable Bag
        {
            get
            {
                BagTable? def = null;
                FindBag(ref def);
                return Require(def, ContentSection.Bag);
            }
        }

        public RunLimitsTable RunLimits
        {
            get
            {
                RunLimitsTable? def = null;
                FindRunLimits(ref def);
                return Require(def, ContentSection.RunLimits);
            }
        }

        public IReadOnlyList<TitleDef> Titles
        {
            get
            {
                IReadOnlyList<TitleDef>? defs = null;
                FindTitles(ref defs);
                return Require(defs, ContentSection.Titles);
            }
        }

        public IReadOnlyList<CosmeticDef> Cosmetics
        {
            get
            {
                IReadOnlyList<CosmeticDef>? defs = null;
                FindCosmetics(ref defs);
                return Require(defs, ContentSection.Cosmetics);
            }
        }

        public PlazaDef Plaza
        {
            get
            {
                PlazaDef? def = null;
                FindPlaza(ref def);
                return Require(def, ContentSection.Plaza);
            }
        }

        public NetRuleSet Net
        {
            get
            {
                NetRuleSet? def = null;
                FindNet(ref def);
                return Require(def, ContentSection.Net);
            }
        }

        public ItemDef Item(ItemCode code)
        {
            ItemDef? def = null;
            FindItem(code, ref def);
            return Require(def, ContentSection.Items);
        }

        // ── 규칙 쪽 접근자의 조회 훅(CONTRACTS 2.3) ──
        partial void FindEnhance(ref EnhanceTable? def);
        partial void FindEconomy(ref EconomyTable? def);
        partial void FindDrops(ref DropTable? def);
        partial void FindLeagues(ref LeagueTable? def);
        partial void FindSeason(ref SeasonTable? def);
        partial void FindShop(ref ShopCatalog? def);
        partial void FindBag(ref BagTable? def);
        partial void FindRunLimits(ref RunLimitsTable? def);
        partial void FindTitles(ref IReadOnlyList<TitleDef>? defs);
        partial void FindCosmetics(ref IReadOnlyList<CosmeticDef>? defs);
        partial void FindPlaza(ref PlazaDef? def);
        partial void FindNet(ref NetRuleSet? def);
        partial void FindItem(ItemCode code, ref ItemDef? def);
    }
}

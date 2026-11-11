using System.Collections.Generic;
using Wizard.Content;
using Wizard.Rules.Enhance;
using Wizard.Sim;

namespace Wizard.Rules.League
{
    // CONTRACTS 4.3. 리그는 소지한 아이템의 레벨로 나눈다(DECISIONS 1.9). 유료 재화 값을 읽지 않는다(WZ0020)
    public interface ILeagueRules
    {
        int WandIl(WandState wand);
        int EquippedIl(WandState wand);
        int HoldingIl(IReadOnlyList<WandState> wands);
        LeagueId LeagueOf(int holdingIl14dMax);
        WandSnapshot ApplyLimits(WandSnapshot wand, LeagueLimits limits);   // 넘치는 단계·영웅·확장·변이를 깎아 적용
        LeagueLimits LimitsFor(LeagueId league);
    }
}

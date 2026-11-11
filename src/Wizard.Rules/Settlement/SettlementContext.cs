using System;
using System.Collections.Generic;
using Wizard.Content;

namespace Wizard.Rules.Settlement
{
    // CONTRACTS 4.3
    public sealed class SettlementContext
    {
        public ProfileKind Profile;
        public bool SeasonTagOn;
        public bool PhantomMetBefore, RevengeDoneBefore;
        public int WeeklyRunIndex;                        // 주간 도전 넷째 판부터 가방 0
        public bool Tidy;                                 // 원정 정리(가방 자동, 순위 미반영)
        public IReadOnlyList<int> BagSelection = Array.Empty<int>();
    }
}

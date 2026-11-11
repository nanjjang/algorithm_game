using System;
using System.Collections.Generic;

namespace Wizard.Rules.Settlement
{
    // CONTRACTS 4.3
    public sealed class SettlementResult
    {
        public IReadOnlyList<Grant> Grants = Array.Empty<Grant>();       // 재화·물건 지급 목록(귀속 여부 포함)
        public IReadOnlyList<BagEntry> Bag = Array.Empty<BagEntry>();
        public int BagCapacity, HeroLimit;
        public ProfileProgress Progress = new ProfileProgress();          // 최고 웨이브·점수, 비기 해금, 첫 만남·복수 표시
    }
}

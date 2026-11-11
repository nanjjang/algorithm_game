using System;
using System.Collections.Generic;

namespace Wizard.Rules.Enhance
{
    // CONTRACTS 4.2. 순수 계산의 결과. 저장은 호출자가 한다
    public sealed class EnhanceResolution
    {
        public EnhanceOutcome Outcome;
        public WandState WandAfter = new WandState();
        public ProbBreakdown Breakdown;
        public int RollBp, SuccessBp, GreatBp, MutationEndBp;
        public EnhanceCost Cost;
        public IReadOnlyList<ItemDelta> Consumed = Array.Empty<ItemDelta>();   // 귀속 → 일반 → 대기 순으로 뺀 내역
        public bool CharmConsumed, SlotExpanded, RecordPeakUpdated, ScarAdded, UsedPendingAssets;
        public MutationOffer? Offer;                                           // MutationOffered 일 때
        public int MilestoneStage;                                             // 0이면 없음
        public NoticeKind Notice;
    }
}

using System;
using System.Collections.Generic;
using Wizard.Rules.Enhance;

namespace Wizard.Protocol.Http
{
    // CONTRACTS 8.2, spec 08 4.11절의 필드. 화면은 이 결과를 받은 뒤에 연출을 재생한다
    public sealed class EnhanceResultDto
    {
        public Guid AttemptId { get; set; }
        public EnhanceOutcome Outcome { get; set; }
        public int StageBefore { get; set; }
        public int StageAfter { get; set; }
        public bool RecordPeakUpdated { get; set; }
        public bool SlotExpanded { get; set; }
        public bool SlotAlreadyFull { get; set; }
        public bool CharmConsumed { get; set; }
        public int AuraBefore { get; set; }
        public int AuraAfter { get; set; }
        public string? MutationCandidate { get; set; }
        public string? MutationPushedOut { get; set; }
        public EnhanceFallbackDto? Fallback { get; set; }
        public ProbBreakdownDto Breakdown { get; set; } = new ProbBreakdownDto();
        public int RollBp { get; set; }
        public int SuccessBp { get; set; }
        public int GreatBp { get; set; }
        public int MutationEndBp { get; set; }
        public EnhanceCostDto Cost { get; set; } = new EnhanceCostDto();
        public MilestoneDto? Milestone { get; set; }
        public string? ServerNotice { get; set; }
        public bool ScarAdded { get; set; }
        public WandView Wand { get; set; } = new WandView();
        public List<BalanceView> Balances { get; set; } = new List<BalanceView>();
        public List<StackView> Items { get; set; } = new List<StackView>();
    }
}

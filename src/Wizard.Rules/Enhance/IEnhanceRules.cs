using System.Collections.Generic;
using Wizard.Content;

namespace Wizard.Rules.Enhance
{
    // CONTRACTS 4.2. 서버와 오프라인의 차이는 rollBp 를 누가 뽑느냐뿐이다. 확률·비용·실패 규칙은 다섯 지팡이가 똑같다(DECISIONS 2.4)
    public interface IEnhanceRules
    {
        RuleCheck Validate(WandState wand, EnhanceRequest request, InventoryView inventory);
        ProbBreakdown Probability(WandState wand, IReadOnlyList<SacrificeItem> sacrifices);
        EnhanceCost CostFor(int stage);
        bool DropPossible(WandState wand);
        EnhanceResolution Resolve(WandState wand, EnhanceRequest request, InventoryView inventory, int rollBp, IRandomSource mutationPick);   // rollBp ∈ [0, 10000)
        EnhanceResolution ResolveMutation(WandState wand, MutationDecision decision);
        RuleCheck ValidateRemoveMutation(WandState wand, MutationId mutation, InventoryView inventory);
        CarveResolution ResolveCarve(WandState wand, InventoryView inventory, int rollBp);
        EnhanceExpectation Expect(WandState wand, ExpectationAssumptions assumptions);   // 마르코프 연쇄(spec 08 5절)
    }
}

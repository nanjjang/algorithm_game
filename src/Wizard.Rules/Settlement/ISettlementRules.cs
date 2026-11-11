namespace Wizard.Rules.Settlement
{
    // CONTRACTS 4.3. 입력은 facts 와 맥락뿐이다. RunOutcome(Sim 타입)을 받지 않는다
    public interface ISettlementRules
    {
        SettlementResult Settle(RunResultFacts facts, SettlementContext context, IRandomSource rng);
        int BagCapacity(int clearedWave);
        int BagHeroLimit(int clearedWave);
    }
}

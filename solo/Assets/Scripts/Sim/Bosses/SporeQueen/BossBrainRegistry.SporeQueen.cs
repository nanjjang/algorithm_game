namespace Wizard.Solo.Sim.Bosses
{
    public static partial class BossBrainRegistry
    {
        static partial void RegisterSporeQueen(BossBrainTable t) { t.Add(Ids.Boss.SporeQueen, () => new SporeQueenBrain()); }
    }
}

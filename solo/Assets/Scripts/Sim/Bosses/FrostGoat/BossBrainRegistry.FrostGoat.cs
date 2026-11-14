namespace Wizard.Solo.Sim.Bosses
{
    public static partial class BossBrainRegistry
    {
        static partial void RegisterFrostGoat(BossBrainTable t) { t.Add(Ids.Boss.FrostGoat, () => new FrostGoatBrain()); }
    }
}

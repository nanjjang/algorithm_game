namespace Wizard.Solo.Sim.Bosses
{
    public static partial class BossBrainRegistry
    {
        static partial void RegisterKilnBoar(BossBrainTable t) { t.Add(Ids.Boss.KilnBoar, () => new KilnBoarBrain()); }
    }
}

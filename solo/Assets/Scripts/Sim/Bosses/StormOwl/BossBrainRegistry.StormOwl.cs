namespace Wizard.Solo.Sim.Bosses
{
    public static partial class BossBrainRegistry
    {
        static partial void RegisterStormOwl(BossBrainTable t) { t.Add(Ids.Boss.StormOwl, () => new StormOwlBrain()); }
    }
}

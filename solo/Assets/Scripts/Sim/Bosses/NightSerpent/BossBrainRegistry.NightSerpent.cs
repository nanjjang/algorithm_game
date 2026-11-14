namespace Wizard.Solo.Sim.Bosses
{
    public static partial class BossBrainRegistry
    {
        static partial void RegisterNightSerpent(BossBrainTable t) { t.Add(Ids.Boss.NightSerpent, () => new NightSerpentBrain()); }
    }
}

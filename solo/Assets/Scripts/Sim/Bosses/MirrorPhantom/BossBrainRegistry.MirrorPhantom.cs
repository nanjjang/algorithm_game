namespace Wizard.Solo.Sim.Bosses
{
    public static partial class BossBrainRegistry
    {
        static partial void RegisterMirrorPhantom(BossBrainTable t) { t.Add(Ids.Boss.MirrorPhantom, () => new MirrorPhantomBrain()); }
    }
}

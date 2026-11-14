namespace Wizard.Solo.Sim
{
    public static partial class SimModuleFactory
    {
        static partial void CreateBosses(ref BossModule m) { m = new Bosses.BossSystem(); }
    }
}

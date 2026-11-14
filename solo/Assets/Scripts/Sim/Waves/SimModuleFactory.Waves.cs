namespace Wizard.Solo.Sim
{
    public static partial class SimModuleFactory
    {
        static partial void CreateWaves(ref WaveModule m) { m = new Waves.WaveDirector(); }
    }
}

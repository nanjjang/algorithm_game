namespace Wizard.Solo.Sim
{
    public static partial class SimModuleFactory
    {
        static partial void CreateRun(ref RunModule m) { m = new Run.RunFlow(); }
    }
}

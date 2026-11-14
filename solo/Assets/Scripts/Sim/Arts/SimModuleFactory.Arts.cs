namespace Wizard.Solo.Sim
{
    // 등록 자리(00-architecture.md 2.1절): 비기 모듈.
    public static partial class SimModuleFactory
    {
        static partial void CreateArts(ref ArtModule m) { m = new Arts.ArtSystem(); }
    }
}

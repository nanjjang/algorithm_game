// 등록 자리(00 문서 2.1절): 위협 모듈을 끼운다.
namespace Wizard.Solo.Sim
{
    public static partial class SimModuleFactory
    {
        static partial void CreateThreats(ref ThreatModule m) { m = new Threats.ThreatSystem(); }
    }
}

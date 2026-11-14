namespace Wizard.Solo.Sim
{
    // 등록 자리(00 문서 2.1절): 플레이어 모듈을 PlayerSystem으로 끼운다.
    public static partial class SimModuleFactory
    {
        static partial void CreatePlayer(ref PlayerModule m) { m = new Player.PlayerSystem(); }
    }
}

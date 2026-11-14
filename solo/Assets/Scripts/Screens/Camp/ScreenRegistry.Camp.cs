namespace Wizard.Solo.UI
{
    /// <summary>Camp 묶음 화면 등록(00 문서 2.1절·8절): 전리품 고르기, 야영(모드 넷), 비기 트라이, 허수아비 연습.</summary>
    public static partial class ScreenRegistry
    {
        static partial void RegisterCamp(IScreenManager m, GameContext ctx)
        {
            m.Register(ScreenId.LootPick, () => new Screens.Camp.LootPickScreen());
            m.Register(ScreenId.Camp, () => new Screens.Camp.CampScreen());
            m.Register(ScreenId.ArtTrie, () => new Screens.Camp.ArtTrieScreen());
            m.Register(ScreenId.DummyPractice, () => new Screens.Camp.DummyPracticeScreen());
        }
    }
}

using Wizard.Solo.Screens.Start;

namespace Wizard.Solo.UI
{
    /// <summary>시작 묶음 등록(G18, 00 문서 2.1절·8절): FirstRunPrompt, Title, ProfileSelect, OfflineGuide, MainMenu.</summary>
    public static partial class ScreenRegistry
    {
        static partial void RegisterStart(IScreenManager m, GameContext ctx)
        {
            m.Register(ScreenId.FirstRunPrompt, () => new FirstRunPromptScreen());
            m.Register(ScreenId.Title, () => new TitleScreen());
            m.Register(ScreenId.ProfileSelect, () => new ProfileSelectScreen());
            m.Register(ScreenId.OfflineGuide, () => new OfflineGuideScreen());
            m.Register(ScreenId.MainMenu, () => new MainMenuScreen());
            m.Register(ScreenId.CoopLobby, () => new Wizard.Solo.Screens.Online.CoopLobbyScreen());
            m.Register(ScreenId.Plaza, () => new Wizard.Solo.Screens.Online.PlazaScreen());
            m.Register(ScreenId.OnlineProfile, () => new Wizard.Solo.Screens.Online.OnlineProfileScreen());
            m.Register(ScreenId.OnlineAssembly, () => new Wizard.Solo.Screens.Online.OnlineAssemblyScreen());
        }
    }
}

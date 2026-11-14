namespace Wizard.Solo.UI
{
    /// <summary>G24 Settings 묶음 등록(00 문서 2.1절·8절): Settings, KeyRebind.</summary>
    public static partial class ScreenRegistry
    {
        static partial void RegisterSettings(IScreenManager m, GameContext ctx)
        {
            m.Register(ScreenId.Settings, () => new Wizard.Solo.Screens.Settings.SettingsScreen());
            m.Register(ScreenId.KeyRebind, () => new Wizard.Solo.Screens.Settings.KeyRebindScreen());
        }
    }
}

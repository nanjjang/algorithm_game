namespace Wizard.Solo.UI
{
    /// <summary>공용 창 등록(F3, 00 문서 8절의 Common 묶음).</summary>
    public static partial class ScreenRegistry
    {
        static partial void RegisterCommon(IScreenManager m, GameContext ctx)
        {
            m.Register(ScreenId.ConfirmDialog, () => new UiConfirmDialog());
            m.Register(ScreenId.Tooltip, () => new UiTooltipScreen());
            m.Register(ScreenId.Loading, () => new UiLoadingScreen());
            m.Register(ScreenId.SaveRestored, () => new UiSaveRestoredDialog());
        }
    }
}

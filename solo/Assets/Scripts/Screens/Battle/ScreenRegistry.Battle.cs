namespace Wizard.Solo.UI
{
    /// <summary>Battle 묶음 등록(00 문서 2.1·8절): Battle, Pause, ControlsView.</summary>
    public static partial class ScreenRegistry
    {
        static partial void RegisterBattle(IScreenManager m, GameContext ctx)
        {
            m.Register(ScreenId.Battle, () => new global::Wizard.Solo.Screens.Battle.BattleScreen());
            m.Register(ScreenId.Pause, () => new global::Wizard.Solo.Screens.Battle.PauseScreen());
            m.Register(ScreenId.ControlsView, () => new global::Wizard.Solo.Screens.Battle.ControlsViewScreen());
        }
    }
}

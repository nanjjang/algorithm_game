namespace Wizard.Solo.UI
{
    public static partial class ScreenRegistry
    {
        /// <summary>Prep 묶음(00 문서 8절): 원정 준비 화면 하나.</summary>
        static partial void RegisterPrep(IScreenManager m, GameContext ctx)
        {
            m.Register(ScreenId.ExpeditionPrep, () => new Screens.Prep.ExpeditionPrepScreen());
        }
    }
}

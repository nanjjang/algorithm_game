namespace Wizard.Solo.UI
{
    // 00 문서 2.1절: Tutorial 묶음의 등록 자리.
    public static partial class ScreenRegistry
    {
        static partial void RegisterTutorial(IScreenManager m, GameContext ctx)
        {
            m.Register(ScreenId.Tutorial, () => new global::Wizard.Solo.Screens.Story.TutorialScreen());
        }
    }
}

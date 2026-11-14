namespace Wizard.Solo.UI
{
    // 00 문서 2.1절: Tutorial 묶음의 등록 자리.
    public static partial class ScreenRegistry
    {
        static partial void RegisterTutorial(IScreenManager m, GameContext ctx)
        {
            m.Register(ScreenId.Tutorial, () => new global::Wizard.Solo.Screens.Story.TutorialScreen());
            // 조작·도감(일시정지 메뉴에서 배운 것 다시 보기)과 문맥 힌트 쪽지(처음 일어난 일을 한 번 알려 준다).
            m.Register(ScreenId.Codex, () => new global::Wizard.Solo.Screens.Hints.CodexScreen());
            global::Wizard.Solo.Screens.Hints.HintHost.Attach(ctx);
        }
    }
}

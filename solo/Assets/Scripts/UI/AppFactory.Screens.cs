using Wizard.Solo.UI;

namespace Wizard.Solo
{
    public static partial class AppFactory
    {
        /// <summary>
        /// F3: 화면 층 캔버스 다섯, EventSystem(InputSystemUIInputModule), 화면 관리자. GameObject는 DontDestroyOnLoad.
        /// 다시 불리면(PlayMode 테스트의 Bootstrap.Boot 반복) 앞서 만든 관리자·캔버스·EventSystem을 먼저 걷는다.
        /// </summary>
        static partial void CreateScreens(GameContext ctx, ref IScreenManager screens)
        {
            screens = ScreenManager.Create(ctx);
        }
    }
}

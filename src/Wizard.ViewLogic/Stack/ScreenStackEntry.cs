namespace Wizard.ViewLogic.Stack
{
    // 스택의 한 칸. ExplicitReturnTarget 은 열 때 ScreenArgs.ReturnTarget 으로 따로 정한 돌아갈 곳이다(없으면 None = 바로 아래 화면)
    public readonly struct ScreenStackEntry
    {
        public readonly IScreen Screen;
        public readonly ScreenArgs Args;

        public ScreenStackEntry(IScreen screen, ScreenArgs args)
        {
            Screen = screen;
            Args = args;
        }

        public ScreenId Id => Screen.Id;
        public ScreenLayer Layer => Screen.Layer;
        public ScreenId ExplicitReturnTarget => Args.ReturnTarget;
    }
}

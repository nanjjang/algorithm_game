namespace Wizard.ViewLogic.Common
{
    // spec 13 0.2절: 화면 타이머는 고정 60Hz 화면 틱(UiTick)으로 진행한다 [임시, U-58]. 게임 틱과 따로다.
    public static class UiTime
    {
        public const int UiTicksPerSecond = 60;   // 단위 환산 상수
        public const int MsPerSecond = 1000;      // 단위 환산 상수

        // 밀리초 → 화면 틱(반올림). 250ms = 15t, 500ms = 30t
        public static int MsToUiTicks(int ms) => (ms * UiTicksPerSecond + MsPerSecond / 2) / MsPerSecond;
    }
}

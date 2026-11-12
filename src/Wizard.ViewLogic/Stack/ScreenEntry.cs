namespace Wizard.ViewLogic.Stack
{
    // spec 13 1.1절 "진입점이 여럿인 화면은 같은 화면 하나이고 ScreenArgs.entry 로 진입점만 다르다" [확정].
    // 값은 spec 13 4.2(MainMenu), 5.1(PlazaGate, DuelEntry), 8.2(Camp), 15.3(PlazaMenu, Plaza)절에 나오는 진입점이다.
    // 번호는 더하기만 한다.
    public enum ScreenEntry
    {
        None = 0,
        MainMenu = 1,
        PlazaMenu = 2,
        Plaza = 3,
        PlazaGate = 4,
        DuelEntry = 5,
        Camp = 6,
    }
}

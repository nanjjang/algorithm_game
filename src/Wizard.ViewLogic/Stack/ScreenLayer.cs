namespace Wizard.ViewLogic.Stack
{
    // spec 13 1.1절. 화면이 어느 층에 그려지고 입력을 어떻게 받는가.
    //   Fullscreen  아래의 모든 화면을 덮는다(보이지 않고 입력도 받지 않는다). 광장에서 연 전체 화면도 광장을 덮는다
    //   Panel       아래 화면 위에 일부만 겹친다. 입력을 가로막는지는 IScreen.BlocksInput 이 정한다
    //   Modal       아래 화면이 비치지만 입력은 언제나 가로막는다(BlocksInput 값과 무관)
    //   Toast       스택과 따로 맨 위에 뜬다. 입력과 포커스를 받지 않고 Pop·Replace·Back 의 대상이 아니다
    //   HudOverlay  전장 위의 HUD(전투 HUD, 광장 겹침 화면). 보통 스택 맨 아래에 있다
    public enum ScreenLayer
    {
        Fullscreen = 0,
        Panel = 1,
        Modal = 2,
        Toast = 3,
        HudOverlay = 4,
    }
}

namespace Wizard.ViewLogic.Common
{
    // 누를 수 있는 것에서 생긴 일. spec 13 21절 ui.common.* 연결점에 대응한다(바인딩은 M9-PRS-01 이 UiEventKeys 로 잇는다).
    //   HoverEnter/HoverExit   ui.common.hover_enter / hover_exit
    //   CursorMove             ui.common.cursor_move (대상 id, BracketSize)
    //   Press/Release          ui.common.press / release
    //   ToggleOn/ToggleOff     ui.common.toggle_on / toggle_off
    //   ConfirmCheck           ui.common.confirm_check (새로 Picked 가 됨)
    //   DisabledPress          ui.common.disabled_press (이유 키. spec 13 U-15: 아무 일 없음 + 이유 한 줄 다시)
    //   Activated              눌림이 끝까지 됨. 화면이 할 일을 한다(연결점 없음)
    public enum InteractionEventKind
    {
        HoverEnter = 0,
        HoverExit = 1,
        CursorMove = 2,
        Press = 3,
        Release = 4,
        ToggleOn = 5,
        ToggleOff = 6,
        ConfirmCheck = 7,
        DisabledPress = 8,
        Activated = 9,
    }
}

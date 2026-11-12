using System;

namespace Wizard.ViewLogic.Common
{
    // spec 13 1.3절. 지금 보여야 하는 표시의 모음(입력 모드를 반영한 결과). InteractableGroup.VisualOf 가 계산한다.
    //   Hover      올림 바탕(마우스 모드, 켜진 것)
    //   Cursor     움직이는 커서 괄호(키보드 모드)
    //   Confirmed  확정(Picked): 멈춘 괄호 + 확정 체크
    //   Pressed    누름 바탕(켜진 것)
    //   On         켜짐(토글·탭·거르기)
    //   Disabled   꺼짐
    [Flags]
    public enum InteractVisual
    {
        None = 0,
        Hover = 1,
        Cursor = 2,
        Confirmed = 4,
        Pressed = 8,
        On = 16,
        Disabled = 32,
    }
}

namespace Wizard.Unity.UI.Common
{
    // 메뉴에서 물리 키가 뜻하는 것(spec 13 "방향키 칸 이동, J 넣기·강화, K 빼기, Esc 나가기"). 키 배치는 UiKeyMap
    public enum UiKeyAction
    {
        None = 0,
        Up = 1,
        Down = 2,
        Left = 3,
        Right = 4,
        Confirm = 5,     // J: 누르는 동안 누름, 떼면 Activate(spec 14 4.3 KeyConfirm)
        Secondary = 6,   // K: 빼기 따위. 화면이 뜻을 정한다(UiScreenView.OnKey)
        Back = 7,        // Esc: ScreenStack.Back
    }
}

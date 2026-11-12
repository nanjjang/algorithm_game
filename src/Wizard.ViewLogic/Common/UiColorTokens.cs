namespace Wizard.ViewLogic.Common
{
    // spec 14 4.2절 색 토큰 가운데 공용 기반이 가리키는 것. 색 값은 표시 계층이 쥐고 여기서는 식별자로만 가리킨다.
    // 위험색(빨강·자홍)은 메뉴와 창에 쓰지 않으므로 여기에 두지 않는다. 조심할 버튼은 호박색이다(spec 13 1.5, spec 14 4.2).
    public static class UiColorTokens
    {
        public const string SelectYellow = "select_yellow";   // 선택 괄호 전용(spec 14 U-26: 토큰 하나로 바꿀 수 있게)
        public const string Slate = "slate";                  // 꺼진 것의 선택 괄호, 보조 글자
        public const string AlertAmber = "alert_amber";       // 조심 버튼의 테와 경고 삼각
        public const string Ink = "ink";                      // 괄호 바깥 2px 테
    }
}

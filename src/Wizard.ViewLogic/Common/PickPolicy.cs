namespace Wizard.ViewLogic.Common
{
    // 눌러 고른 것(Picked)을 묶음 안에서 몇 개 두는가. spec 14 4.3 의사코드는 "Activate(t); t.Picked = true"만 적는다.
    // Single: 새로 고르면 다른 것의 Picked 를 푼다("지금 고른 것"이 하나인 목록·메뉴) [임시: 기본값]
    // Multiple: 따로따로 남는다(여러 장을 고르는 화면)
    public enum PickPolicy
    {
        Single = 0,
        Multiple = 1,
    }
}

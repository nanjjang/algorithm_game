namespace Wizard.BalanceLab.Feel
{
    // 화력 비를 재는 두 봇. 둘 다 J 만 누르고 떼며(이동·비기 없음) 같은 연습 세계의 같은 조립으로 돈다
    internal enum BotKind
    {
        HoldFire = 1,       // 꾹 누르기: J 를 계속 누른다. 열이 차서 잠기고 풀리면 다시 쏜다(GDD 18절 "꾹 누르기 6.9초 주기")
        CritZone = 2,       // 임계 구간 플레이: 임계 n발 → 0.2초 대기 → 4n 만큼 냉각, 의 반복(GDD 18절)
    }
}

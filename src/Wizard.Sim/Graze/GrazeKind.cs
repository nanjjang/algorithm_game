namespace Wizard.Sim.Graze
{
    // spec 01 8.1·8.3. 스침을 일으킨 것의 종류. GrazeAwarded·GrazeCosmeticOnly 이벤트의 B 칸에 이 번호가 들어간다.
    // 번호는 더하기만 하고 바꾸지 않는다(표시 계층의 바인딩이 읽는다).
    internal enum GrazeKind : byte
    {
        Bullet = 0,        // 적의 탄(보스 탄 포함, mon 3.2)
        Dash = 1,          // 돌진 상태인 적의 몸(늑대 돌진 등, mon 2.2)
        Zone = 2,          // 예고 구역이 터지는 순간의 경계 바깥
        Skill = 3,         // 비기가 직접 인정한 스침(벼락 질주, 잔상 걸음. spec 01 8.1 10번)
    }
}

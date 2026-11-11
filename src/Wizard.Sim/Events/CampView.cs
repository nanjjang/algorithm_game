using Wizard.Sim;

namespace Wizard.Sim.Events
{
    // CONTRACTS 3.3. 화면은 이것을 그대로 읽고 따로 상태를 두지 않는다
    public readonly struct CampView
    {
        public readonly CampSub Sub;                      // 정본 상태(해시에 들어간다). 나머지 칸(넘긴 웨이브, 회복 횟수, 카드, 보관함, 해금 차례)은 M4-RUN-01 이 더한다
    }
}

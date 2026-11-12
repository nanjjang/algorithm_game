namespace Wizard.Sim.Events
{
    // CONTRACTS 3.3. 화면은 이것을 그대로 읽고 따로 상태를 두지 않는다.
    // CONTRACTS 3.3이 이름을 든 칸 가운데 스칼라인 것: 하위 상태, 방금 넘긴 웨이브, 이 야영의 회복 횟수, 해금 차례.
    // 전리품 카드와 보관함은 목록이라 담는 방식(ISimView 의 범위 접근자 등)이 계약에 아직 없다(OPEN-QUESTIONS)
    public readonly struct CampView
    {
        public readonly CampSub Sub;                      // 정본 상태(해시에 들어간다). 야영이 아니면 None
        public readonly int ClearedWave;                  // 방금 넘긴 웨이브. 야영이 아니면 0
        public readonly int HealsThisCamp;                // 이 야영에서 회복을 산 횟수
        public readonly bool UnlockTurnPending;           // 이 야영에 비기 해금 차례가 있는가(spec 03 15절)

        public CampView(CampSub sub, int clearedWave, int healsThisCamp, bool unlockTurnPending)
        {
            Sub = sub;
            ClearedWave = clearedWave;
            HealsThisCamp = healsThisCamp;
            UnlockTurnPending = unlockTurnPending;
        }
    }
}

namespace Wizard.Netcode.Duel
{
    // CONTRACTS 6.4, DECISIONS-TECH T-64. DuelHostCore.Status 가 값으로 돌려주는 호스트의 진행 상태.
    // 지금 있는 칸은 "진행"과 "끝"이다. 끝의 결과(승자, 라운드), 몰수(누가), 끊김 타이머의 칸은 그것을 만드는 작업(M14-NET-01 의
    // 끊김·몰수·재접속)이 더한다. 칸은 더하기만 한다.
    public readonly struct DuelHostStatus
    {
        // 다음에 확정할 틱 = 지금까지 확정한 틱의 수 = 호스트 세계의 SimWorld.Tick(다음에 처리할 틱, T-62 의 틱의 뜻).
        // 마지막 확정 틱 = NextConfirmTick − 1
        public readonly uint NextConfirmTick;

        // 호스트 세계의 경기가 끝났다(SimWorld.IsOver). 그 뒤로는 틱을 확정하지 않는다
        public readonly bool IsOver;

        public DuelHostStatus(uint nextConfirmTick, bool isOver)
        {
            NextConfirmTick = nextConfirmTick;
            IsOver = isOver;
        }
    }
}

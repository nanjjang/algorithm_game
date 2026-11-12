namespace Wizard.Sim.Events
{
    // CONTRACTS 3.3. 시작 틱과 길이를 준다. 표시는 이것으로만 채움을 계산한다(표시 쪽 시계를 쓰지 않는다).
    // 모양·위치 칸은 spec 05 14.1(telegraph.start)이 주인이고 그 영역의 작업이 채운다
    public readonly struct TelegraphView
    {
        public readonly uint StartTick;
        public readonly int LengthTicks;                  // 판정 틱 = StartTick + LengthTicks

        public TelegraphView(uint startTick, int lengthTicks)
        {
            StartTick = startTick;
            LengthTicks = lengthTicks;
        }
    }
}

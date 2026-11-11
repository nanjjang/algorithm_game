namespace Wizard.Sim.Input
{
    // CONTRACTS 3.1. 플레이어 한 명의 순번 공간. InputRecorder 와 DuelClientCore 만 가진다
    public struct SeqCounter
    {
        public uint Last;                   // 마지막으로 매긴 순번. 0이면 아직 없음

        public uint Next() => ++Last;
    }
}

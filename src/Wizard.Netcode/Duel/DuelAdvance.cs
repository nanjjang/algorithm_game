namespace Wizard.Netcode.Duel
{
    // CONTRACTS 6.4. 예측 틱이 확정 + 8을 넘으면 Stalled(예측 중지, 확정 대기). 입력 시계는 멈추지 않는다
    public readonly struct DuelAdvance
    {
        public readonly bool Stalled;
        public readonly int TicksSimulated, RollbackDepth;
        public readonly uint PredictedTick, ConfirmedTick;
        
        public DuelAdvance(bool stalled, int ticksSimulated, int rollbackDepth, uint predictedTick, uint confirmedTick)
        {
        Stalled = stalled;
        TicksSimulated = ticksSimulated;
        RollbackDepth = rollbackDepth;
        PredictedTick = predictedTick;
        ConfirmedTick = confirmedTick;
        }
    }
}

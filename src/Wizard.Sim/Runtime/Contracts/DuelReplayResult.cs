using Wizard.Sim.Duel;

namespace Wizard.Sim
{
    // CONTRACTS 3.8. ReplayRunner.RunDuel 의 결과
    public sealed class DuelReplayResult
    {
        public ReplayStatus Status;
        public DuelOutcome? Outcome;                      // Completed 일 때만
        public uint TicksSimulated;
        public string? FaultDetail;                       // SimFault 일 때 예외의 종류와 틱(운영자용)
    }
}

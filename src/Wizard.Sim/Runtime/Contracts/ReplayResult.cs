using Wizard.Sim.Waves;

namespace Wizard.Sim
{
    // CONTRACTS 3.8
    public sealed class ReplayResult
    {
        public ReplayStatus Status;
        public RunOutcome? Outcome;                       // Completed 일 때만
        public uint TicksSimulated;
        public string? FaultDetail;                       // SimFault 일 때 예외의 종류와 틱(운영자용)
    }
}

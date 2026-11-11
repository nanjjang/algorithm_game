using Wizard.Sim.Duel;

namespace Wizard.Sim
{
    // CONTRACTS 3.8. ReplayRunner.RunDuel 의 결과
    public sealed class DuelReplayResult
    {
        public ReplayStatus Status;
        public DuelOutcome? Outcome;
        public uint TicksSimulated;
    }
}

using System;

namespace Wizard.Sim.Duel
{
    // CONTRACTS 3.9
    public sealed class DuelOutcome
    {
        public DuelWinner Winner;
        public DuelRoundResult[] Rounds = Array.Empty<DuelRoundResult>();
        public uint EndTick;
        public ulong FinalStateHash;
    }
}

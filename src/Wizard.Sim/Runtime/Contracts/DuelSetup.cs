using System;
using Wizard.Sim.Duel;

namespace Wizard.Sim
{
    // CONTRACTS 3.2. WriteCanonical(WZDR 머리말)은 M14-DUL-01 이 더한다
    public sealed class DuelSetup
    {
        public Guid MatchId;
        public ulong Seed;
        public DuelRoomOptions Options = new DuelRoomOptions();
        public DuelPlayerSetup[] Players = Array.Empty<DuelPlayerSetup>();    // 길이 2. 리그 제한을 적용한 뒤의 값
    }
}

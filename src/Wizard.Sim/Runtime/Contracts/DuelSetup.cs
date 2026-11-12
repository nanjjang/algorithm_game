using System;
using Wizard.Sim.Duel;

namespace Wizard.Sim
{
    // CONTRACTS 3.2. WriteCanonical(WZDR 머리말)은 아직 없다: DuelRoomOptions 의 칸과 WZDR 머리말의 칸 배치(11.1절)가
    // 정해진 뒤에 더한다(DECISIONS-TECH T-46 의 따라오는 일)
    public sealed class DuelSetup
    {
        public Guid MatchId;
        public ulong Seed;
        public DuelRoomOptions Options = new DuelRoomOptions();
        public DuelPlayerSetup[] Players = Array.Empty<DuelPlayerSetup>();    // 길이 2. 리그 제한을 적용한 뒤의 값
    }
}

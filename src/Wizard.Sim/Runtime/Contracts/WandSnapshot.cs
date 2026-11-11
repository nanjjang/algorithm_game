using System;
using Wizard.Content;

namespace Wizard.Sim
{
    // CONTRACTS 3.2. 서버가 아는 지팡이 상태. run.wand_snapshot, duel_match.snapshots
    public sealed class WandSnapshot
    {
        public CharacterId Character;
        public int Stage;
        public int SlotsExtra;
        public MutationId[] Mutations = Array.Empty<MutationId>();     // 받은 순서
        public BlockId Core;
        public BlockId[] Links = Array.Empty<BlockId>();               // 길이 = 기본 연결 칸 + SlotsExtra
    }
}

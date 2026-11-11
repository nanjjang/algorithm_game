using System.Collections.Generic;

namespace Wizard.Protocol.Http
{
    // CONTRACTS 8.2
    public sealed class CharacterView
    {
        public int Character { get; set; }                // 1..5
        public long EquippedWandId { get; set; }
        public List<string> UnlockedArts { get; set; } = new List<string>();    // 공용 비기 id. 시작 3종은 싣지 않아도 참
        public long BestScore { get; set; }
        public int BestWave { get; set; }
    }
}

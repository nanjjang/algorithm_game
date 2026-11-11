using Wizard.Content;
using Wizard.Core;

namespace Wizard.Sim.Bosses
{
    // CONTRACTS 3.6
    public struct BossState
    {
        public EntityId Id;
        public BossId Boss;
        public int Phase;
        // 나머지는 M8-BOS-01 이 더한다(spec 06 1절)
    }
}

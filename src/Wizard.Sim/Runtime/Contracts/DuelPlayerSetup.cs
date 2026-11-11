using Wizard.Content;
using Wizard.Core;

namespace Wizard.Sim
{
    // CONTRACTS 3.2. 입력 지연 D는 여기에 없다. 시뮬레이션은 D를 모른다(0.3절, 10.5절)
    public sealed class DuelPlayerSetup
    {
        public PlayerSlot Slot;
        public WandSnapshot AppliedWand = new WandSnapshot();
        public int ActualStage;
        public int MaxHpPoints;
        public ArtMask UnlockedArts;
    }
}

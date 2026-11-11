using Wizard.Content;

namespace Wizard.Sim.Arts
{
    // spec 03. 이 판에서 쓸 수 있는 비기(공용 해금 + 고유 비기)
    public readonly struct UnlockSet
    {
        public readonly ArtMask Arts;
        
        public UnlockSet(ArtMask arts)
        {
        Arts = arts;
        }
    }
}

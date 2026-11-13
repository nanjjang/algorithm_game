using Wizard.Content;

namespace Wizard.Sim.Spell
{
    // CONTRACTS 3.4(T-57), spec 02 6.3 ScopeProjectile
    public readonly struct ScopeProjectile
    {
        public readonly BlockId Block;
        public readonly int Slot;

        public ScopeProjectile(BlockId block, int slot)
        {
            Block = block;
            Slot = slot;
        }
    }
}

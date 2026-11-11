using Wizard.Content;

namespace Wizard.Rules.Enhance
{
    // CONTRACTS 4.2
    public readonly struct SacrificeItem
    {
        public readonly BlockId Block;
        public readonly int Qty;
        
        public SacrificeItem(BlockId block, int qty)
        {
        Block = block;
        Qty = qty;
        }
    }
}

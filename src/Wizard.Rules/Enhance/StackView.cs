namespace Wizard.Rules.Enhance
{
    // CONTRACTS 4.2. item_code 의 (qty, pending, socketed, bound)
    public readonly struct StackView
    {
        public readonly long Qty, Pending, Socketed, Bound;
        
        public StackView(long qty, long pending, long socketed, long bound)
        {
        Qty = qty;
        Pending = pending;
        Socketed = socketed;
        Bound = bound;
        }
    }
}

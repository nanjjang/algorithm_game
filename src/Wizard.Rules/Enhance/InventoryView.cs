using System;

namespace Wizard.Rules.Enhance
{
    // CONTRACTS 4.2. 검사에 필요한 잔액과 수량만
    public sealed class InventoryView
    {
        public long Gold, GoldPending;
        public Func<string, StackView> Stack = _ => default;   // item_code → (qty, pending, socketed, bound)
    }
}

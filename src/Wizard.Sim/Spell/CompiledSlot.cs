using Wizard.Content;

namespace Wizard.Sim.Spell
{
    // CONTRACTS 3.4(T-57). 슬롯 하나(0 = 코어, 1..L)의 블록, 몫, 들어간 스코프 깊이. 빈 칸은 ScopeDepth 가 그 칸 앞의 스코프다
    public readonly struct CompiledSlot
    {
        public readonly int Slot;
        public readonly BlockId Block;
        public readonly SlotRole Role;
        public readonly int ScopeDepth;   // Trigger 는 그것이 연 스코프의 깊이

        public CompiledSlot(int slot, BlockId block, SlotRole role, int scopeDepth)
        {
            Slot = slot;
            Block = block;
            Role = role;
            ScopeDepth = scopeDepth;
        }
    }
}

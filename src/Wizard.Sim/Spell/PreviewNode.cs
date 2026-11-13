using Wizard.Content;

namespace Wizard.Sim.Spell
{
    // CONTRACTS 3.4(T-57), spec 02 14.4. 미리보기 트리의 투사체 마디 하나.
    // 마디 번호 = SpellPreview.Nodes 의 색인(스코프 깊이 순, 그 안에서 슬롯 순) = SpellNodeExecuted 의 마디 번호(CONTRACTS 3.3)
    public readonly struct PreviewNode
    {
        public readonly int Index;        // 마디 번호
        public readonly int ScopeDepth;   // 들여쓰기 단계(0..3)
        public readonly int Slot;         // 회로 칸(커서 연동)
        public readonly BlockId Block;
        public readonly bool IsOwner;     // 스코프의 주인(굵은 테)
        public readonly bool HasWarning;  // 이 칸에 걸린 진단이 있다(경고 딱지)

        public PreviewNode(int index, int scopeDepth, int slot, BlockId block, bool isOwner, bool hasWarning)
        {
            Index = index;
            ScopeDepth = scopeDepth;
            Slot = slot;
            Block = block;
            IsOwner = isOwner;
            HasWarning = hasWarning;
        }
    }
}

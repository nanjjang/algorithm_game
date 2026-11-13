using System;

namespace Wizard.Sim.Spell
{
    // CONTRACTS 3.4(T-57), spec 02 14.4. 미리보기 트리의 스코프 하나(들여쓰기 한 단, 세로 묶음 선).
    // 불변: 컴파일러가 채운 뒤 바꾸지 않는다
    public sealed class PreviewScope
    {
        public int Depth;                    // 0..3
        public int TriggerSlot = SpellScope.NoSlot;   // 이 스코프를 연 트리거의 칸. S0 은 -1
        public bool TriggerHasWarning;       // 트리거 칸에 걸린 진단(투사체 없음, 상한으로 생략)
        public int FirstNode, NodeCount;     // SpellPreview.Nodes[FirstNode .. FirstNode + NodeCount)
        public int SplitCount = 1;           // 가지 수(1이면 가지 없음, 2 / 3)
        public int SplitSlot = SpellScope.NoSlot;     // 유효한 분열의 칸. 없으면 -1
        public int[] ModifierSlots = Array.Empty<int>();       // 변형 꼬리(슬롯 순). 한 줄에 몇 개를 그리고 "+N"으로 줄일지는 그리는 쪽의 값이다
        public bool[] ModifierHasWarning = Array.Empty<bool>();   // ModifierSlots 와 같은 길이(효과 없는 조합)
        public int DepthLabelHundredths;     // 배율 꼬리표 "×0.72" = 72: DepthMultiplier 를 소수 둘째 자리에서 내린 값(14.4). S0 은 100
        public bool Omitted;                 // 상한 가지치기로 실행하지 않는다(점선 마디 + "생략")
    }
}

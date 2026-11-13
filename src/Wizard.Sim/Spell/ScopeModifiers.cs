using System;

namespace Wizard.Sim.Spell
{
    // CONTRACTS 3.4(T-57), spec 02 6.3 ScopeModifiers. 스코프의 변형 블록을 누적한 값(6.1 "현재 스코프의 모든 투사체에 붙는다(누적)", CP-1, CP-3).
    // 겹침 규칙은 spec 02 18절: 튕김·관통은 블록 수만큼 더하고(U6, U7), 크기는 블록 수를 세어 둔다(반지름 곱과 상한은 실행기, U8).
    // 블록 하나의 값은 BlockDef 에서 읽는다(DamageBonusPermille, Params.BounceCount, Params.PierceCount. T-53).
    // 불변: 컴파일러가 채운 뒤 바꾸지 않는다
    public sealed class ScopeModifiers
    {
        public int DamageBonusPermille;   // Σ 피해 보너스(탄환 크기 +200, 관통 +100)
        public int BounceCount;           // Σ 튕김 횟수(블록 1개당 Params.BounceCount)
        public int PierceCount;           // Σ 관통 수(블록 1개당 Params.PierceCount)
        public int SizeDoublings;         // 탄환 크기 블록 수
        public bool Homing, Frost, TripleHeat, Corrosion;
        public int[] SourceSlots = Array.Empty<int>();   // 변형 블록의 슬롯(슬롯 순). 미리보기의 꼬리
    }
}

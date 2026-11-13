using System;
using Wizard.Core;
using Wizard.Sim.Spell.Compile;

namespace Wizard.Sim.Spell
{
    // CONTRACTS 3.4(T-57), spec 02 6.3 SpellScope 를 CONTRACTS 0.3 의 표현(열 HU, 배수 천분율)으로 옮긴 것.
    // 스코프는 한 줄의 사슬이다(CP-2): CompiledSpell.Scopes[d].Depth == d.
    // 불변: 컴파일러가 채운 뒤 바꾸지 않는다
    public sealed class SpellScope
    {
        public const int NoSlot = -1;

        private const int PermilleOne = 1000;               // 천분율 1.0(단위 환산)

        public int Depth;                                   // 0..3
        public int TriggerSlot = NoSlot;                    // 이 스코프를 연 트리거의 슬롯. S0 은 -1
        public int OwnerSlot = NoSlot;                      // 주인 투사체(스코프의 첫 투사체)의 슬롯. 없으면 -1 (CP-7)
        public ScopeProjectile[] Projectiles = Array.Empty<ScopeProjectile>();   // 슬롯 순
        public ScopeModifiers Mods = new ScopeModifiers();
        public int SplitCount = 1;                          // 1, 2, 3 (BlockDef.SplitCount)
        public int SplitSlot = NoSlot;                      // 유효한 분열의 슬롯. 없으면 -1
        public int SplitHeatMulPermille = PermilleOne;      // 유효한 분열의 열 배수(1600 / 2200). 없으면 1000(천분율 1.0)
        public int FanHalfAngleCenti;                       // 유효한 분열의 부채꼴 반각(BlockParams.FanHalfAngleCenti. T-53). 없으면 0
        public int HeatSumHu;                               // 이 스코프 블록 열의 합(밑줄 반영)
        public int HeatHu;                                  // HeatSumHu × 분열 배수(HT-1, HT-3)
        public int InstancesPerParent;                      // |Projectiles| × SplitCount
        public int InstancesTotal;                          // 부모 InstancesTotal × InstancesPerParent (S0 은 InstancesPerParent)
        public Fix64 DepthMultiplier;                       // decay^Depth (PowTable)

        // 부채꼴의 갈래 index(0..SplitCount-1)의 각도 오프셋(센티도). 실행기가 쓴다(spec 02 9.3, T-53 5번).
        // 갈래 순서(index 오름차순)가 실행 순서다(8.3)
        public int FanOffsetCenti(int index) => FanAngles.OffsetCenti(FanHalfAngleCenti, SplitCount, index);
    }
}

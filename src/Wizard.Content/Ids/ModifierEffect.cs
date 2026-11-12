namespace Wizard.Content
{
    // spec 02 4.2, DECISIONS-TECH T-53. 변형 블록의 효과 종류. 투사체·제어 블록은 None 이다.
    // 이름은 블록의 정본 이름을 따른다(CONTRACTS 2.1 대응표: Overheat3 → TripleHeat, Erosion → Corrosion).
    // 번호는 팩의 blocks 절에 그대로 들어간다. 더하기만 하고 바꾸지 않는다
    public enum ModifierEffect : byte
    {
        None = 0,
        Bounce = 1,                // 벽 튕김. BlockParams.BounceCount
        Pierce = 2,                // 관통. BlockParams.PierceCount
        Size = 3,                  // 판정 반지름 배수. BlockParams.RadiusMulPermille·RadiusMulCapPermille
        Homing = 4,                // 유도. BlockParams.HomingTurnCentiPerTick·HomingConeHalfCenti·HomingRange
        Frost = 5,                 // 적중 감속. BlockParams.SlowPermille·SlowTicks
        TripleHeat = 6,            // 임계 배율 교체. BlockParams.CritMultiplier
        Corrosion = 7,             // 적중한 적이 받는 피해 증가. BlockParams.DamageTakenBonusPermille·DamageTakenBonusTicks
    }
}

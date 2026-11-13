using Wizard.Core;

namespace Wizard.Sim.Spell.Compile
{
    // spec 02 9.3 부채꼴, DECISIONS-TECH T-53 5번: 갈래 i(0부터)의 각도 = −반각 + i × (2 × 반각) ÷ (갈래 수 − 1).
    // 2갈래 ±1000, 3갈래 −1500 / 0 / +1500(반각은 BlockParams.FanHalfAngleCenti). 갈래가 하나면 0.
    // 갈래 순서(i 오름차순)가 실행 순서다(8.3 "부채꼴 각도 오름차순")
    internal static class FanAngles
    {
        public static int OffsetCenti(int halfAngleCenti, int count, int index)
        {
            if (count < 1 || index < 0 || index >= count || halfAngleCenti < 0)
            {
                throw new SimInvariantException("FanAngles: index or count out of range");
            }
            if (count == 1)
            {
                return 0;
            }
            return -halfAngleCenti + FixMath.DivInt(index * 2 * halfAngleCenti, count - 1);
        }
    }
}

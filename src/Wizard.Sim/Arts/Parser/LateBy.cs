namespace Wizard.Sim.Arts
{
    // spec 03 7.5: 늦음 표시의 초는 소수 둘째 자리 올림 = ceil(초과량 × 100 / 15,360) / 100 [임시, U-12].
    // 화면 문구는 표시 계층이 이름 표로 만든다. 코어는 백분의 일 초 정수만 낸다(153u → 1 = "0.01초")
    public static class LateBy
    {
        private const long HundredthsPerSecond = 100;         // 단위 환산 상수

        public static int HundredthsCeil(long lateByUnits)
        {
            if (lateByUnits <= 0)
            {
                return 0;
            }
            long scaled = unchecked(lateByUnits * HundredthsPerSecond);
            return (int)((scaled + SimTime.InputUnitsPerSecond - 1) / SimTime.InputUnitsPerSecond);
        }
    }
}

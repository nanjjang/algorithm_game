using Wizard.Core;

namespace Wizard.Sim
{
    // spec 01 2.1, CONTRACTS 0.1. 시간 단위: 틱(60/초)과 입력 시각(틱 × 256 + 오프셋, 1단위 = 1/15,360초).
    // 기획 수치는 data/ 에 십진 초로 적고 정수로 바꿔 팩에 싣는다. 이 형식은 그 환산을 정수만으로 하는 기준식이다
    // (실수형 없이, 가장 가까운 정수 · 같으면 짝수. CONTRACTS 0.3). 코어 판정은 이미 정수가 된 값만 쓴다.
    public static class SimTime
    {
        public const int OffsetsPerTick = 256;
        public const int InputUnitsPerSecond = SimVersion.TickRate * OffsetsPerTick;   // 15,360
        public const int MaxDecimalScale = 9;

        private const long DecimalBase = 10L;

        public static long InputTime(uint tick, byte offset) => (long)tick * OffsetsPerTick + offset;

        // 십진 초 = mantissa × 10^(−scale). 예: 0.07초 = (7, 2). round(초 × 60)
        public static long TicksFromDecimalSeconds(long mantissa, int scale) => Scale(mantissa, scale, SimVersion.TickRate);

        // round(초 × 15,360). spec 01 2.1의 "초 → 입력 시각 환산"
        public static long InputUnitsFromDecimalSeconds(long mantissa, int scale) => Scale(mantissa, scale, InputUnitsPerSecond);

        private static long Scale(long mantissa, int scale, int rate)
        {
            if (scale < 0 || scale > MaxDecimalScale)
            {
                throw new SimInvariantException("SimTime: decimal scale out of range");
            }
            long limit = FixMath.DivLong(long.MaxValue, rate);
            if (mantissa > limit || mantissa < -limit)
            {
                throw new SimInvariantException("SimTime: value out of range");
            }
            long n = mantissa * rate;
            long d = 1L;
            for (int i = 0; i < scale; i++)
            {
                d *= DecimalBase;                  // 10^scale, scale ≤ 9
            }
            long q = FixMath.DivLong(n, d);
            long r = FixMath.ModLong(n, d);
            long absR = r < 0 ? -r : r;
            long twice = absR * 2;                 // absR < d ≤ 10^9 이라 넘치지 않는다
            bool odd = (q & 1L) != 0;
            if (twice > d || (twice == d && odd))
            {
                q += n < 0 ? -1L : 1L;             // 0에서 먼 쪽으로 한 칸
            }
            return q;
        }
    }
}

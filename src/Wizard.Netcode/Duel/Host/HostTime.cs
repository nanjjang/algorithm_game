using System;
using Wizard.Sim;

namespace Wizard.Netcode.Duel
{
    // DuelHostCore 의 시각 셈(spec 10 14.6·14.8, CONTRACTS 10.6). 시각은 전부 인자로 받은 µs 정수다(호스트는 시계를 모른다, T-19).
    // 시각의 기준: 경기 시계. 틱 T 는 T × 10^6 / 60 µs 에 시작한다(spec 10 14.6 [임시] "라운드 첫 카운트다운 틱 0 이 서버 시각 0").
    // 틱 길이(16,666.67µs)가 정수가 아니므로 나누지 않고 양변에 60 을 곱해 비교한다(반올림 오차 없음).
    internal static class HostTime
    {
        // 단위 환산
        private const long MicrosPerSecond = 1000000;
        private const long MicrosPerMilli = 1000;
        private const long TicksPerSecond = SimVersion.TickRate;
        private const long InputUnitsPerSecond = SimTime.InputUnitsPerSecond;

        // 받는 시각의 범위(±2^47µs ≈ 4.4년). 이 안이면 × 15,360 이 long 을 넘지 않는다
        public const long MaxAbsTimeUs = 1L << 47;

        public static void Require(long timeUs, string paramName)
        {
            if (timeUs > MaxAbsTimeUs || timeUs < -MaxAbsTimeUs)
            {
                throw new ArgumentOutOfRangeException(paramName, "time must be within ±2^47 µs of the match clock origin");
            }
        }

        // 시각 us 까지 시작한 틱의 수 = floor(us × 60 / 10^6). 틱 T 는 이 값이 T + 1 이상이면 끝났다
        public static long TicksStartedBy(long us) => FloorDiv(us * TicksPerSecond, MicrosPerSecond);

        // us ≥ 틱 (tick + 1) 의 시작 = 틱 tick 의 마감(spec 10 14.8 "늦은 시각": T + 1틱 전에 도착해야 한다)
        public static bool AtOrAfterTickEnd(long us, uint tick) => us * TicksPerSecond >= ((long)tick + 1) * MicrosPerSecond;

        // 입력 시각(틱 × 256 + 오프셋, 1/15,360초)으로 내림. CONTRACTS 10.6 의 ToInputTime
        public static long InputTimeAt(long us) => FloorDiv(us * InputUnitsPerSecond, MicrosPerSecond);

        // 틱 tick 이 시작하는 시각 − us (µs, 내림). 마감 여유(margin)의 셈
        public static long MicrosUntilTickStart(long tick, long us) => FloorDiv(tick * MicrosPerSecond - us * TicksPerSecond, TicksPerSecond);

        // ServerTick.server_time_ms(u32). 음수 시각은 0, ms 는 내림(32비트에서 돈다)
        public static uint ToWireMillis(long us) => us <= 0 ? 0U : unchecked((uint)(us / MicrosPerMilli));

        public static long MillisToMicros(long ms) => ms * MicrosPerMilli;

        // µs → ms(내림), short 범위로 자른다. ServerTick.margin_ms_estimate
        public static short ToMarginMillis(long us)
        {
            long ms = FloorDiv(us, MicrosPerMilli);
            if (ms > short.MaxValue)
            {
                return short.MaxValue;
            }
            return ms < short.MinValue ? short.MinValue : (short)ms;
        }

        private static long FloorDiv(long a, long b)
        {
            long q = a / b;
            if (a % b != 0 && (a < 0) != (b < 0))
            {
                q--;
            }
            return q;
        }
    }
}

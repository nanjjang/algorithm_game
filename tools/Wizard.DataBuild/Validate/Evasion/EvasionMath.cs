using System;

namespace Wizard.DataBuild.Validate.Evasion
{
    // 회피 검산의 수와 식(spec 05 1.2 R1~R4, 3.8, 10절). 실수형 없이 정수와 decimal 로만 계산한다(같은 입력이면 같은 결과).
    // 단위: 길이 milli 칸, 속도 milli 칸/초, 시간 ms(보고용 µs), 각도 µrad(판정은 spec 10.6 대로 mrad 내림).
    // 반올림은 늘 플레이어에게 불리한 쪽(검사를 엄하게 하는 쪽)이다: 벗어나는 거리와 적 속도는 올림, 예고 시간·플레이어 속도·탄 사이 각도는 내림.
    public static class EvasionMath
    {
        // R1: 알아차리는 시간 0.25초와 여유 0.05초(spec 05 1.2 R1, 10.2)
        public const int ReactionMs = 250;
        public const int MarginMs = 50;

        // R3: 발사 지점에서 3칸 떨어진 곳의 이웃 탄 중심 사이 1.0칸 이상, 탄속 9 이하(spec 05 1.2 R3, 10.6)
        public const int SpacingMeasureMilli = 3000;
        public const int SpacingMinMilli = 1000;
        public const int ScatterSpeedMaxMilli = 9000;

        // R4: 발사 0.4초(24틱) 전에 방향을 고정한다(spec 05 1.2 R4, 10.2)
        public const int AimLockMinTicks = 24;

        // 북두꺼비 버프: 이동 속도 ×1.2, 상한 3.7(spec 05 3.8 BuffedSpeed)
        public const int ToadBuffSpeedPermille = 1200;
        public const int ToadBuffCapMilli = 3700;

        // 서리밭 위 이동 −30%(spec 05 1.2 보충 4, 10.4)
        public const int FrostSpeedPermille = 700;

        public const int MilliPerUnit = 1000;           // 1칸 = 1000 milli 칸, 1초 = 1000ms
        public const int MicroPerMilli = 1000;
        public const int TicksPerSecond = 60;           // spec 05 0.3
        public const int PermilleDen = 1000;
        public const int FullTurnDegrees = 360;
        public const int HalfTurnDegrees = 180;

        // decimal 이 담을 수 있는 자리까지의 π(28자리)
        public const decimal Pi = 3.1415926535897932384626433833m;

        private const int SinTerms = 40;                // 테일러 급수의 항 수 상한(|x| ≤ π/2 이면 20항 안에 0 으로 줄어든다)

        // ---- 단위 바꾸기 ----

        public static int CeilMilli(decimal units) => ToInt(decimal.Ceiling(units * MilliPerUnit));

        public static int FloorMilli(decimal units) => ToInt(decimal.Floor(units * MilliPerUnit));

        // 틱 → ms(내림). 60 의 약수 배인 예고는 정확하다(66틱 → 1100ms)
        public static int TicksToMs(int ticks) => (int)((long)ticks * MilliPerUnit / TicksPerSecond);

        // ---- 벗어나는 거리(spec 05 10.1). hit = 피격 반지름(milli) ----

        public static int EscapeCircleMilli(int radiusMilli, int hitMilli) => radiusMilli + hitMilli;

        // 띠·조준선: 폭의 절반 + 피격 반지름
        public static int EscapeBandMilli(int widthMilli, int hitMilli) => CeilDiv(widthMilli, 2) + hitMilli;

        // 부채꼴: R·sinα / (1 + sinα) + 피격 반지름, α = 전체각 / 2 ≤ 90°. 옆으로 나가는 거리 ρ·sinα = 뒤로 나가는 거리 R − ρ 인 자리
        public static int EscapeFanMilli(decimal radiusCells, decimal fanDegrees, int hitMilli)
        {
            if (fanDegrees <= 0m || fanDegrees > HalfTurnDegrees)
            {
                throw new ArgumentOutOfRangeException(nameof(fanDegrees), "fan angle must be in (0, 180]");
            }
            decimal s = Sin(fanDegrees / 2m * Pi / HalfTurnDegrees);
            return CeilMilli(radiusCells * s / (1m + s)) + hitMilli;
        }

        // 두 띠의 교차점(십자): √2 × (w/2 + 피격 반지름)
        public static int EscapeCrossMilli(int widthMilli, int hitMilli)
        {
            long x = EscapeBandMilli(widthMilli, hitMilli);
            return (int)CeilSqrt(2L * x * x);
        }

        // ---- R1(spec 05 10.2) ----

        // (예고 − 300ms) × V ≥ 벗어나는 거리 × 1000. 같은 뜻: 예고 − (0.25 + d/V) ≥ 0.05
        public static bool R1(int telegraphMs, int escapeMilli, int speedMilli) =>
            (long)(telegraphMs - ReactionMs - MarginMs) * speedMilli >= (long)escapeMilli * MilliPerUnit;

        // 벗어나는 데 걸리는 시간 0.25 + d/V (µs, 올림). 검산표의 캐릭터 칸 값
        public static long NeedMicros(int escapeMilli, int speedMilli) =>
            (long)ReactionMs * MicroPerMilli + CeilDiv((long)escapeMilli * MilliPerUnit * MicroPerMilli, speedMilli);

        // 여유 = 예고 − (0.25 + d/V) (µs). R1 은 여유 ≥ 0.05초
        public static long MarginMicros(int telegraphMs, int escapeMilli, int speedMilli) =>
            (long)telegraphMs * MicroPerMilli - NeedMicros(escapeMilli, speedMilli);

        // ---- R2(spec 05 3.8, 10.5) ----

        // baseSpeed ≥ 3.7 ? baseSpeed : min(baseSpeed × 1.2, 3.7)
        public static int BuffedSpeedMilli(int baseMilli) =>
            baseMilli >= ToadBuffCapMilli ? baseMilli : Math.Min(CeilDiv(baseMilli * ToadBuffSpeedPermille, PermilleDen), ToadBuffCapMilli);

        // R2 가 보는 속도: 보스는 북두꺼비 버프를 받지 않는다(spec 05 3.8, TV-R2-08)
        public static int ContactSpeedMilli(int baseMilli, bool isBoss) => isBoss ? baseMilli : BuffedSpeedMilli(baseMilli);

        public static bool R2(int effectiveSpeedMilli, int playerSpeedMilli) => effectiveSpeedMilli < playerSpeedMilli;

        // V_frost = V × 0.7(내림. 3760 → 2632)
        public static int FrostSpeedMilli(int speedMilli) => (int)((long)speedMilli * FrostSpeedPermille / PermilleDen);

        // ---- R3(spec 05 10.6) ----

        public static long DegreesToMicroRad(decimal degrees) =>
            (long)decimal.Floor(degrees * Pi / HalfTurnDegrees * MilliPerUnit * MicroPerMilli);

        // 한 번에 쏘는 탄 count 발이 spreadDegrees 씩 벌어질 때 이웃한 두 탄 사이의 가장 작은 각(도). 한 바퀴를 넘어 겹치는 탄까지 본다.
        // count < 2 이면 이웃이 없다(-1)
        public static decimal MinGapDegrees(decimal spreadDegrees, int count)
        {
            if (count < 2)
            {
                return -1m;
            }
            var angles = new decimal[count];
            for (int i = 0; i < count; i++)
            {
                angles[i] = decimal.Remainder(spreadDegrees * i, FullTurnDegrees);
            }
            Array.Sort(angles);                       // decimal 의 전순서. 같은 값끼리는 구별할 것이 없다
            decimal min = angles[0] + FullTurnDegrees - angles[count - 1];
            for (int i = 1; i < count; i++)
            {
                decimal gap = angles[i] - angles[i - 1];
                if (gap < min)
                {
                    min = gap;
                }
            }
            return min;
        }

        // spec 10.6 의 정수식: 3000 × Δθ_mrad(내림) ≥ 1,000,000
        public static bool R3Spacing(long gapMicroRad) =>
            (long)SpacingMeasureMilli * (gapMicroRad / MicroPerMilli) >= (long)SpacingMinMilli * MilliPerUnit;

        // 거리 distMilli 에서의 호 길이 d × Δθ (µ칸). 보고용
        public static long ArcMicro(long gapMicroRad, int distMilli) => distMilli * gapMicroRad / MilliPerUnit;

        // 이어 쏘는 탄(서리 숨결처럼 시간차 줄기): hypot(속도 / 초당 발수, d × 한 발 사이 각) (µ칸, 내림. mon_calc.py 와 같은 식)
        public static long StreamSpacingMicro(int speedMilli, int shotsPerSecond, decimal sweepDegreesPerSecond, int distMilli)
        {
            if (shotsPerSecond < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(shotsPerSecond), "shots per second must be positive");
            }
            long radial = (long)speedMilli * MicroPerMilli / shotsPerSecond;
            long lateral = ArcMicro(DegreesToMicroRad(sweepDegreesPerSecond / shotsPerSecond), distMilli);
            return FloorSqrt(radial * radial + lateral * lateral);
        }

        public static bool R3StreamSpacing(long spacingMicro) => spacingMicro >= (long)SpacingMinMilli * MicroPerMilli;

        // ---- 정수 도우미 ----

        public static int CeilDiv(int a, int b) => (int)CeilDiv((long)a, b);

        public static long CeilDiv(long a, long b)
        {
            long q = a / b;
            return (a % b != 0 && (a < 0) == (b < 0)) ? q + 1 : q;
        }

        public static long FloorSqrt(long n)
        {
            if (n < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(n));
            }
            long lo = 0;
            long hi = Math.Min(n, 3037000499L) + 1;   // 3037000499² 는 long 에 들어가는 가장 큰 제곱
            while (hi - lo > 1)
            {
                long mid = lo + (hi - lo) / 2;
                if (mid * mid <= n)
                {
                    lo = mid;
                }
                else
                {
                    hi = mid;
                }
            }
            return lo;
        }

        public static long CeilSqrt(long n)
        {
            long r = FloorSqrt(n);
            return r * r == n ? r : r + 1;
        }

        // sin x (x 는 라디안, |x| ≤ π/2 에서 쓴다). 테일러 급수를 decimal 로 더한다
        public static decimal Sin(decimal x)
        {
            decimal term = x;
            decimal sum = x;
            decimal x2 = x * x;
            for (int n = 1; n <= SinTerms; n++)
            {
                term = -term * x2 / ((2 * n) * (2 * n + 1));
                if (term == 0m)
                {
                    break;
                }
                sum += term;
            }
            return sum;
        }

        private static int ToInt(decimal v)
        {
            if (v < int.MinValue || v > int.MaxValue)
            {
                throw new OverflowException("value out of int range");
            }
            return (int)v;
        }
    }
}

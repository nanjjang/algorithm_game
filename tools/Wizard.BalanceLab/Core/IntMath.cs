using System;
using Wizard.Sim;

namespace Wizard.BalanceLab.Core
{
    // 계측 도구의 비율·환산은 전부 정수로 한다(Fix64 Raw 를 실수로 나누지 않는다. CLAUDE.md 3절의 정신을 도구도 따른다).
    // 판정은 내림한 천분율이 아니라 교차 곱으로 한다: 90.0%가 89.99%로 내려가 통과·실패가 뒤집히지 않게.
    internal static class IntMath
    {
        private const int PermilleScale = 1000;
        private const int FracBits = 32;                       // Fix64 는 Q32.32

        // floor(num × 1000 ÷ den). den ≤ 0 이면 0
        public static long PermilleFloor(long num, long den)
        {
            if (den <= 0)
            {
                return 0;
            }
            return (long)((Int128)num * PermilleScale / den);
        }

        // num ÷ den ≥ permille ÷ 1000 (den > 0)
        public static bool AtLeastPermille(long num, long den, int permille)
        {
            return den > 0 && (Int128)num * PermilleScale >= (Int128)permille * den;
        }

        // num ÷ den < permille ÷ 1000 (den > 0)
        public static bool BelowPermille(long num, long den, int permille)
        {
            return den > 0 && (Int128)num * PermilleScale < (Int128)permille * den;
        }

        // (dA ÷ tA) ÷ (dB ÷ tB) ≥ permille ÷ 1000. 틱 tA, tB 동안 쌓인 값 dA, dB 의 초당 비율(모두 양수일 때만 참)
        public static bool RateAtLeastPermille(long dA, long tA, long dB, long tB, int permille)
        {
            return tA > 0 && tB > 0 && dA > 0 && dB > 0
                && (Int128)dA * tB * PermilleScale >= (Int128)permille * tA * dB;
        }

        // floor((dA ÷ tA) ÷ (dB ÷ tB) × 1000). 분모가 0이면 0
        public static long RatePermilleFloor(long dA, long tA, long dB, long tB)
        {
            if (tA <= 0 || dB <= 0)
            {
                return 0;
            }
            return (long)((Int128)dA * tB * PermilleScale / ((Int128)tA * dB));
        }

        // Fix64 Raw → 1/1000 단위(내림, 양수 기준)
        public static long RawToMilli(long raw)
        {
            return (long)(((Int128)raw * PermilleScale) >> FracBits);
        }

        // 틱 수 동안 쌓인 피해(Raw)의 초당 값을 1/1000 단위로: damage ÷ (ticks ÷ 60)
        public static long DpsMilli(long damageRaw, long ticks)
        {
            if (ticks <= 0)
            {
                return 0;
            }
            return (long)(((Int128)damageRaw * SimVersion.TickRate * PermilleScale) / ((Int128)ticks << FracBits));
        }

        // 입력 시각 단위(1/15,360초) → 밀리초(반올림). 음수는 0
        public static long UnitsToMs(long units)
        {
            if (units <= 0)
            {
                return 0;
            }
            return (units * PermilleScale + SimTime.InputUnitsPerSecond / 2) / SimTime.InputUnitsPerSecond;
        }

        // 밀리초 → 틱(내림)
        public static long MsToTicks(long ms)
        {
            return ms * SimVersion.TickRate / PermilleScale;
        }
    }
}

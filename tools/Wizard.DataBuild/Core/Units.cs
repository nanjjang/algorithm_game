using System;
using System.Globalization;

namespace Wizard.DataBuild.Core
{
    // 데이터의 십진 값 → 코어의 정수 단위(ARCHITECTURE 8.2 2단계, spec 14 0.3, CONTRACTS 0.3).
    //   칸 → Q32.32 Raw(1칸 = 2^32), px → Raw(1px = 2^32 / 64 = 2^26), 칸/초 → 틱당 Raw(÷ 60),
    //   초 → 틱(× 60), 열 → HU(× 120), 도 → 센티도(× 100), % → 천분율(× 10)·만분율(× 100),
    //   배율 → 천분율(× 1000, 1.6 → 1600), 센티(× 100, 4.7 칸/초 → 470)
    // 반올림은 가장 가까운 정수, 같으면 짝수(CONTRACTS 0.3 통합 표). 계산은 decimal 로만 한다(실수형 없음).
    // 곱은 정확해야 한다: decimal 의 가수가 넘쳐 자리를 버리면(입력이 지나치게 정밀하면) 반올림하지 않고 DataBuildException.
    // 나눗셈(÷ 60)은 decimal 의 나머지 연산으로 정확히 반올림한다(몫을 나눗셈 결과에서 반올림하지 않는다).
    public static class Units
    {
        public const long RawPerCell = 4294967296L;        // 2^32 (spec 14 0.3)
        public const int PixelsPerCell = 64;               // 1칸 = 64px (DECISIONS 2.1)
        public const long RawPerPixel = RawPerCell / PixelsPerCell;   // 2^26 = 67,108,864
        public const int TicksPerSecond = 60;              // 1초 = 60틱
        public const int HuPerHeat = 120;                  // 열 1 = 120 HU (CONTRACTS 0.3)
        public const int CentiPerDegree = 100;             // 한 바퀴 = 36,000 센티도
        public const int PermillePerPercent = 10;
        public const int BpPerPercent = 100;
        public const int PermillePerUnit = 1000;
        public const int CentiPerUnit = 100;

        public static long CellsToRaw(decimal cells) => ToLong(cells, RawPerCell, 1, "칸");

        public static long PixelsToRaw(decimal px) => ToLong(px, RawPerPixel, 1, "px");

        public static long CellsPerSecondToRawPerTick(decimal cellsPerSecond) => ToLong(cellsPerSecond, RawPerCell, TicksPerSecond, "칸/초");

        public static int SecondsToTicks(decimal seconds) => ToInt(seconds, TicksPerSecond, 1, "초");

        public static int HeatToHu(decimal heat) => ToInt(heat, HuPerHeat, 1, "열");

        public static int DegreesToCenti(decimal degrees) => ToInt(degrees, CentiPerDegree, 1, "도");

        public static int PercentToPermille(decimal percent) => ToInt(percent, PermillePerPercent, 1, "%");

        public static int PercentToBp(decimal percent) => ToInt(percent, BpPerPercent, 1, "%");

        public static int RatioToPermille(decimal ratio) => ToInt(ratio, PermillePerUnit, 1, "배율");

        public static int ToCenti(decimal value) => ToInt(value, CentiPerUnit, 1, "센티");

        // round_half_even(value × multiplier ÷ divisor). multiplier ≥ 1, divisor ≥ 1.
        public static decimal RoundHalfEven(decimal value, long multiplier, long divisor)
        {
            if (multiplier < 1 || divisor < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(multiplier), "multiplier and divisor must be positive");
            }
            decimal product;
            try
            {
                product = value * multiplier;
            }
            catch (OverflowException)
            {
                throw new DataBuildException("값 " + Show(value) + " × " + multiplier.ToString(CultureInfo.InvariantCulture) + " 이 decimal 범위를 넘는다");
            }
            // decimal 곱은 정수와 곱할 때 지수(소수 자리 수)를 그대로 둔다. 가수가 넘쳐 자리를 버렸을 때만 지수가 줄어든다.
            if (product.Scale != value.Scale)
            {
                throw new DataBuildException("값 " + Show(value) + " 은 자리가 너무 많아 정확히 바꿀 수 없다(소수 자리를 줄인다)");
            }
            bool negative = product < 0m;
            decimal a = negative ? -product : product;
            decimal whole = decimal.Truncate(a / divisor);
            decimal rem = a - whole * divisor;          // 정확하다(whole·divisor 는 정수, a 는 정확한 값)
            // 몫을 나눗셈으로 구했으므로 경계에서 한 칸 어긋날 수 있다. 나머지를 [0, divisor) 로 맞춘다.
            while (rem < 0m)
            {
                whole -= 1m;
                rem += divisor;
            }
            while (rem >= divisor)
            {
                whole += 1m;
                rem -= divisor;
            }
            decimal twice = rem * 2m;
            if (twice > divisor || (twice == divisor && decimal.Remainder(whole, 2m) != 0m))
            {
                whole += 1m;
            }
            return negative ? -whole : whole;
        }

        private static long ToLong(decimal value, long multiplier, long divisor, string unit)
        {
            decimal r = RoundHalfEven(value, multiplier, divisor);
            if (r < long.MinValue || r > long.MaxValue)
            {
                throw new DataBuildException(unit + " 값 " + Show(value) + " 을 바꾼 정수가 64비트 범위를 넘는다");
            }
            return (long)r;
        }

        private static int ToInt(decimal value, long multiplier, long divisor, string unit)
        {
            decimal r = RoundHalfEven(value, multiplier, divisor);
            if (r < int.MinValue || r > int.MaxValue)
            {
                throw new DataBuildException(unit + " 값 " + Show(value) + " 을 바꾼 정수가 32비트 범위를 넘는다");
            }
            return (int)r;
        }

        private static string Show(decimal v) => v.ToString(CultureInfo.InvariantCulture);
    }
}

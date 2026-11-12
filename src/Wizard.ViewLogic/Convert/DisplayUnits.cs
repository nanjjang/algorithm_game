using System;
using Wizard.Sim;

namespace Wizard.ViewLogic.Convert
{
    // 표시 쪽 단위 환산(엔진 없음). 고정소수점 → 실수는 여기가 아니라 FixConvert 에만 있다(CONTRACTS 13, T-27).
    //   길이: 1칸 = 64px(spec 13 0절 "1칸 = 64px(DEC 2.1)", spec 02 0절). 명세의 그림 크기(px)를 표시 좌표(1칸 = 1유닛)로 옮길 때 쓴다.
    //   시간: 1틱 = 1/60초, 입력 시각 1단위 = 1/15,360초(spec 01 2.1, SimTime). 정수 단위를 화면의 밀리초로 바꾼다.
    public static class DisplayUnits
    {
        public const int PixelsPerCell = 64;               // 단위 환산(spec 13 0절)
        public const int MillisecondsPerSecond = 1000;     // 단위 환산

        // 그림 크기(px) → 칸
        public static float PxToCells(float px) => px / PixelsPerCell;

        // 입력 시각 단위 → 밀리초(실수). 섬 간격 표시(spec 03 21절 끝, GDD 16 1단계)
        public static double InputUnitsToMilliseconds(long units) =>
            units * (double)MillisecondsPerSecond / SimTime.InputUnitsPerSecond;

        // 틱 수 → 밀리초(실수)
        public static double TicksToMilliseconds(long ticks) =>
            ticks * (double)MillisecondsPerSecond / SimVersion.TickRate;

        // 화면에 찍는 정수 밀리초: 가장 가까운 정수, 같으면 0에서 먼 쪽. 표시 전용이다(판정에 쓰지 않는다)
        public static long RoundMilliseconds(double ms) => (long)Math.Round(ms, MidpointRounding.AwayFromZero);
    }
}

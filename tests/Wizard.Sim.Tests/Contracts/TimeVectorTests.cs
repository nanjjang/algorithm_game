using Wizard.Core;
using Wizard.Sim.Input;
using Xunit;

namespace Wizard.Sim.Tests.Contracts
{
    // spec 01 2.1·14.1 T-C01~T-C03. 기대값은 명세의 표 그대로다
    public sealed class TimeVectorTests
    {
        [Theory]
        [InlineData(7L, 2, 1075L)]      // 0.07초 (섬)
        [InlineData(18L, 2, 2765L)]     // 0.18초 (만개)
        [InlineData(2L, 1, 3072L)]      // 0.2초
        [InlineData(25L, 2, 3840L)]     // 0.25초
        [InlineData(3L, 1, 4608L)]      // 0.3초
        [InlineData(8L, 2, 1229L)]      // 0.08초 (대각 시야 유지 창)
        public void S01_T_C01__SecondsToInputUnits(long mantissa, int scale, long expected)
        {
            Assert.Equal(expected, SimTime.InputUnitsFromDecimalSeconds(mantissa, scale));
        }

        [Theory]
        [InlineData(1L, 0, 60L)]
        [InlineData(2L, 1, 12L)]
        [InlineData(30L, 1, 180L)]      // 3.0초
        [InlineData(25L, 1, 150L)]
        [InlineData(35L, 1, 210L)]
        [InlineData(15L, 1, 90L)]
        [InlineData(20L, 1, 120L)]      // 2.0초
        [InlineData(15L, 2, 9L)]        // 0.15초
        [InlineData(6L, 1, 36L)]
        [InlineData(45L, 2, 27L)]
        [InlineData(35L, 2, 21L)]
        [InlineData(3L, 1, 18L)]
        [InlineData(5L, 1, 30L)]
        public void S01_T_C02__SecondsToTicks(long mantissa, int scale, long expected)
        {
            Assert.Equal(expected, SimTime.TicksFromDecimalSeconds(mantissa, scale));
        }

        [Fact]
        public void S01_T_C03__InputTimeOfTick60Offset128()
        {
            var e = new InputEvent(60, 128, LogicalKey.Up, true, 1);
            Assert.Equal(15488L, e.Time);
            Assert.Equal(15488L, SimTime.InputTime(60, 128));
            Assert.Equal(15488L, new QuantizedKey(60, 128, LogicalKey.Up, true).Time);
            // 표시용 실제 시각 ≈ 1,008.33ms: 15,488 / 15,360초 = 1,008,333.3…µs (정수로 검산한다. 실수 환산은 코어 밖)
            Assert.Equal(1008333L, 15488L * 1000000L / SimTime.InputUnitsPerSecond);
        }

        [Fact]
        public void TimeUnitConstants_MatchSpec01Section2_1()
        {
            Assert.Equal(60, SimVersion.TickRate);
            Assert.Equal(256, SimTime.OffsetsPerTick);
            Assert.Equal(15360, SimTime.InputUnitsPerSecond);
        }

        [Theory]
        [InlineData(1L, 3, 0L)]         // 0.001초 × 60 = 0.06 → 0
        [InlineData(25L, 3, 2L)]        // 0.025 × 60 = 1.5 → 2 (같으면 짝수)
        [InlineData(75L, 4, 0L)]        // 0.0075 × 60 = 0.45 → 0
        [InlineData(125L, 4, 1L)]       // 0.0125 × 60 = 0.75 → 1
        [InlineData(5L, 3, 0L)]         // 0.005 × 60 = 0.3 → 0
        [InlineData(-25L, 3, -2L)]      // −1.5 → −2 (같으면 짝수)
        [InlineData(-125L, 4, -1L)]     // −0.75 → −1
        [InlineData(417L, 4, 3L)]       // 0.0417 × 60 = 2.502 → 3
        [InlineData(75L, 3, 4L)]        // 0.075 × 60 = 4.5 → 4 (같으면 짝수)
        [InlineData(125L, 3, 8L)]       // 0.125 × 60 = 7.5 → 8 (같으면 짝수)
        [InlineData(1000000000L, 9, 60L)] // 1.000000000초
        public void TicksFromDecimalSeconds_RoundsNearestTiesToEven(long mantissa, int scale, long expected)
        {
            Assert.Equal(expected, SimTime.TicksFromDecimalSeconds(mantissa, scale));
        }

        [Fact]
        public void OutOfRangeArgumentsAreInvariantViolations()
        {
            Assert.Throws<SimInvariantException>(() => SimTime.TicksFromDecimalSeconds(1L, -1));
            Assert.Throws<SimInvariantException>(() => SimTime.TicksFromDecimalSeconds(1L, SimTime.MaxDecimalScale + 1));
            Assert.Throws<SimInvariantException>(() => SimTime.InputUnitsFromDecimalSeconds(long.MaxValue, 0));
        }
    }
}

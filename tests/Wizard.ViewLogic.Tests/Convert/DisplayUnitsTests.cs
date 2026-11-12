using System.Numerics;
using Wizard.Core;
using Wizard.ViewLogic.Convert;
using Xunit;

namespace Wizard.ViewLogic.Tests.Convert
{
    // 기대값은 명세의 단위 정의에서 손으로 셈한 것이다: 1칸 = 64px(spec 13 0절), 1초 = 60틱 = 15,360 입력 단위(spec 01 2.1)
    public sealed class DisplayUnitsTests
    {
        [Fact]
        public void PixelsToCells_UsesSixtyFourPixelsPerCell()
        {
            Assert.Equal(0.6875f, DisplayUnits.PxToCells(44f));      // 열 링 반지름 44px(effects 10)
            Assert.Equal(0.09375f, DisplayUnits.PxToCells(6f));
            Assert.Equal(1f, DisplayUnits.PxToCells(64f));
        }

        [Fact]
        public void InputUnitsToMilliseconds()
        {
            Assert.Equal(1000.0, DisplayUnits.InputUnitsToMilliseconds(15360));
            Assert.Equal(25.0, DisplayUnits.InputUnitsToMilliseconds(384));
            Assert.Equal(1000.0 / 60.0, DisplayUnits.InputUnitsToMilliseconds(256), 12);
            // 섬 창 0.07초 = round(0.07 × 15,360) = 1,075 단위(spec 03 1.2) → 69.986…ms → 70
            Assert.Equal(70L, DisplayUnits.RoundMilliseconds(DisplayUnits.InputUnitsToMilliseconds(1075)));
            Assert.Equal(-1000.0, DisplayUnits.InputUnitsToMilliseconds(-15360));
        }

        [Fact]
        public void TicksToMilliseconds()
        {
            Assert.Equal(1000.0, DisplayUnits.TicksToMilliseconds(60));
            Assert.Equal(400.0, DisplayUnits.TicksToMilliseconds(24));
        }

        [Fact]
        public void RoundMilliseconds_HalfAwayFromZero()
        {
            Assert.Equal(3L, DisplayUnits.RoundMilliseconds(2.5));
            Assert.Equal(-3L, DisplayUnits.RoundMilliseconds(-2.5));
            Assert.Equal(2L, DisplayUnits.RoundMilliseconds(2.49));
        }

        [Fact]
        public void DirectionsUseTheSameFlipAsPositions()
        {
            // 전장은 y 가 아래로 는다(N = 화면 위 = 전장 y 감소). 표시 좌표는 y 가 위로 는다
            Vector2 up = FixConvert.ToWorld(new FixVec2(Fix64.Zero, Fix64.FromInt(-1)));
            Assert.Equal(new Vector2(0f, 1f), up);
        }
    }
}

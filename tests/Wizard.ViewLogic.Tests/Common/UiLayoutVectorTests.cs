using Wizard.ViewLogic.Common;
using Xunit;

namespace Wizard.ViewLogic.Tests.Common
{
    // spec 14 7.7 TV-144~147, 4.3절 UiScale·UiOrigin 의사코드
    public sealed class UiLayoutVectorTests
    {
        [Fact]
        public void S14_TV_144__Window1920x1080Scale1()
        {
            Assert.Equal(1, UiLayout.UiScale(1920, 1080));
            PixelPoint o = UiLayout.UiOrigin(1920, 1080, 1);
            Assert.Equal(0, o.X);
            Assert.Equal(0, o.Y);
        }

        [Fact]
        public void S14_TV_145__Window2560x1440Scale1WithWiderMargins()
        {
            Assert.Equal(1, UiLayout.UiScale(2560, 1440));
            // 여백 증가: 1배 그림을 가운데에 두고 남는 자리는 여백((2560-1920)/2, (1440-1080)/2)
            PixelPoint o = UiLayout.UiOrigin(2560, 1440, 1);
            Assert.Equal(320, o.X);
            Assert.Equal(180, o.Y);
        }

        [Fact]
        public void S14_TV_146__Window3840x2160Scale2()
        {
            Assert.Equal(2, UiLayout.UiScale(3840, 2160));
            PixelPoint o = UiLayout.UiOrigin(3840, 2160, 2);
            Assert.Equal(0, o.X);
            Assert.Equal(0, o.Y);
        }

        [Fact]
        public void S14_TV_147__Window1600x900RequestIsRaisedTo1920x1080()
        {
            PixelSize s = UiLayout.ClampWindowSize(1600, 900);
            Assert.Equal(1920, s.Width);
            Assert.Equal(1080, s.Height);
            // 내려 그리지 않는다: 최소 창에서 배율은 1
            Assert.Equal(1, UiLayout.UiScale(s.Width, s.Height));
        }

        [Theory]
        [InlineData(3840, 1080, 1)]   // 한쪽만 크면 들어가는 정수 배는 1
        [InlineData(5760, 3240, 3)]
        [InlineData(1000, 500, 1)]    // 1보다 작아지지 않는다(max(1, s))
        public void UiScaleIsTheLargestIntegerMultipleThatFits(int w, int h, int expected)
        {
            Assert.Equal(expected, UiLayout.UiScale(w, h));
        }

        [Fact]
        public void LargerRequestsAreKept()
        {
            PixelSize s = UiLayout.ClampWindowSize(2560, 1440);
            Assert.Equal(2560, s.Width);
            Assert.Equal(1440, s.Height);
            PixelSize t = UiLayout.ClampWindowSize(1600, 1200);
            Assert.Equal(1920, t.Width);
            Assert.Equal(1200, t.Height);
        }

        [Fact]
        public void GridRule()
        {
            Assert.True(UiLayout.IsOnGrid(48));
            Assert.False(UiLayout.IsOnGrid(50));
        }
    }
}

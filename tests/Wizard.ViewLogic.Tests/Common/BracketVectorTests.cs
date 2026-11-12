using Wizard.ViewLogic.Common;
using Xunit;

namespace Wizard.ViewLogic.Tests.Common
{
    // spec 14 7.7 TV-140~143·153, spec 13 TV-243. 기대값은 명세 표 그대로다
    public sealed class BracketVectorTests
    {
        [Fact]
        public void S14_TV_140__Target64x64IsSmallArm12Sprite16()
        {
            Assert.Equal(BracketSize.Small, Brackets.SizeFor(64, 64));
            BracketMetrics m = Brackets.MetricsOf(BracketSize.Small);
            Assert.Equal(12, m.ArmPx);
            Assert.Equal(16, m.SpritePx);
        }

        [Fact]
        public void S14_TV_141__Target65x40IsMediumArm20Sprite24()
        {
            Assert.Equal(BracketSize.Medium, Brackets.SizeFor(65, 40));
            BracketMetrics m = Brackets.MetricsOf(BracketSize.Medium);
            Assert.Equal(20, m.ArmPx);
            Assert.Equal(24, m.SpritePx);
        }

        [Fact]
        public void S14_TV_142__Target240x336IsLargeByLongEdgeArm28Sprite32()
        {
            Assert.Equal(BracketSize.Large, Brackets.SizeFor(240, 336));
            BracketMetrics m = Brackets.MetricsOf(BracketSize.Large);
            Assert.Equal(28, m.ArmPx);
            Assert.Equal(32, m.SpritePx);
        }

        [Fact]
        public void S14_TV_143__Target240x100IsMedium()
        {
            Assert.Equal(BracketSize.Medium, Brackets.SizeFor(240, 100));
        }

        [Theory]
        [InlineData(64, 64, BracketSize.Small)]
        [InlineData(65, 40, BracketSize.Medium)]
        [InlineData(240, 336, BracketSize.Large)]
        [InlineData(336, 376, BracketSize.Large)]
        public void S13_TV_243__BracketSizeTable(int w, int h, BracketSize expected)
        {
            Assert.Equal(expected, Brackets.SizeFor(w, h));
        }

        [Fact]
        public void S14_TV_153__CursorPulsePeriod30TicksTwoFramesOf15TicksOffsetsMinus4ThenMinus6()
        {
            Assert.Equal(30, Brackets.PulsePeriodTicks);
            Assert.Equal(2, Brackets.PulseFrames);
            Assert.Equal(250, Brackets.PulseFrameMs);
            Assert.Equal(15, Brackets.PulseFrameTicks);

            // 0~14t 는 첫 프레임 [-4,-4], 15~29t 는 둘째 [-6,-6], 30t 에 처음으로 돌아온다
            Assert.Equal(0, Brackets.PulseFrame(0));
            Assert.Equal(0, Brackets.PulseFrame(14));
            Assert.Equal(1, Brackets.PulseFrame(15));
            Assert.Equal(1, Brackets.PulseFrame(29));
            Assert.Equal(0, Brackets.PulseFrame(30));
            Assert.Equal(1, Brackets.PulseFrame(45));

            PixelPoint f0 = Brackets.PulseOffset(0);
            PixelPoint f1 = Brackets.PulseOffset(1);
            Assert.Equal(-4, f0.X);
            Assert.Equal(-4, f0.Y);
            Assert.Equal(-6, f1.X);
            Assert.Equal(-6, f1.Y);
        }

        [Fact]
        public void PulseFollowsTheGroupsUiTicksAndRestartsOnCursorMove()
        {
            var mode = new InputModeTracker();
            using var g = new InteractableGroup(mode);
            Interactable a = g.Add(new Interactable("a", 64, 64));
            Interactable b = g.Add(new Interactable("b", 64, 64));
            g.MoveCursor(a);
            Assert.Equal(0, g.CursorPulseFrame);
            for (int i = 0; i < 15; i++)
            {
                g.Tick();
            }
            Assert.Equal(1, g.CursorPulseFrame);
            g.MoveCursor(b);
            Assert.Equal(0, g.CursorPulseFrame);
        }

        [Fact]
        public void EdgeBoundaries()
        {
            Assert.Equal(BracketSize.Small, Brackets.SizeFor(1, 64));
            Assert.Equal(BracketSize.Medium, Brackets.SizeFor(1, 65));
            Assert.Equal(BracketSize.Medium, Brackets.SizeFor(240, 240));
            Assert.Equal(BracketSize.Large, Brackets.SizeFor(241, 1));
        }
    }
}

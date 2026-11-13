using Wizard.Core;
using Wizard.ViewLogic.Presentation;
using Xunit;

namespace Wizard.ViewLogic.Tests.Presentation.Bindings
{
    // CONTRACTS 0.3 T-50: 연속 스침의 음높이(chainIndex 0~5)는 표시 계층이 센다
    public sealed class GrazeChainCounterTests
    {
        private static readonly PlayerSlot P0 = new PlayerSlot(0);

        [Fact]
        public void WindowBoundaryIsInclusive()
        {
            var c = new GrazeChainCounter(new GrazeChainSettings(120, 5));
            Assert.Equal(0, c.Next(P0, 1000));
            Assert.Equal(1, c.Next(P0, 1120));          // 정확히 2초
            Assert.Equal(0, c.Next(P0, 1241));          // 2초 + 1틱
        }

        [Fact]
        public void SameTickCountsAsConsecutive()
        {
            // 같은 틱에 여러 시전이 인정되면 GrazeAwarded 가 여러 개 나온다(CONTRACTS 3.3). 차가 0이므로 이어진 것
            var c = new GrazeChainCounter(new GrazeChainSettings(120, 5));
            Assert.Equal(0, c.Next(P0, 50));
            Assert.Equal(1, c.Next(P0, 50));
            Assert.Equal(2, c.Next(P0, 50));
        }

        [Fact]
        public void TickGoingBackRestarts()
        {
            var c = new GrazeChainCounter(new GrazeChainSettings(120, 5));
            c.Next(P0, 500);
            Assert.Equal(1, c.Next(P0, 510));
            Assert.Equal(0, c.Next(P0, 10));            // 새 판
            Assert.Equal(0, c.Current(new PlayerSlot(2)));
        }

        [Fact]
        public void MaxStepComesFromSettings()
        {
            var c = new GrazeChainCounter(new GrazeChainSettings(60, 2));
            Assert.Equal(0, c.Next(P0, 0));
            Assert.Equal(1, c.Next(P0, 60));
            Assert.Equal(2, c.Next(P0, 120));
            Assert.Equal(2, c.Next(P0, 180));
            Assert.Equal(2, c.Current(P0));
        }
    }
}

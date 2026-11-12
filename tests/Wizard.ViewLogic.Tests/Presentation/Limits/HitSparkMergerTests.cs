using Wizard.ViewLogic.Presentation.Limits;
using Xunit;

namespace Wizard.ViewLogic.Tests.Presentation.Limits
{
    // effects.md 9.3 분류 2: 같은 적에게 0.05초 안에 3번 넘게 맞으면 큰 불꽃 하나(입자 8)로 합친다
    public sealed class HitSparkMergerTests
    {
        [Fact]
        public void FourthHitWithin50msBecomesOneBigSpark()
        {
            var m = new HitSparkMerger(ParticleBudgetJson.Table);
            Assert.Equal(HitSparkKind.Normal, m.OnHit(1, 0));
            Assert.Equal(HitSparkKind.Normal, m.OnHit(1, 10));
            Assert.Equal(HitSparkKind.Normal, m.OnHit(1, 20));
            Assert.Equal(HitSparkKind.MergedBig, m.OnHit(1, 30));
            Assert.Equal(HitSparkKind.Absorbed, m.OnHit(1, 40));
            Assert.Equal(HitSparkKind.Normal, m.OnHit(2, 40));          // 다른 적은 따로 센다
        }

        [Fact]
        public void HitsSpreadOverMoreThan50msStayNormal()
        {
            var m = new HitSparkMerger(ParticleBudgetJson.Table);
            for (int i = 0; i < 20; i++)
            {
                Assert.Equal(HitSparkKind.Normal, m.OnHit(1, i * 20));  // 창 (t−50, t] 안에는 많아야 3번
            }
            m.Prune(10_000);
            Assert.Equal(0, m.TrackedTargets);
        }
    }
}

using Wizard.ViewLogic.Presentation.Limits;
using Xunit;

namespace Wizard.ViewLogic.Tests.Presentation.Limits
{
    // effects.md 9.3 "넘쳤을 때" 6번: 피해 숫자는 화면에 40개까지, 같은 적에게 0.2초 안의 피해는 한 숫자로
    public sealed class DamageNumberLimiterTests
    {
        [Fact]
        public void SameTargetWithin200msMerges()
        {
            var d = new DamageNumberLimiter(ParticleBudgetJson.Table);
            DamageNumberOp a = d.Submit(1, 10, 0, 800);
            DamageNumberOp b = d.Submit(1, 5, 199, 800);
            Assert.Equal(DamageNumberOpKind.New, a.Kind);
            Assert.Equal(DamageNumberOpKind.Merged, b.Kind);
            Assert.Equal(a.Id, b.Id);
            Assert.Equal(15, b.Amount);
            DamageNumberOp c = d.Submit(1, 5, 200, 800);
            Assert.Equal(DamageNumberOpKind.New, c.Kind);
            Assert.Equal(2, d.LiveCount);
        }

        [Fact]
        public void AtMost40OnScreenAndTheOldestGoesFirst()
        {
            var d = new DamageNumberLimiter(ParticleBudgetJson.Table);
            long firstId = d.Submit(0, 1, 0, 5000).Id;
            for (int i = 1; i < 40; i++)
            {
                Assert.Equal(0, d.Submit(i, 1, 0, 5000).EvictedId);
            }
            Assert.Equal(40, d.LiveCount);
            DamageNumberOp over = d.Submit(100, 1, 1, 5000);
            Assert.Equal(firstId, over.EvictedId);
            Assert.Equal(40, d.LiveCount);
            d.Expire(5001);
            Assert.Equal(0, d.LiveCount);
        }
    }
}

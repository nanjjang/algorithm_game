using Wizard.Core;
using Wizard.Rules;
using Wizard.Rules.Enhance;
using Xunit;

namespace Wizard.Rules.Tests
{
    // CONTRACTS 4.1·0.2: 규칙의 거절은 예외가 아니라 RuleCheck 다.
    public sealed class RuleCheckTests
    {
        [Fact]
        public void PassAndFail()
        {
            Assert.True(RuleCheck.Pass.Ok);
            Assert.Equal(ErrorCode.None, RuleCheck.Pass.Code);
            RuleCheck fail = RuleCheck.Fail(ErrorCode.MaxStage);
            Assert.False(fail.Ok);
            Assert.Equal(ErrorCode.MaxStage, fail.Code);
        }

        [Fact]
        public void RequestDefaultsAreEmptyNotNull()
        {
            var request = new EnhanceRequest();
            Assert.Empty(request.Sacrifices);
            Assert.Equal(0L, new InventoryView().Stack("mat.stone_low").Qty);
        }
    }
}

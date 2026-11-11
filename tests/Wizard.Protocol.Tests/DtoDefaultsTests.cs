using Wizard.Protocol.Http;
using Xunit;

namespace Wizard.Protocol.Tests
{
    // CONTRACTS 8.2. DTO 는 get; set; 클래스이고 컬렉션은 null 이 아니다(T-20)
    public sealed class DtoDefaultsTests
    {
        [Fact]
        public void WandViewDefaults()
        {
            var w = new WandView();
            Assert.Equal("start", w.Origin);
            Assert.Equal("none", w.LockState);
            Assert.NotNull(w.Loadout);
            Assert.Empty(w.Mutations);
        }

        [Fact]
        public void ProfileAndEnhanceDefaults()
        {
            Assert.Empty(new ProfileView().Characters);
            Assert.Empty(new ProfileView().TradeMissing);
            var e = new EnhanceResultDto();
            Assert.NotNull(e.Wand);
            Assert.NotNull(e.Breakdown);
            Assert.NotNull(e.Cost);
            Assert.Equal(1, new RunStartRequest().StartWave);
        }

        [Fact]
        public void PurchaseFlowCodes()
        {
            Assert.Equal(0, (int)PurchaseFlow.ClientReceipt);
            Assert.Equal(2, (int)PurchaseFlow.RedeemCode);
        }
    }
}

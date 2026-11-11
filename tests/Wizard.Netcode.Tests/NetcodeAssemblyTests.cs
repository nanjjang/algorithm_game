using System.Linq;
using Wizard.Netcode.Duel;
using Xunit;

namespace Wizard.Netcode.Tests
{
    // ARCHITECTURE P8, T-19: 넷코드는 소켓과 시계가 없는 순수 클래스다(분석기 WZ0003·WZ0019 와 같이 본다).
    public sealed class NetcodeAssemblyTests
    {
        [Fact]
        public void DoesNotReferenceSocketsOrClient()
        {
            string[] refs = typeof(EventPhase).Assembly.GetReferencedAssemblies().Select(a => a.Name!).ToArray();
            Assert.DoesNotContain("System.Net.Sockets", refs);
            Assert.DoesNotContain("Wizard.Client", refs);
        }

        [Fact]
        public void EventPhaseCodes()
        {
            Assert.Equal(0, (int)EventPhase.Predicted);
            Assert.Equal(1, (int)EventPhase.Confirmed);
            Assert.Equal(2, (int)EventPhase.Retracted);
            var adv = new DuelAdvance(true, 3, 2, 108, 100);
            Assert.True(adv.Stalled);
            Assert.Equal(108u, adv.PredictedTick);
        }
    }
}

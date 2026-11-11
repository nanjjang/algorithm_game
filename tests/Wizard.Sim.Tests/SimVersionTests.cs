using System.Linq;
using Xunit;

namespace Wizard.Sim.Tests
{
    // CONTRACTS 3.2, T-18. CoreVersion 은 VersionStamp 가 만든 hex 32자다. 맞는지는 tools/ci/gen-check.sh 가 본다.
    public sealed class SimVersionTests
    {
        [Fact]
        public void CoreVersion_IsLowercaseHex32()
        {
            Assert.Equal(32, SimVersion.CoreVersion.Length);
            Assert.True(SimVersion.CoreVersion.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')));
            Assert.Equal(60, SimVersion.TickRate);
            Assert.Equal(1, SimVersion.FactsFormat);
        }
    }
}

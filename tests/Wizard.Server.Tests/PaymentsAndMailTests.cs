using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wizard.Server.App.Mail;
using Wizard.Server.App.Payments;
using Xunit;

namespace Wizard.Server.Tests
{
    // CONTRACTS 7.1, ARCHITECTURE P11·T-43. PostgreSQL 을 쓰는 통합 테스트는 M10-DB-01 이 tools/ci/pg-test.sh 와 같이 더한다.
    public sealed class PaymentsAndMailTests
    {
        [Fact]
        public async Task NullPaymentProvider_IsAlwaysDisabled()
        {
            var p = new NullPaymentProvider();
            Assert.False(p.IsEnabled);
            Assert.Equal("none", p.Id);
            VerifyResult r = await p.VerifyAsync(new PurchaseOrder(), "proof");
            Assert.False(r.Ok);
            Assert.Empty(await p.PollAsync(System.DateTime.UtcNow));
        }

        [Fact]
        public async Task NullMailSender_DoesNotAccept()
        {
            var m = new NullMailSender();
            Assert.False(m.IsEnabled);
            MailSendResult r = await m.SendAsync("a@b.c", MailKind.ResetPassword, new MailBody(), CancellationToken.None);
            Assert.False(r.Accepted);
        }

        [Fact]
        public void ApiDoesNotReferencePaymentFakes()
        {
            var api = typeof(Wizard.Server.Api.Program).Assembly;
            Assert.DoesNotContain(api.GetReferencedAssemblies(), a => a.Name == "Wizard.Server.Payments.Fakes");
        }
    }
}

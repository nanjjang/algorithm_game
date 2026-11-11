using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Wizard.Protocol;

namespace Wizard.Server.App.Payments
{
    // CONTRACTS 7.1. 언제나 꺼져 있다. 결제를 연결하지 않아도 모든 기능이 돈다(DECISIONS 1.14)
    public sealed class NullPaymentProvider : IPaymentProvider
    {
        public string Id => "none";

        public PurchaseFlow Flow => PurchaseFlow.ServerOrder;

        public bool IsEnabled => false;

        public Task<ProviderOrder> CreateAsync(PurchaseOrder order, Product product) =>
            Task.FromResult(new ProviderOrder(null, null, null));

        public Task<VerifyResult> VerifyAsync(PurchaseOrder order, string? proof) =>
            Task.FromResult(new VerifyResult(false, true, null, "payments_disabled"));

        public bool TryParseWebhook(HttpRequest request, [MaybeNullWhen(false)] out ProviderEvent providerEvent)
        {
            providerEvent = null;
            return false;
        }

        public Task<IReadOnlyList<ProviderEvent>> PollAsync(DateTime since) =>
            Task.FromResult<IReadOnlyList<ProviderEvent>>(Array.Empty<ProviderEvent>());
    }
}

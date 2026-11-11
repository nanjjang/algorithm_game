using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Wizard.Protocol;

namespace Wizard.Server.App.Payments
{
    // CONTRACTS 7.1. 결제는 빈 자리다(ARCHITECTURE P11). 출시 서버에는 NullPaymentProvider 하나만 등록한다
    public interface IPaymentProvider
    {
        string Id { get; }                                // "none", "fake-receipt", "fake-order", 나중에 "pg-…"
        PurchaseFlow Flow { get; }
        bool IsEnabled { get; }                           // NullPaymentProvider 는 항상 false
        Task<ProviderOrder> CreateAsync(PurchaseOrder order, Product product);
        Task<VerifyResult> VerifyAsync(PurchaseOrder order, string? proof);   // 제공자 서버에 직접 확인
        bool TryParseWebhook(HttpRequest request, [MaybeNullWhen(false)] out ProviderEvent providerEvent);
        Task<IReadOnlyList<ProviderEvent>> PollAsync(DateTime since);
    }
}

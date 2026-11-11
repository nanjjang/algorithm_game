using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wizard.Content;
using Wizard.Core;
using Wizard.Protocol.Http;

namespace Wizard.Client
{
    // CONTRACTS 6.1. 변경 요청의 requestId 는 화면이 동작 하나에 한 번 만들고, 다시 보낼 때 같은 값을 쓴다
    public interface IMarketService
    {
        Task<Result<MarketPage>> SearchAsync(MarketQuery query, CancellationToken ct = default);
        Task<Result<PriceStat>> GetPriceAsync(string itemKey, CancellationToken ct = default);
        Task<Result<ListingReceipt>> ListAsync(Guid requestId, MarketListRequest request, CancellationToken ct = default);
        Task<Result<PurchaseReceipt>> BuyAsync(Guid requestId, long listingId, long qty, long expectedUnitPrice, CancellationToken ct = default);
        Task<Result<Unit>> CancelAsync(Guid requestId, long listingId, CancellationToken ct = default);
    }
}

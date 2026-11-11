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
    public interface ISeasonService
    {
        Task<Result<SeasonView>> GetCurrentAsync(CancellationToken ct = default);
        Task<Result<HeirEstimate>> GetHeirEstimateAsync(CancellationToken ct = default);
        Task<Result<SeasonResultView>> GetMyResultAsync(long seasonId, CancellationToken ct = default);
        Task<Result<SeasonShopView>> GetShopAsync(CancellationToken ct = default);                                  // 시즌 상점(시즌 주화로 산다)
        Task<Result<BuyResult>> BuyAsync(Guid requestId, string sku, int qty, CancellationToken ct = default);
        Task<Result<ClaimResult>> ClaimRewardAsync(Guid requestId, long seasonId, CancellationToken ct = default);  // 보상은 우편으로 가 있고 이 호출은 그 우편을 받는다
    }
}

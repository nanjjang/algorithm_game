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
    public interface IWandService
    {
        Task<Result<WandView>> SetLoadoutAsync(Guid requestId, long wandId, LoadoutDto loadout, CancellationToken ct = default);
        Task<Result<WandView>> ClearLoadoutAsync(Guid requestId, long wandId, CancellationToken ct = default);
        Task<Result<WandView>> SetScarVisibilityAsync(Guid requestId, long wandId, bool show, CancellationToken ct = default);
        Task<Result<WandView>> SetSkinAsync(Guid requestId, long wandId, string? skinId, CancellationToken ct = default);
        Task<Result<ReleaseResult>> ReleaseAsync(Guid requestId, IReadOnlyList<long> wandIds, string? typedName, CancellationToken ct = default);
    }
}

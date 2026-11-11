using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wizard.Content;
using Wizard.Core;
using Wizard.Protocol.Http;
using Wizard.Rules.Enhance;

namespace Wizard.Client
{
    // CONTRACTS 6.1. 변경 요청의 requestId 는 화면이 동작 하나에 한 번 만들고, 다시 보낼 때 같은 값을 쓴다
    public interface IEnhanceService
    {
        Task<Result<EnhanceResultDto>> EnhanceAsync(Guid requestId, long wandId, IReadOnlyList<SacrificeDto> sacrifices, bool useCharm, CancellationToken ct = default);
        Task<Result<EnhanceResultDto>> ResolveMutationAsync(Guid requestId, long wandId, Guid attemptId, MutationDecision decision, CancellationToken ct = default);
        Task<Result<WandView>> RemoveMutationAsync(Guid requestId, long wandId, MutationId mutation, CancellationToken ct = default);
        Task<Result<CarveResultDto>> CarveSlotAsync(Guid requestId, long wandId, CancellationToken ct = default);
    }
}

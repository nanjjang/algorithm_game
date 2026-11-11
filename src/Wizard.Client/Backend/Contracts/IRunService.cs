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
    public interface IRunService
    {
        Task<Result<RunStartTicket>> StartAsync(Guid requestId, RunStartRequest request, CancellationToken ct = default);
        Task<Result<Unit>> SubmitCheckpointAsync(Guid runId, RunCheckpointDto checkpoint, ReadOnlyMemory<byte> recordSoFar, CancellationToken ct = default);
        Task<Result<RunSettlementView>> FinishAsync(Guid runId, RunSummaryDto summary, ReadOnlyMemory<byte> record, CancellationToken ct = default);
        Task<Result<RunSettlementView>> TidyAsync(Guid runId, CancellationToken ct = default);                        // 원정 정리
        Task<Result<IReadOnlyList<RunStatusView>>> ListOpenAsync(CancellationToken ct = default);                      // 미정산 판
        Task<Result<RunStatusView>> GetAsync(Guid runId, CancellationToken ct = default);
        Task<Result<WeeklyInfo>> GetWeeklyAsync(CancellationToken ct = default);                                       // 오프라인: 게임에 든 씨앗 목록에서
    }
}

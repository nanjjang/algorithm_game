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
    public interface ISocialService
    {
        Task<Result<FriendListView>> GetFriendsAsync(CancellationToken ct = default);
        Task<Result<Unit>> RequestFriendAsync(Guid requestId, long accountId, CancellationToken ct = default);
        Task<Result<Unit>> RespondAsync(Guid incomingRequestId, bool accept, CancellationToken ct = default);   // 친구·거래·도전 공용
        Task<Result<Unit>> RemoveFriendAsync(Guid requestId, long accountId, CancellationToken ct = default);
        Task<Result<IReadOnlyList<RecentMetView>>> GetRecentAsync(CancellationToken ct = default);
        Task<Result<Unit>> BlockAsync(Guid requestId, long accountId, bool block, CancellationToken ct = default);
        Task<Result<InspectView>> InspectAsync(long accountId, CancellationToken ct = default);
        Task<Result<long>> ReportAsync(Guid requestId, ReportRequest report, CancellationToken ct = default);
        event Action<IncomingRequest> RequestReceived;
        event Action<FriendPresence> PresenceChanged;
    }
}

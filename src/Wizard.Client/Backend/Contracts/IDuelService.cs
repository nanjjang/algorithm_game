using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wizard.Content;
using Wizard.Core;
using Wizard.Protocol.Http;
using Wizard.Sim.Duel;

namespace Wizard.Client
{
    // CONTRACTS 6.1. 변경 요청의 requestId 는 화면이 동작 하나에 한 번 만들고, 다시 보낼 때 같은 값을 쓴다
    public interface IDuelService
    {
        Task<Result<DuelStatusView>> GetStatusAsync(CancellationToken ct = default);          // 리그, IL, 점수, 대기열 조건
        Task<Result<QueueTicket>> EnqueueAsync(Guid requestId, DuelEntrySelection selection, CancellationToken ct = default);   // 서버가 회선을 잰 뒤 QueueChanged 로 알린다
        void Dequeue();
        void AcceptMatch(Guid matchId);                                     // duel.match_accept. accept_by_ms 안에
        void SelectPrep(Guid matchId, DuelEntrySelection selection, bool locked);   // duel.prep_select
        Task<Result<IReadOnlyList<RoomSummary>>> ListRoomsAsync(CancellationToken ct = default);
        Task<Result<RoomState>> CreateRoomAsync(Guid requestId, DuelRoomOptions options, CancellationToken ct = default);
        Task<Result<RoomState>> JoinRoomAsync(long roomId, string? password, bool spectate, CancellationToken ct = default);
        void SetRoomOptions(long roomId, DuelRoomOptions options);          // duel.room_options. 방장만
        void SetReady(long roomId, bool ready, DuelEntrySelection selection);
        void LeaveRoom(long roomId);
        Task<Result<Unit>> ChallengeAsync(long targetAccountId, bool applyLeagueRules, CancellationToken ct = default);
        void Forfeit(Guid matchId);                                         // duel.forfeit
        Task<Result<Unit>> RejoinAsync(Guid matchId, CancellationToken ct = default);                    // duel.rejoin. 답은 MatchStarting 으로 다시 온다(새 키, epoch+1)
        void StartSpectate(long roomId);
        void StopSpectate(long roomId);
        Task<Result<Unit>> PromoteLeagueAsync(Guid requestId, LeagueId target, CancellationToken ct = default);
        Task<Result<DuelResultView>> GetResultAsync(Guid matchId, CancellationToken ct = default);
        event Action<DuelMatchFound> MatchFound;          // 상대, accept_by_ms
        event Action<DuelPrepState> PrepChanged;          // 양쪽의 고른 상태와 남은 시간
        event Action<DuelMatchStart> MatchStarting;       // 설정, 키, epoch, 주소, (재접속이면) 이어 갈 틱·순번·키 상태
        event Action<RoomState> RoomChanged;
        event Action<QueueStatus> QueueChanged;
    }
}

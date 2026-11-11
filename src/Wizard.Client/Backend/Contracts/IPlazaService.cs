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
    public interface IPlazaService
    {
        Task<Result<PlazaWelcome>> JoinAsync(PlazaJoinRequest request, CancellationToken ct = default);
        void Leave();
        void SendInput(PlazaInput input);                 // 10틱마다. 응답 없음
        void SendEmote(string emoteId);
        void SetActivity(PlazaActivity activity);
        Task<Result<Unit>> InteractAsync(PlazaTarget target, CancellationToken ct = default);
        event Action<PlazaSnapshot> SnapshotReceived;
        event Action<PlazaMemberEvent> MemberChanged;
    }
}

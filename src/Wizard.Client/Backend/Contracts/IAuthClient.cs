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
    public interface IAuthClient
    {
        Task<Result<BootStatus>> BootAsync(CancellationToken ct = default);                    // 상태 파일 → 버전 → /healthz, /v1/features
        Task<Result<IOnlineBackend>> SignupAsync(SignupRequest request, CancellationToken ct = default);
        Task<Result<IOnlineBackend>> LoginAsync(LoginRequest request, CancellationToken ct = default);
        Task<Result<IOnlineBackend>> ResumeAsync(CancellationToken ct = default);                             // 저장된 갱신 토큰으로
        Task<Result<Unit>> RequestRecoveryAsync(string loginId, string email, CancellationToken ct = default);
        Task<Result<Unit>> FindIdsAsync(string email, CancellationToken ct = default);
        Task<Result<Unit>> ConfirmRecoveryAsync(string loginId, string code, string newPassword, CancellationToken ct = default);
    }
}

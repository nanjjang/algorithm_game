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
    public interface IAccountService
    {
        Task<Result<Unit>> SendEmailCodeAsync(CancellationToken ct = default);
        Task<Result<Unit>> VerifyEmailAsync(string code, CancellationToken ct = default);
        Task<Result<Unit>> ChangePasswordAsync(string current, string next, CancellationToken ct = default);
        Task<Result<Unit>> RequestEmailChangeAsync(string currentPassword, string newEmail, CancellationToken ct = default);
        Task<Result<Unit>> ConfirmEmailChangeAsync(string code, CancellationToken ct = default);
        Task<Result<Unit>> RequestDeletionAsync(string currentPassword, CancellationToken ct = default);
        Task<Result<Unit>> CancelDeletionAsync(CancellationToken ct = default);
        Task<Result<ProfileView>> ChangeNicknameAsync(Guid requestId, string nickname, CancellationToken ct = default);   // 골드 상점의 item.rename. 값은 Rules.Economy
        Task<Result<Unit>> LogoutAsync(CancellationToken ct = default);
    }
}

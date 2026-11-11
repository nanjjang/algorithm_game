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
    public interface IProfileService
    {
        Task<Result<ProfileView>> GetAsync(CancellationToken ct = default);
        Task<Result<ProfileView>> SetEquippedWandAsync(Guid requestId, CharacterId character, long wandId, CancellationToken ct = default);
        Task<Result<ProfileView>> SetTitleAsync(Guid requestId, string? titleId, CancellationToken ct = default);
        Task<Result<ProfileView>> SetCosmeticsAsync(Guid requestId, CharacterId character, CosmeticLoadout loadout, CancellationToken ct = default);
    }
}

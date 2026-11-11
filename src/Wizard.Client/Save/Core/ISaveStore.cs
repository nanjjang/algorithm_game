using System;
using System.Threading;
using System.Threading.Tasks;
using Wizard.Core;

namespace Wizard.Client.Save
{
    // CONTRACTS 6.2. 임시 파일에 쓰고 이름을 바꾼다. 직전 3세대 보관
    public interface ISaveStore
    {
        Task<Result<SaveLoad>> LoadAsync(string name, CancellationToken ct = default);     // 깨졌으면 직전 세대 + Restored 표시
        Task<Result<Unit>> SaveAsync(string name, ReadOnlyMemory<byte> utf8Json, CancellationToken ct = default);
    }
}

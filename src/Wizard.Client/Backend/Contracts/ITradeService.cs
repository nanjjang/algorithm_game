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
    public interface ITradeService
    {
        Task<Result<Unit>> RequestAsync(long targetAccountId, CancellationToken ct = default);
        void SetSlot(long tradeId, int slotIndex, TradeSlot? item);
        void SetGold(long tradeId, long gold);
        void SetLocked(long tradeId, bool locked);
        Task<Result<TradeResult>> ConfirmAsync(Guid requestId, long tradeId, int revision, bool lossWarningAck, CancellationToken ct = default);
        void Cancel(long tradeId);
        event Action<TradeState> StateChanged;
    }
}

using System;
using Wizard.Protocol.Http;

namespace Wizard.Client
{
    // CONTRACTS 6.1. Kind == Server 일 때만 존재한다. OfflineBackend 는 구현하지 않는다
    public interface IOnlineBackend : IBackend
    {
        ConnectionState Connection { get; }
        event Action<ConnectionState> ConnectionChanged;
        event Action<ServerNotice> NoticeReceived;        // +10 이상 성공, +13, 운영 공지
        IAccountService Account { get; }
        IPlazaService Plaza { get; }
        IChatService Chat { get; }
        ISocialService Social { get; }
        ITradeService Trade { get; }
        IMarketService Market { get; }
        IMailService Mail { get; }
        IDuelService Duel { get; }
        IBoardService Boards { get; }
        ISeasonService Season { get; }
    }
}

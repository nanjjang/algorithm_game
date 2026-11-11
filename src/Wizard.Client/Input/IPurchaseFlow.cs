using System.Threading.Tasks;
using Wizard.Protocol;
using Wizard.Protocol.Http;

namespace Wizard.Client
{
    // CONTRACTS 6.5. 출시 빌드에는 구현을 등록하지 않는다(spec 12 10.2, DECISIONS 1.14)
    public interface IPurchaseFlow
    {
        PurchaseFlow Flow { get; }
        Task<string?> AuthorizeAsync(OrderTicket ticket); // 영수증형: 영수증 / 주문형: 창이 닫힐 때까지 기다린 뒤 null / 코드형: 입력한 코드
    }
}

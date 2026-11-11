using System.Threading.Tasks;
using Wizard.Content;

namespace Wizard.Server.Data
{
    // CONTRACTS 7.2. 재화와 물건의 유일한 변경 통로
    public interface ILedger
    {
        Task<long> AddCurrencyAsync(IUnitOfWork uow, long accountId, CurrencyCode currency, CurrencyBucket bucket, long delta, bool pending, LedgerRef reference);
        Task AddItemAsync(IUnitOfWork uow, long accountId, string itemRef, long delta, bool pending, LedgerRef reference);
        Task SpendStarlightAsync(IUnitOfWork uow, long accountId, long amount, LedgerRef reference);   // 무상부터
    }
}

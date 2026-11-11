using System.Threading.Tasks;

namespace Wizard.Server.Data
{
    // CONTRACTS 7.2
    public interface IRowLocks
    {
        Task LockAccountsAsync(IUnitOfWork uow, params long[] accountIds);   // 여러 계정은 번호 오름차순
        Task<WandRow> LockWandAsync(IUnitOfWork uow, long wandId);
    }
}

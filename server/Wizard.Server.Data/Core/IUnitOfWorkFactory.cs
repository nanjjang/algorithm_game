using System.Threading;
using System.Threading.Tasks;

namespace Wizard.Server.Data
{
    // CONTRACTS 7.2
    public interface IUnitOfWorkFactory
    {
        Task<IUnitOfWork> BeginAsync(CancellationToken ct);
    }
}

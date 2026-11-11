using System.Threading;
using System.Threading.Tasks;

namespace Wizard.Client.Net
{
    // CONTRACTS 6.2
    public interface IHttpTransport
    {
        Task<HttpReply> SendAsync(HttpCall call, CancellationToken ct);
    }
}

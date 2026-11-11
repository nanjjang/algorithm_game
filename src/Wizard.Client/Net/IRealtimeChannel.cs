using System;
using System.Threading;
using System.Threading.Tasks;
using Wizard.Core;

namespace Wizard.Client.Net
{
    // CONTRACTS 6.2. 웹소켓 하나
    public interface IRealtimeChannel
    {
        Task<Result<Unit>> ConnectAsync(Uri uri, string accessToken, CancellationToken ct);
        void SendText(string json);
        void SendBinary(ReadOnlySpan<byte> frame);
        event Action<string> TextReceived;
        event Action<ReadOnlyMemory<byte>> BinaryReceived;
        event Action<ErrorCode> Closed;
    }
}

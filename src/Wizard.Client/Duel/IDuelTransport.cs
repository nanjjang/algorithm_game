using System;

namespace Wizard.Client.Duel
{
    // CONTRACTS 6.3. 구현: UdpDuelTransport, WebSocketDuelTransport, LoopbackTransport
    public interface IDuelTransport : IDisposable
    {
        DuelTransportKind Kind { get; }
        void Send(ReadOnlySpan<byte> packet);             // 10절의 패킷 하나. 막지 않는다
        int Receive(Span<byte> buffer);                   // 받은 패킷 길이. 없으면 0. 막지 않는다
    }
}

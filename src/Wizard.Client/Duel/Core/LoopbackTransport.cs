using System;

namespace Wizard.Client.Duel
{
    // CONTRACTS 6.3. 기기 안의 DuelHostCore(LoopbackHost)에 직접 붙는 전송. 한 기기 2인과 봇 연습이 쓴다.
    // 싣는 것은 10절의 패킷 바이트 그대로다(UDP·웹소켓과 같은 봉인, 같은 키, 같은 번호). 막지 않는다.
    // 보낸 패킷이 닿는 시각 = 호스트의 지금(마지막 Pump) + 올라가는 지연. 받는 것은 닿는 시각이 호스트의 지금 이하인 것뿐이다.
    // 자동 매치에는 들어갈 수 없다(Kind == Loopback, CONTRACTS 6.3).
    public sealed class LoopbackTransport : IDuelTransport
    {
        private LoopbackHost? _host;
        private readonly int _slot;

        internal LoopbackTransport(LoopbackHost host, int slot)
        {
            _host = host;
            _slot = slot;
        }

        public DuelTransportKind Kind => DuelTransportKind.Loopback;

        // 닫힌 뒤에는 아무것도 하지 않는다(망이 끊긴 것과 같다)
        public void Send(ReadOnlySpan<byte> packet) => _host?.FromClient(_slot, packet);

        public int Receive(Span<byte> buffer) => _host == null ? 0 : _host.ToClient(_slot, buffer);

        public void Dispose()
        {
            _host?.Detach(_slot);
            _host = null;
        }
    }
}

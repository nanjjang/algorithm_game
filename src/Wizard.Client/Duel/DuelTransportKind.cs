namespace Wizard.Client.Duel
{
    // CONTRACTS 6.3. 자동 매치는 Udp 일 때만 들어갈 수 있다
    public enum DuelTransportKind : byte
    {
        Udp = 0,
        WebSocket = 1,
        Loopback = 2,
    }
}

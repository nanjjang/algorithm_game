namespace Wizard.Protocol.Duel
{
    // CONTRACTS 10.1. ver_type 의 아랫 4비트. 번호는 더하기만 한다
    public enum DuelPacketKind : byte
    {
        ClientInput = 1,
        ServerTick = 2,
        SyncRequest = 3,
        SyncReply = 4,
        Hello = 5,
        HelloAck = 6,
        Bye = 7,
    }
}

namespace Wizard.Protocol.Duel
{
    // CONTRACTS 10.1. nonce 의 첫 바이트
    public enum DuelDirection : byte
    {
        ClientToServer = 0,
        ServerToClient = 1,
    }
}

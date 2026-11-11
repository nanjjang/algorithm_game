namespace Wizard.Client
{
    // CONTRACTS 6.1. IOnlineBackend.Connection
    public enum ConnectionState : byte
    {
        Offline = 0,
        Connecting = 1,
        Online = 2,
        Reconnecting = 3,
    }
}

namespace Wizard.Server.App.Payments
{
    // CONTRACTS 7.1, 0.3(spec 12의 이름)
    public enum OrderStatus
    {
        Created,
        PendingAuth,
        Authorized,
        Granted,
        Cancelled,
        Refunded,
        Revoked,
    }
}

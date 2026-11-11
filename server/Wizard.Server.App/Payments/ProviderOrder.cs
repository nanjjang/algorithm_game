namespace Wizard.Server.App.Payments
{
    // CONTRACTS 7.1
    public sealed record ProviderOrder(string? ProviderRef, string? CheckoutUrl, string? CheckoutToken);
}

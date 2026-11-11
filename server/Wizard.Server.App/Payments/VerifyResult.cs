namespace Wizard.Server.App.Payments
{
    // CONTRACTS 7.1
    public sealed record VerifyResult(bool Ok, bool Final, string? ProviderRef, string? Error);
}

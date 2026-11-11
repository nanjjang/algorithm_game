namespace Wizard.Server.App.Mail
{
    // CONTRACTS 7.1
    public sealed record MailSendResult(bool Accepted, bool Retryable, string? ProviderRef, string? Error);
}

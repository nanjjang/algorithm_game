using System;

namespace Wizard.Server.App.Payments
{
    // CONTRACTS 7.1
    public sealed record ProviderEvent(string ProviderRef, ProviderEventKind Kind, DateTime At, string Raw);
}

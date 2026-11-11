using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Wizard.Server.App.Mail
{
    // CONTRACTS 7.1, T-36. 출시 구성에는 ApiMailSender(M10-ACC-05)를 등록한다
    public interface IMailSender
    {
        string Id { get; }                                // "api", "smtp-relay", "null"
        bool IsEnabled { get; }                           // NullMailSender 는 false
        Task<MailSendResult> SendAsync(string toAddress, MailKind kind, MailBody body, CancellationToken ct);
        Task<IReadOnlyList<MailDeliveryEvent>> PollDeliveryAsync(DateTime since);
    }
}

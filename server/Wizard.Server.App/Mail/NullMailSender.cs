using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Wizard.Server.App.Mail
{
    // CONTRACTS 7.1. 개발과 테스트용. 가입과 로그인은 그대로 되고 복구만 꺼진다
    public sealed class NullMailSender : IMailSender
    {
        public string Id => "null";

        public bool IsEnabled => false;

        public Task<MailSendResult> SendAsync(string toAddress, MailKind kind, MailBody body, CancellationToken ct) =>
            Task.FromResult(new MailSendResult(false, false, null, "mail_disabled"));

        public Task<IReadOnlyList<MailDeliveryEvent>> PollDeliveryAsync(DateTime since) =>
            Task.FromResult<IReadOnlyList<MailDeliveryEvent>>(Array.Empty<MailDeliveryEvent>());
    }
}

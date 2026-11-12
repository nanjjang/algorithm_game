namespace Wizard.Server.Data.Accounts
{
    // spec 12 8.1절 mail_kind(mail_outbox.kind). Wizard.Server.App.Mail.MailKind 와 같은 다섯 가지다(Data 는 App 을 참조하지 않으므로 따로 둔다)
    public enum MailOutboxKind
    {
        VerifyEmail = 0,             // 'verify_email'
        ResetPassword = 1,           // 'reset_password'
        FindLoginIds = 2,            // 'find_login_ids'
        NotifyPasswordChanged = 3,   // 'notify_password_changed'
        NotifyEmailChanged = 4,      // 'notify_email_changed'
    }
}

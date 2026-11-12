using System;

namespace Wizard.Server.Data.Accounts
{
    // 계정 묶음의 열거형 ↔ DB 문자열(spec 12 8.1절: 열거값은 text + CHECK). 0010_accounts.sql 의 CHECK 와 같은 값이다.
    public static class AccountDbNames
    {
        public static string TokenKind(AccountTokenKind kind)
        {
            switch (kind)
            {
                case AccountTokenKind.VerifyEmail: return "verify_email";
                case AccountTokenKind.ResetPassword: return "reset_password";
                case AccountTokenKind.ChangeEmail: return "change_email";
                default: throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }

        public static AccountTokenKind ParseTokenKind(string value)
        {
            switch (value)
            {
                case "verify_email": return AccountTokenKind.VerifyEmail;
                case "reset_password": return AccountTokenKind.ResetPassword;
                case "change_email": return AccountTokenKind.ChangeEmail;
                default: throw new ArgumentOutOfRangeException(nameof(value), value, "unknown account_token.kind");
            }
        }

        public static string MailKind(MailOutboxKind kind)
        {
            switch (kind)
            {
                case MailOutboxKind.VerifyEmail: return "verify_email";
                case MailOutboxKind.ResetPassword: return "reset_password";
                case MailOutboxKind.FindLoginIds: return "find_login_ids";
                case MailOutboxKind.NotifyPasswordChanged: return "notify_password_changed";
                case MailOutboxKind.NotifyEmailChanged: return "notify_email_changed";
                default: throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }

        public static MailOutboxKind ParseMailKind(string value)
        {
            switch (value)
            {
                case "verify_email": return MailOutboxKind.VerifyEmail;
                case "reset_password": return MailOutboxKind.ResetPassword;
                case "find_login_ids": return MailOutboxKind.FindLoginIds;
                case "notify_password_changed": return MailOutboxKind.NotifyPasswordChanged;
                case "notify_email_changed": return MailOutboxKind.NotifyEmailChanged;
                default: throw new ArgumentOutOfRangeException(nameof(value), value, "unknown mail_outbox.kind");
            }
        }

        public static string MailStatus(MailOutboxStatus status)
        {
            switch (status)
            {
                case MailOutboxStatus.Queued: return "queued";
                case MailOutboxStatus.Sent: return "sent";
                case MailOutboxStatus.Failed: return "failed";
                case MailOutboxStatus.Dropped: return "dropped";
                default: throw new ArgumentOutOfRangeException(nameof(status), status, null);
            }
        }

        public static MailOutboxStatus ParseMailStatus(string value)
        {
            switch (value)
            {
                case "queued": return MailOutboxStatus.Queued;
                case "sent": return MailOutboxStatus.Sent;
                case "failed": return MailOutboxStatus.Failed;
                case "dropped": return MailOutboxStatus.Dropped;
                default: throw new ArgumentOutOfRangeException(nameof(value), value, "unknown mail_outbox.status");
            }
        }

        public static string Sanction(SanctionKind kind)
        {
            switch (kind)
            {
                case SanctionKind.Chat: return "chat";
                case SanctionKind.Trade: return "trade";
                case SanctionKind.Duel: return "duel";
                case SanctionKind.Login: return "login";
                default: throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }

        public static SanctionKind ParseSanction(string value)
        {
            switch (value)
            {
                case "chat": return SanctionKind.Chat;
                case "trade": return SanctionKind.Trade;
                case "duel": return SanctionKind.Duel;
                case "login": return SanctionKind.Login;
                default: throw new ArgumentOutOfRangeException(nameof(value), value, "unknown sanction.kind");
            }
        }

        public static string RecoveryKind(RecoveryRequestKind kind)
        {
            switch (kind)
            {
                case RecoveryRequestKind.ResetPassword: return "reset_password";
                case RecoveryRequestKind.FindLoginIds: return "find_login_ids";
                default: throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }
    }
}

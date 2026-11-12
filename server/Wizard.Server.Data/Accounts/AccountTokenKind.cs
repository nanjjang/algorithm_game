namespace Wizard.Server.Data.Accounts
{
    // spec 12 8.1절 token_kind. DB 에는 AccountDbNames.TokenKind 의 문자열로 들어간다(번호는 싣지 않는다)
    public enum AccountTokenKind
    {
        VerifyEmail = 0,      // 'verify_email'   2.9절
        ResetPassword = 1,    // 'reset_password' 2.11절
        ChangeEmail = 2,      // 'change_email'   2.10절
    }
}

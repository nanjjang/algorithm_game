namespace Wizard.Server.Data.Accounts
{
    // PgAccountStore.CreateAsync 의 결과. 유일성 위반은 예외가 아니라 값이다(트랜잭션이 취소되지 않는다)
    public enum AccountCreateOutcome
    {
        Created = 0,
        LoginIdTaken = 1,     // login_id UNIQUE
        NicknameTaken = 2,    // lower(nickname) 유일 색인(대소문자 무시, 2.1절 [임시])
    }
}

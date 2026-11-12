namespace Wizard.Server.Data.Accounts
{
    // recovery_request_log.kind [임시]: 재설정 요청(spec 12 2.11절)과 아이디 찾기(2.12절). 두 가지는 한도를 같이 센다
    public enum RecoveryRequestKind
    {
        ResetPassword = 0,   // 'reset_password'
        FindLoginIds = 1,    // 'find_login_ids'
    }
}

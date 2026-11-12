namespace Wizard.Server.Data.Accounts
{
    // spec 12 8.1절 sanction_kind
    public enum SanctionKind
    {
        Chat = 0,     // 'chat'
        Trade = 1,    // 'trade'
        Duel = 2,     // 'duel'
        Login = 3,    // 'login'
    }
}

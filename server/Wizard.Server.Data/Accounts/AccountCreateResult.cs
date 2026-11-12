namespace Wizard.Server.Data.Accounts
{
    public readonly struct AccountCreateResult
    {
        public AccountCreateResult(AccountCreateOutcome outcome, long accountId)
        {
            Outcome = outcome;
            AccountId = accountId;
        }

        public AccountCreateOutcome Outcome { get; }

        public long AccountId { get; }   // Created 일 때만 의미가 있다(아니면 0)

        public bool Created => Outcome == AccountCreateOutcome.Created;
    }
}

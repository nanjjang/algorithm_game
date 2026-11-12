using System;

namespace Wizard.Server.Data.Accounts
{
    // PgLoginAttemptStore 가 읽는 집계 한 줄(내부 매핑용)
    internal sealed class LoginFailureRow
    {
        public long Failures { get; set; }
        public DateTime? LastFailureAt { get; set; }
    }
}

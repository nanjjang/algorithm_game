using System;

namespace Wizard.Server.Data.Accounts
{
    // 2.4절: 마지막 성공 이후의 연속 실패 수 n 과 마지막 실패 시각. 대기 시간 WaitSeconds(n)은 App 이 계산한다
    public readonly struct LoginFailureState
    {
        public LoginFailureState(int consecutiveFailures, DateTime? lastFailureAt)
        {
            ConsecutiveFailures = consecutiveFailures;
            LastFailureAt = lastFailureAt;
        }

        public int ConsecutiveFailures { get; }

        public DateTime? LastFailureAt { get; }
    }
}

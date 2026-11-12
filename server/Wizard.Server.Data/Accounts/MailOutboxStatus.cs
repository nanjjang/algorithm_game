namespace Wizard.Server.Data.Accounts
{
    // spec 12 8.1절 mail_status, 2.8절 상태 기계. Failed 는 "마지막 시도가 실패, 다시 시도 대기"를 보이는 표시값이고 다시 꺼내진다.
    // 값은 이 넷뿐이다(DECISIONS-TECH T-56): 배달 확인은 Sent 그대로, 반송은 Sent → Dropped(MailOutboxRow.Bounced)
    public enum MailOutboxStatus
    {
        Queued = 0,    // 'queued'
        Sent = 1,      // 'sent'
        Failed = 2,    // 'failed'
        Dropped = 3,   // 'dropped'
    }
}

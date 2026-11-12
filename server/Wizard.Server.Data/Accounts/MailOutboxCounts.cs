namespace Wizard.Server.Data.Accounts
{
    // 한 기간에 만든 발송 줄의 상태별 수(2.8절 실패율 감시: 최근 1시간 10통 이상이면서 dropped 20% 이상 [임시]. 판정은 App).
    // Dropped 에는 반송된 줄도 든다(T-56)
    public sealed class MailOutboxCounts
    {
        public long Queued { get; set; }
        public long Sent { get; set; }
        public long Failed { get; set; }
        public long Dropped { get; set; }

        public long Total => Queued + Sent + Failed + Dropped;
    }
}

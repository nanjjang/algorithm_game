namespace Wizard.Netcode.Duel
{
    // CONTRACTS 6.4·10.6, DECISIONS-TECH T-64. 한 사람의 넷코드 통계(spec 10 14.8 "사람별로 패킷 도착 시각 분포, 봉인 마감에 걸린 횟수,
    // 버려진 이벤트 수", duel_match.net_stats 의 원천). DuelHostCore.StatsFor 가 값으로 돌려준다. 경기 처음부터 센 값이고 줄지 않는다.
    // 서버가 센 값이다(클라이언트가 알려 준 값은 하나도 없다). 칸은 더하기만 한다.
    public struct DuelNetStats
    {
        // 패킷(CONTRACTS 10.1)
        public long PacketsVerified;          // 이 사람의 token_id 로 와서 태그 검증을 통과한 패킷
        public long PacketsUnauthentic;       // 이 사람의 token_id 로 왔지만 태그가 틀린 패킷(열지 않고 버림)
        public long PacketsReplayed;          // 재전송 창이 버린 패킷(이미 받은 번호, 창보다 오래된 번호)
        public long PacketsMalformed;         // 태그는 맞지만 ClientInput 본문 형식이 틀린 패킷(OrderViolations 에도 들어 있다)

        // 입력 검증(CONTRACTS 10.6). 몰수는 OrderViolations 만 센다
        public long OrderViolations;          // (0) 한 패킷 안의 역행으로 버린 패킷. 형식으로 적을 수 없는 역행(형식 오류)을 포함한다
        public long EventsAccepted;
        public long EventsLate;               // reason 1: 봉인된 틱, 또는 서버 시각 T + 1틱 뒤에 도착
        public long EventsEarly;              // reason 2: 이른 시각(미래 주장)
        public long EventsQuantity;           // reason 3: 틱당 8개 초과
        public long SeqsLost;                 // reason 5: 유실로 소비한 순번의 수
        public long EventsKeyStateIgnored;    // 키 상태 모순(눌린 키의 누름, 안 눌린 키의 뗌). 알리지 않는다
        public long NoticesOverflowed;        // 알림 대기열이 차서 알리지 못한 순번의 수(통계에는 위 칸에 들어 있다)

        // 봉인(spec 10 14.6)
        public long TicksSealedByDeadline;    // 패킷이 아니라 서버 시각(T + 1틱)으로 닫힌 틱의 수

        // 도착 시각 분포: 받은 이벤트마다 (마감 − 도착) µs. 마감 = 그 틱의 끝
        public long ArrivalMarginCount;
        public long ArrivalMarginSumUs;
        public long ArrivalMarginMinUs;       // ArrivalMarginCount 가 0 이면 0
    }
}

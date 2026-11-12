namespace Wizard.Protocol.Duel
{
    // CONTRACTS 10.3 ServerTick 의 dropped 항목 reason, 10.6 의 검증 순서
    public enum DuelDropReason : byte
    {
        Late = 1,           // 봉인된 틱, 또는 서버 시각 T + 1틱 뒤에 도착
        Early = 2,          // 이른 시각(미래 주장)
        Quantity = 3,       // 틱당 8개 초과
        Order = 4,          // 한 패킷 안의 역행(패킷 거부)
        Lost = 5,           // 겹쳐 보내는 창 밖으로 밀려나 영영 오지 않는 순번
    }
}

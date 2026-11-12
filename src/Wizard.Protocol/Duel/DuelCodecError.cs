namespace Wizard.Protocol.Duel
{
    // 코덱이 패킷을 거절한 이유. 진단과 통계용이다(통신에는 싣지 않는다).
    // 어느 것이든 그 패킷은 버린다. 10.6절의 순서 위반(몰수 계수)과는 다르다: 그것은 형식이 맞는 패킷을 호스트가 판정한다.
    public enum DuelCodecError : byte
    {
        None = 0,
        Truncated = 1,      // 필드가 끝나기 전에 바이트가 모자란다
        TooLarge = 2,       // 1,200바이트 상한(본문은 1,200 − 29)을 넘는다
        BadVersion = 3,     // ver_type 의 윗 4비트가 1이 아니다
        UnknownKind = 4,    // ver_type 의 아랫 4비트가 1~7 이 아니다
        OutOfRange = 5,     // 값이 형식이 정한 범위를 넘는다(개수, 키, 이유, 플래그, 틱·순번의 32비트 넘침, 번호 0)
        TrailingBytes = 6,  // 본문이 끝났는데 바이트가 남았다
        NonCanonical = 7,   // varuint 가 가장 짧은 꼴이 아니다(같은 값의 다른 바이트를 받지 않는다: 쓰기 → 읽기 → 쓰기가 같게)
        BadKey = 8,         // 키 길이가 32바이트가 아니다
        Unauthentic = 9,    // 태그 검증 실패
        Unrepresentable = 10, // 쓰는 쪽: 형식으로 적을 수 없는 열(틱이나 순번이 앞 이벤트보다 작다)
    }
}

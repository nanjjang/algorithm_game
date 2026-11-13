namespace Wizard.Sim
{
    // CONTRACTS 3.8. InputRecordCodec.TryRead·DuelRecordCodec.TryRead 가 RecordCorrupt 로 돌려주는 까닭. 번호는 더하기만 한다
    public enum RecordError : byte
    {
        None = 0,
        BadMagic = 1,
        BadVersion = 2,
        Truncated = 3,
        TrailingBytes = 4,
        ShaMismatch = 5,
        TickOverflow = 6,
        TickBackwards = 7,
        TooManyKeys = 8,
        KeyOrder = 9,
        CommandAfterKey = 10,
        BadKey = 11,
        BadOp = 12,
        UnknownTag = 13,
        EndTickOverLimit = 14,
        BadSlot = 15,        // WZDR: 슬롯이 0·1 이 아니다
        BadHeader = 16,      // magic·format_version 은 맞는데 머리말이 정규 바이트가 아니다(모자란 길이 포함)
        BadFlags = 17,       // 꼬리말 flags 에 이 형식 판이 모르는 비트
        BadSeq = 18,         // WZDR: 순번이 0 이거나 u32 를 넘는다(슬롯 안에서 늘어나야 한다는 규칙은 없다, T-64). .wzrc: 순번 공간(uint)을 다 썼다
    }
}

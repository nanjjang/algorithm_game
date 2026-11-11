namespace Wizard.Sim
{
    // CONTRACTS 3.8. InputRecordCodec.TryRead 가 RecordCorrupt 로 돌려주는 까닭(목록의 순서 그대로)
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
    }
}

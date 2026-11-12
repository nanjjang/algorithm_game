namespace Wizard.Sim
{
    // 본문 레코드의 종류(CONTRACTS 11.1의 꼬리표)
    internal enum RecordKind : byte
    {
        Input = 1,
        Command = 2,
        Pause = 3,
        Checkpoint = 4,
        DuelInput = 5,
    }
}

namespace Wizard.ReplayCli
{
    // 기록 파일의 형식(CONTRACTS 11.1): .wzrc(무한 웨이브·연습, 플레이어 하나), .wzdr(결투의 확정 입력, 두 사람)
    public enum RecordFormat : byte
    {
        Wzrc = 1,
        Wzdr = 2,
    }
}

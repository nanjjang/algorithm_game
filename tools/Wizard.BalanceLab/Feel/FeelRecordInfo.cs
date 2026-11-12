namespace Wizard.BalanceLab.Feel
{
    // 분석한 기록 파일의 신원과 재실행 확인 결과(판정 근거의 머리)
    internal sealed class FeelRecordInfo
    {
        public string FileName = string.Empty;
        public long Bytes;
        public string Sha256 = string.Empty;
        public uint EndTick;
        public int InputCount;
        public int CommandCount;
        public int PauseCount;
        public bool HashMatches;             // 재실행의 마지막 해시 = 꼬리말의 final_state_hash
        public bool HashCheckSkipped;        // --skip-hash-check
        public int RejectedCommands;         // 재실행이 받지 않은 명령(0이어야 한다)
        public int DataVersion;
        public string CoreVersion = string.Empty;
    }
}

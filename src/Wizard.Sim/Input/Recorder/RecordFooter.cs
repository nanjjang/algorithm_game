namespace Wizard.Sim
{
    // CONTRACTS 11.1 꼬리말: end_tick u32, final_state_hash u64, flags u8(비트 0 = debug_used). record_sha256 은 읽을 때 대조만 한다
    internal readonly struct RecordFooter
    {
        public readonly uint EndTick;
        public readonly ulong FinalStateHash;
        public readonly byte Flags;

        public RecordFooter(uint endTick, ulong finalStateHash, byte flags)
        {
            EndTick = endTick;
            FinalStateHash = finalStateHash;
            Flags = flags;
        }

        public bool DebugUsed => (Flags & InputRecordFormat.FlagDebugUsed) != 0;
    }
}

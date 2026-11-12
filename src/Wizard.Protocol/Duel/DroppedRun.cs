namespace Wizard.Protocol.Duel
{
    // CONTRACTS 10.3 ServerTick 의 dropped 항목: u32 seq, u8 reason(1~5), u16 run(같은 이유로 이어진 순번의 수 − 1).
    // seq .. seq + run 이 같은 이유로 버려졌거나 유실로 소비됐다.
    public readonly struct DroppedRun
    {
        public const int WireBytes = 7;

        public readonly uint Seq;
        public readonly DuelDropReason Reason;
        public readonly ushort Run;

        public DroppedRun(uint seq, DuelDropReason reason, ushort run)
        {
            Seq = seq;
            Reason = reason;
            Run = run;
        }

        public uint LastSeq => Seq + Run;

        // 순번은 1부터, 마지막 순번이 32비트 안, 이유는 1~5
        public bool IsValid => Seq != 0U && Run <= uint.MaxValue - Seq
                               && Reason >= DuelDropReason.Late && Reason <= DuelDropReason.Lost;
    }
}

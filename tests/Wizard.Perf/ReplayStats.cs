namespace Wizard.Perf
{
    // 재실행 속도 벤치의 결과
    internal sealed class ReplayStats
    {
        public double TicksPerSecond;      // 회차 중 가장 빠른 값
        public double RealtimeMultiple;    // TicksPerSecond ÷ 60(참고)
        public int RecordTicks;
        public int RecordBytes;
        public int Rounds;
    }
}

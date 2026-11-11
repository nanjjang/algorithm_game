namespace Wizard.Client
{
    // CONTRACTS 6.5. 실수 시각 ↔ 틱. 무한 웨이브는 SimClock, 결투는 서버와 맞춘 입력 시계
    public readonly struct InputClock
    {
        public readonly double EpochSeconds;
        public readonly double TickSeconds;               // 1/60 ± 0.5%
        
        public InputClock(double epochSeconds, double tickSeconds)
        {
        EpochSeconds = epochSeconds;
        TickSeconds = tickSeconds;
        }
    }
}

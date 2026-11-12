namespace Wizard.Netcode.Clock
{
    // CONTRACTS 6.4, spec 10 14.4 Sync 의 결과. 서버 시각 = 클라이언트 시각 + OffsetUs.
    // default(ClockOffset)는 "쓸 표본이 없었다"(IsValid 거짓, 나머지 0)이다.
    public readonly struct ClockOffset
    {
        /// <summary>쓸 표본이 하나라도 있었다. 거짓이면 나머지 칸은 0 이고 시계를 맞추지 못한 것이다</summary>
        public readonly bool IsValid;

        /// <summary>서버 시각 − 클라이언트 시각(µs). 서버가 뒤면 음수</summary>
        public readonly long OffsetUs;

        /// <summary>고른 표본의 왕복(µs). 0 이상</summary>
        public readonly long RttUs;

        /// <summary>고른 표본이 입력 열에서 놓인 자리(진단용)</summary>
        public readonly int SampleIndex;

        public ClockOffset(long offsetUs, long rttUs, int sampleIndex)
        {
            IsValid = true;
            OffsetUs = offsetUs;
            RttUs = rttUs;
            SampleIndex = sampleIndex;
        }

        /// <summary>클라이언트 단조 시계의 시각을 서버 시각으로 옮긴다</summary>
        public long ToServerUs(long clientUs) => clientUs + OffsetUs;
    }
}

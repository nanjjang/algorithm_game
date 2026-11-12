namespace Wizard.Perf
{
    // 스냅숏 저장 + 복원 벤치 한 시나리오의 결과(한 쌍 = SaveSnapshot 한 번 + LoadSnapshot 한 번)
    internal sealed class SnapshotStats
    {
        public double MeanMicros;          // 회차별 평균 가운데 가장 작은 값
        public double P99Micros;           // 회차별 p99 가운데 가장 작은 값
        public long AllocBytes;            // 저장·복원 전체의 관리 힙 할당(회차 중 가장 작은 값)
        public long AllocBytesMax;         // 회차 중 가장 큰 값(참고)
        public int PairsPerRound;
        public int Rounds;
    }
}

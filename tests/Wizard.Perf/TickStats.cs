namespace Wizard.Perf
{
    // 틱 벤치 한 시나리오의 결과
    internal sealed class TickStats
    {
        public double MeanMicros;            // 회차별 평균 가운데 가장 작은 값
        public double P99Micros;             // 회차별 p99 가운데 가장 작은 값
        public double MaxMicros;             // 그 회차의 가장 긴 틱(참고. 판정하지 않는다)
        public long StepAllocBytes;          // 0단계 명령이 없는 잰 틱 전체에서 Step 이 관리 힙에 할당한 바이트(회차 중 가장 작은 값)
        public long StepAllocBytesMax;       // 회차 중 가장 큰 값(참고)
        public long CommandTickAllocBytes;   // 0단계 명령이 있는 틱의 할당(참고. 0단계 컴파일은 할당 0의 예외다, CONTRACTS 3.2)
        public int CommandTicks;             // 한 회차의 잰 틱 가운데 0단계 명령이 있는 틱
        public int TicksPerRound;
        public int Rounds;

        // 부하의 크기(참고, 마지막 회차). 살아 있는 수는 ISimView 의 목록 길이다(M1 에서는 그 목록을 채우는 훅이 아직 없어 0이다).
        // 생긴 수는 이벤트로 센 틱당 개수다(시제품의 탄·적도 이벤트를 낸다)
        public double MeanViewProjectiles;
        public int MaxViewProjectiles;
        public double MeanViewEnemies;
        public int MaxViewEnemies;
        public double ProjectilesSpawnedPerTick;
        public double EnemiesSpawnedPerTick;
        public double EnemyShotsPerTick;
        public double EventsPerTick;
    }
}

namespace Wizard.Perf
{
    // 벤치의 길이. Full 은 perf.sh 와 기준선이 쓰고, Quick 은 단위 시험(할당 0 단언, 하네스가 도는지)이 쓴다.
    // Quick 은 JIT 데우기를 하지 않으므로 시간 값이 기준선과 견줄 수 없다(settings 칸이 달라 check 가 거절한다).
    // 시간 값은 회차마다 새 세계로 재고 가장 좋은 회차를 쓴다(다른 프로세스가 끼어든 회차를 거른다). 회차를 짧게(수 ms) 많이 두는 것은
    // 한 번의 선점(수 ms)이 회차 평균을 크게 바꾸므로 선점 없는 회차가 하나라도 남게 하려는 것이다. 할당은 회차 중 가장 작은 값이다
    // (Step 이 할당하면 모든 회차에서 0보다 크다. 런타임 쪽 일회성 할당이 한 회차에 잡혀도 거짓 실패가 나지 않는다).
    internal sealed class BenchSettings
    {
        public int JitWarmupMillis;      // 시나리오마다 재기 전에 각본을 되풀이해 돌리는 시간(JitWarmup)
        public int WarmupTicks;          // 회차마다 재기 전에 돌리는 틱(시제품의 슬라임이 차오르는 시간)
        public int MeasureTicks;         // 회차마다 재는 틱
        public int Rounds;
        public int SnapshotIterations;   // 회차마다 저장·복원 쌍의 수
        public int ReplayTicks;          // 재실행 기록의 길이
        public int ReplayRounds;

        public static BenchSettings Full() => new BenchSettings
        {
            JitWarmupMillis = 1500,
            WarmupTicks = 1200,
            MeasureTicks = 3600,
            Rounds = 40,
            SnapshotIterations = 1000,
            ReplayTicks = 36000,
            ReplayRounds = 20,
        };

        public static BenchSettings Quick() => new BenchSettings
        {
            JitWarmupMillis = 0,
            WarmupTicks = 300,
            MeasureTicks = 900,
            Rounds = 3,
            SnapshotIterations = 300,
            ReplayTicks = 3600,
            ReplayRounds = 1,
        };

        // 각본이 덮어야 하는 틱 수(재기 + 데우기 + 저장·복원 벤치의 진행)
        public int ScriptTicks => WarmupTicks + System.Math.Max(MeasureTicks, SnapshotIterations * 2) + 1;
    }
}

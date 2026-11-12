using System;
using System.Diagnostics;
using Wizard.Sim;

namespace Wizard.Perf
{
    // 재기 전의 JIT 데우기. .NET 의 계층 컴파일은 자주 불린 메서드를 100ms 남짓 조용한 뒤에 다른 스레드에서 최적화 코드로 바꾼다.
    // 틱 수로만 데우면(1,200틱 ≈ 1ms) 재는 동안 덜 최적화된 코드와 바뀐 코드가 섞여 같은 빌드도 회차마다 몇 배씩 달라진다.
    // 그래서 시나리오마다 정해진 시간 동안 각본 전체를 새 세계로 되풀이해 돌린 뒤(서버가 오래 돈 뒤의 상태) 잰다.
    internal static class JitWarmup
    {
        // millis 동안 pass 를 되풀이한다(0이면 하지 않는다). 적어도 한 번은 돈다
        public static void For(int millis, Action pass)
        {
            if (millis <= 0)
            {
                return;
            }
            long until = Stopwatch.GetTimestamp() + millis * Stopwatch.Frequency / Durations.MillisPerSecond;
            do
            {
                pass();
            }
            while (Stopwatch.GetTimestamp() < until);
        }

        // 틱 벤치와 스냅숏 벤치가 부르는 코드(Step, 저장·복원, 해시)를 각본 끝까지 한 번 돌린다
        public static void ScenarioPass(PerfScenario scenario, StepFeed feed)
        {
            SimWorld world = ScenarioInfo.Create(scenario);
            SimSnapshot snapshot = world.CreateSnapshotBuffer();
            var sink = new CountingSink();
            while (world.Tick < (uint)feed.Ticks)
            {
                if ((world.Tick & SnapshotEveryMask) == 0)
                {
                    world.SaveSnapshot(snapshot);
                    world.ComputeStateHash();
                    world.LoadSnapshot(snapshot);
                }
                feed.StepAt(world, sink);
            }
        }

        private const uint SnapshotEveryMask = 7;   // 8틱마다
    }
}

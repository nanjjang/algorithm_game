using Wizard.Sim;
using Xunit;

namespace Wizard.Perf.Tests
{
    // PIPELINE 7.5, CONTRACTS 3.2: Step 은 관리 힙에 할당하지 않는다(0단계의 주문 컴파일만 예외. 결투에는 0단계 명령이 없다).
    // 벤치(perf.sh)만이 아니라 시험으로도 단언한다(M1-PERF-01). 벤치 코드와 따로 쓴 고리(PerfFixtures.MeasureStepAllocation)로 잰다.
    // 한 회차만 재고 0이어야 한다(회차 중 최솟값을 쓰는 벤치보다 엄격하다).
    [Collection(AllocationCollection.Name)]
    public sealed class StepAllocationTests
    {
        [Fact]
        public void Duel2p_StepAllocatesZeroBytes()
        {
            StepFeed feed = PerfFixtures.Feed(PerfScenario.Duel2p, PerfFixtures.WarmupTicks + PerfFixtures.MeasureTicks + 1);
            SimWorld world = ScenarioInfo.Create(PerfScenario.Duel2p);
            var sink = new CountingSink();
            PerfFixtures.MeasureStepAllocation(world, feed, sink, PerfFixtures.WarmupTicks, PerfFixtures.MeasureTicks,
                                               out long stepBytes, out long commandBytes, out int commandTicks);
            Assert.Equal(0, commandTicks);           // 결투에는 0단계 명령이 없다
            Assert.Equal(0L, commandBytes);
            Assert.Equal(0L, stepBytes);
            Assert.Equal(2, world.PlayerCount);
        }

        [Fact]
        public void PracticeProto_StepAllocatesZeroBytes_WithBulletsEnemiesAndCommands()
        {
            StepFeed feed = PerfFixtures.Feed(PerfScenario.PracticeProto, PerfFixtures.WarmupTicks + PerfFixtures.MeasureTicks + 1);
            SimWorld world = ScenarioInfo.Create(PerfScenario.PracticeProto);
            var sink = new CountingSink();
            while (world.Tick < (uint)PerfFixtures.WarmupTicks)
            {
                feed.StepAt(world, sink);
            }
            long spawned0 = sink.ProjectilesSpawned, enemies0 = sink.EnemiesSpawned, shots0 = sink.EnemyShotsFired;
            PerfFixtures.MeasureStepAllocation(world, feed, sink, PerfFixtures.WarmupTicks, PerfFixtures.MeasureTicks,
                                               out long stepBytes, out long commandBytes, out int commandTicks);
            Assert.Equal(0L, stepBytes);
            // M1 의 0단계 명령(시제품의 조립 바꾸기 SetSlot)은 SpellTable 에 컴파일하지 않는다 → 예외에 들지 않으므로 이 틱도 0이다.
            // 조립 확정(Depart)의 컴파일이 들어오면 그 작업이 이 단언을 "컴파일한 틱"만 빼도록 고친다
            Assert.True(commandTicks > 0, "the measured window has no stage-0 command");
            Assert.Equal(0L, commandBytes);
            // 빈 세계에서 0이 나온 것이 아니다: 재는 동안 플레이어 탄, 적, 적 탄이 생긴다
            Assert.True(sink.ProjectilesSpawned > spawned0, "no player projectile spawned in the measured window");
            Assert.True(sink.EnemiesSpawned > enemies0, "no enemy spawned in the measured window");
            Assert.True(sink.EnemyShotsFired > shots0, "no enemy shot in the measured window");
        }

        [Fact]
        public void Endless1p_StepAllocatesZeroBytes()
        {
            StepFeed feed = PerfFixtures.Feed(PerfScenario.Endless1p, PerfFixtures.WarmupTicks + PerfFixtures.MeasureTicks + 1);
            SimWorld world = ScenarioInfo.Create(PerfScenario.Endless1p);
            PerfFixtures.MeasureStepAllocation(world, feed, new CountingSink(), PerfFixtures.WarmupTicks, PerfFixtures.MeasureTicks,
                                               out long stepBytes, out long commandBytes, out int commandTicks);
            Assert.Equal(0, commandTicks);
            Assert.Equal(0L, commandBytes);
            Assert.Equal(0L, stepBytes);
        }

        // 결함 주입: Step 안에서 할당이 일어나면 같은 고리가 0보다 큰 값을 낸다(위 단언이 헛돌지 않는다)
        [Fact]
        public void AllocationInsideStep_IsCaught()
        {
            StepFeed feed = PerfFixtures.Feed(PerfScenario.PracticeProto, PerfFixtures.WarmupTicks + PerfFixtures.MeasureTicks + 1);
            SimWorld world = ScenarioInfo.Create(PerfScenario.PracticeProto);
            var sink = new AllocatingSink();
            PerfFixtures.MeasureStepAllocation(world, feed, sink, PerfFixtures.WarmupTicks, PerfFixtures.MeasureTicks,
                                               out long stepBytes, out _, out _);
            Assert.True(sink.Count > 0);
            Assert.True(stepBytes > 0, "an allocation inside Step was not counted");
        }

        // 벤치의 계수(TickBench)도 같은 결론을 내고, 결함을 넣으면 잡는다
        [Fact]
        public void TickBench_ReportsZero_AndCatchesAnAllocatingSink()
        {
            var settings = new BenchSettings
            {
                WarmupTicks = PerfFixtures.WarmupTicks, MeasureTicks = PerfFixtures.MeasureTicks, Rounds = 1, SnapshotIterations = 1,
            };
            StepFeed feed = PerfFixtures.Feed(PerfScenario.PracticeProto, settings.ScriptTicks);
            TickStats clean = TickBench.Measure(PerfScenario.PracticeProto, feed, settings, () => new CountingSink());
            Assert.Equal(0L, clean.StepAllocBytes);
            Assert.Equal(0L, clean.CommandTickAllocBytes);
            Assert.True(clean.CommandTicks > 0);
            Assert.True(clean.ProjectilesSpawnedPerTick > 0 && clean.EnemiesSpawnedPerTick > 0);

            TickStats faulty = TickBench.Measure(PerfScenario.PracticeProto, feed, settings, () => new AllocatingSink());
            Assert.True(faulty.StepAllocBytes > 0);
        }
    }
}

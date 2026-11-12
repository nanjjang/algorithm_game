using System;
using System.Threading;
using Wizard.ReplayCli;
using Xunit;

namespace Wizard.Sim.Tests.Determinism
{
    // ARCHITECTURE 11 "세계의 격리", PIPELINE 7.3 "동시 실행": 세계 16개를 16스레드에서 같은 기록으로 동시에 돌린 틱별 해시가 단일 실행과 같다
    // (정적 버퍼가 섞이지 않는다. 서버는 여러 세계를 여러 스레드에서 동시에 돌린다). 세계끼리 나누는 것은 불변인 ContentDb 뿐이다.
    public sealed class ConcurrencyTests
    {
        private const int Threads = 16;

        // 스레드마다 세계 하나를 만들어 문이 열리면 동시에 끝까지 돌린다
        private static ulong[][] RunConcurrently(Func<int, (WorldKind Kind, RecordFeed Feed)> job)
        {
            var results = new ulong[Threads][];
            var errors = new Exception?[Threads];
            using (var gate = new Barrier(Threads))
            {
                var threads = new Thread[Threads];
                for (int i = 0; i < Threads; i++)
                {
                    int me = i;
                    threads[i] = new Thread(() =>
                    {
                        try
                        {
                            (WorldKind kind, RecordFeed feed) = job(me);
                            SimWorld world = DetKit.World(kind, feed);
                            gate.SignalAndWait();
                            results[me] = Replayer.TickHashes(world, feed);
                        }
                        catch (Exception e)
                        {
                            errors[me] = e;
                        }
                    });
                    threads[i].Start();
                }
                foreach (Thread t in threads)
                {
                    t.Join();
                }
            }
            foreach (Exception? e in errors)
            {
                if (e != null)
                {
                    throw new InvalidOperationException("a world thread failed", e);
                }
            }
            return results;
        }

        [Theory]
        [InlineData(WorldKind.Practice)]
        [InlineData(WorldKind.Duel)]
        public void SixteenWorldsOnSixteenThreadsWithTheSameRecordMatchASingleRun(WorldKind kind)
        {
            RecordFeed feed = DetKit.Feed(DetKit.Record(kind, 0xC0C0_0001UL, 0xC0C0_0002UL, DetKit.Minute, "dense"));
            ulong[] single = DetKit.Hashes(kind, feed);
            ulong[][] all = RunConcurrently(_ => (kind, feed));
            for (int i = 0; i < Threads; i++)
            {
                HashAssert.SameEveryTick(single, all[i], "thread " + i + " vs single run");
            }
        }

        // 서로 다른 일을 하는 세계가 섞여 돌 때(연습·결투·무한 웨이브, 기록도 제각각)도 각자 단일 실행과 같다
        [Fact]
        public void DifferentWorldsSideBySideMatchTheirSingleRuns()
        {
            WorldKind[] kinds = { WorldKind.Practice, WorldKind.Duel, WorldKind.Endless, WorldKind.Practice };
            var feeds = new RecordFeed[Threads];
            var singles = new ulong[Threads][];
            for (int i = 0; i < Threads; i++)
            {
                WorldKind kind = kinds[i % kinds.Length];
                feeds[i] = DetKit.Feed(DetKit.Record(kind, 0xC0C1_0000UL + (ulong)i, 0xC0C1_1000UL + (ulong)i, 1200, FuzzRun.Profiles[i % FuzzRun.Profiles.Length]));
                singles[i] = DetKit.Hashes(kind, feeds[i]);
            }
            ulong[][] all = RunConcurrently(i => (kinds[i % kinds.Length], feeds[i]));
            for (int i = 0; i < Threads; i++)
            {
                HashAssert.SameEveryTick(singles[i], all[i], "thread " + i + " (" + kinds[i % kinds.Length] + ") vs its single run");
            }
        }
    }
}

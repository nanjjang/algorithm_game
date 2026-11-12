using System;
using System.Collections.Generic;
using Wizard.Core;
using Wizard.ReplayCli;
using Wizard.TestKit.Shared.Fuzz;
using Xunit;
using Xunit.Abstractions;

namespace Wizard.Sim.Tests.Determinism
{
    // PIPELINE 7.3 "되감기 일치", CONTRACTS 3.2 "LoadSnapshot(s) 뒤에 같은 입력으로 Step 하면 저장하지 않고 진행한 것과 같은 해시":
    // 무작위 입력 1,000판(판마다 3,600틱)에서 임의 틱에 SaveSnapshot → k틱 진행(k = 1~16) → LoadSnapshot → 다시 진행한 해시가
    // 끊지 않고 진행한 해시(기록을 만든 원래 실행)와 그 뒤 모든 틱에서 같다.
    // 상태 절마다도 본다(절 목록은 코어의 절 열거형에서 읽는다. 뒤에 더해지는 절도 저절로 대상이다, StateSections):
    //   복원 직후의 모든 절 = 저장할 때의 모든 절, 다시 k틱 진행한 뒤의 모든 절 = 처음 k틱 진행했을 때의 모든 절(같은 입력일 때).
    // 나아가는 k틱에 절반은 다른 입력을 넣는다(넷코드의 예측이 틀린 경우). 복원이 그 흔적을 남기면 안 된다.
    public sealed class RewindFuzzTests
    {
        private const ulong Seed = 0x7E31_0D00_0000_0001UL;
        private const int Runs = 1000;
        private const int MaxK = 16;

        private readonly ITestOutputHelper _output;

        public RewindFuzzTests(ITestOutputHelper output)
        {
            _output = output;
        }

        // 나아가는 동안 넣는 다른 입력(종류마다 하나. 판 길이와 같다)
        private static readonly Lazy<RecordFeed> NoisePractice = new Lazy<RecordFeed>(() => DetKit.Feed(DetKit.PracticeRecord(0x401_5EUL, DetKit.Minute, "dense")));
        private static readonly Lazy<RecordFeed> NoiseEndless = new Lazy<RecordFeed>(() =>
            DetKit.Feed(DetKit.EndlessRecord(0x401_5E00UL, 0x401_5E01UL, DetKit.Minute, "dense")));
        private static readonly Lazy<RecordFeed> NoiseDuel = new Lazy<RecordFeed>(() =>
            DetKit.Feed(DetKit.DuelRecord(0x401_5E02UL, 0x401_5E03UL, DetKit.Minute, "dense")));

        private static RecordFeed Noise(WorldKind kind) =>
            kind == WorldKind.Practice ? NoisePractice.Value : kind == WorldKind.Endless ? NoiseEndless.Value : NoiseDuel.Value;

        private sealed class Shrinker : IFuzzShrinker<FuzzRun>
        {
            public IEnumerable<FuzzRun> Shrink(FuzzRun value) => value.Smaller();
        }

        // 성질 하나. sabotage 는 이 퍼저가 결함을 잡는지 보는 자리(복원 직후 세계를 일부러 건드린다)
        internal static string? Property(FuzzRun run, Action<SimWorld>? sabotage = null)
        {
            var reference = new ulong[run.Ticks + 1];
            RecordFeed feed = DetKit.Feed(DetKit.Record(run.Kind, run.WorldSeed, run.BotSeed, run.Ticks, run.Profile, reference));
            RecordFeed forwardFeed = run.Noisy ? Noise(run.Kind) : feed;
            SimWorld w = DetKit.World(run.Kind, feed);
            var sink = new CountingSink();

            Replayer.RunTo(w, feed, run.At, sink);
            if (w.ComputeStateHash() != reference[run.At])
            {
                return "replay differs from the recording run before the snapshot, at tick " + run.At;
            }
            StateImage atSave = StateSections.Capture(w);
            SimSnapshot snap = w.CreateSnapshotBuffer();
            w.SaveSnapshot(snap);
            if (snap.Tick != run.At || w.ComputeStateHash() != reference[run.At])
            {
                return "SaveSnapshot changed the world or recorded the wrong tick";
            }

            for (int i = 0; i < run.K; i++)
            {
                Replayer.StepOnce(w, forwardFeed, sink);
            }
            StateImage? forward = run.Noisy ? null : StateSections.Capture(w);

            w.LoadSnapshot(snap);
            sabotage?.Invoke(w);
            if (w.Tick != run.At)
            {
                return "LoadSnapshot put the world at tick " + w.Tick + ", not " + run.At;
            }
            List<SectionDifference> restored = StateImage.Diff(atSave, StateSections.Capture(w));
            if (restored.Count > 0)
            {
                return "LoadSnapshot did not restore every section: " + StateImage.Describe(restored);
            }

            uint redoEnd = run.At + (uint)run.K;
            while (w.Tick < feed.EndTick)
            {
                Replayer.StepOnce(w, feed, sink);
                if (w.ComputeStateHash() != reference[w.Tick])
                {
                    List<SectionDifference> d = StateImage.Diff(HashAssert.FreshImageAt(run.Kind, feed, w.Tick), StateSections.Capture(w));
                    return "after rewinding, tick " + w.Tick + " differs from the uninterrupted run: " + StateImage.Describe(d);
                }
                if (w.Tick == redoEnd && forward != null)
                {
                    List<SectionDifference> d = StateImage.Diff(forward, StateSections.Capture(w));
                    if (d.Count > 0)
                    {
                        return "re-running the same k ticks gave different sections: " + StateImage.Describe(d);
                    }
                }
            }
            return null;
        }

        [Fact]
        public void ThousandRandomRuns_RewindAnywhereByOneToSixteenTicks_MatchTheUninterruptedRun()
        {
            DetKit.ParallelFuzz(_output, Seed, Runs, (ref Pcg32 rng) => FuzzRun.Generate(ref rng, DetKit.Minute, MaxK), r => Property(r), new Shrinker(),
                                r => r.ToString());
        }

        // 고정 사례: 판의 첫 틱(0), 끝 바로 앞(k = 16), 다른 입력으로 나아가기. 세 종류 모두
        [Theory]
        [InlineData(WorldKind.Practice)]
        [InlineData(WorldKind.Endless)]
        [InlineData(WorldKind.Duel)]
        public void EdgeTicks(WorldKind kind)
        {
            foreach ((uint at, int k, bool noisy) in new[] { (0U, 1, false), (0U, MaxK, true), (600U - MaxK, MaxK, false), (299U, 7, true) })
            {
                var run = new FuzzRun { Kind = kind, Profile = "dense", BotSeed = 0xED6EUL, WorldSeed = 0xED6E01UL, Ticks = 600, At = at, K = k, Noisy = noisy };
                Assert.Null(Property(run));
            }
        }

        // 이 퍼저가 잡는 결함(완료 정의: 일부러 결함을 넣어 본다). 복원 뒤 세계가 저장한 상태와 한 곳이라도 다르면 그 절의 이름으로 실패한다
        [Fact]
        public void CatchesAWorldThatIsNotFullyRestored()
        {
            var run = new FuzzRun { Kind = WorldKind.Practice, Profile = "dense", BotSeed = 0xBAD1UL, Ticks = 300, At = 120, K = 5 };
            Assert.Null(Property(run));
            string? rng = Property(run, w => w.HeldRng(RngStream.Weather).NextU32());
            Assert.NotNull(rng);
            Assert.Contains("Rng(16)", rng);
            string? id = Property(run, w => w.NewEntityId());
            Assert.NotNull(id);
            Assert.Contains("Rng(16)", id);                    // 개체 id 카운터는 Rng 절의 세계 공통 스칼라다
        }
    }
}

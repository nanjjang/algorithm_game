using System;
using System.Collections.Generic;
using Wizard.Core;
using Wizard.ReplayCli;
using Wizard.TestKit.Shared.Fuzz;
using Xunit;
using Xunit.Abstractions;

namespace Wizard.Sim.Tests.Determinism
{
    // PIPELINE 7.3 "직렬화 일치", CONTRACTS 3.2 WriteState/ReadState(재접속, 해시 불일치 복구, 관전 시작):
    // 임의 틱의 WriteState 를 새 세계에 ReadState 해 이어 돌린 해시가 끊지 않은 실행과 그 뒤 모든 틱에서 같다.
    // 절마다도 본다(절 목록은 코어의 절 열거형에서. StateSections): 받은 세계의 모든 절 = 보낸 세계의 모든 절, 받은 세계가 다시 쓴 바이트 = 받은 바이트.
    // 받는 세계의 절반은 먼저 다른 틱까지 돌려 둔다(ReadState 가 남김없이 덮어써야 한다).
    public sealed class SerializationFuzzTests
    {
        private const ulong Seed = 0x5E71_A112_0000_0001UL;
        private const int Runs = 1000;

        private readonly ITestOutputHelper _output;

        public SerializationFuzzTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private sealed class Shrinker : IFuzzShrinker<FuzzRun>
        {
            public IEnumerable<FuzzRun> Shrink(FuzzRun value) => value.Smaller();
        }

        private static byte[] Bytes(SimWorld w)
        {
            var b = new ByteWriter(4096);
            w.WriteState(b);
            return b.ToArray();
        }

        // tamper 는 이 퍼저가 결함을 잡는지 보는 자리(보낸 바이트를 받기 전에 건드린다)
        internal static string? Property(FuzzRun run, Func<byte[], StateImage, byte[]>? tamper = null)
        {
            var reference = new ulong[run.Ticks + 1];
            RecordFeed feed = DetKit.Feed(DetKit.Record(run.Kind, run.WorldSeed, run.BotSeed, run.Ticks, run.Profile, reference));
            var sink = new CountingSink();
            SimWorld source = DetKit.World(run.Kind, feed);
            Replayer.RunTo(source, feed, run.At, sink);
            byte[] sent = Bytes(source);
            StateImage sentImage = StateSections.Capture(source);
            if (tamper != null)
            {
                sent = tamper(sent, sentImage);
            }

            SimWorld target = DetKit.World(run.Kind, feed);
            if (run.DirtyTarget)
            {
                Replayer.RunTo(target, feed, (run.At + (uint)run.K * 37U) % run.Ticks, sink);
            }
            try
            {
                target.ReadState(sent);
            }
            catch (SimInvariantException e)
            {
                return "ReadState rejected the bytes WriteState made at tick " + run.At + ": " + e.Message;
            }
            if (target.Tick != run.At)
            {
                return "ReadState put the world at tick " + target.Tick + ", not " + run.At;
            }
            List<SectionDifference> d = StateImage.Diff(sentImage, StateSections.Capture(target));
            if (d.Count > 0)
            {
                return "the received world differs from the sent one: " + StateImage.Describe(d);
            }
            if (!Bytes(target).AsSpan().SequenceEqual(sent))
            {
                return "WriteState of the received world is not the bytes it received";
            }
            while (target.Tick < feed.EndTick)
            {
                Replayer.StepOnce(target, feed, sink);
                if (target.ComputeStateHash() != reference[target.Tick])
                {
                    List<SectionDifference> s = StateImage.Diff(HashAssert.FreshImageAt(run.Kind, feed, target.Tick), StateSections.Capture(target));
                    return "after ReadState, tick " + target.Tick + " differs from the uninterrupted run: " + StateImage.Describe(s);
                }
            }
            return null;
        }

        [Fact]
        public void ThousandRandomRuns_WriteStateAnywhereIntoAFreshWorld_ContinuesLikeTheUninterruptedRun()
        {
            DetKit.ParallelFuzz(_output, Seed, Runs, (ref Pcg32 rng) => FuzzRun.Generate(ref rng, DetKit.Minute, 1), r => Property(r), new Shrinker(),
                                r => r.ToString());
        }

        [Theory]
        [InlineData(WorldKind.Practice)]
        [InlineData(WorldKind.Endless)]
        [InlineData(WorldKind.Duel)]
        public void FirstAndLastTicks(WorldKind kind)
        {
            foreach ((uint at, bool dirty) in new[] { (0U, false), (0U, true), (599U, true), (300U, false) })
            {
                var run = new FuzzRun { Kind = kind, Profile = "dense", BotSeed = 0x5E1AUL, WorldSeed = 0x5E1A01UL, Ticks = 600, At = at, K = 1, DirtyTarget = dirty };
                Assert.Null(Property(run));
            }
        }

        // 이 퍼저가 잡는 결함: 보낸 바이트의 한 절 안 한 바이트가 바뀌면(ReadState 가 받든 거절하든) 실패하고, 받았다면 그 절의 이름이 나온다
        [Fact]
        public void CatchesACorruptedSection()
        {
            var run = new FuzzRun { Kind = WorldKind.Practice, Profile = "dense", BotSeed = 0xBAD2UL, Ticks = 300, At = 150, K = 1 };
            Assert.Null(Property(run));
            string? heat = Property(run, (bytes, image) => FlipLastByteOf(bytes, image, "Heat"));
            Assert.NotNull(heat);
            Assert.Contains("Heat(2)", heat);
        }

        // 그 절의 마지막 바이트(값 칸)의 낮은 비트를 뒤집는다
        private static byte[] FlipLastByteOf(byte[] bytes, StateImage image, string section)
        {
            int pos = 0;
            foreach (SectionBytes s in image.Sections)
            {
                pos += s.Bytes.Length;
                if (s.Info.Name == section)
                {
                    byte[] copy = (byte[])bytes.Clone();
                    copy[pos - 1] ^= 1;
                    return copy;
                }
            }
            throw new ArgumentException("no section " + section);
        }
    }
}

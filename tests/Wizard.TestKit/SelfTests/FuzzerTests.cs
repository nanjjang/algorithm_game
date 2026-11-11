using System;
using System.Collections.Generic;
using System.Linq;
using Wizard.Core;
using Wizard.TestKit.Shared.Fuzz;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Wizard.TestKit.SelfTests
{
    // 퍼저: 고정 씨앗에서 시작하고, 실패하면 재현용 씨앗과 줄인 입력을 낸다(M0-TE-01 수락 조건, test-engineer 규칙)
    public sealed class FuzzerTests
    {
        private const ulong Seed = 0x5eed_0001UL;
        private readonly ITestOutputHelper _output;

        public FuzzerTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private static string? NoByte42(byte[] b) => Array.IndexOf(b, (byte)0x2a) >= 0 ? "contains 0x2a" : null;

        [Fact]
        public void SameSeed_GivesTheSameCases_DifferentSeed_DoesNot()
        {
            FuzzGen<byte[]> gen = FuzzGens.Bytes(0, 32);
            string Run(ulong seed)
            {
                var seen = new List<string>();
                Fuzzer.Run(new FuzzOptions(seed, 50), gen, b => { seen.Add(FuzzFormat.Hex(b)); return null; }, null, FuzzFormat.Hex);
                return string.Join("|", seen);
            }
            Assert.Equal(Run(Seed), Run(Seed));
            Assert.NotEqual(Run(Seed), Run(Seed + 1));
        }

        // 시드에서 사례 씨앗을 뽑는 방법은 Pcg32(씨앗, "fuzz" 흐름)의 두 출력(상위, 하위)이다. 바꾸면 모든 재현 씨앗이 무효가 된다
        [Fact]
        public void CaseSeeds_ComeFromPcg32_AndAreStable()
        {
            var master = new Pcg32(Seed, 0x66757a7aUL);
            ulong first = ((ulong)master.NextU32() << 32) | master.NextU32();
            ulong second = ((ulong)master.NextU32() << 32) | master.NextU32();
            Assert.Equal(first, Fuzzer.CaseSeed(Seed, 0));
            Assert.Equal(second, Fuzzer.CaseSeed(Seed, 1));
        }

        [Fact]
        public void Failure_ReportsSeeds_AndAMinimalInput()
        {
            FuzzResult<byte[]> r = Fuzzer.Run(new FuzzOptions(Seed, 500), FuzzGens.Bytes(0, 64), NoByte42, FuzzShrinkers.Bytes, FuzzFormat.Hex);
            _output.WriteLine(r.Report);
            Assert.False(r.Passed);
            Assert.Equal(new byte[] { 0x2a }, r.Minimized);
            Assert.Contains((byte)0x2a, r.Original);
            Assert.Equal(Fuzzer.CaseSeed(Seed, r.FailingCase), r.FailingCaseSeed);
            Assert.Equal(r.FailingCase + 1, r.CasesRun);
            Assert.Contains("seed:            0x000000005eed0001", r.Report);
            Assert.Contains("FuzzOptions.Replay(0x" + r.FailingCaseSeed.ToString("x16") + "UL)", r.Report);
            Assert.Contains("minimized input: [1 bytes] 2a", r.Report);
            Assert.Contains("message:         contains 0x2a", r.Report);
        }

        [Fact]
        public void Replay_OfTheReportedCaseSeed_RebuildsTheSameInput_AndTheSameMinimum()
        {
            FuzzGen<byte[]> gen = FuzzGens.Bytes(0, 64);
            FuzzResult<byte[]> r = Fuzzer.Run(new FuzzOptions(Seed, 500), gen, NoByte42, FuzzShrinkers.Bytes, FuzzFormat.Hex);
            Assert.Equal(r.Original, Fuzzer.Generate(r.FailingCaseSeed, gen));
            FuzzResult<byte[]> again = Fuzzer.Run(FuzzOptions.Replay(r.FailingCaseSeed), gen, NoByte42, FuzzShrinkers.Bytes, FuzzFormat.Hex);
            Assert.False(again.Passed);
            Assert.Equal(1, again.CasesRun);
            Assert.Equal(r.Original, again.Original);
            Assert.Equal(r.Minimized, again.Minimized);
        }

        [Fact]
        public void Shrinking_ConvergesToTheBoundary_ForIntegersAndLists()
        {
            FuzzResult<long> n = Fuzzer.Run(new FuzzOptions(Seed, 100), FuzzGens.Int64Any(), v => v >= 1000 ? "too big" : null,
                FuzzShrinkers.Int64, FuzzFormat.Int64);
            Assert.False(n.Passed);
            Assert.Equal(1000L, n.Minimized);

            FuzzProperty<List<int>> sumAtMost60 = xs => xs.Sum() > 60 ? "sum > 60" : null;
            IFuzzShrinker<List<int>> shrinker = FuzzShrinkers.ListOf(FuzzShrinkers.Int32);
            FuzzResult<List<int>> l = Fuzzer.Run(new FuzzOptions(Seed, 200), FuzzGens.ListOf(FuzzGens.Int32(-50, 50), 0, 20),
                sumAtMost60, shrinker, xs => FuzzFormat.List(xs, FuzzFormat.Int32));
            _output.WriteLine(l.Report);
            Assert.False(l.Passed);
            Assert.Equal(61, l.Minimized.Sum());
            Assert.DoesNotContain(l.Minimized, x => x <= 0);
            // 줄인 입력은 1-최소다: 어느 후보(원소 하나 지우기, 원소 하나 줄이기)도 더는 실패하지 않는다
            Assert.All(shrinker.Shrink(l.Minimized), c => Assert.Null(sumAtMost60(c)));
        }

        [Fact]
        public void Exceptions_CountAsViolations_AndAreNamed()
        {
            FuzzResult<byte[]> r = Fuzzer.Run(new FuzzOptions(Seed, 100), FuzzGens.Bytes(0, 16),
                b => b.Length > 3 ? throw new InvalidOperationException("boom") : null, FuzzShrinkers.Bytes, FuzzFormat.Hex);
            Assert.False(r.Passed);
            Assert.Equal(4, r.Minimized.Length);
            Assert.Equal(new byte[4], r.Minimized);
            Assert.Contains("threw System.InvalidOperationException: boom", r.Report);
        }

        [Fact]
        public void ShrinkSteps_AreBounded()
        {
            var opt = new FuzzOptions(Seed, 100) { MaxShrinkSteps = 3 };
            FuzzResult<byte[]> r = Fuzzer.Run(opt, FuzzGens.Bytes(40, 64), NoByte42, FuzzShrinkers.Bytes, FuzzFormat.Hex);
            Assert.False(r.Passed);
            Assert.Equal(3, r.ShrinkSteps);
        }

        [Fact]
        public void PassingProperty_RunsEveryCase()
        {
            FuzzResult<byte[]> r = FuzzAssert.BytesHold(_output, new FuzzOptions(Seed, 300), 0, 8, b => b.Length <= 8 ? null : "long");
            Assert.True(r.Passed);
            Assert.Equal(300, r.CasesRun);
        }

        [Fact]
        public void FuzzAssert_ThrowsWithTheReport()
        {
            XunitException ex = Assert.Throws<XunitException>(() =>
                FuzzAssert.BytesHold(null, new FuzzOptions(Seed, 500), 0, 64, NoByte42));
            Assert.Contains("case seed:", ex.Message);
            Assert.Contains("minimized input: [1 bytes] 2a", ex.Message);
        }

        [Fact]
        public void Options_RejectZeroCases()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new FuzzOptions(Seed, 0));
        }
    }
}

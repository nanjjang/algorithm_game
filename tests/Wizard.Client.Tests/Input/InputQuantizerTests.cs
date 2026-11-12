using System;
using System.Linq;
using System.Reflection;
using Wizard.Client;
using Wizard.Core;
using Wizard.Sim.Input;
using Xunit;

namespace Wizard.Client.Tests.Input
{
    // CONTRACTS 6.5, spec 01 4.2: 틱 = floor((시각 − epoch) / tickSeconds) + D, 오프셋 = 틱 안의 위치 × 256(내림), 순번 없음.
    // 기대값은 정의에서 직접 셈한다(경계 k·tickSeconds의 앞뒤 1마이크로초 → (k − 1 + D, 255) / (k + D, 0)).
    public sealed class InputQuantizerTests
    {
        private const double Micro = 1e-6;
        private const double NominalTick = 1.0 / 60.0;

        public static TheoryData<double, double> Clocks => new TheoryData<double, double>
        {
            { 0.0, NominalTick },
            { 12345.678, NominalTick },
            { 1_000_000.0, NominalTick },
            { 3.25, NominalTick * 0.995 },        // 입력 시계 드리프트 보정 −0.5%
            { 98765.4321, NominalTick * 1.005 },  // +0.5%
        };

        private static readonly long[] BoundaryTicks = { 1, 2, 59, 60, 61, 255, 256, 3600, 216_000, 2_160_000 };   // 10시간 = 2,160,000틱

        [Theory]
        [MemberData(nameof(Clocks))]
        public void TickBoundaryPlusMinusOneMicrosecond(double epoch, double tickSeconds)
        {
            var clock = new InputClock(epoch, tickSeconds);
            foreach (int d in new[] { 0, 2, 3, 4 })
            {
                foreach (long k in BoundaryTicks)
                {
                    double boundary = epoch + k * tickSeconds;

                    QuantizedKey before = One(new RawKeyEvent(LogicalKey.Fire, true, boundary - Micro), clock, d);
                    Assert.Equal((uint)(k - 1 + d), before.Tick);
                    Assert.Equal(255, before.Offset);

                    QuantizedKey after = One(new RawKeyEvent(LogicalKey.Fire, true, boundary + Micro), clock, d);
                    Assert.Equal((uint)(k + d), after.Tick);
                    Assert.Equal(0, after.Offset);
                }
            }
        }

        [Theory]
        [MemberData(nameof(Clocks))]
        public void OffsetBoundariesInsideTick(double epoch, double tickSeconds)
        {
            var clock = new InputClock(epoch, tickSeconds);
            const long k = 1234;
            for (int j = 1; j < InputQuantizer.OffsetsPerTick; j++)
            {
                double edge = epoch + (k + j / 256.0) * tickSeconds;    // 1/256틱 ≈ 65µs라 ±1µs는 이웃 칸으로 넘지 않는다
                QuantizedKey lo = One(new RawKeyEvent(LogicalKey.Up, true, edge - Micro), clock, 0);
                QuantizedKey hi = One(new RawKeyEvent(LogicalKey.Up, true, edge + Micro), clock, 0);
                Assert.Equal((uint)k, lo.Tick);
                Assert.Equal(j - 1, lo.Offset);
                Assert.Equal((uint)k, hi.Tick);
                Assert.Equal(j, hi.Offset);
            }
        }

        [Fact]
        public void ExactlyAtEpochIsTickDOffsetZero()
        {
            var clock = new InputClock(500.0, NominalTick);
            QuantizedKey q = One(new RawKeyEvent(LogicalKey.Skill, false, 500.0), clock, 3);
            Assert.Equal(3u, q.Tick);
            Assert.Equal(0, q.Offset);
            Assert.Equal(LogicalKey.Skill, q.Key);
            Assert.False(q.Down);
        }

        [Fact]
        public void BeforeEpochAndNaNClampToEpoch()
        {
            var clock = new InputClock(500.0, NominalTick);
            QuantizedKey early = One(new RawKeyEvent(LogicalKey.Left, true, 499.0), clock, 2);
            Assert.Equal(2u, early.Tick);
            Assert.Equal(0, early.Offset);
            QuantizedKey nan = One(new RawKeyEvent(LogicalKey.Left, true, double.NaN), clock, 0);
            Assert.Equal(0u, nan.Tick);
            Assert.Equal(0, nan.Offset);
        }

        [Fact]
        public void HugeTimeClampsWithoutException()
        {
            var clock = new InputClock(0.0, NominalTick);
            QuantizedKey q = One(new RawKeyEvent(LogicalKey.Fire, true, 1e300), clock, 4);
            Assert.Equal(uint.MaxValue, q.Tick);
            Assert.Equal(255, q.Offset);
            QuantizedKey inf = One(new RawKeyEvent(LogicalKey.Fire, true, double.PositiveInfinity), clock, 0);
            Assert.Equal(uint.MaxValue, inf.Tick);
        }

        [Fact]
        public void SameTickAndOffsetAreInKeyClassOrderAndStableWithinClass()
        {
            var clock = new InputClock(0.0, NominalTick);
            double t = 10 * NominalTick + 0.3 * NominalTick;     // 모두 같은 (틱, 오프셋)
            RawKeyEvent[] raw =
            {
                new RawKeyEvent(LogicalKey.Pause, true, t),
                new RawKeyEvent(LogicalKey.Skill, true, t),
                new RawKeyEvent(LogicalKey.Left, true, t),
                new RawKeyEvent(LogicalKey.Fire, true, t),
                new RawKeyEvent(LogicalKey.Reverse, true, t),
                new RawKeyEvent(LogicalKey.Up, true, t),
                new RawKeyEvent(LogicalKey.Stance, true, t),
            };
            QuantizedKey[] got = Run(new InputQuantizer(), raw, clock, 0);
            // 자세·반전 0(들어온 순서: Reverse, Stance) → 방향 1(Left, Up) → J 2 → K 3 → 일시정지 4
            Assert.Equal(new[] { LogicalKey.Reverse, LogicalKey.Stance, LogicalKey.Left, LogicalKey.Up, LogicalKey.Fire, LogicalKey.Skill, LogicalKey.Pause },
                got.Select(q => q.Key).ToArray());
            Assert.All(got, q => Assert.Equal(got[0].Time, q.Time));
        }

        [Fact]
        public void DifferentOffsetsKeepTimeOrderEvenAgainstKeyClass()
        {
            var clock = new InputClock(0.0, NominalTick);
            RawKeyEvent[] raw =
            {
                new RawKeyEvent(LogicalKey.Skill, true, 5.10 * NominalTick),
                new RawKeyEvent(LogicalKey.Up, true, 5.50 * NominalTick),
            };
            QuantizedKey[] got = Run(new InputQuantizer(), raw, clock, 0);
            Assert.Equal(LogicalKey.Skill, got[0].Key);
            Assert.Equal(LogicalKey.Up, got[1].Key);
        }

        [Fact]
        public void TapInsideOneOffsetKeepsDownBeforeUp()
        {
            var clock = new InputClock(0.0, NominalTick);
            double t = 7.5 * NominalTick;
            RawKeyEvent[] raw = { new RawKeyEvent(LogicalKey.Fire, true, t), new RawKeyEvent(LogicalKey.Fire, false, t + Micro) };
            QuantizedKey[] got = Run(new InputQuantizer(), raw, clock, 0);
            Assert.True(got[0].Down);
            Assert.False(got[1].Down);
            Assert.Equal(got[0].Time, got[1].Time);
        }

        [Fact]
        public void OutputTimeNeverDecreasesWithinAndAcrossCalls()
        {
            var clock = new InputClock(0.0, NominalTick);
            var quantizer = new InputQuantizer();
            QuantizedKey[] first = Run(quantizer, new[]
            {
                new RawKeyEvent(LogicalKey.Up, true, 20.5 * NominalTick),
                new RawKeyEvent(LogicalKey.Up, false, 20.2 * NominalTick),     // 입력원이 시각을 거꾸로 줬다
            }, clock, 0);
            Assert.Equal(first[0].Time, first[1].Time);
            Assert.True(first[0].Down);       // 입력원이 정한 순서(누름 → 뗌)를 지킨다

            QuantizedKey[] second = Run(quantizer, new[] { new RawKeyEvent(LogicalKey.Left, true, 19.0 * NominalTick) }, clock, 0);
            Assert.Equal(first[1].Time, second[0].Time);

            QuantizedKey[] third = Run(quantizer, new[] { new RawKeyEvent(LogicalKey.Left, false, 30.0 * NominalTick + Micro) }, clock, 0);
            Assert.Equal(30u, third[0].Tick);
            Assert.Equal(0, third[0].Offset);
        }

        // 시계 재기준. 같은 인스턴스를 계속 쓰고 호출마다 시계가 바뀐다. 기대값은 바뀐 시계의 계약 식(CONTRACTS 6.5)이다.
        // 일시정지 60초: 재개 때 epoch가 60초 밀린다(spec 01 3.6). Pause 누름은 재개 전 시계로 양자화된다(ARCHITECTURE 6.1).
        [Fact]
        public void PauseResumeRebaseUsesNewClockFormula()
        {
            var q = new InputQuantizer();
            QuantizedKey[] pause = Run(q, new[] { new RawKeyEvent(LogicalKey.Pause, true, 160.0001) }, new InputClock(0.0, NominalTick), 0);
            Assert.Equal(9600u, pause[0].Tick);                      // floor(160.0001 × 60) = 9600
            Assert.Equal(1, pause[0].Offset);                        // 0.006 × 256 = 1.536

            QuantizedKey[] up = Run(q, new[] { new RawKeyEvent(LogicalKey.Up, true, 160.5001) }, new InputClock(60.0, NominalTick), 0);
            Assert.Equal(6030u, up[0].Tick);                         // floor(100.5001 × 60) = 6030
            Assert.Equal(1, up[0].Offset);

            var queue = new TickInputQueue();
            queue.Enqueue(up);
            var taken = new QuantizedKey[InputOrder.MaxEventsPerTickPerPlayer];
            Assert.Equal(1, queue.TakeForTick(6030, taken));
        }

        // 판 두 번(새 판·새 경기도 새 epoch). 두 번째 판의 첫 키는 그 판의 틱 60에 들어간다
        [Fact]
        public void NewRunClockAfterPreviousRunUsesNewClockFormula()
        {
            var q = new InputQuantizer();
            QuantizedKey[] first = Run(q, new[] { new RawKeyEvent(LogicalKey.Up, true, 700.0001) }, new InputClock(100.0, NominalTick), 0);
            Assert.Equal(36000u, first[0].Tick);

            QuantizedKey[] second = Run(q, new[] { new RawKeyEvent(LogicalKey.Fire, true, 801.0001) }, new InputClock(800.0, NominalTick), 0);
            Assert.Equal(60u, second[0].Tick);
            Assert.Equal(1, second[0].Offset);

            var queue = new TickInputQueue();
            queue.Enqueue(second);
            var taken = new QuantizedKey[InputOrder.MaxEventsPerTickPerPlayer];
            Assert.Equal(1, queue.TakeForTick(60, taken));
            Assert.Equal(LogicalKey.Fire, taken[0].Key);
            Assert.Equal(0, queue.Count);
        }

        // 지연 D와 틱 길이(입력 시계 보정)도 호출마다 바뀔 수 있다. 앞선 호출이 더 늦은 출력을 냈어도 바뀐 시계의 식 그대로다
        [Fact]
        public void DelayAndTickLengthChangeBetweenCallsUseFormula()
        {
            var q = new InputQuantizer();
            double ta = (600.0 + 128.5 / 256.0) * NominalTick;          // epoch 0 기준 600틱 + 128.5/256틱(≈ 10.008초)
            QuantizedKey[] a = Run(q, new[] { new RawKeyEvent(LogicalKey.Up, true, ta) }, new InputClock(0.0, NominalTick), 4);
            Assert.Equal(604u, a[0].Tick);
            Assert.Equal(128, a[0].Offset);

            // 하드웨어 시각은 뒤인데 시계(epoch 7, +0.5%)와 D(4 → 0)가 바뀌어 출력이 앞 호출보다 이르다. 그래도 계약 식 값이다
            double slow = NominalTick * 1.005;
            double tb = 7.0 + (185.0 + 64.5 / 256.0) * slow;            // epoch 7 기준 185틱 + 64.5/256틱(≈ 10.103초)
            Assert.True(tb > ta);
            QuantizedKey[] b = Run(q, new[] { new RawKeyEvent(LogicalKey.Up, false, tb) }, new InputClock(7.0, slow), 0);
            Assert.Equal(185u, b[0].Tick);
            Assert.Equal(64, b[0].Offset);
        }

        // 깨진 시각 하나(+∞, 범위를 넘는 값) 뒤의 키는 계약 식대로 나온다(깨진 시각을 기억하지 않는다)
        [Fact]
        public void BrokenTimeDoesNotPoisonLaterEvents()
        {
            var clock = new InputClock(0.0, NominalTick);
            var q = new InputQuantizer();
            QuantizedKey[] inf = Run(q, new[] { new RawKeyEvent(LogicalKey.Fire, true, double.PositiveInfinity) }, clock, 0);
            Assert.Equal(uint.MaxValue, inf[0].Tick);
            Assert.Equal(255, inf[0].Offset);

            QuantizedKey[] next = Run(q, new[] { new RawKeyEvent(LogicalKey.Fire, false, 1.0001) }, clock, 0);
            Assert.Equal(60u, next[0].Tick);
            Assert.Equal(1, next[0].Offset);

            QuantizedKey[] huge = Run(q, new[] { new RawKeyEvent(LogicalKey.Up, true, 1e300) }, clock, 0);
            Assert.Equal(uint.MaxValue, huge[0].Tick);
            QuantizedKey[] after = Run(q, new[] { new RawKeyEvent(LogicalKey.Up, false, 2.0001) }, clock, 0);
            Assert.Equal(120u, after[0].Tick);
            Assert.Equal(1, after[0].Offset);

            // 한 호출 안에서도 같다. 깨진 이벤트는 마지막 틱에 묶여 뒤로 가고, 앞뒤 이벤트는 식대로다
            QuantizedKey[] mixed = Run(q, new[]
            {
                new RawKeyEvent(LogicalKey.Left, true, 3.0001),
                new RawKeyEvent(LogicalKey.Left, false, double.PositiveInfinity),
                new RawKeyEvent(LogicalKey.Skill, true, 3.5001),
            }, clock, 0);
            Assert.Equal(180u, mixed[0].Tick);
            Assert.Equal(LogicalKey.Left, mixed[0].Key);
            Assert.Equal(210u, mixed[1].Tick);
            Assert.Equal(LogicalKey.Skill, mixed[1].Key);
            Assert.Equal(uint.MaxValue, mixed[2].Tick);
        }

        // NaN·−∞는 앞서 받은 시각으로 본다(앞이 없으면 epoch). 뒤의 키는 식대로다
        [Fact]
        public void NaNAndNegativeInfinityAfterEventsTakePreviousTime()
        {
            var clock = new InputClock(0.0, NominalTick);
            var q = new InputQuantizer();
            QuantizedKey[] got = Run(q, new[]
            {
                new RawKeyEvent(LogicalKey.Up, true, 4.0001),
                new RawKeyEvent(LogicalKey.Up, false, double.NaN),
                new RawKeyEvent(LogicalKey.Left, true, double.NegativeInfinity),
            }, clock, 0);
            Assert.All(got, k => Assert.Equal(240u, k.Tick));
            Assert.Equal(new[] { LogicalKey.Up, LogicalKey.Up, LogicalKey.Left }, got.Select(k => k.Key).ToArray());
            Assert.True(got[0].Down);
            Assert.False(got[1].Down);

            QuantizedKey[] later = Run(q, new[] { new RawKeyEvent(LogicalKey.Left, false, 5.0001) }, clock, 0);
            Assert.Equal(300u, later[0].Tick);
        }

        // 하드웨어 시각이 줄지 않는 정상 입력이면, 호출마다 시계·D가 무작위로 바뀌어도 모든 키가 그 호출 시계의 계약 식 값과 같다.
        // 시각은 1/256틱 칸의 한가운데에 두어 기대값을 정수로 정한다(내림이 흔들리지 않는다)
        [Fact]
        public void MonotoneHardwareTimeWithRandomClocksMatchesFormulaEveryCall()
        {
            var rng = new Pcg32(20261112UL, 7UL);
            var q = new InputQuantizer();
            double hw = 1000.0;
            for (int call = 0; call < 400; call++)
            {
                double tickSeconds = NominalTick * (0.995 + 0.01 * rng.NextInt(1001) / 1000.0);
                double epoch = hw - rng.NextInt(100_000) * NominalTick;          // 앞으로도 뒤로도 크게 바뀐다
                int d = rng.NextInt(8);
                var clock = new InputClock(epoch, tickSeconds);
                int n = rng.NextRange(1, 6);
                var raw = new RawKeyEvent[n];
                var expectTime = new long[n];
                for (int i = 0; i < n; i++)
                {
                    long units = (long)Math.Ceiling((hw - epoch) / tickSeconds * InputQuantizer.OffsetsPerTick) + rng.NextInt(300);
                    double t = epoch + (units + 0.5) / InputQuantizer.OffsetsPerTick * tickSeconds;
                    hw = t;
                    raw[i] = new RawKeyEvent(LogicalKey.Up, (i & 1) == 0, t);
                    expectTime[i] = units + ((long)d << 8);
                }
                QuantizedKey[] got = Run(q, raw, clock, d);
                for (int i = 0; i < n; i++)
                {
                    Assert.Equal((uint)(expectTime[i] >> 8), got[i].Tick);
                    Assert.Equal((int)(expectTime[i] & 255), got[i].Offset);
                    Assert.Equal(raw[i].Down, got[i].Down);
                }
            }
        }

        // 같은 시계에서 하드웨어 시각이 거꾸로 오면, 시계가 바뀐 뒤에도 앞서 받은 하드웨어 시각을 바뀐 시계로 셈한다
        [Fact]
        public void BackwardHardwareTimeAcrossClockChangeUsesPreviousTimeWithNewClock()
        {
            var q = new InputQuantizer();
            Run(q, new[] { new RawKeyEvent(LogicalKey.Up, true, 10.0001) }, new InputClock(0.0, NominalTick), 0);
            QuantizedKey[] got = Run(q, new[] { new RawKeyEvent(LogicalKey.Up, false, 9.0) }, new InputClock(5.0, NominalTick), 0);
            Assert.Equal(300u, got[0].Tick);                          // 10.0001초를 epoch 5로: floor(5.0001 × 60) = 300
            Assert.Equal(1, got[0].Offset);
        }

        [Fact]
        public void EmptyInputWritesNothing()
        {
            var clock = new InputClock(0.0, NominalTick);
            Assert.Equal(0, new InputQuantizer().Quantize(ReadOnlySpan<RawKeyEvent>.Empty, clock, 0, Span<QuantizedKey>.Empty));
        }

        [Fact]
        public void RejectsProgrammerErrors()
        {
            var good = new InputClock(0.0, NominalTick);
            var raw = new[] { new RawKeyEvent(LogicalKey.Up, true, 1.0) };
            var q = new InputQuantizer();
            Assert.Throws<ArgumentOutOfRangeException>(() => q.Quantize(raw, new InputClock(0.0, 0.0), 0, new QuantizedKey[1]));
            Assert.Throws<ArgumentOutOfRangeException>(() => q.Quantize(raw, new InputClock(0.0, -1.0), 0, new QuantizedKey[1]));
            Assert.Throws<ArgumentOutOfRangeException>(() => q.Quantize(raw, new InputClock(0.0, double.NaN), 0, new QuantizedKey[1]));
            Assert.Throws<ArgumentOutOfRangeException>(() => q.Quantize(raw, new InputClock(double.NaN, NominalTick), 0, new QuantizedKey[1]));
            Assert.Throws<ArgumentOutOfRangeException>(() => q.Quantize(raw, good, -1, new QuantizedKey[1]));
            Assert.Throws<ArgumentException>(() => q.Quantize(raw, good, 0, Array.Empty<QuantizedKey>()));
            Assert.Throws<ArgumentException>(() => q.Quantize(new[] { new RawKeyEvent((LogicalKey)9, true, 1.0) }, good, 0, new QuantizedKey[1]));
        }

        // "틱과 무관하게 부른다": 틱 번호를 받지 않고, 순번 없는 QuantizedKey를 낸다(CONTRACTS 6.5, 3.1)
        [Fact]
        public void SignatureMatchesContractAndCarriesNoSequence()
        {
            MethodInfo m = typeof(InputQuantizer).GetMethod(nameof(InputQuantizer.Quantize))!;
            Assert.Equal(typeof(int), m.ReturnType);
            ParameterInfo[] p = m.GetParameters();
            Assert.Equal(4, p.Length);
            Assert.Equal(typeof(ReadOnlySpan<RawKeyEvent>), p[0].ParameterType);
            Assert.Equal(typeof(InputClock).MakeByRefType(), p[1].ParameterType);
            Assert.True(p[1].IsIn);
            Assert.Equal(typeof(int), p[2].ParameterType);
            Assert.Equal("inputDelayTicks", p[2].Name);
            Assert.Equal(typeof(Span<QuantizedKey>), p[3].ParameterType);

            string[] fields = typeof(QuantizedKey).GetFields().Select(f => f.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();
            Assert.Equal(new[] { "Down", "Key", "Offset", "Tick" }, fields);
        }

        private static QuantizedKey One(RawKeyEvent e, InputClock clock, int d) => Run(new InputQuantizer(), new[] { e }, clock, d)[0];

        private static QuantizedKey[] Run(InputQuantizer q, RawKeyEvent[] raw, InputClock clock, int d)
        {
            var output = new QuantizedKey[raw.Length];
            int n = q.Quantize(raw, clock, d, output);
            Assert.Equal(raw.Length, n);
            return output;
        }
    }
}

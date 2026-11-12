using System;
using Wizard.Client.Run;
using Wizard.Core;
using Xunit;

namespace Wizard.Client.Tests.Run
{
    // 수락 조건 1: FixedTickDriver 가 부를 SimClock(NextTickDue, Advance, Alpha)이 단조 시계 값만 받는다.
    // 프레임 간격이 흔들려도 틱 수가 실제 시간과 1틱 안에서 맞는다.
    // 기대 틱 수는 정수로 셈한다: 시작에서 µs 가 지났으면 floor(µs × 60 / 1,000,000).
    public sealed class SimClockTests
    {
        public static TheoryData<long, ulong> Starts => new TheoryData<long, ulong>
        {
            { 0L, 1UL },
            { 4_000_000_321L, 2UL },            // 기기가 켜진 지 4000초
            { 950_400_000_017L, 3UL },          // 11일
        };

        [Theory]
        [MemberData(nameof(Starts))]
        public void TickCountStaysWithinOneTickOfRealTimeUnderJitteryFrames(long startUs, ulong seed)
        {
            var clock = new SimClock(RunKit.Sec(startUs));
            var rng = new Pcg32(seed, 5UL);
            long elapsed = 0;
            const long Hours2 = 2L * 3600 * RunKit.UsPerSecond;
            long maxDiff = 0;
            while (elapsed < Hours2)
            {
                int r = rng.NextInt(1000);
                elapsed += r < 5 ? rng.NextRange(200_000, 900_000)       // 걸림
                         : r < 300 ? rng.NextRange(500, 4_000)           // 아주 짧은 프레임
                         : rng.NextRange(4_000, 40_000);
                double now = RunKit.Sec(startUs + elapsed);
                while (clock.NextTickDue(now))
                {
                    clock.Advance();
                }
                long expected = RunKit.TicksAt(elapsed);
                long diff = Math.Abs((long)clock.Tick - expected);
                maxDiff = Math.Max(maxDiff, diff);
                Assert.True(diff <= 1, $"at {elapsed}us: tick {clock.Tick}, real {expected}");
                double alpha = clock.Alpha(now);
                Assert.InRange(alpha, 0.0, 1.0);
            }
            Assert.True(clock.Tick >= 2 * 3600 * 60 - 1);
        }

        [Fact]
        public void TicksRunOnlyAfterTheirIntervalHasPassed()
        {
            var clock = new SimClock(10.0);
            Assert.False(clock.NextTickDue(10.0));
            Assert.False(clock.NextTickDue(10.0 + 0.016));
            Assert.True(clock.NextTickDue(10.0 + 0.0167));
            clock.Advance();
            Assert.Equal(1U, clock.Tick);
            Assert.False(clock.NextTickDue(10.0 + 0.0167));
        }

        [Fact]
        public void TimeGoingBackwardsDoesNotMoveTheClock()
        {
            var clock = new SimClock(RunKit.Sec(RunKit.StartUs));
            double t1 = RunKit.Sec(RunKit.StartUs + 1_008_000);           // 60.48틱
            while (clock.NextTickDue(t1))
            {
                clock.Advance();
            }
            Assert.Equal(60U, clock.Tick);
            double a1 = clock.Alpha(t1);
            Assert.InRange(a1, 0.47, 0.49);

            double back = RunKit.Sec(RunKit.StartUs + 100_000);
            Assert.False(clock.NextTickDue(back));
            Assert.Equal(a1, clock.Alpha(back));                         // 앞서 받은 시각으로 본다
            Assert.Equal(60U, clock.Tick);

            // 거꾸로 간 뒤 조금 앞으로: 앞서 받은 가장 늦은 시각보다 늦어야 진행한다
            Assert.False(clock.NextTickDue(RunKit.Sec(RunKit.StartUs + 1_010_000)));
            Assert.True(clock.NextTickDue(RunKit.Sec(RunKit.StartUs + 1_017_000)));
        }

        [Theory]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        [InlineData(double.NegativeInfinity)]
        public void NonFiniteTimeIsRejected(double bad)
        {
            var clock = new SimClock(1.0);
            Assert.Throws<ArgumentOutOfRangeException>(() => clock.NextTickDue(bad));
            Assert.Throws<ArgumentOutOfRangeException>(() => clock.Alpha(bad));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SimClock(bad));
            Assert.Equal(0U, clock.Tick);
        }

        [Fact]
        public void AlphaIsThePartOfTheCurrentTickThatHasPassed()
        {
            var clock = new SimClock(RunKit.Sec(RunKit.StartUs));
            long us = 10 * RunKit.UsPerSecond / 60 + RunKit.UsPerSecond / 240;   // 10.25틱
            double now = RunKit.Sec(RunKit.StartUs + us);
            while (clock.NextTickDue(now))
            {
                clock.Advance();
            }
            Assert.Equal(10U, clock.Tick);
            Assert.InRange(clock.Alpha(now), 0.249, 0.251);
        }

        [Fact]
        public void StartTickPlacesTheEpochBehindTheStartTime()
        {
            var clock = new SimClock(100.0, 600);
            Assert.Equal(600U, clock.Tick);
            Assert.InRange(clock.EpochSeconds, 89.999, 90.001);
            Assert.False(clock.NextTickDue(100.0));
            Assert.True(clock.NextTickDue(100.0 + 1.0 / 60 + 1e-6));
        }

        [Fact]
        public void PauseStopsTicksAndResumeShiftsTheEpochByThePausedTime()
        {
            var clock = new SimClock(RunKit.Sec(RunKit.StartUs));
            long pauseAt = 100 * RunKit.UsPerSecond / 60 + RunKit.UsPerSecond / 240;    // 100.25틱
            double p = RunKit.Sec(RunKit.StartUs + pauseAt);
            while (clock.NextTickDue(p))
            {
                clock.Advance();
            }
            Assert.Equal(100U, clock.Tick);
            clock.Pause();
            Assert.True(clock.IsPaused);
            double frozen = clock.Alpha(p);
            Assert.InRange(frozen, 0.249, 0.251);

            for (long us = pauseAt; us <= pauseAt + 3 * RunKit.UsPerSecond; us += 16_667)
            {
                double now = RunKit.Sec(RunKit.StartUs + us);
                Assert.False(clock.NextTickDue(now));
                Assert.Equal(frozen, clock.Alpha(now));
            }
            double r = RunKit.Sec(RunKit.StartUs + pauseAt + 3 * RunKit.UsPerSecond);
            Assert.InRange(clock.PausedSeconds(r), 2.999, 3.001);
            double paused = clock.Resume(r);
            Assert.InRange(paused, 2.999, 3.001);
            Assert.False(clock.IsPaused);
            Assert.InRange(clock.Alpha(r), 0.249, 0.251);                  // 멈춘 자리에서 이어진다
            Assert.Throws<InvalidOperationException>(() => clock.Resume(r));

            double later = RunKit.Sec(RunKit.StartUs + pauseAt + 4 * RunKit.UsPerSecond);
            while (clock.NextTickDue(later))
            {
                clock.Advance();
            }
            Assert.InRange((long)clock.Tick, 159L, 161L);                   // 100 + 60
        }

        [Fact]
        public void HaltIsFinal()
        {
            var clock = new SimClock(0.0);
            while (clock.NextTickDue(0.1))
            {
                clock.Advance();
            }
            double a = clock.Alpha(0.1);
            clock.Halt();
            Assert.True(clock.IsHalted);
            Assert.False(clock.NextTickDue(100.0));
            Assert.Equal(a, clock.Alpha(100.0));
            Assert.Throws<InvalidOperationException>(() => clock.Pause());
        }

        [Fact]
        public void InputClockFollowsTheEpoch()
        {
            var clock = new SimClock(50.0);
            Assert.Equal(50.0, clock.InputClock.EpochSeconds);
            Assert.Equal(SimClock.DefaultTickSeconds, clock.InputClock.TickSeconds);
            Assert.Equal(1.0 / 60, SimClock.DefaultTickSeconds);
            // 틱을 돌리지 않은 채 0.5초 지나 멈춤: 밀린 틱의 시간도 틱이 돌지 않은 실제 시간이다(멈춘 순간의 비율은 1에 묶인다)
            clock.NextTickDue(50.5);
            clock.Pause();
            double paused = clock.Resume(52.5);
            Assert.InRange(clock.InputClock.EpochSeconds, 52.5 - 1.0 / 60 - 1e-9, 52.5 - 1.0 / 60 + 1e-9);
            Assert.InRange(paused, 52.5 - 1.0 / 60 - 50.0 - 1e-9, 52.5 - 1.0 / 60 - 50.0 + 1e-9);
        }
    }
}

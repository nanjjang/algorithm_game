using System;
using System.Reflection;
using Wizard.Client.Run;
using Wizard.ViewLogic.Presentation.Limits;
using Xunit;

namespace Wizard.ViewLogic.Tests.Presentation.Limits
{
    // spec 13 18.6 TV-191·192, 14.4절(effects.md 0.2)
    public sealed class HitstopVectorTests
    {
        [Theory]
        [InlineData(MotionPreset.Full, 1f)]
        [InlineData(MotionPreset.Reduced, 0.5f)]
        [InlineData(MotionPreset.Minimal, 0f)]
        public void S13_TV_191__HitstopScalePerPresetAndZeroWhenOff(MotionPreset preset, float on)
        {
            Assert.Equal(on, MotionPresets.HitstopScale(new MotionSettings(preset, 100, 100, true)));
            Assert.Equal(0f, MotionPresets.HitstopScale(new MotionSettings(preset, 100, 100, false)));

            var clock = new HitstopClock();
            int applied = clock.Request(7, 120, 0, new MotionSettings(preset, 100, 100, true));
            Assert.Equal((int)Math.Round(120 * on), applied);
            Assert.Equal(0, clock.Request(8, 120, 0, new MotionSettings(preset, 100, 100, false)));
            Assert.False(clock.IsFrozen(8, 1));
        }

        [Fact]
        public void S13_TV_192__CoreTicksKeepAdvancingWhilePicturesAreFrozen()
        {
            // 같은 프레임 시각으로 두 틱 시계를 돌린다. 한쪽 프레임 루프만 그림 정지를 건다.
            const double frame = 1.0 / 144.0;
            var plain = new SimClock(0.0);
            var withHitstop = new SimClock(0.0);
            var hitstop = new HitstopClock();
            const long target = 42;
            int frozenFrames = 0;
            int ticksWhileFrozen = 0;
            for (int f = 1; f <= 144; f++)
            {
                double now = f * frame;
                long nowMs = (long)Math.Round(now * 1000.0);
                if (f % 20 == 1)
                {
                    hitstop.Request(target, 120, nowMs, MotionSettings.Default);   // PlayerHit: 0.12초
                }
                bool frozen = hitstop.IsFrozen(target, nowMs);
                while (plain.NextTickDue(now))
                {
                    plain.Advance();
                }
                while (withHitstop.NextTickDue(now))
                {
                    withHitstop.Advance();
                    if (frozen)
                    {
                        ticksWhileFrozen++;
                    }
                }
                if (frozen)
                {
                    frozenFrames++;
                    Assert.Equal(0f, hitstop.VisualDeltaSeconds(target, nowMs, (float)frame));   // 그림만 멈춘다
                }
                Assert.Equal(plain.Tick, withHitstop.Tick);                                      // 게임 시간은 멈추지 않는다
                hitstop.Prune(nowMs);
            }
            Assert.True(frozenFrames > 0);
            Assert.True(ticksWhileFrozen > 0);
            Assert.Equal(60u, withHitstop.Tick);
            Assert.False(withHitstop.IsPaused);

            // 그림 정지 시계는 틱 시계·세계를 쥐지 않는다(멈출 수단이 없다)
            foreach (FieldInfo fi in typeof(HitstopClock).GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public))
            {
                string ns = fi.FieldType.Namespace ?? "";
                Assert.False(ns.StartsWith("Wizard.Sim", StringComparison.Ordinal) || ns.StartsWith("Wizard.Client", StringComparison.Ordinal)
                             || ns.StartsWith("Wizard.Netcode", StringComparison.Ordinal), fi.Name + " : " + fi.FieldType);
            }
        }

        [Fact]
        public void OverlappingRequestsOnlyExtend()
        {
            var clock = new HitstopClock();
            clock.Request(1, 200, 0, MotionSettings.Default);
            clock.Request(1, 50, 100, MotionSettings.Default);   // 150 에 끝나는 요청이 200 을 줄이지 않는다
            Assert.True(clock.IsFrozen(1, 199));
            Assert.False(clock.IsFrozen(1, 200));
            Assert.False(clock.IsFrozen(2, 50));
            clock.Prune(200);
            Assert.Equal(0, clock.ActiveCount);
        }

        [Fact]
        public void RequestInTicksConvertsAt60PerSecond()
        {
            var clock = new HitstopClock();
            Assert.Equal(117, clock.RequestTicks(3, 7, 0, MotionSettings.Default));       // 7틱 = 116.7ms → 반올림
            Assert.Equal(100, clock.RequestTicks(4, 6, 0, MotionSettings.Default));       // 6틱 = 0.1초
            Assert.Equal(50, clock.RequestTicks(5, 6, 0, MotionSettings.Of(MotionPreset.Reduced)));
        }
    }
}

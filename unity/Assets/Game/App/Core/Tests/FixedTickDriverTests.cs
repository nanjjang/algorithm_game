#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wizard.Client;
using Wizard.Sim.Input;

namespace Wizard.Unity.App.Core.Tests
{
    // M1-UE-01 수락 조건 3: FixedTickDriver 가 ARCHITECTURE 6.1 의 순서로 돌고 Time.timeScale 을 건드리지 않는다
    public sealed class FixedTickDriverTests
    {
        private const double T = 1.0 / 60;

        [Test]
        public void RunFrame_FollowsArchitecture61Order()
        {
            var log = new List<string>();
            var clock = new FakeClock();
            var driver = new FixedTickDriver(clock, new FakeInput());
            var session = new FakeSession(log, 0);
            driver.AddPresenter(new RecordingPresenter(log, "a"));
            driver.AddPresenter(new RecordingPresenter(log, "b"));
            driver.SetSession(session);

            clock.NowSeconds = 2.5 * T;
            double alpha = driver.RunFrame();

            Assert.That(log, Is.EqualTo(new[]
            {
                "input",
                "step:0", "advance",
                "step:1", "advance",
                "alpha",
                "events:a:0,1", "events:b:0,1",
                "render:a", "render:b",
            }));
            Assert.That(alpha, Is.EqualTo(0.5).Within(1e-6));
            Assert.That(session.Tick, Is.EqualTo(2u));
            Assert.That(driver.TickCount, Is.EqualTo(2));
            Assert.That(driver.FrameCount, Is.EqualTo(1));
        }

        [Test]
        public void RunFrame_NoTickDue_StillTakesInputAndRenders()
        {
            var log = new List<string>();
            var clock = new FakeClock();
            var driver = new FixedTickDriver(clock, new FakeInput());
            var presenter = new RecordingPresenter(log, "a");
            var session = new FakeSession(log, 0);
            driver.AddPresenter(presenter);
            driver.SetSession(session);

            clock.NowSeconds = 0.5 * T;
            driver.RunFrame();

            Assert.That(log, Is.EqualTo(new[] { "input", "alpha", "events:a:", "render:a" }));
            Assert.That(presenter.LastView, Is.SameAs(session.View));
            Assert.That(presenter.LastAlpha, Is.EqualTo(0.5).Within(1e-6));
        }

        [Test]
        public void TickCount_FollowsTheMonotonicClock_NotTheFrameCount()
        {
            var log = new List<string>();
            var clock = new FakeClock();
            var driver = new FixedTickDriver(clock, new FakeInput());
            var session = new FakeSession(log, 0) { LogTicks = false };
            driver.SetSession(session);

            var random = new System.Random(20261112);       // 표시 쪽 테스트의 프레임 간격 흔들기. 코어 난수기와 무관하다
            double now = 0;
            while (now < 10.0)
            {
                now += (1 + random.Next(50)) / 1000.0;     // 1~50ms
                clock.NowSeconds = now;
                driver.RunFrame();
                long expected = (long)Math.Floor(now / T);
                Assert.That((long)session.Tick, Is.InRange(expected - 1, expected + 1), "now=" + now);
            }
            Assert.That(driver.FrameCount, Is.LessThan(driver.TickCount), "frames are sparser than ticks in this run");
        }

        [Test]
        public void LongStall_CatchesUpEveryTickInOneFrame()
        {
            var log = new List<string>();
            var clock = new FakeClock();
            var driver = new FixedTickDriver(clock, new FakeInput());
            var session = new FakeSession(log, 0) { LogTicks = false };
            driver.SetSession(session);

            clock.NowSeconds = 0;
            driver.RunFrame();
            clock.NowSeconds = 0.5;                        // 0.5초 멈춤
            driver.RunFrame();
            Assert.That(session.Tick, Is.EqualTo(30u));
        }

        [Test]
        public void TimeScale_IsNeitherFollowedNorChanged()
        {
            float saved = Time.timeScale;
            try
            {
                Time.timeScale = 0f;
                var log = new List<string>();
                var clock = new FakeClock();
                var driver = new FixedTickDriver(clock, new FakeInput());
                var session = new FakeSession(log, 0) { LogTicks = false };
                driver.SetSession(session);
                for (int frame = 1; frame <= 60; frame++)
                {
                    clock.NowSeconds = frame * T + T / 4;
                    driver.RunFrame();
                }
                Assert.That(session.Tick, Is.EqualTo(60u));
                Assert.That(Time.timeScale, Is.EqualTo(0f));

                Time.timeScale = 2f;
                clock.NowSeconds += 1.0;
                driver.RunFrame();
                Assert.That(session.Tick, Is.EqualTo(120u));
                Assert.That(Time.timeScale, Is.EqualTo(2f));
            }
            finally
            {
                Time.timeScale = saved;
            }
        }

        [Test]
        public void NoSession_DrainsAndDiscardsInput()
        {
            var clock = new FakeClock();
            var input = new FakeInput();
            var driver = new FixedTickDriver(clock, input);
            for (int i = 0; i < 40; i++)
            {
                input.Pending.Add(new RawKeyEvent(LogicalKey.Fire, i % 2 == 0, 0.01 * i));
            }

            Assert.That(driver.RunFrame(), Is.EqualTo(0));
            Assert.That(input.Pending, Is.Empty);
            Assert.That(driver.DiscardedKeyCount, Is.EqualTo(40));
            Assert.That(driver.TickCount, Is.EqualTo(0));
        }

        [Test]
        public void SetSession_StaleKeysAreDropped_HeldKeysArriveAsPressesAtStart()
        {
            var log = new List<string>();
            var clock = new FakeClock();
            var input = new FakeInput();
            var driver = new FixedTickDriver(clock, input);

            input.Pending.Add(new RawKeyEvent(LogicalKey.Up, true, 0.1));
            input.Pending.Add(new RawKeyEvent(LogicalKey.Fire, true, 0.2));
            input.Pending.Add(new RawKeyEvent(LogicalKey.Skill, true, 0.3));
            input.Pending.Add(new RawKeyEvent(LogicalKey.Skill, false, 0.35));
            input.Current = (KeyMask)((1 << (int)LogicalKey.Fire) | (1 << (int)LogicalKey.Up));

            clock.NowSeconds = 1.0;
            var session = new FakeSession(log, 1.0);
            driver.SetSession(session);
            Assert.That(input.Pending, Is.Empty, "stale keys are dropped when the session is set");
            Assert.That(driver.DiscardedKeyCount, Is.EqualTo(4));

            // 꽂은 뒤에 생긴 키는 첫 프레임에 누름들 뒤로 그대로 온다
            input.Pending.Add(new RawKeyEvent(LogicalKey.Down, true, 1.004));
            clock.NowSeconds = 1.01;
            driver.RunFrame();

            Assert.That(session.Received.Count, Is.EqualTo(3));
            AssertKey(session.Received[0], LogicalKey.Up, true, 1.0);
            AssertKey(session.Received[1], LogicalKey.Fire, true, 1.0);
            AssertKey(session.Received[2], LogicalKey.Down, true, 1.004);

            // 그다음 프레임부터는 입력원을 그대로 잇는다
            input.Pending.Add(new RawKeyEvent(LogicalKey.Fire, false, 1.015));
            clock.NowSeconds = 1.02;
            driver.RunFrame();
            Assert.That(session.Received.Count, Is.EqualTo(4));
            AssertKey(session.Received[3], LogicalKey.Fire, false, 1.015);
        }

        [Test]
        public void SetSession_AllNineKeysHeld_ArriveBeforeLaterKeys()
        {
            var clock = new FakeClock { NowSeconds = 2.0 };
            var input = new FakeInput { Current = (KeyMask)0x1FF };
            var driver = new FixedTickDriver(clock, input);
            var session = new FakeSession(new List<string>(), 2.0);
            driver.SetSession(session);
            input.Pending.Add(new RawKeyEvent(LogicalKey.Pause, false, 2.001));
            clock.NowSeconds = 2.005;
            driver.RunFrame();
            Assert.That(session.Received.Count, Is.EqualTo(10));
            for (int k = 0; k < 9; k++)
            {
                AssertKey(session.Received[k], (LogicalKey)k, true, 2.0);
            }
            AssertKey(session.Received[9], LogicalKey.Pause, false, 2.001);
        }

        [Test]
        public void SetSession_NothingHeld_NoSyntheticKeys()
        {
            var log = new List<string>();
            var clock = new FakeClock();
            var input = new FakeInput();
            var driver = new FixedTickDriver(clock, input);
            input.Pending.Add(new RawKeyEvent(LogicalKey.Up, true, 0.1));
            input.Pending.Add(new RawKeyEvent(LogicalKey.Up, false, 0.2));
            var session = new FakeSession(log, 1.0);
            driver.SetSession(session);
            clock.NowSeconds = 1.0;
            driver.RunFrame();
            Assert.That(session.Received, Is.Empty);
        }

        [Test]
        public void SetSessionNull_StopsTicking()
        {
            var log = new List<string>();
            var clock = new FakeClock();
            var driver = new FixedTickDriver(clock, new FakeInput());
            var session = new FakeSession(log, 0) { LogTicks = false };
            driver.SetSession(session);
            clock.NowSeconds = 10 * T;
            driver.RunFrame();
            Assert.That(session.Tick, Is.EqualTo(10u));

            driver.SetSession(null);
            clock.NowSeconds = 20 * T;
            driver.RunFrame();
            Assert.That(session.Tick, Is.EqualTo(10u));
            Assert.That(driver.Session, Is.Null);
        }

        [Test]
        public void SessionFault_PropagatesToTheCaller()
        {
            var log = new List<string>();
            var clock = new FakeClock();
            var driver = new FixedTickDriver(clock, new FakeInput());
            var session = new FakeSession(log, 0) { ThrowAtTick = 1 };
            driver.SetSession(session);
            clock.NowSeconds = 3 * T;
            Assert.Throws<InvalidOperationException>(() => driver.RunFrame());
        }

        [Test]
        public void Presenters_AddOnceAndRemove()
        {
            var log = new List<string>();
            var clock = new FakeClock();
            var driver = new FixedTickDriver(clock, new FakeInput());
            var a = new RecordingPresenter(log, "a");
            driver.AddPresenter(a);
            driver.AddPresenter(a);
            driver.SetSession(new FakeSession(log, 0));
            clock.NowSeconds = 0.5 * T;
            driver.RunFrame();
            Assert.That(log, Is.EqualTo(new[] { "input", "alpha", "events:a:", "render:a" }));

            log.Clear();
            Assert.That(driver.RemovePresenter(a), Is.True);
            driver.RunFrame();
            Assert.That(log, Is.EqualTo(new[] { "input", "alpha" }));
        }

        private static void AssertKey(RawKeyEvent e, LogicalKey key, bool down, double time)
        {
            Assert.That(e.Key, Is.EqualTo(key), "key");
            Assert.That(e.Down, Is.EqualTo(down), "down");
            Assert.That(e.TimeSeconds, Is.EqualTo(time), "time");
        }
    }
}

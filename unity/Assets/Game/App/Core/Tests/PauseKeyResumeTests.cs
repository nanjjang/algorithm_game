#nullable enable
using System;
using NUnit.Framework;
using Wizard.Client;
using Wizard.Sim.Input;

namespace Wizard.Unity.App.Core.Tests
{
    // [임시] 일시정지 화면이 생기기 전의 Pause 키 풀기
    public sealed class PauseKeyResumeTests
    {
        [Test]
        public void ReleaseOfThePressThatPaused_DoesNotResume()
        {
            var session = new FakePausable { Paused = true };
            var input = new FakeInput();
            input.Pending.Add(new RawKeyEvent(LogicalKey.Pause, false, 1.0));
            var resume = new PauseKeyResume(session);
            Assert.That(resume.Wrap(input).Drain(new RawKeyEvent[8]), Is.EqualTo(1));
            Assert.That(session.ResumeRequests, Is.EqualTo(0));
        }

        [Test]
        public void PressAndReleaseWhilePaused_RequestsResumeOnce()
        {
            var session = new FakePausable { Paused = true };
            var input = new FakeInput();
            input.Pending.Add(new RawKeyEvent(LogicalKey.Pause, true, 1.0));
            input.Pending.Add(new RawKeyEvent(LogicalKey.Pause, false, 1.1));
            var resume = new PauseKeyResume(session);
            var buffer = new RawKeyEvent[8];
            Assert.That(resume.Wrap(input).Drain(buffer), Is.EqualTo(2));
            Assert.That(session.ResumeRequests, Is.EqualTo(1));

            // 키는 바꾸지 않고 넘긴다
            Assert.That(buffer[0].Key, Is.EqualTo(LogicalKey.Pause));
            Assert.That(buffer[0].Down, Is.True);
            Assert.That(buffer[1].Down, Is.False);
            Assert.That(buffer[1].TimeSeconds, Is.EqualTo(1.1));
        }

        [Test]
        public void PressAndReleaseAcrossFrames_RequestsResume()
        {
            var session = new FakePausable { Paused = true };
            var input = new FakeInput();
            var resume = new PauseKeyResume(session);
            var buffer = new RawKeyEvent[8];

            input.Pending.Add(new RawKeyEvent(LogicalKey.Pause, true, 1.0));
            resume.Wrap(input).Drain(buffer);
            Assert.That(session.ResumeRequests, Is.EqualTo(0));
            input.Pending.Add(new RawKeyEvent(LogicalKey.Pause, false, 1.5));
            resume.Wrap(input).Drain(buffer);
            Assert.That(session.ResumeRequests, Is.EqualTo(1));
        }

        [Test]
        public void NotPaused_PauseKeyDoesNothing_AndOtherKeysNeverResume()
        {
            var session = new FakePausable { Paused = false };
            var input = new FakeInput();
            var resume = new PauseKeyResume(session);
            var buffer = new RawKeyEvent[8];
            input.Pending.Add(new RawKeyEvent(LogicalKey.Pause, true, 1.0));
            resume.Wrap(input).Drain(buffer);

            // 누름이 멈추기 전이었으므로 멈춘 뒤의 뗌으로는 풀지 않는다
            session.Paused = true;
            input.Pending.Add(new RawKeyEvent(LogicalKey.Pause, false, 1.1));
            input.Pending.Add(new RawKeyEvent(LogicalKey.Fire, true, 1.2));
            input.Pending.Add(new RawKeyEvent(LogicalKey.Fire, false, 1.3));
            resume.Wrap(input).Drain(buffer);
            Assert.That(session.ResumeRequests, Is.EqualTo(0));
        }

        [Test]
        public void Unwrapped_Throws()
        {
            var resume = new PauseKeyResume(new FakePausable());
            Assert.Throws<InvalidOperationException>(() => resume.Drain(new RawKeyEvent[1]));
        }
    }
}

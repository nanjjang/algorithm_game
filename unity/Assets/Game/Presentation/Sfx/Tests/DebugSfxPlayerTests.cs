#nullable enable
using NUnit.Framework;
using Wizard.ViewLogic.Presentation;

namespace Wizard.Unity.Presentation.Sfx.Tests
{
    // 수락 2: 빈 소리 재생기는 식별자(와 연속 스침의 음높이 단)를 남긴다. 소리를 내지 않는다
    public sealed class DebugSfxPlayerTests
    {
        private long _now;

        [Test]
        public void Play_LeavesALineWithIdentifierAndPitchStep()
        {
            var p = new DebugSfxPlayer(() => _now);
            p.Play("sfx_c07");
            p.Play("sfx_c10", 3);
            Assert.That(p.Count, Is.EqualTo(2));
            Assert.That(p.PlayCount, Is.EqualTo(2));
            Assert.That(p.Mark(0).Label, Is.EqualTo("sfx_c07"));
            Assert.That(p.Mark(1).Label, Is.EqualTo("sfx_c10 p3"));
            Assert.That(p.Mark(1).PitchStep, Is.EqualTo(3));
        }

        [Test]
        public void LinesExpire()
        {
            var p = new DebugSfxPlayer(() => _now);
            p.Play("sfx_c07");
            p.Advance(DebugSfxPlayer.MarkLifetimeMs - 1);
            Assert.That(p.Count, Is.EqualTo(1));
            p.Advance(DebugSfxPlayer.MarkLifetimeMs);
            Assert.That(p.Count, Is.EqualTo(0));
        }

        [Test]
        public void SetBus_KeepsTheVolumeBetweenZeroAndOne()
        {
            var p = new DebugSfxPlayer(() => _now);
            Assert.That(p.BusVolume(SfxBus.Sfx), Is.EqualTo(1f));
            p.SetBus(SfxBus.Sfx, 0.25f);
            p.SetBus(SfxBus.Music, 2f);
            p.SetBus(SfxBus.Ui, -1f);
            Assert.That(p.BusVolume(SfxBus.Sfx), Is.EqualTo(0.25f));
            Assert.That(p.BusVolume(SfxBus.Music), Is.EqualTo(1f));
            Assert.That(p.BusVolume(SfxBus.Ui), Is.EqualTo(0f));
        }
    }
}

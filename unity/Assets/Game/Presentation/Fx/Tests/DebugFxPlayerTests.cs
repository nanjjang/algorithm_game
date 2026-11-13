#nullable enable
using NUnit.Framework;
using Wizard.Content;
using Wizard.Netcode.Duel;
using Wizard.ViewLogic.Presentation;

namespace Wizard.Unity.Presentation.Fx.Tests
{
    // 수락 2: 빈 그림 재생기는 식별자와 위치를 남기고(겹침 화면이 찍는다), 번복은 0.1초 흐려지다 사라진다(CONTRACTS 13)
    public sealed class DebugFxPlayerTests
    {
        private long _now;

        private DebugFxPlayer Player() => new DebugFxPlayer(() => _now);

        private static FxRequest Req(string id, float x, float y, EventPhase phase) =>
            new FxRequest(id, new System.Numerics.Vector2(x, y), 0f, Element.None, 0, 0, phase);

        [Test]
        public void Play_LeavesAMarkWithIdentifierAndPosition()
        {
            DebugFxPlayer p = Player();
            p.Play(Req("fx_lock_x", 1.5f, -2f, EventPhase.Confirmed));
            Assert.That(p.Count, Is.EqualTo(1));
            Assert.That(p.PlayCount, Is.EqualTo(1));
            DebugFxMark m = p.Mark(0);
            Assert.That(m.FxId, Is.EqualTo("fx_lock_x"));
            Assert.That(m.WorldPos, Is.EqualTo(new System.Numerics.Vector2(1.5f, -2f)));
            Assert.That(m.Label, Is.EqualTo("fx_lock_x (1.50, -2.00)"));
            Assert.That(m.Phase, Is.EqualTo(EventPhase.Confirmed));
            Assert.That(p.Alpha(0, _now), Is.EqualTo(1f));
        }

        [Test]
        public void Retracted_FadesThePredictedMarkOverOneTenthOfASecond()
        {
            DebugFxPlayer p = Player();
            _now = 1000;
            p.Play(Req("fx_graze_ring", 0f, 0f, EventPhase.Predicted));
            _now = 1050;
            p.Play(Req("fx_graze_ring", 0f, 0f, EventPhase.Retracted));
            Assert.That(p.Count, Is.EqualTo(1), "fades the same mark instead of adding one");
            Assert.That(p.RetractCount, Is.EqualTo(1));
            Assert.That(p.Mark(0).Phase, Is.EqualTo(EventPhase.Retracted));
            Assert.That(p.Alpha(0, 1050), Is.EqualTo(1f));
            Assert.That(p.Alpha(0, 1100), Is.EqualTo(0.5f).Within(1e-5f));
            p.Advance(1149);
            Assert.That(p.Count, Is.EqualTo(1));
            p.Advance(1150);
            Assert.That(p.Count, Is.EqualTo(0), "gone after 0.1 s");
        }

        [Test]
        public void Retracted_WithoutALivePrediction_LeavesAFadingMark()
        {
            DebugFxPlayer p = Player();
            p.Play(Req("fx_graze_ring", 2f, 3f, EventPhase.Retracted));
            Assert.That(p.Count, Is.EqualTo(1));
            Assert.That(p.Mark(0).Fading, Is.True);
            p.Advance(_now + DebugFxPlayer.RetractFadeMs);
            Assert.That(p.Count, Is.EqualTo(0));
        }

        [Test]
        public void ConfirmedMarksExpireAndStopAllClears()
        {
            DebugFxPlayer p = Player();
            p.Play(Req("a", 0f, 0f, EventPhase.Confirmed));
            p.Advance(DebugFxPlayer.MarkLifetimeMs - 1);
            Assert.That(p.Count, Is.EqualTo(1));
            p.Advance(DebugFxPlayer.MarkLifetimeMs);
            Assert.That(p.Count, Is.EqualTo(0));

            p.Play(Req("b", 0f, 0f, EventPhase.Confirmed));
            p.StopAll();
            Assert.That(p.Count, Is.EqualTo(0));
        }

        [Test]
        public void OverCapacity_DropsTheOldestMark()
        {
            DebugFxPlayer p = Player();
            for (int i = 0; i <= DebugFxPlayer.Capacity; i++)
            {
                p.Play(Req("fx" + i, i, 0f, EventPhase.Confirmed));
            }
            Assert.That(p.Count, Is.EqualTo(DebugFxPlayer.Capacity));
            Assert.That(p.Mark(0).FxId, Is.EqualTo("fx1"));
            Assert.That(p.Mark(DebugFxPlayer.Capacity - 1).FxId, Is.EqualTo("fx" + DebugFxPlayer.Capacity));
        }
    }
}

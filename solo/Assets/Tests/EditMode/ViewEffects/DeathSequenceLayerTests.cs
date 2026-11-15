using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.Sim;
using Wizard.Solo.Tests.Hud;
using Wizard.Solo.View;
using Wizard.Solo.View.Effects;
using Wizard.Solo.View.Enemies;

namespace Wizard.Solo.Tests.ViewEffects
{
    /// <summary>쓰러짐 연출 층(표시 전용, Juice.json death 절). 기대값은 그 표에서 읽는다(값 자체는 솔로 임시 — QA에서 정한다).</summary>
    public class DeathSequenceLayerTests
    {
        GameObject _canvas;
        HudFakePool _pool;
        HudFakeSound _sound;
        DeathSequenceLayer _layer;
        readonly List<float> _hitStops = new List<float>();

        [SetUp]
        public void SetUp()
        {
            _canvas = new GameObject("TestHudRoot", typeof(RectTransform));
            ((RectTransform)_canvas.transform).sizeDelta = new Vector2(1920, 1080);
            _pool = new HudFakePool();
            _sound = new HudFakeSound();
            _hitStops.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            if (_layer != null) _layer.Dispose();
            _pool.DestroyAll();
            Object.DestroyImmediate(_canvas);
        }

        static EffectsTuning J => new EffectsTuning(TestContent.Db.SoloExtra(JuiceLayer.JuiceName));
        static float D(string key) => J.F(DeathSequenceLayer.Section, key);

        DeathSequenceLayer Build(SimWorld w, int effectsLevel = 0)
        {
            var ctx = new GameContext { Names = new HudFakeNames(), Sound = _sound, Content = TestContent.Db };
            var vc = new ViewContext
            {
                Game = ctx, World = w, HudRoot = (RectTransform)_canvas.transform, Pool = _pool,
                Options = new ViewOptions { EffectsLevel = effectsLevel },
                HitStop = s => _hitStops.Add(s)
            };
            _layer = new DeathSequenceLayer { FixedRealDt = 0.05f };
            _layer.Build(vc, w);
            return _layer;
        }

        static SimWorld Dead()
        {
            var w = HudTestWorlds.World(RunPhase.WaveCombat, 3);
            w.P0.Hp = 0;
            w.P0.Downed = true;
            return w;
        }

        static SimEvent Ended(RunEndReason r) => new SimEvent { Type = SimEventType.RunEnded, A = (int)r, Slot = -1 };

        void Run(SimWorld w, float seconds)
        {
            for (float t = 0f; t < seconds; t += 0.05f) _layer.Render(w, 1f, 0.05f * _layer.TimeScaleNow);
        }

        [Test]
        public void Death_HpZero__HitStopSlowMoVeilVignetteZoomThud()
        {
            var w = Dead();
            var l = Build(w);
            Assert.IsFalse(l.Active, "판이 끝나기 전에는 아무것도 없다");
            w.Run.Phase = RunPhase.RunEnded;
            l.OnEvent(w, Ended(RunEndReason.HpZero));
            Assert.IsTrue(l.Active);
            Assert.AreEqual(1, _hitStops.Count, "마지막 피격 순간 그림 정지 한 번");
            Assert.AreEqual(D("hitstop_seconds"), _hitStops[0], 1e-4f);

            l.Render(w, 1f, 0f);
            Assert.Less(l.TimeScaleNow, 0.5f, "처음엔 느리다");
            Assert.Greater(l.ZoomNow, 1f, "카메라를 당긴다");
            Run(w, D("slow_seconds") + 0.2f);
            Assert.AreEqual(1f, l.TimeScaleNow, 1e-4f, "슬로모션은 짧게 끝난다");
            Run(w, 1f);
            Assert.AreEqual(D("veil_alpha"), l.VeilAlpha, 0.01f);
            Assert.AreEqual(D("gray_alpha"), l.GrayAlpha, 0.01f);
            Assert.AreEqual(D("vignette_alpha"), l.VignetteAlpha, 0.01f);
            Assert.AreEqual(D("vignette_to"), l.VignetteScale, 0.01f, "비네트가 조여 들었다");
            Assert.IsTrue(l.ThudPlayed);
            CollectionAssert.Contains(_sound.Played, DeathSequenceLayer.ThudSoundId);

            // 같은 판에서 두 번 시작하지 않는다
            l.OnEvent(w, Ended(RunEndReason.HpZero));
            Assert.AreEqual(1, _hitStops.Count);
        }

        [Test]
        public void Death_ColorsAreGrayNavyNeverDanger()
        {
            foreach (var key in new[] { "gray", "veil", "vignette" })
            {
                var c = EffectsTuning.ParseColor(J.S(DeathSequenceLayer.Section, key), Color.magenta);
                Assert.AreNotEqual(Color.magenta, c, key + " 색이 표에 있다");
                Assert.IsFalse(DangerColor.IsDanger((Color32)c), key + " 은 위험색이 아니다");
                Color.RGBToHSV(c, out float h, out float s, out float v);
                // 회색(채도 낮음) 또는 아주 어두운 남색(명도 낮음, 색상각 200~260도)
                bool gray = s < 0.5f;
                bool darkNavy = v < 0.2f && h * 360f >= 200f && h * 360f <= 260f;
                Assert.IsTrue(gray || darkNavy, key + " 은 회색·남색이다 (h " + h * 360f + ", s " + s + ", v " + v + ")");
            }
        }

        [Test]
        public void Death_AbandonOrOtherWorld_NoSequence()
        {
            var w = Dead();
            var l = Build(w);
            l.OnEvent(w, Ended(RunEndReason.Abandon));
            Assert.IsFalse(l.Active, "포기는 바로 정산(연출 없음)");
            var other = Dead();
            l.OnEvent(other, Ended(RunEndReason.HpZero));
            Assert.IsFalse(l.Active, "다른 세계(보조 판)의 이벤트는 무시");
            Assert.AreEqual(0, _hitStops.Count);
        }

        [Test]
        public void Death_MinimumEffects_NoSlowMoNoZoomNoTighten_VeilStill()
        {
            var w = Dead();
            var l = Build(w, effectsLevel: 2);
            w.Run.Phase = RunPhase.RunEnded;
            l.OnEvent(w, Ended(RunEndReason.HpZero));
            l.Render(w, 1f, 0f);
            Assert.AreEqual(1f, l.TimeScaleNow, 1e-4f);
            Assert.AreEqual(1f, l.ZoomNow, 1e-4f);
            Assert.AreEqual(D("vignette_to"), l.VignetteScale, 1e-4f, "조이지 않는다");
            Run(w, 2f);
            Assert.AreEqual(D("veil_alpha"), l.VeilAlpha, 0.01f, "어둡게 하는 덮개는 남는다(번쩍 아님)");
        }

        [Test]
        public void Death_ReducedEffects_HalfDepth()
        {
            var w = Dead();
            var l = Build(w, effectsLevel: 1);
            w.Run.Phase = RunPhase.RunEnded;
            l.OnEvent(w, Ended(RunEndReason.HpZero));
            l.Render(w, 1f, 0f);
            float full = D("slow_scale");
            Assert.AreEqual(Mathf.Lerp(1f, full, 0.5f), l.TimeScaleNow, 0.02f);
            Assert.AreEqual(1f + (D("zoom") - 1f) * 0.5f, l.ZoomNow, 1e-3f);
        }

        [Test]
        public void Death_RebuildAfterRunEnded_ShowsEndStateWithoutHitStopOrSound()
        {
            var w = Dead();
            w.Run.Phase = RunPhase.RunEnded;
            w.Run.EndReason = RunEndReason.HpZero;
            var l = Build(w);
            Assert.IsTrue(l.Active);
            l.Render(w, 1f, 0.016f);
            Assert.AreEqual(0, _hitStops.Count);
            Assert.AreEqual(1f, l.TimeScaleNow, 1e-4f);
            Assert.AreEqual(D("veil_alpha"), l.VeilAlpha, 0.01f);
            CollectionAssert.DoesNotContain(_sound.Played, DeathSequenceLayer.ThudSoundId);
        }
    }
}

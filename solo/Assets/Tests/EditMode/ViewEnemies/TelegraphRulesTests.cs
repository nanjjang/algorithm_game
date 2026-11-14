using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.Sim;
using Wizard.Solo.View.Enemies;

namespace Wizard.Solo.Tests.ViewEnemies
{
    /// <summary>
    /// 예고 그리기 규칙의 순수 부분. 기대값은 요약서에서 가져온다:
    /// 05 요약서 1.1절(위험색 정의·토큰), 1.2절(차오름 방향, 번쩍 s+N−2·s+N−1, 번쩍 한도 518,400px, 빗금 4px·16px·45도, 1초 전 초당 2회 깜박,
    /// 3겹 테두리 2/4/2px, 앞선 흰 2 + 검정 2, 조준선 2→8px), 7절(엘리트 금색 #FFD24A 2px), 4절(flipX = facing.x &lt; 0),
    /// 08 요약서 12.2절(예고 채움 70/80/90%, 색각 보조), 01 요약서 7.3절(별똥 고리 위험색 아님).
    /// </summary>
    public class TelegraphRulesTests
    {
        const float Px = 1f / 64f;
        EnemyViewTuning _t;

        [SetUp]
        public void SetUp() { _t = EnemyViewTuning.Load(TestContent.Db); }

        // ---------------------------------------------------------------- 차오름 기하

        [TestCase(0f, 0f)]
        [TestCase(0.5f, 1f)]
        [TestCase(1f, 2f)]
        public void S05_TG_FILL_01__CircleRadiusGrowsCenterOut(float fill, float radius)
        {
            Assert.AreEqual(radius, TelegraphGeometry.FillExtent(ZoneShape.Circle, 2f, 0f, 0f, fill, 2f, 8f), 1e-5f);
        }

        [TestCase(0f, 0f)]
        [TestCase(0.5f, 2.5f)]
        [TestCase(1f, 5f)]
        public void S05_TG_FILL_02__BandLengthGrowsAwayFromEnemy(float fill, float len)
        {
            Assert.AreEqual(len, TelegraphGeometry.FillExtent(ZoneShape.Band, 0f, 0f, 5f, fill, 2f, 8f), 1e-5f);
        }

        [TestCase(0f, 0f)]
        [TestCase(0.5f, 1.5f)]
        [TestCase(1f, 3f)]
        public void S05_TG_FILL_03__FanRadiusGrowsApexToArc(float fill, float radius)
        {
            Assert.AreEqual(radius, TelegraphGeometry.FillExtent(ZoneShape.Fan, 3f, 0f, 0f, fill, 2f, 8f), 1e-5f);
        }

        [TestCase(0f, 2f)]
        [TestCase(0.5f, 5f)]
        [TestCase(1f, 8f)]
        public void S05_TG_FILL_04__AimLineThicknessTwoToEightPx(float fill, float px)
        {
            Assert.AreEqual(px, TelegraphGeometry.FillExtent(ZoneShape.AimLine, 0f, 0f, 9f, fill, _t.AimLineMinPx, _t.AimLineMaxPx), 1e-5f);
        }

        [TestCase(0f, 4f)]
        [TestCase(0.5f, 4.5f)]
        [TestCase(1f, 5f)]
        public void S05_TG_FILL_05__RingGrowsInnerToOuter(float fill, float outer)
        {
            Assert.AreEqual(outer, TelegraphGeometry.FillExtent(ZoneShape.Ring, 5f, 4f, 0f, fill, 2f, 8f), 1e-5f);
        }

        [Test]
        public void S05_TG_FILL_06__FillEndsExactlyAtDetonateTick()
        {
            // s = 100, N = 30: s → 0, s + 15 → 0.5, s + N(판정 틱) → 1
            Assert.AreEqual(0f, TelegraphGeometry.Fill(100, 130, 100f), 1e-6f);
            Assert.AreEqual(0.5f, TelegraphGeometry.Fill(100, 130, 115f), 1e-6f);
            Assert.AreEqual(1f, TelegraphGeometry.Fill(100, 130, 130f), 1e-6f);
        }

        [Test]
        public void S05_TG_FILL_07__PaintedFillAreaMatchesShape()
        {
            var p = new TelegraphPainter(_t) { ArenaHeight = 14f };
            var red = DangerColor.WithAlpha(_t.DangerRed, 0.7f);
            // 원 r2, 0.5 → 반지름 1
            AssertRedArea(p, new ZoneSnap { Shape = ZoneShape.Circle, Pos = new Vector2(10, 7), Radius = 2f }, 0.5f, red, Mathf.PI * 1f, 0.01f);
            // 띠 폭 1 길이 4, 0.5 → 2칸²
            AssertRedArea(p, new ZoneSnap { Shape = ZoneShape.Band, Pos = new Vector2(5, 5), Dir = new Vector2(1, 0), Width = 1f, Length = 4f }, 0.5f, red, 2f, 0.001f);
            // 고리 5/4, 0.5 → π(4.5² − 4²)
            AssertRedArea(p, new ZoneSnap { Shape = ZoneShape.Ring, Pos = new Vector2(12, 7), Radius = 5f, InnerRadius = 4f }, 0.5f, red, Mathf.PI * (4.5f * 4.5f - 16f), 0.01f);
            // 부채 90도 r2, 0.5 → π/4
            AssertRedArea(p, new ZoneSnap { Shape = ZoneShape.Fan, Pos = new Vector2(8, 8), Dir = new Vector2(0, -1), Radius = 2f, FanDegrees = 90f }, 0.5f, red, Mathf.PI / 4f, 0.01f);
            // 조준선 길이 9, 0 → 굵기 2px
            AssertRedArea(p, new ZoneSnap { Shape = ZoneShape.AimLine, Pos = new Vector2(3, 3), Dir = new Vector2(1, 0), Width = 0.4f, Length = 9f }, 0f, red, 9f * 2f * Px, 0.001f);
            AssertRedArea(p, new ZoneSnap { Shape = ZoneShape.AimLine, Pos = new Vector2(3, 3), Dir = new Vector2(1, 0), Width = 0.4f, Length = 9f }, 1f, red, 9f * 8f * Px, 0.001f);
        }

        static void AssertRedArea(TelegraphPainter p, ZoneSnap z, float fill, Color32 red, float expected, float relTol)
        {
            var b = new ShapeMeshBuilder();
            p.PaintZoneFill(b, z, ZoneLook.Default(fill, 0.7f));
            float a = b.AreaOfColor(red);
            Assert.AreEqual(expected, a, Mathf.Max(1e-5f, expected * relTol), z.Shape + " fill " + fill);
        }

        // ---------------------------------------------------------------- 번쩍

        [Test]
        public void S05_TG_FLASH_01__FlashOnlyOnDetonateMinusTwoAndOne()
        {
            int d = 200;
            Assert.AreEqual(2, _t.FlashTicksBefore);
            Assert.IsFalse(TelegraphGeometry.IsFlashTick(d - 3, d, _t.FlashTicksBefore));
            Assert.IsTrue(TelegraphGeometry.IsFlashTick(d - 2, d, _t.FlashTicksBefore));
            Assert.IsTrue(TelegraphGeometry.IsFlashTick(d - 1, d, _t.FlashTicksBefore));
            Assert.IsFalse(TelegraphGeometry.IsFlashTick(d, d, _t.FlashTicksBefore));
        }

        [Test]
        public void S05_TG_FLASH_02__LimitIs518400Px()
        {
            Assert.AreEqual(518400f, _t.FlashLimitPx2, 0.5f);
            Assert.AreEqual(60, _t.FlashWindowTicks);
        }

        [Test]
        public void S05_TG_FLASH_03__OverLimitBecomesBorder()
        {
            var f = new FlashLimiter(518400f, 60);
            Assert.IsTrue(f.TryFlash(518400f, 10), "합이 정확히 25%면 넘지 않는다");
            var g = new FlashLimiter(518400f, 60);
            Assert.IsFalse(g.TryFlash(518401f, 10), "한 장이 25%를 넘으면 테두리 강조");
            var h = new FlashLimiter(518400f, 60);
            Assert.IsTrue(h.TryFlash(300000f, 0));
            Assert.IsFalse(h.TryFlash(300000f, 59), "최근 1초 안 합 600,000 > 518,400");
            Assert.AreEqual(300000f, h.RecentSumPx2, 0.5f, "강조로 바뀐 번쩍은 합에 넣지 않는다");
            Assert.IsTrue(h.TryFlash(300000f, 60), "60틱 지나면 첫 번쩍이 창에서 빠진다");
        }

        [Test]
        public void S05_TG_FLASH_04__AreaInPx()
        {
            // 원 반지름 1칸 = π × 64²
            Assert.AreEqual(Mathf.PI * 4096f, TelegraphGeometry.AreaPx2(TelegraphGeometry.AreaCells(ZoneShape.Circle, 1f, 0f, 0f, 0f, 0f)), 0.5f);
            Assert.AreEqual(2f * 3f * 4096f, TelegraphGeometry.AreaPx2(TelegraphGeometry.AreaCells(ZoneShape.Band, 0f, 0f, 2f, 3f, 0f)), 0.5f);
        }

        // ---------------------------------------------------------------- 빗금·깜박

        [Test]
        public void S05_TG_HATCH_01__HatchFourPxEverySixteenAt45()
        {
            Assert.AreEqual(4f, _t.HatchPx);
            Assert.AreEqual(16f, _t.HatchPitchPx);
            Assert.AreEqual(45f, _t.HatchAngle);
            // 4×4칸 띠에 빗금: 덮는 넓이 ≈ 4/16 = 25%
            var p = new TelegraphPainter(_t);
            var b = new ShapeMeshBuilder();
            var h = new ZoneSnap { Shape = ZoneShape.Band, Pos = new Vector2(4, 4), Dir = new Vector2(1, 0), Width = 4f, Length = 4f };
            p.PaintHazardFill(b, h, false, 1f);
            Assert.AreEqual(16f * 0.25f, b.AreaOfColor(DangerColor.WithAlpha(_t.HazardHatch, _t.HatchAlpha)), 16f * 0.25f * 0.05f);
            // 줄 하나의 방향은 45도: 삼각형 정점을 줄 법선에 사영하면 같은 줄 안에서 폭 4px 이하
            var n = new Vector2(Mathf.Cos(135f * Mathf.Deg2Rad), Mathf.Sin(135f * Mathf.Deg2Rad));
            for (int t = 0; t < b.TriangleCount; t++)
            {
                float u0 = Vector2.Dot(b.Vertices[b.Triangles[t * 3]], n), u1 = Vector2.Dot(b.Vertices[b.Triangles[t * 3 + 1]], n), u2 = Vector2.Dot(b.Vertices[b.Triangles[t * 3 + 2]], n);
                float span = Mathf.Max(u0, Mathf.Max(u1, u2)) - Mathf.Min(u0, Mathf.Min(u1, u2));
                Assert.LessOrEqual(span, 4f * Px + 1e-4f);
            }
        }

        [Test]
        public void S05_TG_HATCH_02__BlinkTwicePerSecondInLastSecond()
        {
            Assert.AreEqual(1f, _t.HazardBlinkLeadSeconds);
            Assert.AreEqual(2f, _t.HazardBlinkPerSecond);
            int until = 1000; // 깜박 구간 = 940..999, 반 주기 15틱
            float lead = _t.HazardBlinkLeadSeconds, hz = _t.HazardBlinkPerSecond;
            Assert.IsTrue(TelegraphGeometry.HazardVisible(939, until, lead, hz));
            Assert.IsFalse(TelegraphGeometry.HazardVisible(940, until, lead, hz));
            Assert.IsFalse(TelegraphGeometry.HazardVisible(954, until, lead, hz));
            Assert.IsTrue(TelegraphGeometry.HazardVisible(955, until, lead, hz));
            Assert.IsTrue(TelegraphGeometry.HazardVisible(969, until, lead, hz));
            Assert.IsFalse(TelegraphGeometry.HazardVisible(970, until, lead, hz));
            Assert.IsTrue(TelegraphGeometry.HazardVisible(985, until, lead, hz));
            Assert.IsTrue(TelegraphGeometry.HazardVisible(999, until, lead, hz));
            // 1초에 꺼짐이 2번
            int offs = 0; bool prev = true;
            for (int t = 940; t < 1000; t++) { bool v = TelegraphGeometry.HazardVisible(t, until, lead, hz); if (prev && !v) offs++; prev = v; }
            Assert.AreEqual(2, offs);
            // 웨이브 끝까지 남는 구역은 깜박이지 않는다
            Assert.IsTrue(TelegraphGeometry.HazardVisible(5000, int.MaxValue, lead, hz));
        }

        // ---------------------------------------------------------------- 클립·반전

        [Test]
        public void S05_TG_CLIP_01__FlipOnlyWhenFacingLeft()
        {
            Assert.IsTrue(EnemyLook.FlipX(new Vector2(-0.1f, 0.9f)));
            Assert.IsFalse(EnemyLook.FlipX(new Vector2(0f, 1f)));
            Assert.IsFalse(EnemyLook.FlipX(new Vector2(0.5f, -0.5f)));
        }

        [Test]
        public void S09_TG_CLIP_02__ClipEntrySelection()
        {
            var names = new Dictionary<string, List<string>>
            {
                ["enemy.bat_dusk"] = new List<string> { "fly", "death" },
                ["elite.bat_dusk.dive"] = new List<string> { "dive" },
                ["enemy.wisp_stray"] = new List<string> { "hover", "cast", "blink", "death" },
                ["elite.wisp_stray.twin"] = new List<string> { "hover" },
                ["boss.storm_owl"] = new List<string> { "fly", "dive", "fan", "weather", "phase", "death" },
                ["boss.storm_owl.ground"] = new List<string> { "land" },
            };
            System.Func<string, IReadOnlyList<string>> f = id => names.TryGetValue(id, out var l) ? l : new List<string>();
            EnemyLook.ResolveClip(f, "enemy.bat_dusk", true, "dive", out var e1, out var c1);
            Assert.AreEqual("elite.bat_dusk.dive", e1); Assert.AreEqual("dive", c1);
            EnemyLook.ResolveClip(f, "enemy.bat_dusk", false, "fly", out var e2, out var c2);
            Assert.AreEqual("enemy.bat_dusk", e2); Assert.AreEqual("fly", c2);
            EnemyLook.ResolveClip(f, "enemy.bat_dusk", true, "fly", out var e3, out _);
            Assert.AreEqual("enemy.bat_dusk", e3, "엘리트도 바탕 종 그림");
            EnemyLook.ResolveClip(f, "enemy.wisp_stray", true, "hover", out var e4, out _);
            Assert.AreEqual("elite.wisp_stray.twin", e4, "쌍둥이불 전용 본체");
            EnemyLook.ResolveClip(f, "enemy.wisp_stray", false, "hover", out var e5, out _);
            Assert.AreEqual("enemy.wisp_stray", e5);
            EnemyLook.ResolveClip(f, "boss.storm_owl", false, "land", out var e6, out var c6);
            Assert.AreEqual("boss.storm_owl.ground", e6); Assert.AreEqual("land", c6);
            EnemyLook.ResolveClip(f, "boss.storm_owl", false, "nope", out var e7, out var c7);
            Assert.AreEqual("boss.storm_owl", e7); Assert.AreEqual("nope", c7, "없는 클립은 그림 창고가 첫 클립으로 대신한다");
        }

        // ---------------------------------------------------------------- 위험색

        [Test]
        public void S05_TG_DANGER_01__HsvDefinition()
        {
            Assert.IsTrue(DangerColor.IsDanger(330f, 70f, 70f));
            Assert.IsTrue(DangerColor.IsDanger(15f, 70f, 70f));
            Assert.IsFalse(DangerColor.IsDanger(16f, 100f, 100f));
            Assert.IsFalse(DangerColor.IsDanger(329f, 100f, 100f));
            Assert.IsFalse(DangerColor.IsDanger(0f, 69f, 100f));
            Assert.IsFalse(DangerColor.IsDanger(0f, 100f, 69f));
            Assert.IsTrue(DangerColor.IsDanger(EnemyViewTuning.Hex("#FF3B30")), "danger.zone.red");
            Assert.IsTrue(DangerColor.IsDanger(EnemyViewTuning.Hex("#FF2E88")), "danger.bullet.body");
        }

        [Test]
        public void S05_TG_DANGER_02__TokensFromSpec()
        {
            Assert.AreEqual(EnemyViewTuning.Hex("#FF3B30"), _t.DangerRed);
            Assert.AreEqual(EnemyViewTuning.Hex("#FF3B30"), _t.BorderMid);
            Assert.AreEqual(EnemyViewTuning.Hex("#000000"), _t.BorderOuter);
            Assert.AreEqual(EnemyViewTuning.Hex("#FFFFFF"), _t.BorderInner);
            Assert.AreEqual(2f, _t.BorderOuterPx); Assert.AreEqual(4f, _t.BorderMidPx); Assert.AreEqual(2f, _t.BorderInnerPx);
            Assert.AreEqual(2f, _t.FrontWhitePx); Assert.AreEqual(2f, _t.FrontBlackPx);
            Assert.AreEqual(0.4f, _t.DarkBaseAlpha, 1e-6f);
            Assert.AreEqual(EnemyViewTuning.Hex("#EAF2FF"), _t.RimLight);
            Assert.AreEqual(EnemyViewTuning.Hex("#FF2E88"), _t.WeaponTint);
        }

        [Test]
        public void S05_TG_DANGER_03__NonAttackColorsAreNotDanger()
        {
            var safe = new[]
            {
                _t.HarmlessFill, _t.HarmlessEdge, _t.StarfallRing, _t.EliteGold, _t.RimLight, _t.CorrosionEdge, _t.HpBarBack, _t.HpBarFill,
                _t.Shadow, _t.SpawnBush, _t.ColorblindStripe, _t.ColorblindShotStripe, _t.FrostHatch, _t.BlockedPuff, _t.EnemyOutline
            };
            foreach (var c in safe) Assert.IsFalse(DangerColor.IsDanger(c), "위험색이면 안 된다: " + c);
        }

        [Test]
        public void S05_TG_DANGER_04__HarmlessZoneUsesNoDangerColor()
        {
            var p = new TelegraphPainter(_t);
            foreach (ZoneShape shape in System.Enum.GetValues(typeof(ZoneShape)))
            {
                var z = new ZoneSnap { Shape = shape, Pos = new Vector2(10, 7), Dir = new Vector2(1, 0), Radius = 2f, InnerRadius = 1f, Width = 1f, Length = 3f, FanDegrees = 90f, Harmless = true, StartTick = 0, DetonateTick = 90 };
                var fill = new ShapeMeshBuilder();
                var edge = new ShapeMeshBuilder();
                var look = ZoneLook.Default(0.5f, 0.9f);
                look.Flash = true;
                look.Colorblind = true;
                p.PaintZoneFill(fill, z, look);
                p.PaintZoneEdge(edge, z, look);
                Assert.Greater(edge.TriangleCount, 0);
                foreach (var c in fill.Colors) Assert.IsFalse(DangerColor.IsDanger(c), shape + " 채움 층");
                foreach (var c in edge.Colors) Assert.IsFalse(DangerColor.IsDanger(c), shape + " 테두리 층");
            }
        }

        // ---------------------------------------------------------------- 채움 진하기·색각 보조

        [TestCase(70, 0.7f)]
        [TestCase(80, 0.8f)]
        [TestCase(90, 0.9f)]
        public void S08_TG_FILLPCT_01__FillOpacityOption(int pct, float alpha)
        {
            Assert.AreEqual(alpha, TelegraphGeometry.FillAlpha(pct, _t.FillPctMin, _t.FillPctMax), 1e-6f);
            var p = new TelegraphPainter(_t);
            var b = new ShapeMeshBuilder();
            var z = new ZoneSnap { Shape = ZoneShape.Circle, Pos = new Vector2(10, 7), Radius = 2f, StartTick = 0, DetonateTick = 60 };
            p.PaintZoneFill(b, z, ZoneLook.Default(0.5f, TelegraphGeometry.FillAlpha(pct, _t.FillPctMin, _t.FillPctMax)));
            byte a = (byte)Mathf.RoundToInt(alpha * 255f);
            Assert.Greater(b.AreaOfColor(new Color32(_t.DangerRed.r, _t.DangerRed.g, _t.DangerRed.b, a)), 3f);
        }

        [Test]
        public void S08_TG_CB_01__ColorblindAddsStripesKeepsFillAndEdge()
        {
            var p = new TelegraphPainter(_t);
            var red = DangerColor.WithAlpha(_t.DangerRed, 0.7f);
            var stripe = DangerColor.WithAlpha(_t.ColorblindStripe, _t.ColorblindStripeAlpha);
            foreach (ZoneShape shape in System.Enum.GetValues(typeof(ZoneShape)))
            {
                var z = new ZoneSnap { Shape = shape, Pos = new Vector2(10, 7), Dir = new Vector2(0, 1), Radius = 3f, InnerRadius = 2f, Width = 2f, Length = 4f, FanDegrees = 120f };
                float fill = shape == ZoneShape.AimLine ? 1f : 0.6f;
                var offFill = new ShapeMeshBuilder(); var offEdge = new ShapeMeshBuilder();
                var onFill = new ShapeMeshBuilder(); var onEdge = new ShapeMeshBuilder();
                var look = ZoneLook.Default(fill, 0.7f);
                p.PaintZoneFill(offFill, z, look); p.PaintZoneEdge(offEdge, z, look);
                look.Colorblind = true;
                p.PaintZoneFill(onFill, z, look); p.PaintZoneEdge(onEdge, z, look);
                Assert.AreEqual(offFill.AreaOfColor(red), onFill.AreaOfColor(red), 1e-4f, shape + ": 빨강 채움 넓이는 그대로");
                Assert.AreEqual(0f, offFill.AreaOfColor(stripe), 1e-6f);
                Assert.Greater(onFill.AreaOfColor(stripe), 0f, shape + ": 줄무늬 층이 더해짐");
                Assert.LessOrEqual(onFill.AreaOfColor(stripe), offFill.AreaOfColor(red) + 1e-4f, "줄무늬는 차오른 채움 안에만");
                CollectionAssert.AreEqual(offEdge.Vertices, onEdge.Vertices, shape + ": 테두리 그대로");
                CollectionAssert.AreEqual(offEdge.Colors, onEdge.Colors);
            }
        }

        // ---------------------------------------------------------------- 엘리트 외곽선

        [Test]
        public void S05_TG_ELITE_01__GoldTwoPxOutline()
        {
            EnemyLook.Outline(true, 60, _t, out var c, out var s);
            Assert.AreEqual(EnemyViewTuning.Hex("#FFD24A"), c);
            Assert.AreEqual(2f, s);
            EnemyLook.Outline(false, 40, _t, out var rc, out var rs);
            Assert.AreEqual(new Color32(0xEA, 0xF2, 0xFF, (byte)Mathf.RoundToInt(0.4f * 255f)), rc, "해질녘 림 40%");
            Assert.AreEqual(2f, rs);
        }

        [Test]
        public void S05_TG_RAGE_01__RageRimTwoToSixPxOverPointThreeSeconds()
        {
            Assert.AreEqual(2f, EnemyLook.RageRimPx(0f, _t, 2f), 1e-5f);
            Assert.AreEqual(4f, EnemyLook.RageRimPx(0.15f, _t, 2f), 1e-4f);
            Assert.AreEqual(2f, EnemyLook.RageRimPx(0.3f, _t, 2f), 1e-5f, "0.3초가 지나면 평소 굵기");
        }
    }
}

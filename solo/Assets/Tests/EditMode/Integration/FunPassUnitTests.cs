using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.Data;
using Wizard.Solo.Sim;
using Wizard.Solo.Tests;
using Wizard.Solo.View;

namespace Wizard.Solo.Tests.Integration
{
    /// <summary>재미 조정(Docs/11-fun-pass.md)의 손잡이 하나하나: 덮어쓰기 도구, 보상 나누기, 스침 열 덜기, 시작 결정, 보스 시연, 카메라 맞춤.</summary>
    public sealed class FunPassUnitTests
    {
        // ------------------------------------------------------------ 덮어쓰기 도구

        static JArray Patches(string json) => JArray.Parse(json);

        [Test]
        public void Tuning_Patch__SelectByKeyWildcardAndRoot_SetAndAppend()
        {
            var root = JObject.Parse("{\"a\":1,\"list\":[{\"id\":\"x\",\"v\":1},{\"id\":\"y\",\"v\":2}],\"arr\":[1]}");
            var errors = new List<string>();
            var applied = new List<string>();
            TuningPatches.ApplyToToken(Patches(
                "[{\"id\":\"p1\",\"target\":\"t\",\"select\":\"list[id=y]\",\"set\":{\"v\":20},\"was\":{\"v\":2}}," +
                " {\"id\":\"p2\",\"target\":\"t\",\"select\":[\"list[*]\"],\"set\":{\"w\":true}}," +
                " {\"id\":\"p3\",\"target\":\"t\",\"set\":{\"a\":5}}," +
                " {\"id\":\"p4\",\"target\":\"t\",\"select\":\"arr\",\"append\":[2,3]}," +
                " {\"id\":\"other\",\"target\":\"u\",\"set\":{\"a\":9}}]"), "t", root, errors, applied);
            Assert.IsEmpty(errors, string.Join(" | ", errors));
            CollectionAssert.AreEqual(new[] { "p1", "p2", "p3", "p4" }, applied);
            Assert.AreEqual(5, (int)root["a"]);
            Assert.AreEqual(1, (int)root["list"][0]["v"]);
            Assert.AreEqual(20, (int)root["list"][1]["v"]);
            Assert.IsTrue((bool)root["list"][0]["w"]);
            Assert.AreEqual(3, ((JArray)root["arr"]).Count);
        }

        [Test]
        public void Tuning_Patch__WasMismatchAndEmptySelectionAreErrors()
        {
            var root = JObject.Parse("{\"list\":[{\"wave\":3,\"share\":100}]}");
            var errors = new List<string>();
            var applied = new List<string>();
            TuningPatches.ApplyToToken(Patches(
                "[{\"id\":\"bad_was\",\"target\":\"t\",\"select\":\"list[wave=3]\",\"set\":{\"share\":50},\"was\":{\"share\":60}}," +
                " {\"id\":\"none\",\"target\":\"t\",\"select\":\"list[wave=4]\",\"set\":{\"share\":1}}]"), "t", root, errors, applied);
            Assert.AreEqual(2, errors.Count, string.Join(" | ", errors));
            CollectionAssert.DoesNotContain(applied, "bad_was");
            CollectionAssert.DoesNotContain(applied, "none");
        }

        [Test]
        public void Tuning_Patch__DisabledFileAppliesNothing()
        {
            Assert.IsNull(TuningPatches.EnabledPatches(JObject.Parse("{\"enabled\":false,\"patches\":[]}")));
            Assert.IsNull(TuningPatches.EnabledPatches(JObject.Parse("{\"patches\":[]}")));
            Assert.IsNotNull(TuningPatches.EnabledPatches(JObject.Parse("{\"enabled\":true,\"patches\":[]}")));
        }

        [Test]
        public void Tuning_Load__SpecDbUntouchedTunedDbChangedOnlyWherePatched()
        {
            var spec = TestContent.Db;
            var tuned = FunPassKit.TunedDb;
            Assert.AreEqual(0.07f, spec.Tries.Parser.JustWindowSeconds, 1e-6f);
            Assert.AreEqual(0.085f, tuned.Tries.Parser.JustWindowSeconds, 1e-6f);
            Assert.AreEqual(0.6f, spec.Character(Ids.Char.Of(1)).GrazeRadius, 1e-6f);
            Assert.AreEqual(0.7f, tuned.Character(Ids.Char.Of(1)).GrazeRadius, 1e-6f);
            Assert.AreEqual(0.85f, tuned.Character(Ids.Char.Of(2)).GrazeRadius, 1e-6f);
            // 손대지 않은 값은 같다
            Assert.AreEqual(spec.Character(Ids.Char.Of(1)).LockSeconds, tuned.Character(Ids.Char.Of(1)).LockSeconds, 1e-6f);
            Assert.AreEqual(spec.Waves.HpExpRate, tuned.Waves.HpExpRate, 1e-6f);
            Assert.AreEqual(spec.Waves.Script.Count, tuned.Waves.Script.Count);
            Assert.AreEqual(0, spec.Waves.Script[0].Fixed.Count, "명세 웨이브 1 = 고정 개수 없음");
            Assert.AreEqual(Ids.Enemy.ShamanCap, tuned.Waves.Script[0].Fixed[0].Species, "조정 웨이브 1 = 갓버섯 술사");
            Assert.AreNotEqual(spec.DataVersion, tuned.DataVersion, "조정이 데이터 판에 든다");
        }

        // ------------------------------------------------------------ C. 떼 출현 보상 나누기

        /// <summary>떼 출현 시험 웨이브: 늘린 적이 있는 첫날 웨이브(곡선 1.35).</summary>
        const int HordeWave = 3;

        [Test]
        public void Horde_Split__Wave3KillGoldAndScoreTotalsEqualSpecComposition_MorePayouts()
        {
            // 웨이브 3에서 시작(G09 곡선에서 웨이브 1은 배율 1.0이라 늘린 적이 없다 — QA 조정, Docs/11-fun-pass.md 1절)
            using (var k = new IntegrationKit())
            {
                k.StartRun(1, seed: 5UL, cfg => cfg.StartWave = HordeWave);
                WavePlan plan = null;
                Assert.IsTrue(k.AutoPlay(9000, w =>
                {
                    if (plan == null && w.Wave != null) plan = w.Wave.Plan;
                    return w.Run.Phase == RunPhase.LootPick;
                }, god: true), "웨이브 1 클리어");
                Assert.IsNotNull(plan);
                int originals = 0, extras = 0, expectGold = 0, expectScoreX2 = 0;
                for (int i = 0; i < plan.Entries.Count; i++)
                {
                    var e = plan.Entries[i];
                    if (e.OutsideBudget && !e.Elite) { extras++; continue; }
                    originals++;
                    int cost = k.Db.Enemy(e.Species).Cost;
                    expectGold += cost * k.Db.Drops.GoldPerCost * (e.Elite ? k.Db.Drops.EliteGoldMul : 1);
                    expectScoreX2 += cost * k.Db.Scoring.KillPerCost * (e.Elite ? k.Db.Waves.EliteCostMul : 1) * 2;
                }
                Assert.Greater(extras, 0, "떼 출현(G09)이 켜져 있어야 시험이 된다");
                int killGold = 0, payouts = 0;
                foreach (var ev in k.Events(SimEventType.GoldGained))
                    if (ev.Pos.x >= 0f) { killGold += ev.A; payouts++; }
                var stats = k.W.Run.WaveResults[0].Stats;
                FunPassKit.Log("웨이브 " + HordeWave + ": 원본 " + originals + " + 늘린 " + extras + ", 처치 골드 " + killGold + " (" + payouts + "번), 점수 재료 ×2 " + stats.KillScoreX2);
                Assert.AreEqual(expectGold, killGold, "처치 골드 합 = 명세 구성의 합");
                Assert.AreEqual(expectScoreX2, stats.KillScoreX2, "처치 점수 재료 합 = 명세 구성의 합");
                Assert.Greater(payouts, originals, "늘린 적도 골드가 튄다(나눠서)");
                Assert.AreEqual(originals + extras, stats.KillCount, "늘린 적도 처치로 센다");
            }
        }

        [Test]
        public void Horde_Split__FastClearTargetGrowsWithHorde()
        {
            // 같은 웨이브를 같은 시간에 깼다면, 떼 출현 보정으로 빠른 처리 점수가 명세 목표(60초)보다 크다.
            using (var k = new IntegrationKit())
            {
                k.StartRun(1, seed: 5UL, cfg => cfg.StartWave = HordeWave);
                Assert.IsTrue(k.AutoPlay(9000, w => w.Run.Phase == RunPhase.LootPick, god: true));
                var res = k.W.Run.WaveResults[0];
                Assert.AreEqual(HordeWave, res.Wave);
                var sc = k.Db.Scoring;
                long specFast = System.Math.Max(0L, (long)sc.FastClear.TargetSeconds * 60 - res.Stats.ElapsedTicks) * sc.FastClear.PerSecond;
                long s10 = 9 + HordeWave;
                int specFastScore = (int)(specFast * s10 / (60 * 10));
                Assert.Greater(res.Score.Fast, specFastScore, "떼 배율만큼 목표 시간이 늘었다");
            }
        }

        // ------------------------------------------------------------ B. 시작 결정, D. 스침

        [Test]
        public void Run_WaveStartCrystal__TunedWaves1To3StartWithOneCrystal_SpecZero()
        {
            using (var spec = new IntegrationKit())
            {
                spec.StartRun(1, seed: 3UL);
                spec.Step(2);
                Assert.AreEqual(0, spec.P.Crystals, "명세: 결정 0으로 시작");
            }
            using (var k = new IntegrationKit(FunPassKit.TunedDb))
            {
                k.StartRun(1, seed: 3UL);
                k.Step(2);
                Assert.AreEqual(1, k.P.Crystals, "조정: 웨이브 1은 결정 1개");
                Assert.GreaterOrEqual(k.Count(SimEventType.CrystalGained), 1);
            }
        }

        [Test]
        public void Player_GrazeHeatRelief__InCritZoneDropsHeatButNotBelowCritStart()
        {
            foreach (var tuned in new[] { false, true })
                using (var k = new IntegrationKit(tuned ? FunPassKit.TunedDb : null))
                {
                    var wand = k.Profile.Characters[0].EquippedWandId;
                    k.Begin(k.Ctx.Wands.BuildPracticeConfig(1, wand, RunMode.Practice, null, 1UL).Value);
                    var p = k.P;
                    int crit = p.Derived.CritStartHu;
                    p.HeatHu = SimConst.HeatToHu(95f);
                    k.W.Mods.Player.AwardGraze(k.W, p, 900001, GrazeKind.Bullet, p.Pos);
                    int expect = tuned ? Mathf.Max(crit, SimConst.HeatToHu(95f - 12f)) : SimConst.HeatToHu(95f);
                    Assert.AreEqual(expect, p.HeatHu, tuned ? "조정: 열 −12" : "명세: 임계 구간의 스침은 열을 안 바꾼다");
                    // 임계 시작 바로 위에서는 임계 시작까지만
                    p.HeatHu = crit + SimConst.HeatToHu(3f);
                    k.W.Mods.Player.AwardGraze(k.W, p, 900002, GrazeKind.Bullet, p.Pos);
                    Assert.AreEqual(tuned ? crit : crit + SimConst.HeatToHu(3f), p.HeatHu);
                    // 콤보: 명세 +2, 조정 +5 (두 번 스쳤다)
                    Assert.AreEqual(tuned ? 10 : 4, p.Combo);
                }
        }

        // ------------------------------------------------------------ G. 보스 시연

        [Test]
        public void Waves_BossPreview__TunedWave9HasPhantomPreview_SpecDoesNot()
        {
            int Count(WavePlan plan, string id)
            {
                int n = 0;
                for (int i = 0; i < plan.Entries.Count; i++) if (plan.Entries[i].Species == id && !plan.Entries[i].OutsideBudget) n++;
                return n;
            }
            foreach (var tuned in new[] { false, true })
                using (var k = new IntegrationKit(tuned ? FunPassKit.TunedDb : null))
                {
                    k.StartRun(1, seed: 77UL, cfg => { cfg.PhantomFirst = true; });
                    var plan9 = k.W.Mods.Waves.BuildPlan(k.W, 9);
                    var plan8 = k.W.Mods.Waves.BuildPlan(k.W, 8);
                    string boss = k.W.Mods.Bosses.SelectBoss(k.Db, 10, true);
                    Assert.AreEqual(Ids.Boss.MirrorPhantom, boss);
                    if (tuned) Assert.GreaterOrEqual(Count(plan9, Ids.Enemy.ShamanCap), 2, "허상 시연: 갓버섯 술사 2");
                    // 같은 씨앗이면 같은 구성(야영 미리보기 = 실제)
                    var again = k.W.Mods.Waves.BuildPlan(k.W, 9);
                    Assert.AreEqual(plan9.Entries.Count, again.Entries.Count);
                    for (int i = 0; i < plan9.Entries.Count; i++) Assert.AreEqual(plan9.Entries[i].Species, again.Entries[i].Species);
                    Assert.AreEqual(WaveCost(k.Db, plan8), WaveCost(k.Db, k.W.Mods.Waves.BuildPlan(k.W, 8)));
                    Assert.LessOrEqual(WaveCost(k.Db, plan9), plan9.Budget, "시연은 예산 안");
                }
        }

        static int WaveCost(IContentDb db, WavePlan plan)
        {
            int c = 0;
            for (int i = 0; i < plan.Entries.Count; i++)
            {
                var e = plan.Entries[i];
                if (e.OutsideBudget) continue;
                c += db.Enemy(e.Species).Cost * (e.Elite ? db.Waves.EliteCostMul : 1);
            }
            return c;
        }

        // ------------------------------------------------------------ E. 카메라

        [Test]
        public void Camera_SnapStep__IntegerZoomOneTexel_HalfZoomTwoTexels()
        {
            // 1080p(UI 1배): 줌 2 = 128px/칸 → 그림 1px = 화면 2px → 걸음 = 그림 1px
            Assert.AreEqual(1f / 64f, CameraDirector.SnapStep(128f), 1e-6f);
            // 줌 1.5 = 96px/칸 → 그림 1px = 화면 1.5px → 그림 2px = 화면 3px
            Assert.AreEqual(2f / 64f, CameraDirector.SnapStep(96f), 1e-6f);
            // 줌 1 = 그림 px = 화면 px
            Assert.AreEqual(1f / 64f, CameraDirector.SnapStep(64f), 1e-6f);
            // 맞는 걸음이 없으면 화면 1px
            float odd = 64f * 1.137f;
            Assert.AreEqual(1f / odd, CameraDirector.SnapStep(odd), 1e-6f);
        }

        [Test]
        public void Camera_Zoom15__PositionsStayOnTexelGridWhileFollowing()
        {
            var cam = new CameraDirector { Zoom = 1.5f, IntegerZoom = false, PixelSnap = true, HudBiasPx = 0f };
            float step = 2f / 64f;
            for (int i = 0; i < 120; i++)
            {
                var focus = new Vector2(6f + i * 0.0731f, 5f + i * 0.0417f);
                cam.Step(1f / 60f, 1920, 1080, 1f, focus, Vector2.right, 1f, 24f, 14f);
                Assert.AreEqual(96f, cam.ScreenPixelsPerUnit, 1e-3f);
                float gx = cam.Position.x / step, gy = cam.Position.y / step;
                bool clampedX = Mathf.Abs(cam.Position.x - (24f - 10f)) < 1e-4f || Mathf.Abs(cam.Position.x - 10f) < 1e-4f;
                bool clampedY = Mathf.Abs(cam.Position.y - 5.625f) < 1e-4f || Mathf.Abs(cam.Position.y - (14f - 5.625f)) < 1e-4f;
                if (!clampedX) Assert.AreEqual(Mathf.Round(gx), gx, 1e-3f, "x on grid #" + i);
                if (!clampedY) Assert.AreEqual(Mathf.Round(gy), gy, 1e-3f, "y on grid #" + i);
            }
            // 보이는 전장: 20 × 11.25칸
            Assert.AreEqual(5.625f, cam.OrthographicSize, 1e-4f);
        }
    }
}

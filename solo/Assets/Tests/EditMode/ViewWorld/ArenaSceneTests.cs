using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.Art;
using Wizard.Solo.Sim;
using Wizard.Solo.View;
using Wizard.Solo.View.World;

namespace Wizard.Solo.Tests.ViewWorld
{
    /// <summary>전장 장면 고르기(상태만 본다), 출발 고리 비율, 시간대, 날씨 옵션, 맵·소품 배치.</summary>
    public class ArenaSceneTests
    {
        static WorldTuning T => WorldTuning.From(TestContent.Db);

        static SimWorld NewWorld()
        {
            var w = new SimWorld(TestContent.Db, SimModuleSet.Stubs(), 1);
            return w;
        }

        static void EnterCamp(SimWorld w, RunPhase phase, Vector2 fire, Vector2 marker, float fireR = 1.5f, float markerR = 1f)
        {
            w.Run.Phase = phase;
            w.Run.CampSub = CampSubState.FireIdle;
            w.Run.CampFirePos = fire;
            w.Run.DepartMarkerPos = marker;
            w.Run.CampInteractRadius = fireR;
            w.Run.DepartMarkerRadius = markerR;
        }

        // ---------------------------------------------------------------- 출발 고리 (06 4.3절, 08 8.2절 TV-072: 60틱 = 1초)

        [Test]
        public void S06_4_3__DepartHoldRatio()
        {
            var w = NewWorld();
            EnterCamp(w, RunPhase.Camp, new Vector2(12, 7), new Vector2(16, 7));
            var camp = TestContent.Db.Camp;
            Assert.AreEqual(-1f, WorldViewRules.DepartHoldRatio(w, camp), "FireIdle이면 고리 없음");

            w.Run.CampSub = CampSubState.DepartHold;
            w.Run.DepartHoldStartTick = 500;
            w.Tick = 500;
            Assert.AreEqual(0f, WorldViewRules.DepartHoldRatio(w, camp), 1e-6f);
            w.Tick = 530;
            Assert.AreEqual(0.5f, WorldViewRules.DepartHoldRatio(w, camp), 1e-6f);
            w.Tick = 560;
            Assert.AreEqual(1f, WorldViewRules.DepartHoldRatio(w, camp), 1e-6f);

            w.Run.CampSub = CampSubState.FireIdle;
            Assert.AreEqual(-1f, WorldViewRules.DepartHoldRatio(w, camp));
            w.Run.CampSub = CampSubState.DepartHold;
            w.Run.Phase = RunPhase.WaveCombat;
            Assert.AreEqual(-1f, WorldViewRules.DepartHoldRatio(w, camp), "야영 밖이면 고리 없음");
        }

        // ---------------------------------------------------------------- 모닥불·출발 표식 자리 = RunState 값 (00 5.5절, D-52)

        [Test]
        public void S06_4_3__CampfireAndMarkerFollowRunState()
        {
            var t = T;
            var w = NewWorld();
            EnterCamp(w, RunPhase.Camp, new Vector2(12, 7), new Vector2(16, 7), 1.5f, 1f);
            var s = ArenaScene.From(w, new ViewOptions(), t, TestContent.Db);
            Assert.IsTrue(s.ShowCampfire);
            Assert.AreEqual(new Vector2(12, 7), s.FirePos);
            Assert.AreEqual(new Vector2(16, 7), s.MarkerPos);
            Assert.AreEqual(1.5f, s.FireRadius);
            Assert.AreEqual(1f, s.MarkerRadius);

            // 다른 값을 주면 그대로 따라간다(자리·반경을 스스로 정하지 않는다).
            EnterCamp(w, RunPhase.Camp, new Vector2(5, 3), new Vector2(9.5f, 11), 2.25f, 0.75f);
            s = ArenaScene.From(w, new ViewOptions(), t, TestContent.Db);
            Assert.AreEqual(new Vector2(5, 3), s.FirePos);
            Assert.AreEqual(new Vector2(9.5f, 11), s.MarkerPos);
            Assert.AreEqual(2.25f, s.FireRadius);
            Assert.AreEqual(0.75f, s.MarkerRadius);

            w.Run.Phase = RunPhase.WaveCombat;
            Assert.IsFalse(ArenaScene.From(w, new ViewOptions(), t, TestContent.Db).ShowCampfire, "전투 중엔 모닥불 없음");
        }

        [Test]
        public void S06_4_3__StartCampShowsCampfire()
        {
            var w = NewWorld();
            EnterCamp(w, RunPhase.StartCamp, new Vector2(12, 7), new Vector2(16, 7));
            var s = ArenaScene.From(w, new ViewOptions(), T, TestContent.Db);
            Assert.IsTrue(s.InCamp);
            Assert.IsTrue(s.ShowCampfire, "출발 야영(StartCamp)에서도 모닥불이 보인다");
        }

        // ---------------------------------------------------------------- 날씨 옵션 (08 12.2절 weather_particles)

        [Test]
        public void S08_12_2__WeatherParticlesOffKeepsPuddles()
        {
            var w = NewWorld();
            w.Run.Phase = RunPhase.WaveCombat;
            w.Weather.Active.Add(Ids.Event.Shower);
            w.Weather.Active.Add(Ids.Event.Fog);
            var on = ArenaScene.From(w, new ViewOptions { WeatherParticles = true }, T, TestContent.Db);
            Assert.IsTrue(on.DrawRain); Assert.IsTrue(on.DrawFog); Assert.IsTrue(on.DrawPuddles);
            Assert.AreEqual(3f, on.FogEdgeCells, 1e-6f, "안개 가장자리 3칸(weather_events.json edge_width)");

            var off = ArenaScene.From(w, new ViewOptions { WeatherParticles = false }, T, TestContent.Db);
            Assert.IsFalse(off.DrawRain, "빗줄기 입자 없음");
            Assert.IsFalse(off.DrawFog, "안개 입자 없음");
            Assert.IsTrue(off.DrawPuddles, "물웅덩이(바닥 표시)는 남는다");
        }

        [Test]
        public void S10_3_9__RainOpacityAtMost12Percent()
        {
            var t = T;
            Assert.LessOrEqual(Mathf.Min(t.RainOpacity, t.RainMaxOpacity), 0.12f + 1e-6f);
            Assert.AreEqual(new Vector2Int(20, 10), t.CenterClearCells);
        }

        // ---------------------------------------------------------------- 시간대 (01 4.1절)

        [Test]
        public void S01_4_1__TodTileVariantPerTod()
        {
            Assert.AreEqual("day", ArtAliases.TodVariant(Ids.Tod.Day));
            Assert.AreEqual("dusk", ArtAliases.TodVariant(Ids.Tod.Dusk));
            Assert.AreEqual("night", ArtAliases.TodVariant(Ids.Tod.Night));
            Assert.AreEqual("night", ArtAliases.TodVariant(Ids.Tod.Boss), "보스 = 밤 팔레트 + 달빛 원");
            Assert.IsTrue(WorldViewRules.IsNightLike(Ids.Tod.Boss));
            Assert.IsFalse(WorldViewRules.IsNightLike(Ids.Tod.Dusk));
        }

        [TestCase(1, "tod.day")]
        [TestCase(4, "tod.day")]
        [TestCase(5, "tod.dusk")]
        [TestCase(7, "tod.dusk")]
        [TestCase(8, "tod.night")]
        [TestCase(9, "tod.night")]
        [TestCase(10, "tod.boss")]
        [TestCase(23, "tod.day")]
        public void S01_4_1__TodDuringWaveFromState(int wave, string tod)
        {
            var w = NewWorld();
            w.Run.Phase = RunPhase.WaveCombat;
            w.Wave = new WaveState { Wave = wave };
            Assert.AreEqual(tod, WorldViewRules.TargetTod(w));
        }

        [Test]
        public void S01_4_1__TodInCampIsNextWave()
        {
            var w = NewWorld();
            EnterCamp(w, RunPhase.Camp, new Vector2(12, 7), new Vector2(16, 7));
            w.Run.LastClearedWave = 4;
            w.Run.NextPlan = new WavePlan { Wave = 5, Tod = Ids.Tod.Dusk };
            Assert.AreEqual(Ids.Tod.Dusk, WorldViewRules.TargetTod(w), "웨이브 4 뒤 야영에서 해질녘으로 바뀐다");
            w.Run.NextPlan = null;
            Assert.AreEqual(Ids.Tod.Dusk, WorldViewRules.TargetTod(w), "구성이 없으면 마지막으로 넘긴 웨이브 + 1");
            w.Run.Phase = RunPhase.Practice;
            Assert.AreEqual(Ids.Tod.Day, WorldViewRules.TargetTod(w));
        }

        [Test]
        public void S01_4_1__TodTransitionFourSecondsOnlyInCamp()
        {
            int ticks = SimConst.Ticks(TestContent.Db.Camp.TodTransitionSeconds);
            Assert.AreEqual(240, ticks, "야영 중 4초");
            var tr = new TodTransition();
            tr.Update(Ids.Tod.Day, 0, true, ticks);
            Assert.AreEqual(Ids.Tod.Day, tr.To);
            Assert.IsFalse(tr.Blending(0), "처음엔 전환 없이 바로");

            tr.Update(Ids.Tod.Dusk, 100, true, ticks);
            Assert.AreEqual(Ids.Tod.Day, tr.From);
            Assert.AreEqual(Ids.Tod.Dusk, tr.To);
            Assert.AreEqual(0f, tr.Progress(100), 1e-6f);
            Assert.AreEqual(0.5f, tr.Progress(220), 1e-6f);
            Assert.AreEqual(1f, tr.Progress(340), 1e-6f);
            Assert.IsFalse(tr.Blending(340));

            tr.Update(Ids.Tod.Night, 400, false, ticks);
            Assert.AreEqual(Ids.Tod.Night, tr.From, "야영 밖에서 바뀌면 바로 바꾼다(전투 중엔 전환하지 않는다)");
            Assert.IsFalse(tr.Blending(400));
        }

        // ---------------------------------------------------------------- 맵 6벌·소품 (09 7.4절, 01 4.3절)

        [TestCase(0, "field.01")]
        [TestCase(5, "field.06")]
        [TestCase(6, "field.01")]
        public void S09_7_4__MapFromPropVariant(int variant, string map)
        {
            Assert.AreEqual(map, WorldViewRules.MapIdFor(variant, T));
        }

        [Test]
        public void S01_4_3__PropsLeaveCenter20x10Empty()
        {
            var t = T;
            var art = TestArt.Library;
            Assert.AreEqual(6, t.MapIds.Count);
            foreach (var id in t.MapIds)
            {
                var map = ArenaMap.Parse(art.WorldMapJson(id));
                Assert.IsNotNull(map, id);
                Assert.AreEqual(24, map.Width); Assert.AreEqual(14, map.Height);
                Assert.AreEqual(12, map.Spawns.Count, "등장구 각 변 3곳");
                foreach (var p in map.Props)
                {
                    var info = art.WorldInfo(p.Sprite);
                    int wTiles = info != null ? Mathf.CeilToInt(info.FrameSize.X / 64f) : 1;
                    Assert.IsFalse(WorldViewRules.OverlapsCenter(p.Tile.x, p.Tile.y, wTiles, 1, 24, 14, t.CenterClearCells),
                        id + " " + p.Sprite + " " + p.Tile);
                }
            }
            for (int i = 0; i < t.DiscardedItemTiles.Count; i++)
            {
                var tile = t.DiscardedItemTiles[i];
                Assert.IsFalse(WorldViewRules.OverlapsCenter(tile.x, tile.y, 1, 1, 24, 14, t.CenterClearCells), "버린 물건 " + tile);
            }
            foreach (var tile in t.PuddleTiles)
                Assert.IsFalse(WorldViewRules.OverlapsCenter(tile.x, tile.y, 1, 1, 24, 14, t.CenterClearCells), "물웅덩이 " + tile);
            Assert.AreEqual(6, t.DiscardedItemProps.Count, "버린 물건 6종(weather_events.json variants)");
        }

        [Test]
        public void S01_4_3__CenterRegionBounds()
        {
            var c = new Vector2Int(20, 10);
            Assert.IsTrue(WorldViewRules.OverlapsCenter(2, 2, 1, 1, 24, 14, c));
            Assert.IsTrue(WorldViewRules.OverlapsCenter(21, 11, 1, 1, 24, 14, c));
            Assert.IsFalse(WorldViewRules.OverlapsCenter(1, 5, 1, 1, 24, 14, c));
            Assert.IsFalse(WorldViewRules.OverlapsCenter(22, 5, 1, 1, 24, 14, c));
            Assert.IsFalse(WorldViewRules.OverlapsCenter(12, 12, 1, 1, 24, 14, c));
            Assert.IsTrue(WorldViewRules.OverlapsCenter(1, 5, 2, 1, 24, 14, c), "폭 2칸 소품이 안으로 걸치면 겹침");
        }

        [Test]
        public void S09_7_4__SpriteAnchorFromOrigin()
        {
            var a = WorldViewRules.SpriteAnchorSim(new Vector2Int(3, 0), new PixelPoint(32, 62));
            Assert.AreEqual(3.5f, a.x, 1e-6f);
            Assert.AreEqual(62f / 64f, a.y, 1e-6f);
            var spawnW = WorldViewRules.SpriteAnchorSim(new Vector2Int(0, 2), new PixelPoint(32, 126));
            Assert.AreEqual(2f + 126f / 64f, spawnW.y, 1e-6f, "1×2칸 등장구의 바닥");
        }

        // ---------------------------------------------------------------- 상태만으로 고르기 (00 7절 Build 약속)

        [Test]
        public void S00_7__SceneFromStateOnly_CampNightShower()
        {
            var w = NewWorld();
            EnterCamp(w, RunPhase.Camp, new Vector2(12, 7), new Vector2(16, 7));
            w.Run.LastClearedWave = 7;
            w.Run.NextPlan = new WavePlan { Wave = 8, Tod = Ids.Tod.Night };
            w.Weather.Active.Add(Ids.Event.Shower);
            w.Weather.PropVariant = 3;
            var s = ArenaScene.From(w, new ViewOptions(), T, TestContent.Db);
            Assert.IsTrue(s.InCamp);
            Assert.IsTrue(s.ShowCampfire);
            Assert.AreEqual(Ids.Tod.Night, s.TodId);
            Assert.IsTrue(s.NightLike);
            Assert.IsTrue(s.Shower);
            Assert.IsTrue(s.DrawRain);
            Assert.AreEqual("field.04", s.MapId);
            Assert.IsFalse(s.BossMoon);
        }
    }
}

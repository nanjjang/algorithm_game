using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.Art;
using Wizard.Solo.Meta;
using Wizard.Solo.Sim;
using Wizard.Solo.View;
using Wizard.Solo.View.World;

namespace Wizard.Solo.Tests.ViewWorld
{
    /// <summary>
    /// 층을 실제 그림 창고·SpritePool로 세워 본다(00 7절 Build 약속: 이벤트 없이 지금 상태만 보고 그린다).
    /// </summary>
    public class WorldLayerBuildTests
    {
        readonly List<TestViewRig> _rigs = new List<TestViewRig>();
        readonly List<IWorldViewLayer> _layers = new List<IWorldViewLayer>();

        [SetUp]
        public void SetUp()
        {
            _rigs.Clear();
            _layers.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var l in _layers) l.Dispose();
            foreach (var r in _rigs) r.Destroy();
            _rigs.Clear();
            _layers.Clear();
        }

        TestViewRig Rig(SimWorld w, ViewOptions o = null)
        {
            var r = new TestViewRig(w, o);
            _rigs.Add(r);
            return r;
        }

        T Track<T>(T layer) where T : IWorldViewLayer
        {
            _layers.Add(layer);
            return layer;
        }

        static SimWorld CampNightShowerWorld()
        {
            var w = new SimWorld(TestContent.Db, SimModuleSet.Stubs(), 1);
            w.Tick = 1000;
            w.Run.Phase = RunPhase.Camp;
            w.Run.CampSub = CampSubState.FireIdle;
            w.Run.CampFirePos = new Vector2(12, 7);
            w.Run.DepartMarkerPos = new Vector2(16, 7);
            w.Run.CampInteractRadius = 1.5f;
            w.Run.DepartMarkerRadius = 1f;
            w.Run.LastClearedWave = 8;
            w.Run.NextPlan = new WavePlan { Wave = 9, Tod = Ids.Tod.Night };
            w.Weather.Active.Add(Ids.Event.Shower);
            w.Weather.PropVariant = 2;
            return w;
        }

        [Test]
        public void S00_7__ArenaBuildFromStateOnly_CampNightShower()
        {
            var w = CampNightShowerWorld();
            var rig = Rig(w);
            var layer = Track(new ArenaLayer());
            layer.Build(rig.Vc, w);

            Assert.AreEqual(24 * 14, layer.TileCount, "24×14 바닥 타일");
            Assert.AreEqual("field.03", layer.MapId);
            Assert.AreEqual(Ids.Tod.Night, layer.Tod.To);
            Assert.IsFalse(layer.Tod.Blending(w.Tick), "Build는 전환 없이 지금 시간대로");
            Assert.IsTrue(layer.VignetteShown, "밤 비네트");
            Assert.IsFalse(layer.MoonlightShown, "보스 웨이브가 아니면 달빛 원 없음");
            Assert.IsNotNull(layer.Campfire, "야영 중 모닥불");
            var fire = layer.Campfire.transform.localPosition;
            var expect = rig.Vc.ToView(new Vector2(12, 7));
            Assert.AreEqual(expect.x, fire.x, 1e-5f);
            Assert.AreEqual(expect.y, fire.y, 1e-5f);
            Assert.IsNotNull(layer.DepartMarker);
            var marker = layer.DepartMarker.transform.localPosition;
            Assert.AreEqual(16f, marker.x, 1e-5f);
            Assert.AreEqual(7f, marker.y, 1e-5f);
            Assert.AreEqual(4, layer.ActiveRainCount, "빗줄기 4띠(가운데 20×10칸은 비움)");
            Assert.Greater(layer.PuddleCount, 0, "물웅덩이");
            Assert.IsNull(layer.HoldRing, "FireIdle이면 출발 고리 없음");
        }

        [Test]
        public void S06_4_3__ArenaFollowsRunStateAndDepartRing()
        {
            var w = CampNightShowerWorld();
            var rig = Rig(w);
            var layer = Track(new ArenaLayer());
            layer.Build(rig.Vc, w);

            w.Run.CampFirePos = new Vector2(6, 4);
            w.Run.DepartMarkerPos = new Vector2(10, 4);
            w.Run.CampSub = CampSubState.DepartHold;
            w.Run.DepartHoldStartTick = w.Tick;
            w.Tick += 30;
            layer.Render(w, 0f, 0.5f);
            var fire = layer.Campfire.transform.localPosition;
            Assert.AreEqual(6f, fire.x, 1e-5f);
            Assert.AreEqual(14f - 4f, fire.y, 1e-5f);
            Assert.AreEqual(10f, layer.DepartMarker.transform.localPosition.x, 1e-5f);
            Assert.AreEqual(0.5f, layer.Scene.DepartRatio, 1e-6f);
            Assert.IsNotNull(layer.HoldRing, "DepartHold면 표식 위 고리");
            Assert.AreEqual(10f, layer.HoldRing.transform.localPosition.x, 1e-5f);

            // 전투로 넘어가면 모닥불·표식이 사라진다.
            w.Run.Phase = RunPhase.WavePrelude;
            w.Wave = new WaveState { Wave = 9 };
            layer.Render(w, 0f, 0.5f);
            Assert.IsNull(layer.Campfire);
            Assert.IsNull(layer.DepartMarker);
        }

        [Test]
        public void S06_4_3__StartCampBuildShowsCampfire()
        {
            var w = CampNightShowerWorld();
            w.Run.Phase = RunPhase.StartCamp;
            var rig = Rig(w);
            var layer = Track(new ArenaLayer());
            layer.Build(rig.Vc, w);
            Assert.IsNotNull(layer.Campfire);
        }

        [Test]
        public void S08_12_2__ArenaWeatherParticlesOff()
        {
            var w = CampNightShowerWorld();
            w.Weather.Active.Add(Ids.Event.Fog);
            var rig = Rig(w, new ViewOptions { WeatherParticles = false });
            var layer = Track(new ArenaLayer());
            layer.Build(rig.Vc, w);
            Assert.AreEqual(0, layer.ActiveRainCount, "빗줄기 0");
            Assert.AreEqual(0, layer.ActiveFogCount, "안개 0");
            Assert.Greater(layer.PuddleCount, 0, "물웅덩이는 남는다");

            rig.Vc.Options.WeatherParticles = true; // 설정이 바뀌면 다음 프레임부터(층은 매 프레임 읽는다)
            layer.Render(w, 0f, 0.016f);
            Assert.AreEqual(4, layer.ActiveRainCount);
            Assert.AreEqual(4, layer.ActiveFogCount);
        }

        [Test]
        public void S01_4_1__BossWaveMoonlight()
        {
            var w = new SimWorld(TestContent.Db, SimModuleSet.Stubs(), 1);
            w.Run.Phase = RunPhase.WaveCombat;
            w.Wave = new WaveState { Wave = 10 };
            var rig = Rig(w);
            var layer = Track(new ArenaLayer());
            layer.Build(rig.Vc, w);
            Assert.AreEqual(Ids.Tod.Boss, layer.Scene.TodId);
            Assert.IsTrue(layer.MoonlightShown);
            Assert.IsTrue(layer.VignetteShown);
        }

        // ---------------------------------------------------------------- 캐릭터

        static SimWorld PlayerWorld(string charId = "char.1")
        {
            var w = new SimWorld(TestContent.Db, SimModuleSet.Stubs(), 1);
            w.Run.Phase = RunPhase.WaveCombat;
            w.Wave = new WaveState { Wave = 8 };
            var p = w.Players[0];
            p.Active = true;
            p.CharId = charId;
            p.Def = TestContent.Db.Character(charId);
            p.Pos = p.PrevPos = new Vector2(12, 7);
            p.Facing = Dir8.S;
            p.Wand.Stage = 5;
            p.Wand.LinkSlotCount = p.Def != null ? p.Def.BaseLinkSlots : 4;
            p.Hp = p.MaxHp = 800;
            p.Derived.CritStartHu = SimConst.HeatToHu(85f);
            return w;
        }

        [Test]
        public void S02_1__CharacterFeetBelowCenter()
        {
            var w = PlayerWorld();
            var rig = Rig(w);
            var layer = Track(new CharacterLayer());
            layer.Build(rig.Vc, w);
            var f = layer.Figure(0);
            Assert.IsNotNull(f);
            var body = f.Body.transform.localPosition;
            Assert.AreEqual(12f, body.x, 1e-5f);
            Assert.AreEqual(6.6f, body.y, 1e-5f, "판정 중심 (12,7) → 발 (12, 7.4) → Unity y 6.6");
            Assert.AreEqual(SortBands.Bodies + SortBands.YOrder(7.4f), f.Body.sortingOrder);
            Assert.AreEqual(SortBands.Shadows, f.Shadow.sortingOrder);
            Assert.AreEqual("idle", f.CurrentClip.Clip);
            Assert.IsNotNull(f.Body.sprite);
            Assert.IsNotNull(f.Wand.sprite);
        }

        [Test]
        public void S00_7__CharacterBuildFromStateOnly_Downed()
        {
            var w = PlayerWorld();
            w.Players[0].Downed = true;
            w.Players[0].Facing = Dir8.W;
            var rig = Rig(w);
            var layer = Track(new CharacterLayer());
            layer.Build(rig.Vc, w);
            var f = layer.Figure(0);
            Assert.AreEqual("death", f.CurrentClip.Clip, "이벤트 없이 상태(Downed)만으로 쓰러짐");
            Assert.AreEqual(Facing3.Down, f.CurrentClip.Facing);
            Assert.AreEqual(5, f.CurrentFrame, "진행 중인 세계에 다시 세우면 마지막 장(빈 몸)");
            Assert.IsFalse(f.Wand.enabled, "빈 몸 동안 손 지팡이 없음");
        }

        [Test]
        public void S09_2_2__CharacterEventsDriveOneShotClips()
        {
            var w = PlayerWorld();
            var rig = Rig(w);
            var layer = Track(new CharacterLayer());
            layer.Build(rig.Vc, w);
            var f = layer.Figure(0);

            layer.OnEvent(w, new SimEvent { Type = SimEventType.ShotFired, Slot = 0 });
            layer.Render(w, 0f, 0.01f);
            Assert.AreEqual("fire", f.CurrentClip.Clip);
            layer.Render(w, 0f, 1f);
            Assert.AreEqual("idle", f.CurrentClip.Clip, "발사 클립(3장 14fps)이 끝나면 서기");

            layer.OnEvent(w, new SimEvent { Type = SimEventType.PlayerHit, Slot = 0 });
            layer.Render(w, 0f, 0.01f);
            Assert.AreEqual("hit", f.CurrentClip.Clip);

            w.Players[0].Moving = true;
            w.Players[0].Facing = Dir8.NW;
            layer.Render(w, 0f, 1f);
            Assert.AreEqual("walk", f.CurrentClip.Clip);
            Assert.AreEqual(Facing3.Right, f.CurrentClip.Facing);
            Assert.IsTrue(f.Body.flipX);
        }

        [Test]
        public void S08_12_2__WandFxLevelParticles()
        {
            var w = PlayerWorld();
            var p = w.Players[0];
            p.HeatHu = SimConst.HeatToHu(90f); // 임계 → 요청 2(김 2배)
            var opts = new ViewOptions { WandFxLevel = OptionValues.All };
            var rig = Rig(w, opts);
            var layer = Track(new CharacterLayer());
            layer.Build(rig.Vc, w);
            var f = layer.Figure(0);
            var t = layer.Tuning;
            Assert.AreEqual(WandHeatLook.Critical, f.HeatLook);
            Assert.AreEqual(t.WandIdleParticlesCritical, f.ParticleCount);

            opts.WandFxLevel = OptionValues.Reduced;
            layer.Render(w, 0f, 0.016f);
            Assert.AreEqual((t.WandIdleParticlesCritical + 1) / 2, f.ParticleCount);

            opts.WandFxLevel = OptionValues.Minimal;
            layer.Render(w, 0f, 0.016f);
            Assert.AreEqual(0, f.ParticleCount);
            Assert.IsTrue(f.Wand.enabled, "최소에서도 지팡이 본체는 그린다");
        }

        [Test]
        public void S10_3_5__GrazeAfterimagesThree()
        {
            var w = PlayerWorld();
            var p = w.Players[0];
            var rig = Rig(w);
            var layer = Track(new CharacterLayer());
            layer.Build(rig.Vc, w);
            var f = layer.Figure(0);
            // 지난 틱의 자리를 쌓는다.
            for (int i = 0; i < 20; i++)
            {
                w.Tick++;
                p.PrevPos = p.Pos;
                p.Pos += new Vector2(0.05f, 0f);
                layer.Render(w, 1f, 1f / 60f);
            }
            Assert.AreEqual(0, f.GhostCount);
            p.GrazeBuffUntilTick = w.Tick + 90;
            w.Tick++;
            layer.Render(w, 1f, 1f / 60f);
            Assert.AreEqual(layer.Tuning.AfterimageCount, f.GhostCount);
            Assert.AreEqual(3, layer.Tuning.AfterimageCount, "잔상 3장(10 3.5절)");
        }

        [Test]
        public void S08_14_2__InvulnReducedIsTranslucent()
        {
            var w = PlayerWorld();
            var p = w.Players[0];
            p.InvulnUntilTick = w.Tick + 60;
            var opts = new ViewOptions { EffectsLevel = 1 };
            var rig = Rig(w, opts);
            var layer = Track(new CharacterLayer());
            layer.Build(rig.Vc, w);
            Assert.AreEqual(0.4f, layer.Figure(0).BodyAlpha, 1e-5f, "줄임: 깜빡임 없이 60% 투명");
        }

        [Test]
        public void S07_10__MutationAndExpansionMarks()
        {
            var w = PlayerWorld();
            var p = w.Players[0];
            p.Wand.Mutations.Add(Ids.Mutation.GlassWand);
            p.Wand.Mutations.Add(Ids.Mutation.TwistedBarrel);
            p.Wand.LinkSlotCount = p.Def.BaseLinkSlots + 1;
            p.Wand.TempStageDrop = 2;
            var rig = Rig(w);
            var layer = Track(new CharacterLayer());
            layer.Build(rig.Vc, w);
            Assert.AreEqual(2 + 1 + 2, layer.Figure(0).MarkCount, "변이 2점 + 확장 1 + 흰 금 2");
        }
    }
}

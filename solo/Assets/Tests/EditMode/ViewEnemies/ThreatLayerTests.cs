using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.Sim;
using Wizard.Solo.View;
using Wizard.Solo.View.Enemies;

namespace Wizard.Solo.Tests.ViewEnemies
{
    /// <summary>
    /// 층 단위 확인: Build의 약속(이벤트 없이 상태만으로 집어 든다, 00 문서 7절), 줄일 수 없는 것(10 요약서 3.10절 — EffectsLevel과 무관),
    /// 예고 채움 진하기·색각 보조(08 요약서 12.2절), 번쩍 틱과 한도(05 요약서 1.2절).
    /// </summary>
    public class ThreatLayerTests
    {
        ViewRig _rig;
        ThreatLayer _threats;
        EnemyLayer _enemies;

        [SetUp]
        public void SetUp()
        {
            _rig = new ViewRig();
            _threats = new ThreatLayer();
            _enemies = new EnemyLayer();
        }

        [TearDown]
        public void TearDown()
        {
            _threats.Dispose();
            _enemies.Dispose();
            _rig.Destroy();
        }

        ZoneEntity AddZone(int id, ZoneShape shape, Vector2 pos, float radius, int start, int detonate, bool harmless = false)
        {
            var z = new ZoneEntity { Id = id, Shape = shape, Pos = pos, Radius = radius, InnerRadius = radius * 0.5f, Width = 1f, Length = 4f, FanDegrees = 90f, StartTick = start, DetonateTick = detonate, LockTick = start, Harmless = harmless };
            _rig.World.Zones.Add(z);
            return z;
        }

        void Populate()
        {
            var w = _rig.World;
            w.Tick = 31; // 마지막으로 처리한 틱 = 30
            AddZone(101, ZoneShape.Circle, new Vector2(6, 6), 1.5f, 10, 70);
            AddZone(102, ZoneShape.Band, new Vector2(12, 4), 0f, 20, 80);
            w.EnemyShots.Add(new EnemyShotEntity { Id = 201, Pos = new Vector2(8, 8), PrevPos = new Vector2(7.9f, 8), Vel = new Vector2(5, 0), Radius = 0.12f, SpawnTick = 25 });
            w.EnemyShots.Add(new EnemyShotEntity { Id = 202, Pos = new Vector2(9, 8), PrevPos = new Vector2(9, 8), Vel = new Vector2(0, 5), Radius = 0.18f, SpawnTick = 25 });
            w.Hazards.Add(new HazardEntity { Id = 301, Kind = HazardKind.ThornField, Shape = ZoneShape.Circle, Pos = new Vector2(16, 10), Radius = 1.2f, StartTick = 0, UntilTick = 400 });
            w.Obstacles.Add(new ObstacleEntity { Id = 401, Kind = "boulder", Pos = new Vector2(18, 4), Radius = 0.5f });
            w.Enemies.Add(new EnemyState { Id = 1, DefId = Ids.Enemy.SlimeMoss, Pos = new Vector2(4, 4), PrevPos = new Vector2(4, 4), Ai = EnemyAiState.Act, AnimClip = "move", Hp = 10, MaxHp = 10, BodyRadius = 0.4f });
            w.Enemies.Add(new EnemyState { Id = 2, DefId = Ids.Enemy.WolfBramble, Pos = new Vector2(5, 9), PrevPos = new Vector2(5, 9), Ai = EnemyAiState.Act, AnimClip = "move", Hp = 10, MaxHp = 10, BodyRadius = 0.4f });
        }

        [Test]
        public void S00_TG_BUILD_01__PicksUpExistingStateWithoutEvents()
        {
            Populate();
            _enemies.Build(_rig.Vc, _rig.World);
            _threats.Build(_rig.Vc, _rig.World);
            // Build 직후(첫 프레임 전)에 이미 상태에서 집어 들었다
            Assert.AreEqual(2, _threats.TrackedZones);
            Assert.AreEqual(2, _threats.TrackedShots);
            Assert.AreEqual(1, _threats.TrackedHazards);
            Assert.AreEqual(1, _threats.TrackedObstacles);
            Assert.AreEqual(2, _enemies.TrackedCount);
            Assert.IsNotNull(_enemies.BodyOf(1));
            Assert.IsNotNull(_threats.ShotRenderer(201));
            _threats.Render(_rig.World, 0.5f, 1f / 60f);
            Assert.Greater(_threats.FillMesh.TriangleCount, 0);
            Assert.Greater(_threats.EdgeMesh.TriangleCount, 0);
            Assert.AreEqual(SortBands.EnemyShots, _threats.ShotRenderer(201).sortingOrder, "적 탄은 EnemyShots 띠");
        }

        [Test]
        public void S00_TG_BUILD_02__VanishedStateIsDropped()
        {
            Populate();
            _threats.Build(_rig.Vc, _rig.World);
            _rig.World.EnemyShots.Clear();
            _rig.World.Zones.Clear();
            _threats.Render(_rig.World, 0f, 1f / 60f);
            Assert.AreEqual(0, _threats.TrackedShots);
            Assert.AreEqual(0, _threats.TrackedZones);
        }

        [Test]
        public void S00_TG_BUILD_03__DetonatedEventAfterZoneLeftListStillHandled()
        {
            Populate();
            _threats.Build(_rig.Vc, _rig.World);
            _rig.World.Zones.RemoveAll(z => z.Id == 101); // 판정 틱: Compact가 목록에서 뺀다
            _threats.OnEvent(_rig.World, new SimEvent { Type = SimEventType.TelegraphDetonated, EntityId = 101, Slot = -1 });
            _threats.Render(_rig.World, 0f, 1f / 60f);
            Assert.AreEqual(1, _threats.TrackedZones);
            Assert.Greater(_rig.Pool.CountSpritesWithOrder(SortBands.Bursts + 10), 0, "판정 터짐 그림");
        }

        [Test]
        public void S10_TG_NOREDUCE_01__EffectsLevelTwoKeepsTelegraphsAndShots()
        {
            Populate();
            _threats.Build(_rig.Vc, _rig.World);
            _rig.Vc.Options.EffectsLevel = 0;
            _threats.Render(_rig.World, 0.5f, 0f);
            int fill0 = _threats.FillMesh.TriangleCount, edge0 = _threats.EdgeMesh.TriangleCount, shots0 = _threats.TrackedShots;
            float area0 = _threats.FillMesh.TotalArea();
            _rig.Vc.Options.EffectsLevel = 2;
            _threats.Render(_rig.World, 0.5f, 0f);
            Assert.AreEqual(fill0, _threats.FillMesh.TriangleCount);
            Assert.AreEqual(edge0, _threats.EdgeMesh.TriangleCount);
            Assert.AreEqual(area0, _threats.FillMesh.TotalArea(), 1e-4f);
            Assert.AreEqual(shots0, _threats.TrackedShots);
            Assert.IsTrue(_threats.ShotRenderer(201).enabled);
            Assert.IsTrue(_threats.ShotRenderer(202).enabled);
        }

        [Test]
        public void S08_TG_FILLPCT_02__LayerReadsTelegraphFillOption()
        {
            var w = _rig.World;
            w.Tick = 31;
            AddZone(101, ZoneShape.Circle, new Vector2(6, 6), 2f, 0, 60);
            _threats.Build(_rig.Vc, w);
            var t = _threats.Tuning;
            foreach (var pct in new[] { 70, 80, 90 })
            {
                _rig.Vc.Options.TelegraphFillPct = pct;
                _threats.Render(w, 0f, 0f);
                var col = DangerColor.WithAlpha(t.DangerRed, pct / 100f);
                Assert.Greater(_threats.FillMesh.AreaOfColor(col), 1f, "진하기 " + pct);
            }
        }

        [Test]
        public void S08_TG_CB_02__ColorblindAddsStripesToFillAndShots()
        {
            Populate();
            _threats.Build(_rig.Vc, _rig.World);
            _rig.Vc.Options.ColorblindAssist = false;
            _threats.Render(_rig.World, 0.5f, 0f);
            int edgeOff = _threats.EdgeMesh.TriangleCount;
            int fillOff = _threats.FillMesh.TriangleCount;
            Assert.AreEqual(0, _threats.ShotStripeMesh.TriangleCount);
            _rig.Vc.Options.ColorblindAssist = true;
            _threats.Render(_rig.World, 0.5f, 0f);
            Assert.Greater(_threats.FillMesh.TriangleCount, fillOff, "채움에 줄무늬가 더해짐");
            Assert.AreEqual(edgeOff, _threats.EdgeMesh.TriangleCount, "테두리 그대로");
            Assert.Greater(_threats.ShotStripeMesh.TriangleCount, 0, "적 탄 주황 줄무늬");
            foreach (var c in _threats.ShotStripeMesh.Colors) Assert.AreEqual(_threats.Tuning.ColorblindShotStripe, c);
            Assert.AreEqual(2, _threats.TrackedShots, "탄 몸은 그대로");
        }

        [Test]
        public void S05_TG_FLASH_05__LayerFlashesOnLastTwoTicks()
        {
            var w = _rig.World;
            AddZone(101, ZoneShape.Circle, new Vector2(6, 6), 2f, 0, 60);
            _threats.Build(_rig.Vc, w);
            var t = _threats.Tuning;
            float zone = Mathf.PI * 4f;
            var white = DangerColor.WithAlpha(t.FlashWhite, 1f);
            w.Tick = 58; // 처리한 틱 57 = D − 3
            _threats.Render(w, 0f, 0f);
            Assert.Less(_threats.FillMesh.AreaOfColor(white), zone * 0.2f);
            w.Tick = 59; // 58 = D − 2
            _threats.Render(w, 0f, 0f);
            Assert.Greater(_threats.FillMesh.AreaOfColor(white), zone * 0.95f);
            w.Tick = 60; // 59 = D − 1
            _threats.Render(w, 0f, 0f);
            Assert.Greater(_threats.FillMesh.AreaOfColor(white), zone * 0.95f);
        }

        [Test]
        public void S05_TG_FLASH_06__LayerSwitchesToBorderOverLimit()
        {
            var w = _rig.World;
            // 반지름 5칸 원 = π·25·4096 ≈ 321,700px. 둘이면 25%(518,400px)를 넘는다
            AddZone(101, ZoneShape.Circle, new Vector2(6, 7), 5f, 0, 60);
            AddZone(102, ZoneShape.Circle, new Vector2(18, 7), 5f, 0, 60);
            _threats.Build(_rig.Vc, w);
            var t = _threats.Tuning;
            w.Tick = 58; // 57 = D − 3: 번쩍 전
            _threats.Render(w, 0f, 0f);
            float edgeWhiteBefore = _threats.EdgeMesh.AreaOfColor(DangerColor.WithAlpha(t.FlashWhite, 1f));
            w.Tick = 59;
            _threats.Render(w, 0f, 0f);
            float one = Mathf.PI * 25f;
            float whiteFill = _threats.FillMesh.AreaOfColor(DangerColor.WithAlpha(t.FlashWhite, 1f));
            Assert.Greater(whiteFill, one * 0.95f);
            Assert.Less(whiteFill, one * 1.2f, "둘째 번쩍은 구역 전체가 아니라 테두리 강조");
            // 강조된 쪽: 흰 선 8px 테두리가 테두리 층에 있다(3겹의 흰 2px보다 넓다)
            float ring8 = 2f * Mathf.PI * 5f * (8f / 64f);
            Assert.Greater(_threats.EdgeMesh.AreaOfColor(DangerColor.WithAlpha(t.FlashWhite, 1f)) - edgeWhiteBefore, ring8 * 0.8f);
        }

        [Test]
        public void S01_TG_STAR_01__StarfallRingIsNotDanger()
        {
            var w = _rig.World;
            w.Tick = 11;
            w.Starfall.Phase = StarfallPhase.Telegraph;
            w.Starfall.Pos = new Vector2(12, 7);
            w.Starfall.PhaseStartTick = 0;
            _threats.Build(_rig.Vc, w);
            _threats.Render(w, 0f, 1f / 60f);
            Assert.Greater(_threats.StarMesh.TriangleCount, 0);
            foreach (var c in _threats.StarMesh.Colors) Assert.IsFalse(DangerColor.IsDanger(c));
        }

        [Test]
        public void S05_TG_HAZARD_01__FrostFieldUsesMagentaEdgeWhiteHatch()
        {
            var w = _rig.World;
            w.Tick = 11;
            w.Hazards.Add(new HazardEntity { Id = 301, Kind = HazardKind.FrostField, SlowOnly = true, Shape = ZoneShape.Circle, Pos = new Vector2(10, 7), Radius = 2f, UntilTick = int.MaxValue });
            _threats.Build(_rig.Vc, w);
            _threats.Render(w, 0f, 0f);
            var t = _threats.Tuning;
            Assert.Greater(_threats.EdgeMesh.AreaOfColor(DangerColor.WithAlpha(t.FrostEdge, 1f)), 0f);
            Assert.Greater(_threats.FillMesh.AreaOfColor(DangerColor.WithAlpha(t.FrostHatch, t.HatchAlpha)), 0f);
            Assert.AreEqual(0f, _threats.FillMesh.AreaOfColor(DangerColor.WithAlpha(t.HazardHatch, t.HatchAlpha)), 1e-6f);
        }

        [Test]
        public void S05_TG_ELITE_02__EliteBodyGetsGoldOutlineViaPropertyBlock()
        {
            var w = _rig.World;
            w.Tick = 5;
            w.Enemies.Add(new EnemyState
            {
                Id = 7, DefId = Ids.Enemy.SlimeMoss, Elite = TestContent.Db.Elite("elite.slime_moss") ?? new Wizard.Solo.Data.EliteDef { Id = "elite.slime_moss" },
                Pos = new Vector2(10, 7), PrevPos = new Vector2(10, 7), Facing = new Vector2(-1, 0), Ai = EnemyAiState.Act, AnimClip = "move", Hp = 5, MaxHp = 10, BodyRadius = 0.4f
            });
            _enemies.Build(_rig.Vc, w);
            _enemies.Render(w, 0f, 1f / 60f);
            var body = _enemies.BodyOf(7);
            Assert.IsNotNull(body);
            Assert.IsTrue(body.flipX, "facing.x < 0 → 좌우 반전");
            var mpb = new MaterialPropertyBlock();
            body.GetPropertyBlock(mpb);
            Color c = mpb.GetColor(Wizard.Solo.Art.SpriteShaderProps.OutlineColor);
            Assert.AreEqual(0xFF / 255f, c.r, 0.01f);
            Assert.AreEqual(0xD2 / 255f, c.g, 0.01f);
            Assert.AreEqual(0x4A / 255f, c.b, 0.01f);
            Assert.AreEqual(2f, mpb.GetFloat(Wizard.Solo.Art.SpriteShaderProps.OutlineSize), 1e-5f);
            Assert.IsTrue(_rig.Art.MonsterRequests.Contains("elite.horn:idle"), "뿔 표식");
        }

        [Test]
        public void S05_TG_BOSS_01__BossBodyFoldsWhenBossStateClears()
        {
            var w = _rig.World;
            w.Tick = 5;
            w.Run.Phase = RunPhase.WaveCombat;
            var e = new EnemyState { Id = 9, DefId = Ids.Boss.KilnBoar, IsBoss = true, Pos = new Vector2(12, 7), PrevPos = new Vector2(12, 7), Ai = EnemyAiState.Act, AnimClip = "walk", Hp = 0, MaxHp = 100, BodyRadius = 1f };
            w.Enemies.Add(e);
            w.Boss = new BossState { EnemyId = 9, State = BossFightState.Dead };
            _enemies.Build(_rig.Vc, w);
            _enemies.Render(w, 0f, 1f / 60f);
            // 처치 틱: 몸통은 목록에서 빠지고 EnemyKilled(B = 2)가 온다 → 느린 사망 연출로 남는다
            w.Enemies.Clear();
            _enemies.OnEvent(w, new SimEvent { Type = SimEventType.EnemyKilled, EntityId = 9, B = 2, Slot = -1 });
            w.Run.Phase = RunPhase.WaveClearing;
            _enemies.Render(w, 0f, 0.1f);
            Assert.IsTrue(_enemies.IsDying(9));
            Assert.IsNotNull(_enemies.BodyOf(9));
            w.Boss = null; // Bosses.Dismiss
            _enemies.Render(w, 0f, 0.1f);
            Assert.AreEqual(0, _enemies.TrackedCount);
        }

        [Test]
        public void S05_TG_ENEMY_01__NormalEnemyRemovedWhenKilled()
        {
            Populate();
            _enemies.Build(_rig.Vc, _rig.World);
            _rig.World.Enemies.RemoveAll(x => x.Id == 1);
            _enemies.OnEvent(_rig.World, new SimEvent { Type = SimEventType.EnemyKilled, EntityId = 1, B = 0, Slot = -1 });
            _enemies.Render(_rig.World, 0f, 0f);
            Assert.AreEqual(1, _enemies.TrackedCount);
            Assert.IsNull(_enemies.BodyOf(1));
        }
    }
}

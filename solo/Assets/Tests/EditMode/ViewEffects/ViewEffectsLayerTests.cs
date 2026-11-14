using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.App;
using Wizard.Solo.Art;
using Wizard.Solo.Meta;
using Wizard.Solo.Sim;
using Wizard.Solo.Tests.Art;
using Wizard.Solo.View;
using Wizard.Solo.View.Effects;

namespace Wizard.Solo.Tests.ViewEffects
{
    /// <summary>소리 재생 기록(무음).</summary>
    public sealed class RecordingSoundPlayer : ISoundPlayer
    {
        public readonly List<KeyValuePair<string, SoundBus>> Played = new List<KeyValuePair<string, SoundBus>>();
        public readonly List<string> Music = new List<string>();
        public void Play(string eventId, SoundBus bus = SoundBus.Sfx, float volume = 1f, float pitch = 1f) =>
            Played.Add(new KeyValuePair<string, SoundBus>(eventId, bus));
        public void SetMusic(string trackId) => Music.Add(trackId);
        public void SetVolume(SoundBus bus, float volume01) { }
        public int Count(string id)
        {
            int n = 0;
            foreach (var p in Played) if (p.Key == id) n++;
            return n;
        }
    }

    /// <summary>
    /// G16 층을 실제 그림 창고(디스크) + 스프라이트 풀로 세워 본다: 지금 상태만으로 Build, 옵션 적용, 약점 알아냄 문양 1.5초,
    /// 피격 표시, 소리 연결. 카메라가 없어 WorldToHud는 (0,0)이다(글자 자리는 보지 않는다).
    /// </summary>
    public class ViewEffectsLayerTests
    {
        GameObject _root, _textRoot;
        SpritePool _pool;
        ArtLibrary _art;
        RecordingSoundPlayer _sound;
        ViewContext _vc;
        SimWorld _w;
        EffectsShared _shared;
        ShotLayer _shots;
        EffectLayer _fx;
        AudioLayer _audio;
        readonly List<float> _shakes = new List<float>();
        readonly List<float> _hitStops = new List<float>();

        [SetUp]
        public void SetUp()
        {
            _shakes.Clear();
            _hitStops.Clear();
            _root = new GameObject("G16TestWorld");
            _textRoot = new GameObject("G16TestText", typeof(RectTransform));
            _art = new ArtLibrary(new DiskArtSource());
            _pool = new SpritePool(_art, _root.transform);
            _sound = new RecordingSoundPlayer();
            var ctx = new GameContext { Art = _art, Names = TestContent.Names, Content = TestContent.Db, Sound = _sound };
            _w = new SimWorld(TestContent.Db, SimModuleSet.Stubs(), 1);
            var p = _w.Players[0];
            p.Active = true;
            p.CharId = Ids.Char.C3;
            p.Def = TestContent.Db.Character(Ids.Char.C3);
            p.Pos = p.PrevPos = new Vector2(12f, 7f);
            p.Facing = Dir8.E;
            _w.Run.Phase = RunPhase.WaveCombat;
            _w.Wave.Wave = 1;
            _vc = new ViewContext
            {
                Game = ctx, World = _w, WorldRoot = _root.transform, Pool = _pool, WorldTextRoot = (RectTransform)_textRoot.transform,
                HudRoot = (RectTransform)_textRoot.transform, Options = new ViewOptions(),
                Shake = (px, s) => _shakes.Add(px), HitStop = s => _hitStops.Add(s)
            };
            _shared = new EffectsShared(ViewEffectsTestData.Fresh());
            _shots = new ShotLayer(_shared);
            _fx = new EffectLayer(_shared);
            _audio = new AudioLayer(ViewEffectsTestData.Fresh(), (a, b) => 0f);
        }

        [TearDown]
        public void TearDown()
        {
            _shots.Dispose();
            _fx.Dispose();
            _audio.Dispose();
            _pool.DestroyAll();
            Object.DestroyImmediate(_textRoot);
            Object.DestroyImmediate(_root);
        }

        void BuildAll()
        {
            _shots.Build(_vc, _w);
            _fx.Build(_vc, _w);
            _audio.Build(_vc, _w);
        }

        void Event(SimEvent e)
        {
            e.Tick = e.Tick == 0 ? _w.Tick : e.Tick;
            _shots.OnEvent(_w, e);
            _fx.OnEvent(_w, e);
            _audio.OnEvent(_w, e);
        }

        void Frame(float dt = 1f / 60f)
        {
            _shots.Render(_w, 1f, dt);
            _fx.Render(_w, 1f, dt);
            _audio.Render(_w, 1f, dt);
        }

        ProjectileEntity AddShot(int id, string blockId = Ids.Block.MagicBolt)
        {
            var b = TestContent.Db.Block(blockId);
            var pr = new ProjectileEntity
            {
                Id = id, OwnerSlot = 0, Block = b, State = ProjectileState.Flying, Pos = new Vector2(5f, 5f), PrevPos = new Vector2(5f, 5f),
                Dir = new Vector2(1f, 0f), Radius = b.HitRadius, SpawnTick = _w.Tick, Source = ShotSource.Fire
            };
            _w.Projectiles.Add(pr);
            return pr;
        }

        // ------------------------------------------------------------ 지금 상태만으로 Build (IWorldViewLayer 약속, 4.1절 보조 판)

        [Test]
        public void S00_7__BuildPicksUpExistingShotsZonesDrops()
        {
            AddShot(5);
            _w.PlayerZones.Add(new PlayerZoneEntity { Id = 6, Kind = PlayerZoneKind.Kiln, Pos = new Vector2(12, 7), Radius = 2.5f, StartTick = 0, UntilTick = 600 });
            _w.Drops.Add(new DropEntity { Id = 7, Kind = DropKind.MaterialBundle, Pos = new Vector2(8, 8), SpawnTick = _w.Tick });
            BuildAll();
            Assert.IsTrue(_shots.HasShotView(5)); // 이미 날고 있는 탄
            Assert.IsTrue(_shots.HasZoneView(6)); // 깔린 지대
            Assert.AreEqual(1, _fx.DropViewCount); // 깔린 드랍
            Assert.IsTrue(_fx.HasDropPillar(7)); // 재료 꾸러미 빛기둥
            Assert.AreEqual(0, _fx.DamageNumberCount); // 일회 효과는 다시 내지 않는다

            _w.Projectiles.Clear();
            _w.PlayerZones.Clear();
            _w.Drops.Clear();
            Frame();
            Assert.AreEqual(0, _shots.ShotViewCount);
            Assert.AreEqual(0, _shots.ZoneViewCount);
            Assert.AreEqual(0, _fx.DropViewCount);
        }

        [Test]
        public void S06_DropPillarLastsOneSecond()
        {
            _w.Drops.Add(new DropEntity { Id = 7, Kind = DropKind.MaterialBundle, Pos = new Vector2(8, 8), SpawnTick = 0 });
            BuildAll();
            Assert.IsTrue(_fx.HasDropPillar(7));
            _w.Tick = 60;
            Frame();
            Assert.IsFalse(_fx.HasDropPillar(7)); // 빛기둥 1.0초
            var old = new DropEntity { Id = 8, Kind = DropKind.MaterialBundle, Pos = new Vector2(9, 8), SpawnTick = 0 };
            _w.Drops.Add(old);
            Frame();
            Assert.IsFalse(_fx.HasDropPillar(8)); // 다시 세울 때 지난 꾸러미는 빛기둥을 다시 내지 않는다
        }

        // ------------------------------------------------------------ 옵션: 내 탄·사본·지대 투명도, 꼬리, 각주 점, 임계 테

        [Test]
        public void S08_12_2__MyBulletOpacityMultiplies()
        {
            AddShot(5);
            _vc.Options.MyBulletOpacity = 0.5f;
            BuildAll();
            Frame();
            Assert.AreEqual(0.5f, _shots.BodyAlpha(5), 1e-4f);
        }

        [Test]
        public void S10_3_2__BulletInsideTelegraphMaskCappedAt40()
        {
            AddShot(5);
            _w.Zones.Add(new ZoneEntity { Id = 90, Shape = ZoneShape.Circle, Pos = new Vector2(5, 5), Radius = 1f, StartTick = 0, DetonateTick = 600 });
            BuildAll();
            Frame();
            Assert.AreEqual(0.4f, _shots.BodyAlpha(5), 1e-4f); // 예고 마스크 안 40%
            _vc.Options.MyBulletOpacity = 0.3f;
            Frame();
            Assert.AreEqual(0.3f, _shots.BodyAlpha(5), 1e-4f); // 옵션을 곱한 뒤 상한
            _w.Tick = 598;
            Frame();
            Assert.AreEqual(0f, _shots.BodyAlpha(5), 1e-4f); // 번쩍이는 마지막 2프레임 0%
        }

        [Test]
        public void S08_12_2__CloneZoneOpacityMultipliesZones()
        {
            _w.PlayerZones.Add(new PlayerZoneEntity { Id = 6, Kind = PlayerZoneKind.Kiln, Pos = new Vector2(12, 7), Radius = 2.5f, StartTick = 0, UntilTick = 600 });
            _w.PlayerZones.Add(new PlayerZoneEntity { Id = 7, Kind = PlayerZoneKind.IceScreen, Pos = new Vector2(14, 7), Width = 3f, Thickness = 0.5f, Facing = Dir8.N, StartTick = 0, UntilTick = 600 });
            _w.PlayerZones.Add(new PlayerZoneEntity { Id = 8, Kind = PlayerZoneKind.CopyCircle, Pos = new Vector2(10, 7), Radius = 0.75f, StartTick = 0, UntilTick = 600 });
            _vc.Options.CloneZoneOpacity = 0.5f;
            BuildAll();
            _w.Tick = 30;
            Frame();
            Assert.AreEqual(0.5f, _shots.ZoneAlpha(6), 1e-4f); // 불가마 × 0.5
            Assert.AreEqual(0.375f, _shots.ZoneAlpha(7), 1e-4f); // 병풍 75% × 0.5
            Assert.AreEqual(0.5f, _shots.ZoneAlpha(8), 1e-4f); // 사본 마법진 × 0.5
        }

        [Test]
        public void S08_12_2__BulletTrailOnShortOff()
        {
            AddShot(5);
            BuildAll();
            _vc.Options.BulletTrail = OptionValues.On;
            Frame();
            Assert.AreEqual(16f / 64f, _shots.TrailLengthCells(5), 1e-3f); // +0~+3 꼬리 16px
            _vc.Options.BulletTrail = OptionValues.Short;
            Frame();
            Assert.AreEqual(8f / 64f, _shots.TrailLengthCells(5), 1e-3f);
            _vc.Options.BulletTrail = OptionValues.Off;
            Frame();
            Assert.AreEqual(0f, _shots.TrailLengthCells(5), 1e-6f);
        }

        [Test]
        public void S02_8_3__DepthFootnoteDotsForDepthDecayWand()
        {
            var a = AddShot(5);
            a.ScopeDepth = 2;
            var b = AddShot(6);
            b.ScopeDepth = 5;
            var c = AddShot(7);
            c.ScopeDepth = 0;
            BuildAll();
            Frame();
            Assert.AreEqual(2, _shots.DepthDotCount(5));
            Assert.AreEqual(3, _shots.DepthDotCount(6)); // 1~3개
            Assert.AreEqual(0, _shots.DepthDotCount(7));
            _w.Players[0].Def = TestContent.Db.Character(Ids.Char.C1);
            Frame();
            Assert.AreEqual(0, _shots.DepthDotCount(5)); // 깊이 감쇠 특성이 아니면 없음
        }

        [Test]
        public void S03_14__CritShotDoubleRim()
        {
            var a = AddShot(5);
            a.IsCrit = true;
            AddShot(6);
            BuildAll();
            Frame();
            Assert.IsTrue(_shots.HasCritRim(5));
            Assert.IsFalse(_shots.HasCritRim(6));
        }

        // ------------------------------------------------------------ 약점·저항 처음 알아냄 (03 14절, D-54, H-15)

        [Test]
        public void S03_14__AffinityGlyphFor90TicksOnce()
        {
            _w.Enemies.Add(new EnemyState { Id = 40, Pos = new Vector2(10, 5), BodyRadius = 0.4f, Hp = 10, MaxHp = 10 });
            BuildAll();
            _w.Tick = 100;
            Event(new SimEvent { Type = SimEventType.AffinityDiscovered, Slot = 0, EntityId = 40, A = (int)Affinity.Weak, Id = Ids.Element.Fire, Pos = new Vector2(10, 5), Tick = 100 });
            Assert.AreEqual(1, _fx.AffinityMarkerCount);
            Assert.AreEqual(1, _sound.Count("hit.affinity_discovered")); // 소리 사건 H-15 한 번
            _w.Tick = 189;
            Frame();
            Assert.AreEqual(1, _fx.AffinityMarkerCount); // 89틱에는 남아 있다
            _w.Tick = 190;
            Frame();
            Assert.AreEqual(0, _fx.AffinityMarkerCount); // 1.5초(90틱)
        }

        // ------------------------------------------------------------ 피해 숫자 옵션

        [Test]
        public void S08_12_2__DamageNumberModeOffAndCritWeakOnly()
        {
            BuildAll();
            _vc.Options.DamageNumberMode = OptionValues.Off;
            Event(new SimEvent { Type = SimEventType.EnemyDamaged, EntityId = 40, A = 10, B = DamageFlags.Crit, Id = Ids.Element.Arcane, Pos = new Vector2(5, 5) });
            Assert.AreEqual(0, _fx.DamageNumberCount); // off: 전부 없음
            _vc.Options.DamageNumberMode = OptionValues.CritWeakOnly;
            Event(new SimEvent { Type = SimEventType.EnemyDamaged, EntityId = 41, A = 10, B = 0, Id = Ids.Element.Arcane, Pos = new Vector2(5, 5) });
            Assert.AreEqual(0, _fx.DamageNumberCount); // crit_weak_only: 일반 숫자 없음
            Event(new SimEvent { Type = SimEventType.EnemyDamaged, EntityId = 42, A = 45, B = DamageFlags.Crit, Id = Ids.Element.Arcane, Pos = new Vector2(5, 5) });
            Assert.AreEqual(1, _fx.DamageNumberCount);
            Assert.AreEqual("45!", _fx.DamageNumberGlyphs(0));
            _vc.Options.DamageNumberMode = OptionValues.All;
            Event(new SimEvent { Type = SimEventType.GoldGained, A = 12, Pos = new Vector2(6, 6) });
            Assert.AreEqual(2, _fx.DamageNumberCount);
            Assert.AreEqual(DamageNumberKind.Gold, _fx.DamageNumberKindAt(1));
        }

        [Test]
        public void S08_6_2__SameEnemyHitsMergeIntoOneNumber()
        {
            BuildAll();
            _w.Tick = 10;
            Event(new SimEvent { Type = SimEventType.EnemyDamaged, EntityId = 40, A = 12, Id = Ids.Element.Arcane, Pos = new Vector2(5, 5), Tick = 10 });
            Event(new SimEvent { Type = SimEventType.EnemyDamaged, EntityId = 40, A = 13, Id = Ids.Element.Arcane, Pos = new Vector2(5, 5), Tick = 20 });
            Assert.AreEqual(1, _fx.DamageNumberCount);
            Assert.AreEqual("25", _fx.DamageNumberGlyphs(0));
        }

        // ------------------------------------------------------------ 바닥 흔적, 처치 조각, 기술 이름

        [Test]
        public void S08_12_2__GroundMarksOffDrawsNothing()
        {
            BuildAll();
            _vc.Options.GroundMarks = false;
            Event(new SimEvent { Type = SimEventType.Explosion, Slot = 0, Id = Ids.Element.Fire, Pos = new Vector2(5, 5), F = 1.5f });
            Assert.AreEqual(0, _fx.GroundMarksSpawned);
            _vc.Options.GroundMarks = true;
            Event(new SimEvent { Type = SimEventType.Explosion, Slot = 0, Id = Ids.Element.Fire, Pos = new Vector2(15, 5), F = 1.5f });
            Assert.AreEqual(1, _fx.GroundMarksSpawned);
        }

        [Test]
        public void S08_12_2__KillShardsSmokeOnly()
        {
            BuildAll();
            _vc.Options.KillShards = OptionValues.SmokeOnly;
            Event(new SimEvent { Type = SimEventType.EnemyKilled, EntityId = 40, Id = Ids.Enemy.SlimeMoss, A = 3, B = 0, Pos = new Vector2(5, 5) });
            Frame();
            Assert.AreEqual(1, _fx.KillSmokesSpawned); // 연기는 남는다
            Assert.AreEqual(0, _fx.KillShardsSpawned); // 조각 없음
            _vc.Options.KillShards = OptionValues.On;
            Event(new SimEvent { Type = SimEventType.EnemyKilled, EntityId = 41, Id = Ids.Enemy.SlimeMoss, A = 3, B = 0, Pos = new Vector2(15, 5) });
            Frame();
            Assert.GreaterOrEqual(_fx.KillShardsSpawned, 4); // 조각 4~6개
            Assert.LessOrEqual(_fx.KillShardsSpawned, 6);
        }

        static string ArtWithDifficulty(bool five)
        {
            foreach (var a in TestContent.Db.Arts) if ((a.Difficulty >= 5) == five) return a.Id;
            return null;
        }

        [Test]
        public void S08_12_2__ArtNamePopupDifficulty5Only()
        {
            BuildAll();
            string easy = ArtWithDifficulty(false), hard = ArtWithDifficulty(true);
            Assert.IsNotNull(easy);
            Assert.IsNotNull(hard);
            _vc.Options.ArtNamePopup = OptionValues.Difficulty5Only;
            Event(new SimEvent { Type = SimEventType.ArtFired, Slot = 0, Id = easy });
            Assert.AreEqual(0, _fx.NamePopupsShown);
            Event(new SimEvent { Type = SimEventType.ArtFired, Slot = 0, Id = hard });
            Assert.AreEqual(1, _fx.NamePopupsShown);
            _vc.Options.ArtNamePopup = OptionValues.Off;
            Event(new SimEvent { Type = SimEventType.ArtFired, Slot = 0, Id = hard });
            Assert.AreEqual(1, _fx.NamePopupsShown);
            _vc.Options.ArtNamePopup = OptionValues.On;
            Event(new SimEvent { Type = SimEventType.ArtFired, Slot = 0, Id = easy });
            Assert.AreEqual(2, _fx.NamePopupsShown);
        }

        // ------------------------------------------------------------ 피격(줄일 수 없음), 그림 정지 셋, 소리 묶음

        [Test]
        public void S10_3_7__PlayerHitStopEdgeAndShake()
        {
            BuildAll();
            _vc.Options.EffectsLevel = 2; // 최소여도 피격 표시는 줄지 않는다
            Event(new SimEvent { Type = SimEventType.PlayerHit, Slot = 0, A = 100, B = 300 });
            Assert.IsTrue(_fx.Overlay.EdgeVisible); // 화면 가장자리
            Assert.AreEqual(0.4f, _fx.Overlay.EdgeAlpha, 1e-4f);
            CollectionAssert.Contains(_hitStops, 0.12f); // 그림 정지 0.12초
            CollectionAssert.Contains(_shakes, 8f); // 흔들림 중
            Event(new SimEvent { Type = SimEventType.PlayerHit, Slot = 0, A = 200, B = 100 });
            CollectionAssert.Contains(_shakes, 12f); // 2칸 피해는 대
            for (int i = 0; i < 20; i++) Frame();
            Assert.IsFalse(_fx.Overlay.EdgeVisible); // 0.3초에 사라짐
            Assert.AreEqual(2, _sound.Count("player.hit"));
        }

        [Test]
        public void S10_3_4__CritHitStop30msAndThunderLance80ms()
        {
            BuildAll();
            _w.Tick = 100;
            Event(new SimEvent { Type = SimEventType.EnemyDamaged, EntityId = 40, A = 10, B = DamageFlags.Crit, Id = Ids.Element.Arcane, Pos = new Vector2(5, 5), Tick = 100 });
            Assert.AreEqual(1, _hitStops.Count);
            Assert.AreEqual(0.03f, _hitStops[0], 1e-5f);
            Event(new SimEvent { Type = SimEventType.ArtFired, Slot = 0, Id = Ids.Art.ThunderSpear, B = 1, Tick = 110 });
            Assert.AreEqual(2, _hitStops.Count);
            Assert.AreEqual(0.07f, _hitStops[1], 1e-5f); // 뇌광창 0.08초 — 1초 0.1초 예산에서 임계가 쓴 0.03을 뺀 나머지
            Event(new SimEvent { Type = SimEventType.ArtFired, Slot = 0, Id = Ids.Art.ThunderSpear, B = 1, Tick = 400 });
            Assert.AreEqual(0.08f, _hitStops[2], 1e-5f); // 1초 지나면 0.08초 그대로
        }

        [Test]
        public void S10_5_2__AudioLayerBusesAndMusic()
        {
            _w.Run.Phase = RunPhase.Camp;
            BuildAll();
            CollectionAssert.Contains(_sound.Music, "bgm.camp"); // 지금 상태로 곡을 고른다
            Event(new SimEvent { Type = SimEventType.ShotFired, Slot = 0 });
            Event(new SimEvent { Type = SimEventType.EnemyShotFired, EntityId = 3 });
            Event(new SimEvent { Type = SimEventType.GrazeAwarded, Slot = 0 });
            Assert.Contains(new KeyValuePair<string, SoundBus>("heat.shot_fired", SoundBus.Wand), _sound.Played);
            Assert.Contains(new KeyValuePair<string, SoundBus>("enemy.projectile_fire", SoundBus.Enemy), _sound.Played);
            Assert.Contains(new KeyValuePair<string, SoundBus>("graze.awarded", SoundBus.Sfx), _sound.Played);
            Event(new SimEvent { Type = SimEventType.TodChanged, Id = Ids.Tod.Dusk });
            Assert.AreEqual("bgm.tod.dusk", _sound.Music[_sound.Music.Count - 1]);
            Event(new SimEvent { Type = SimEventType.CampEntered });
            Assert.AreEqual("bgm.camp", _sound.Music[_sound.Music.Count - 1]);
            int n = _sound.Music.Count;
            Event(new SimEvent { Type = SimEventType.CampEntered });
            Assert.AreEqual(n, _sound.Music.Count); // 같은 곡은 다시 틀지 않는다
        }
    }
}

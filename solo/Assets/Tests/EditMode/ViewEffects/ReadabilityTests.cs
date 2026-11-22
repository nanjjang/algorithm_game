using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.App;
using Wizard.Solo.Art;
using Wizard.Solo.Meta;
using Wizard.Solo.Sim;
using Wizard.Solo.Tests.Art;
using Wizard.Solo.View;
using Wizard.Solo.View.Effects;
using Wizard.Solo.View.Enemies;

namespace Wizard.Solo.Tests.ViewEffects
{
    /// <summary>
    /// 가독성 보강(솔로): 정렬 띠 순서, 연출 우선순위·프레임 상한·군중 감쇠, 큰 고리 합치기, 적 탄 둘레 가림 상한,
    /// 내 쪽 색·그림의 위험색 검사, 설정 옵션 3줄, 피해 숫자 크기.
    /// </summary>
    public class ReadabilityTests
    {
        // ------------------------------------------------------------ 정렬 띠

        [Test]
        public void Layers_EnemyThreatsAboveEverythingMine()
        {
            int enemyBodyTop = SortBands.Bodies + SortBands.YOrder(14f);
            Assert.Less(SortBands.Floor, SortBands.PlayerZones, "바닥 < 내 장판");
            Assert.Less(SortBands.PlayerZones, SortBands.Bodies, "내 장판 < 적 몸");
            Assert.Greater(FxLayers.PlayerBody, enemyBodyTop, "적 몸 < 플레이어");
            Assert.Less(FxLayers.PlayerBody, SortBands.PlayerShots, "플레이어 < 내 탄");
            Assert.Less(SortBands.Bursts + 99, FxLayers.DamageNumbers, "내 이펙트 < 피해 숫자");
            Assert.Less(FxLayers.DamageNumbers + 10, FxLayers.EnemyShots, "피해 숫자 < 적 탄");
            Assert.Less(FxLayers.EnemyShots + 60, FxLayers.TelegraphEdge, "적 탄(+줄무늬) < 예고 테두리");
            Assert.Less(SortBands.DamageNumbers + 99, FxLayers.Offscreen, "전장 모든 것 < 화면 밖 표식");
            Assert.Less(FxLayers.Offscreen, FxLayers.OffscreenTelegraph);
        }

        // ------------------------------------------------------------ 우선순위·프레임 상한

        [Test]
        public void Governor_DropsLowPriorityFirst()
        {
            var g = new FxGovernor { FrameParticles = 100 };
            g.BeginFrame(0);
            int decor = 0;
            while (g.AllowParticles(FxPriority.Decor)) decor++;
            Assert.AreEqual(45, decor, "장식은 합계의 45%까지");
            int hit = 0;
            while (g.AllowParticles(FxPriority.NormalHit)) hit++;
            Assert.AreEqual(25, hit, "일반 타격은 70%까지");
            Assert.IsTrue(g.AllowParticles(FxPriority.Crit), "치명은 아직 남는다");
            Assert.IsFalse(g.AllowParticles(FxPriority.Decor));
            for (int i = 0; i < 200; i++) Assert.IsTrue(g.AllowParticles(FxPriority.PlayerHurt), "내 피격은 버리지 않는다");
            g.BeginFrame(0);
            Assert.IsTrue(g.AllowParticles(FxPriority.Decor), "새 프레임은 다시 0부터");
        }

        [Test]
        public void Governor_NumbersPerFrameCap_ImportantAlwaysPass()
        {
            var g = new FxGovernor { FrameNumbers = 3 };
            g.BeginFrame(0);
            Assert.IsTrue(g.AllowNumber(false));
            Assert.IsTrue(g.AllowNumber(false));
            Assert.IsTrue(g.AllowNumber(false));
            Assert.IsFalse(g.AllowNumber(false));
            Assert.IsTrue(g.AllowNumber(true), "치명·큰 피해는 상한 밖");
            Assert.AreEqual(1, g.DroppedNumbers);
        }

        [Test]
        public void Governor_ShakeDoesNotStackWithinWindow()
        {
            var g = new FxGovernor { ShakeWindow = 0.12f, ShakeMaxPx = 16f };
            g.BeginFrame(0);
            Assert.AreEqual(8f, g.Shake(8f, 1.0f));
            Assert.AreEqual(0f, g.Shake(4f, 1.05f), "창 안의 더 약한 흔들림은 버린다");
            Assert.AreEqual(12f, g.Shake(12f, 1.06f), "더 센 것은 보낸다");
            Assert.AreEqual(16f, g.Shake(40f, 1.5f), "세기 상한");
            Assert.AreEqual(4f, g.Shake(4f, 2.0f), "창이 지나면 다시");
        }

        [Test]
        public void Governor_HitStopIsWeightProportional()
        {
            var g = new FxGovernor { HitStopMax = 0.16f, CrowdFrom = 20, CrowdTo = 70, CrowdHitStopMin = 0f };
            g.BeginFrame(0);
            Assert.AreEqual(0f, g.HitStop(0.05f, FxPriority.NormalHit), "일반 타격은 정지 없음");
            Assert.AreEqual(0f, g.HitStop(0.05f, FxPriority.Decor));
            Assert.AreEqual(0f, g.HitStop(0.05f, FxPriority.Graze), "스침은 정지 금지(10 3.5절)");
            Assert.AreEqual(0.03f, g.HitStop(0.03f, FxPriority.Crit), 1e-6f);
            Assert.AreEqual(0.16f, g.HitStop(0.4f, FxPriority.Crit, true), 1e-6f, "한 번 상한");
            Assert.AreEqual(0.12f, g.HitStop(0.12f, FxPriority.PlayerHurt), 1e-6f);
            g.BeginFrame(80);
            Assert.AreEqual(0f, g.HitStop(0.03f, FxPriority.Crit), 1e-6f, "적이 많으면 치명 정지를 줄인다");
            Assert.AreEqual(0.08f, g.HitStop(0.08f, FxPriority.Crit, true), 1e-6f, "비기·보스는 줄이지 않는다");
        }

        [Test]
        public void Governor_CrowdReducesDecor()
        {
            var g = new FxGovernor { CrowdFrom = 20, CrowdTo = 70, CrowdDecorMin = 0.25f };
            g.BeginFrame(10);
            Assert.AreEqual(1f, g.DecorScale, 1e-6f);
            g.BeginFrame(45);
            Assert.AreEqual(0.625f, g.DecorScale, 1e-4f);
            g.BeginFrame(200);
            Assert.AreEqual(0.25f, g.DecorScale, 1e-6f);
            Assert.AreEqual(1f, g.CountScale(FxPriority.Crit), 1e-6f);
            Assert.AreEqual(1f, g.CountScale(FxPriority.PlayerHurt), 1e-6f);
            Assert.Less(g.CountScale(FxPriority.NormalHit), 1f);
            Assert.Greater(g.CountScale(FxPriority.NormalHit), g.CountScale(FxPriority.Decor));
        }

        [Test]
        public void Governor_ReadsJuiceJson()
        {
            var j = new EffectsTuning(TestContent.Db.SoloExtra(JuiceLayer.JuiceName));
            var g = FxGovernor.FromJuice(j);
            Assert.AreEqual(140, g.FrameParticles);
            Assert.AreEqual(6, g.FrameNumbers);
            Assert.AreEqual(0.45f, g.Share[(int)FxPriority.Decor], 1e-6f);
            Assert.AreEqual(0.16f, g.HitStopMax, 1e-6f);
            for (int i = 1; i < g.Share.Length; i++) Assert.LessOrEqual(g.Share[i], g.Share[i - 1], "순위가 낮을수록 몫의 끝이 작다");
        }

        // ------------------------------------------------------------ 큰 고리 합치기

        [Test]
        public void BigRings_OverlappingBigEventsMergeIntoOneBigger()
        {
            var m = new BigRingMerger { MaxLive = 2, MergeWindow = 0.25f, MergeGrow = 1.2f, MaxRadius = 14f };
            bool merged;
            // 보스 처치: 고리 셋(9, 6.75, 4.5)
            int a = m.Request(new Vector2(10, 7), 0.3f, 9f, 0.8f, Color.white, 0f, out merged);
            Assert.IsFalse(merged);
            m.Request(new Vector2(10, 7), 0.3f, 6.75f, 0.8f, Color.white, 0.1f, out merged);
            Assert.IsTrue(merged); Assert.IsFalse(m.LastGrew, "작은 메아리는 흡수만");
            m.Request(new Vector2(10, 7), 0.3f, 4.5f, 0.8f, Color.white, 0.2f, out merged);
            Assert.AreEqual(1, m.Count);
            // 같은 때 웨이브 클리어(10) → 하나로, 더 크게
            int b = m.Request(new Vector2(12, 7), 0.4f, 10f, 0.85f, Color.white, 0.22f, out merged);
            Assert.IsTrue(merged); Assert.IsTrue(m.LastGrew);
            Assert.AreEqual(a, b);
            Assert.AreEqual(1, m.Count);
            BigRingMerger.Ring r;
            Assert.IsTrue(m.TryGet(a, out r));
            Assert.AreEqual(12f, r.ToR, 1e-4f);
        }

        [Test]
        public void BigRings_AtMostTwoLive()
        {
            var m = new BigRingMerger { MaxLive = 2, MergeWindow = 0.1f };
            bool merged;
            m.Request(Vector2.zero, 0f, 5f, 2f, Color.white, 0f, out merged);
            m.Request(Vector2.zero, 0f, 6f, 2f, Color.white, 0.5f, out merged);
            m.Request(Vector2.zero, 0f, 7f, 2f, Color.white, 1.0f, out merged);
            Assert.IsTrue(merged, "꽉 차면 합친다");
            Assert.AreEqual(2, m.Count);
            m.Prune(5f);
            Assert.AreEqual(0, m.Count);
        }

        // ------------------------------------------------------------ 가림 상한 보강

        [Test]
        public void Occlusion_EnemyShotNeighbourhoodCapsMyFx()
        {
            var f = OcclusionField.FromTuning(ViewEffectsTestData.Shared.Tuning);
            f.EnemyShotCap = 0.5f;
            f.EnemyShotPad = 0.35f;
            Assert.AreEqual(0.8f, f.CapAt(new Vector2(5f, 5f)), 1e-6f);
            f.AddEnemyShot(new Vector2(5f, 5f), 0.15f);
            Assert.AreEqual(0.5f, f.CapAt(new Vector2(5f, 5f)), 1e-6f, "적 탄 자리");
            Assert.AreEqual(0.8f, f.CapAt(new Vector2(7f, 5f)), 1e-6f, "멀면 그대로");
            f.AddMask(new OcclusionField.MaskShape { Shape = ZoneShape.Circle, Pos = new Vector2(5, 5), Radius = 1f });
            Assert.AreEqual(0.4f, f.CapAt(new Vector2(5f, 5f)), 1e-6f, "예고 안이 더 낮다");
        }

        [Test]
        public void Occlusion_BigEffectDimsWhenTouchingTelegraph()
        {
            var f = OcclusionField.FromTuning(ViewEffectsTestData.Shared.Tuning);
            f.AddMask(new OcclusionField.MaskShape { Shape = ZoneShape.Circle, Pos = new Vector2(8f, 5f), Radius = 0.6f });
            Assert.AreEqual(0.8f, f.CapAt(new Vector2(5f, 5f)), 1e-6f, "가운데는 밖");
            Assert.AreEqual(0.4f, f.CapAtDisc(new Vector2(5f, 5f), 3f), 1e-6f, "큰 폭발이 예고에 걸치면 옅어진다");
            Assert.AreEqual(0.4f, f.Admit(new Vector2(5f, 5f), 3f, 1f), 1e-6f);
        }

        // ------------------------------------------------------------ 위험색

        [Test]
        public void Danger_GuardFlagsDangerColorsOnly()
        {
            var g = new PlayerColorGuard();
            LogAssertNoThrow(() => Assert.IsFalse(g.Check(new Color32(0xFF, 0x3B, 0x30, 0xFF), "test.red")));
            Assert.IsTrue(g.Check(new Color32(0xFF, 0x8A, 0x1F, 0xFF), "test.fire"), "불 속성 주황(28도)은 안전");
            Assert.IsTrue(g.Check(new Color32(0x8A, 0x5C, 0xFF, 0xFF), "test.dark"));
            Assert.AreEqual(1, g.Warnings);
            Assert.IsFalse(PlayerColorGuard.IsDanger(JuiceLayer.Safe(new Color32(0xFF, 0x2E, 0x88, 0xFF))), "Safe가 고친 색은 위험색이 아니다");
        }

        static void LogAssertNoThrow(System.Action a)
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            try { a(); } finally { UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false; }
        }

        [Test]
        public void Danger_PlayerElementAndJuiceColorsAreSafe()
        {
            var t = ViewEffectsTestData.Shared.Tuning;
            string[] els = { Ids.Element.Neutral, Ids.Element.Fire, Ids.Element.Lightning, Ids.Element.Arcane, Ids.Element.Ice, Ids.Element.Dark };
            foreach (var el in els)
            {
                Assert.IsFalse(PlayerColorGuard.IsDanger(t.ElementPrimary(el)), "element_primary " + el);
                Assert.IsFalse(PlayerColorGuard.IsDanger(t.ElementSecondary(el)), "element_secondary " + el);
            }
            var j = new EffectsTuning(TestContent.Db.SoloExtra(JuiceLayer.JuiceName));
            string[][] keys =
            {
                new[] { "streak", "milestone_color" }, new[] { "graze", "color" }, new[] { "drops", "gold_color" }, new[] { "drops", "health_color" },
                new[] { "drops", "material_color" }, new[] { "wave_start", "color" }, new[] { "wave_clear", "color" }, new[] { "damage_pop", "big_color" },
                new[] { "offscreen", "spawn_color" }, new[] { "offscreen", "spawn_leaf_color" }, new[] { "offscreen", "enemy_color" }
            };
            foreach (var k in keys)
                Assert.IsFalse(PlayerColorGuard.IsDanger(EffectsTuning.ParseColor(j.S(k), Color.white)), string.Join(".", k));
        }

        /// <summary>내 탄·발사·적중·처치 그림(플레이어 효과 그림 전부)에 위험색 점이 없다(불투명 점 기준).</summary>
        [Test]
        public void Danger_PlayerEffectArtHasNoDangerPixels()
        {
            string art = Path.Combine(TestContent.AssetsDir, "Resources", "Art");
            var files = new List<string>();
            files.AddRange(Directory.GetFiles(Path.Combine(art, "effects", "player"), "*.png", SearchOption.AllDirectories));
            files.AddRange(Directory.GetFiles(Path.Combine(art, "effects", "wand"), "*.png", SearchOption.AllDirectories));
            files.AddRange(Directory.GetFiles(Path.Combine(art, "common", "Combat"), "*.png", SearchOption.AllDirectories));
            Assert.Greater(files.Count, 10);
            var bad = new List<string>();
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                foreach (var f in files)
                {
                    if (!tex.LoadImage(File.ReadAllBytes(f))) continue;
                    var px = tex.GetPixels32();
                    int n = 0, d = 0;
                    for (int i = 0; i < px.Length; i++)
                    {
                        if (px[i].a < 128) continue;
                        n++;
                        if (DangerColor.IsDanger(px[i])) d++;
                    }
                    if (d > 0) bad.Add(Path.GetFileName(f) + " " + d + "/" + n);
                }
            }
            finally { Object.DestroyImmediate(tex); }
            Assert.IsEmpty(bad, string.Join("\n", bad));
        }

        // ------------------------------------------------------------ 설정 옵션

        [Test]
        public void Options_NewRowsDefaultsAndNames()
        {
            var s = new SettingsData();
            Assert.AreEqual(85, SettingsOptions.Int(s, OptionKeys.MyFxOpacity));
            Assert.AreEqual(OptionValues.Small, SettingsOptions.Str(s, OptionKeys.DamageNumberSize));
            Assert.IsFalse(SettingsOptions.Bool(s, OptionKeys.ReduceFlash));
            Assert.AreEqual(100, SettingsOptions.Int(s, OptionKeys.ShakeScale), "화면 흔들림 강도 0~100");
            Assert.AreEqual(0, SettingsOptions.Spec(OptionKeys.ShakeScale).Min);
            Assert.AreEqual(0, SettingsOptions.Spec(OptionKeys.MyFxOpacity).Min);
            Assert.AreEqual(100, SettingsOptions.Spec(OptionKeys.MyFxOpacity).Max);
            CollectionAssert.AreEqual(new[] { OptionValues.Large, OptionValues.Small, OptionValues.Off }, SettingsOptions.Spec(OptionKeys.DamageNumberSize).Choices);
            foreach (var k in new[] { OptionKeys.MyFxOpacity, OptionKeys.DamageNumberSize, OptionKeys.ReduceFlash })
            {
                Assert.IsTrue(TestContent.Names.Has("str.settings.opt." + k), k);
                Assert.IsTrue(TestContent.Names.Has("str.settings.desc." + k), k);
            }
            Assert.IsTrue(TestContent.Names.Has("str.settings.value.large"));
            Assert.IsTrue(TestContent.Names.Has("str.settings.value.small"));

            var o = new ViewOptions();
            SettingsOptions.Set(s, OptionKeys.MyFxOpacity, 40);
            SettingsOptions.Set(s, OptionKeys.DamageNumberSize, OptionValues.Off);
            SettingsOptions.Set(s, OptionKeys.ReduceFlash, true);
            o.ReadFrom(s);
            Assert.AreEqual(0.4f, o.MyFxOpacity, 1e-6f);
            Assert.AreEqual(OptionValues.Off, o.DamageNumberSize);
            Assert.IsTrue(o.ReduceFlash);
            Assert.LessOrEqual(o.FlashScale, ViewOptions.ReduceFlashCap, "섬광 줄이기는 번쩍 배율을 누른다");
        }

        [Test]
        public void Options_SizeScaleAndMyFxAlpha()
        {
            Assert.AreEqual(0f, ReadabilityOptions.DamageNumberSizeScale(OptionValues.Off, 0.7f, 1.1f));
            Assert.AreEqual(0.7f, ReadabilityOptions.DamageNumberSizeScale(OptionValues.Small, 0.7f, 1.1f), 1e-6f);
            Assert.AreEqual(1.1f, ReadabilityOptions.DamageNumberSizeScale(OptionValues.Large, 0.7f, 1.1f), 1e-6f);
            Assert.AreEqual(0.425f, ReadabilityOptions.MyFxAlpha(0.5f, 0.85f), 1e-6f);
            Assert.AreEqual(0f, ReadabilityOptions.MyFxAlpha(0.5f, 0f));
        }
    }

    /// <summary>층을 실제 그림 창고로 세워 본다: 고리 메시, 피해 숫자 크기, 화면 밖 등장 예고.</summary>
    public class ReadabilityLayerTests
    {
        GameObject _root, _textRoot;
        SpritePool _pool;
        ArtLibrary _art;
        ViewContext _vc;
        SimWorld _w;
        EffectsShared _shared;
        ShotLayer _shots;
        EffectLayer _fx;
        JuiceLayer _juice;
        readonly List<float> _hitStops = new List<float>();

        [SetUp]
        public void SetUp()
        {
            _hitStops.Clear();
            _root = new GameObject("ReadabilityWorld");
            _textRoot = new GameObject("ReadabilityText", typeof(RectTransform));
            _art = new ArtLibrary(new DiskArtSource());
            _pool = new SpritePool(_art, _root.transform);
            var ctx = new GameContext { Art = _art, Names = TestContent.Names, Content = TestContent.Db, Sound = new RecordingSoundPlayer() };
            _w = new SimWorld(TestContent.Db, SimModuleSet.Stubs(), 1);
            var p = _w.Players[0];
            p.Active = true;
            p.CharId = Ids.Char.C1;
            p.Def = TestContent.Db.Character(Ids.Char.C1);
            p.Pos = p.PrevPos = new Vector2(12f, 7f);
            p.Facing = Dir8.E;
            _w.Run.Phase = RunPhase.WaveCombat;
            _w.Wave.Wave = 1;
            _vc = new ViewContext
            {
                Game = ctx, World = _w, WorldRoot = _root.transform, Pool = _pool, WorldTextRoot = (RectTransform)_textRoot.transform,
                HudRoot = (RectTransform)_textRoot.transform, Options = new ViewOptions(),
                Shake = (px, s) => { }, HitStop = s => _hitStops.Add(s)
            };
            _shared = new EffectsShared(ViewEffectsTestData.Fresh());
            _shots = new ShotLayer(_shared);
            _fx = new EffectLayer(_shared);
            _juice = new JuiceLayer(_shared, _fx);
            _shots.Build(_vc, _w);
            _fx.Build(_vc, _w);
            _juice.Build(_vc, _w);
        }

        [TearDown]
        public void TearDown()
        {
            _juice.Dispose();
            _shots.Dispose();
            _fx.Dispose();
            _pool.DestroyAll();
            Object.DestroyImmediate(_textRoot);
            Object.DestroyImmediate(_root);
        }

        void Event(SimEvent e)
        {
            e.Tick = _w.Tick;
            _shots.OnEvent(_w, e);
            _fx.OnEvent(_w, e);
            _juice.OnEvent(_w, e);
        }

        void Frame(float dt = 1f / 60f)
        {
            _shots.Render(_w, 1f, dt);
            _fx.Render(_w, 1f, dt);
            _juice.Render(_w, 1f, dt);
        }

        [Test]
        public void BossKillPlusWaveClear_OneBigThinRing()
        {
            Frame();
            Event(new SimEvent { Type = SimEventType.BossKilled, Pos = new Vector2(12f, 6f) });
            Event(new SimEvent { Type = SimEventType.WaveCleared });
            Frame();
            Assert.AreEqual(1, _shared.Rings.BigLiveCount, "보스 처치 + 웨이브 클리어 = 큰 고리 하나");
            Assert.GreaterOrEqual(_shared.Rings.Merged, 1);
            Assert.Greater(_shared.Rings.BigMesh.TriangleCount, 0);
            // 두께가 반경과 무관: 정점 반지름의 폭(가장 바깥 − 가장 안 선)이 몇 그림 px 안
            Frame(0.3f);
            Assert.LessOrEqual(_shared.Rings.BigLiveCount, 2);
        }

        [Test]
        public void RingDimsOverTelegraph_VertexAlphaFollowsCap()
        {
            _shared.Occlusion.AddMask(new OcclusionField.MaskShape { Shape = ZoneShape.Circle, Pos = new Vector2(15f, 7f), Radius = 0.8f, Flashing = true });
            _shared.Rings.Add(new Vector2(12f, 7f), 3f, 3f, 1f, Color.white);
            _shared.Rings.Update(0.016f);
            var mesh = _shared.Rings.BigMesh;
            Assert.Greater(mesh.VertexCount, 0);
            byte maxNear = 0, maxFar = 0;
            Vector3 hot = _vc.ToView(new Vector2(15f, 7f)), far = _vc.ToView(new Vector2(9f, 7f));
            for (int i = 0; i < mesh.VertexCount; i++)
            {
                var v = mesh.Vertices[i];
                if ((new Vector2(v.x, v.y) - new Vector2(hot.x, hot.y)).magnitude < 0.3f) maxNear = (byte)Mathf.Max(maxNear, mesh.Colors[i].a);
                if ((new Vector2(v.x, v.y) - new Vector2(far.x, far.y)).magnitude < 0.3f) maxFar = (byte)Mathf.Max(maxFar, mesh.Colors[i].a);
            }
            Assert.AreEqual(0, maxNear, "번쩍이는 예고 위에서는 0%");
            Assert.Greater(maxFar, 100, "밖은 보인다");
            Assert.LessOrEqual(maxFar, Mathf.CeilToInt(255 * 0.8f) + 1, "내 이펙트 85%를 곱한 뒤 누적 상한 80%");
        }

        EnemyState AddEnemy(int id, Vector2 pos)
        {
            var e = new EnemyState { Id = id, DefId = Ids.Enemy.SlimeMoss, Pos = pos, PrevPos = pos, Hp = 100f, MaxHp = 100f };
            _w.Enemies.Add(e);
            return e;
        }

        [Test]
        public void DamageNumbers_SmallByDefault_CritBigger_LargeOption_Off()
        {
            AddEnemy(301, new Vector2(10f, 7f));
            AddEnemy(302, new Vector2(14f, 7f));
            Frame();
            Event(new SimEvent { Type = SimEventType.EnemyDamaged, EntityId = 301, A = 12, B = 0, Pos = new Vector2(10f, 7f), Id = Ids.Element.Fire, Slot = 0 });
            Event(new SimEvent { Type = SimEventType.EnemyDamaged, EntityId = 302, A = 12, B = DamageFlags.Crit, Pos = new Vector2(14f, 7f), Id = Ids.Element.Fire, Slot = 0 });
            Frame(0.3f); // 튀는 처음 배율이 가라앉은 뒤
            Assert.AreEqual(2, _fx.DamageNumberCount);
            float normal = _fx.DamageNumberScaleAt(0), crit = _fx.DamageNumberScaleAt(1);
            Assert.Less(normal, 0.9f, "기본(작게)은 그림 크기보다 작다");
            Assert.Greater(crit, normal * 1.3f, "치명은 튄다");

            _vc.Options.DamageNumberSize = OptionValues.Large;
            Event(new SimEvent { Type = SimEventType.EnemyDamaged, EntityId = 301, A = 5, B = 0, Pos = new Vector2(10f, 7f), Id = Ids.Element.Fire, Slot = 0 });
            Frame(0.3f);
            Assert.Greater(_fx.DamageNumberScaleAt(0), normal * 1.2f, "크게");
            Assert.AreEqual("17", _fx.DamageNumberGlyphs(0), "같은 적의 연속 피해는 합쳐 한 숫자");

            _vc.Options.DamageNumberSize = OptionValues.Off;
            int before = _fx.DamageNumberCount;
            AddEnemy(303, new Vector2(12f, 9f));
            Event(new SimEvent { Type = SimEventType.EnemyDamaged, EntityId = 303, A = 50, B = DamageFlags.Crit, Pos = new Vector2(12f, 9f), Id = Ids.Element.Fire, Slot = 0 });
            Assert.AreEqual(before, _fx.DamageNumberCount, "끄기");
        }

        [Test]
        public void DamageNumbers_NearbyNewNumbersStackUp()
        {
            AddEnemy(311, new Vector2(10f, 7f));
            AddEnemy(312, new Vector2(10.1f, 7f));
            Frame();
            Event(new SimEvent { Type = SimEventType.EnemyDamaged, EntityId = 311, A = 3, Pos = new Vector2(10f, 7f), Id = Ids.Element.Fire });
            Event(new SimEvent { Type = SimEventType.EnemyDamaged, EntityId = 312, A = 3, Pos = new Vector2(10.1f, 7f), Id = Ids.Element.Fire });
            Assert.AreEqual(2, _fx.DamageNumberCount);
            Assert.Less(_fx.DamageNumberPosAt(1).y, _fx.DamageNumberPosAt(0).y - 0.2f, "겹치는 둘째 숫자는 위로 올라간다");
        }

        [Test]
        public void ManyNormalHitsInOneFrame_NumbersCapped()
        {
            Frame();
            for (int i = 0; i < 30; i++)
            {
                var pos = new Vector2(2f + i * 0.7f, 4f + (i % 3) * 2f);
                AddEnemy(400 + i, pos);
                Event(new SimEvent { Type = SimEventType.EnemyDamaged, EntityId = 400 + i, A = 2, Pos = pos, Id = Ids.Element.Fire });
            }
            Assert.LessOrEqual(_fx.DamageNumberCount, _shared.Governor.FrameNumbers, "프레임당 새 일반 숫자 상한");
            Assert.Greater(_fx.DamageNumberCount, 0);
        }

        [Test]
        public void MyFxOpacityZero_HidesMyRings_NotThreats()
        {
            _vc.Options.MyFxOpacity = 0f;
            _shared.Rings.Add(new Vector2(12f, 7f), 0.2f, 1.2f, 0.5f, Color.white);
            _shared.Rings.Update(0.016f);
            Assert.AreEqual(0, _shared.Rings.SmallMesh.TriangleCount, "내 이펙트 0% = 고리 없음");
        }
    }
}

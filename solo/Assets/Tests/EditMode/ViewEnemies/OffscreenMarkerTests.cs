using System.IO;
using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.App;
using Wizard.Solo.Art;
using Wizard.Solo.Sim;
using Wizard.Solo.Tests.Art;
using Wizard.Solo.Tests.ViewEffects;
using Wizard.Solo.View;
using Wizard.Solo.View.Effects;
using Wizard.Solo.View.Enemies;

namespace Wizard.Solo.Tests.ViewEnemies
{
    /// <summary>
    /// 화면 밖 표식: 카메라를 좁혀 적·예고·곧 나올 적(EnemySpawnNotice)을 화면 밖에 두고, 화살과 등장 예고(화살 + 흔들리는 잎)가 생기는지,
    /// 전장의 모든 것보다 위 띠인지 본다. 실제 그림 창고·풀로 카메라를 렌더 텍스처에 그려 Logs/shots/readability/에 남긴다(눈으로 확인용).
    /// </summary>
    public class OffscreenMarkerTests
    {
        GameObject _root, _textRoot, _camGo;
        SpritePool _pool;
        ViewContext _vc;
        SimWorld _w;
        OffscreenMarkerLayer _layer;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("OffscreenWorld");
            _textRoot = new GameObject("OffscreenText", typeof(RectTransform));
            _camGo = new GameObject("OffscreenCam");
            var cam = _camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 2.5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.2f, 0.12f, 1f);
            cam.transform.position = new Vector3(12f, 7f, -10f);
            var art = new ArtLibrary(new DiskArtSource());
            _pool = new SpritePool(art, _root.transform);
            var ctx = new GameContext { Art = art, Names = TestContent.Names, Content = TestContent.Db, Sound = new RecordingSoundPlayer() };
            _w = new SimWorld(TestContent.Db, SimModuleSet.Stubs(), 1);
            _vc = new ViewContext
            {
                Game = ctx, World = _w, WorldRoot = _root.transform, Camera = cam, Pool = _pool, WorldTextRoot = (RectTransform)_textRoot.transform,
                HudRoot = (RectTransform)_textRoot.transform, Options = new ViewOptions()
            };
            _layer = new OffscreenMarkerLayer();
            _layer.Build(_vc, _w);
        }

        [TearDown]
        public void TearDown()
        {
            _layer.Dispose();
            _pool.DestroyAll();
            Object.DestroyImmediate(_camGo);
            Object.DestroyImmediate(_textRoot);
            Object.DestroyImmediate(_root);
        }

        [Test]
        public void OffscreenEnemiesTelegraphsAndIncomingSpawnsGetEdgeMarks()
        {
            _w.Enemies.Add(new EnemyState { Id = 1, DefId = Ids.Enemy.SlimeMoss, Pos = new Vector2(22f, 7f), Hp = 10f, MaxHp = 10f, Ai = EnemyAiState.Act });
            _w.Enemies.Add(new EnemyState { Id = 2, DefId = Ids.Enemy.SlimeMoss, Pos = new Vector2(12.2f, 7f), Hp = 10f, MaxHp = 10f, Ai = EnemyAiState.Act }); // 화면 안
            _w.Zones.Add(new ZoneEntity { Id = 7, Shape = ZoneShape.Circle, Pos = new Vector2(2f, 7f), Radius = 1f, StartTick = 0, DetonateTick = 60 });
            _layer.OnEvent(_w, new SimEvent { Type = SimEventType.EnemySpawnNotice, Tick = 0, A = 30, Pos = new Vector2(12f, 0.5f), Id = Ids.Enemy.SlimeMoss });
            _layer.OnEvent(_w, new SimEvent { Type = SimEventType.EnemySpawnNotice, Tick = 0, A = 30, Pos = new Vector2(12.5f, 7.5f), Id = Ids.Enemy.SlimeMoss }); // 화면 안 → 덤불(ArenaLayer)
            _layer.Render(_w, 1f, 0.016f);
            Assert.AreEqual(2, _layer.VisibleCount, "화면 밖 적 하나 + 화면 밖 예고 하나(화면 안 적은 없음)");
            Assert.AreEqual(1, _layer.SpawnWarningsVisible, "화면 밖에서 곧 나올 적 하나");
            int above = 0;
            foreach (var sr in _root.GetComponentsInChildren<SpriteRenderer>())
                if (sr.enabled && sr.sortingOrder >= FxLayers.Offscreen) above++;
            Assert.GreaterOrEqual(above, 2 + 1 + 3, "표식은 전장의 모든 것 위 띠");
            Assert.Greater(FxLayers.Offscreen, FxLayers.TelegraphEdge);

            Capture(_vc.Camera, "offscreen-markers-editmode");

            _w.Tick = 31;
            _layer.Render(_w, 1f, 0.016f);
            Assert.AreEqual(0, _layer.SpawnWarningsVisible, "예고 시간이 지나면 사라진다");
            Assert.AreEqual(0, _layer.PendingSpawnNotices);
        }

        static void Capture(Camera cam, string name)
        {
            var rt = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32);
            var prev = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            var old = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(960, 540, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
            tex.Apply();
            RenderTexture.active = old;
            cam.targetTexture = prev;
            string dir = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "Logs", "shots", "readability");
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);
        }
    }
}

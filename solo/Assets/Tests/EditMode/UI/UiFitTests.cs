using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.UI;

namespace Wizard.Solo.Tests.UI
{
    /// <summary>반응형 맞춤 계산(UiFit)과 바탕 넓히기(UiFullBleed — 월드 공간 캔버스로 창 크기를 흉내 낸다, Update는 돌지 않아 Refit을 직접 부른다).</summary>
    public class UiFitTests
    {
        const float Eps = 0.01f;
        const float Aspect169 = 1920f / 1080f;

        // ---------------------------------------------------------------- UiFit (순수)

        [Test]
        public void CanvasSize_DividesScreenByScale()
        {
            Assert.AreEqual(new Vector2(2560f, 1440f), UiFit.CanvasSize(2560, 1440, 1f));
            Assert.AreEqual(new Vector2(1920f, 1080f), UiFit.CanvasSize(3840, 2160, 2f));
            Assert.AreEqual(new Vector2(800f, 600f), UiFit.CanvasSize(800, 600, 0f)); // 배율 0 → 1
        }

        [Test]
        public void SafeMargin_PerAspect()
        {
            // 21:9 (2560×1080, 배율 1): 좌우 320씩.
            Assert.AreEqual(new Vector2(320f, 0f), UiFit.SafeMargin(UiFit.CanvasSize(2560, 1080, UiScale.Compute(2560, 1080))));
            // 16:10 (1920×1200): 위아래 60씩.
            Assert.AreEqual(new Vector2(0f, 60f), UiFit.SafeMargin(UiFit.CanvasSize(1920, 1200, UiScale.Compute(1920, 1200))));
            // 4:3 작은 창 (1024×768, 배율 0.533): 캔버스 1920×1440 → 위아래 180씩.
            var m = UiFit.SafeMargin(UiFit.CanvasSize(1024, 768, UiScale.Compute(1024, 768)));
            Assert.AreEqual(0f, m.x, Eps);
            Assert.AreEqual(180f, m.y, Eps);
            // 16:9 기준은 여백 없음.
            Assert.AreEqual(Vector2.zero, UiFit.SafeMargin(UiFit.CanvasSize(1920, 1080, 1f)));
        }

        [Test]
        public void Bleed_ExtendsOnlyChosenEdges_AndNeverShrinks()
        {
            var authored = new Rect(-960f, 460f, 1920f, 80f); // 위 띠(y 위 +)
            var canvas = Rect.MinMaxRect(-1280f, -540f, 1280f, 540f);
            var r = UiFit.Bleed(authored, canvas, UiBleedEdges.Horizontal);
            Assert.AreEqual(-1280f, r.xMin, Eps);
            Assert.AreEqual(1280f, r.xMax, Eps);
            Assert.AreEqual(460f, r.yMin, Eps);
            Assert.AreEqual(540f, r.yMax, Eps);

            // 캔버스가 더 작으면(줄어드는 쪽) 만든 자리 그대로.
            var small = Rect.MinMaxRect(-100f, 470f, 100f, 530f); // 만든 자리 안쪽
            Assert.AreEqual(authored, UiFit.Bleed(authored, small, UiBleedEdges.All));
        }

        [Test]
        public void Cover_FillsBoxAndCropsCentered()
        {
            // 21:9 상자를 16:9 그림으로 덮으면 높이가 넘친다(위아래 잘림).
            var box = new Rect(-1280f, -540f, 2560f, 1080f);
            var c = UiFit.Cover(box, Aspect169);
            Assert.AreEqual(2560f, c.width, Eps);
            Assert.AreEqual(1440f, c.height, Eps);
            Assert.AreEqual(box.center.x, c.center.x, Eps);
            Assert.AreEqual(box.center.y, c.center.y, Eps);
            Assert.GreaterOrEqual(c.width, box.width);
            Assert.GreaterOrEqual(c.height, box.height);

            // 4:3 상자: 너비가 넘친다(좌우 잘림).
            var s = UiFit.CoverSize(new Vector2(1440f, 1080f), Aspect169);
            Assert.AreEqual(1920f, s.x, Eps);
            Assert.AreEqual(1080f, s.y, Eps);

            // 비율 없음 → 상자 그대로.
            Assert.AreEqual(new Vector2(10f, 20f), UiFit.CoverSize(new Vector2(10f, 20f), 0f));
        }

        [Test]
        public void Contain_FitsInsideBox()
        {
            var s = UiFit.ContainSize(new Vector2(2560f, 1080f), Aspect169);
            Assert.AreEqual(1920f, s.x, Eps);
            Assert.AreEqual(1080f, s.y, Eps);
            var t = UiFit.ContainSize(new Vector2(1920f, 1440f), Aspect169);
            Assert.AreEqual(1920f, t.x, Eps);
            Assert.AreEqual(1080f, t.y, Eps);
            var r = UiFit.Contain(new Rect(0f, 0f, 2560f, 1440f), Aspect169);
            Assert.AreEqual(new Vector2(1280f, 720f), r.center);
        }

        [Test]
        public void Pin_MovesToEdgeKeepingSize()
        {
            var canvas = Rect.MinMaxRect(-1280f, -720f, 1280f, 720f);
            var band = new Rect(-960f, 536f, 1920f, 4f); // 위 테두리 4px
            var top = UiFit.Pin(band, canvas, UiBleedEdges.Top);
            Assert.AreEqual(720f, top.yMax, Eps);
            Assert.AreEqual(4f, top.height, Eps);
            Assert.AreEqual(band.x, top.x, Eps);
            var right = UiFit.Pin(new Rect(956f, -540f, 4f, 1080f), canvas, UiBleedEdges.Right);
            Assert.AreEqual(1280f, right.xMax, Eps);
            Assert.AreEqual(4f, right.width, Eps);
            // 마주 보는 두 쪽 → 그 축은 그대로.
            Assert.AreEqual(band, UiFit.Pin(band, canvas, UiBleedEdges.Vertical));
        }

        [Test]
        public void CoverScaleAbout_CenterMatchesCover_FocalBelowCropsLessAtBottom()
        {
            var authored = new Rect(-960f, -540f, 1920f, 1080f);
            var wide = Rect.MinMaxRect(-1280f, -540f, 1280f, 540f);
            Assert.AreEqual(1280f / 960f, UiFit.CoverScaleAbout(authored, wide, Vector2.zero), Eps);
            // 이미 덮으면 1.
            Assert.AreEqual(1f, UiFit.CoverScaleAbout(authored, authored, Vector2.zero), Eps);
            // 16:10(위아래 60) + 축이 아래쪽(위에서 780px = y −240): 아래 300px을 360으로 → 1.2배.
            var tall = Rect.MinMaxRect(-960f, -600f, 960f, 600f);
            var f = new Vector2(0f, -240f);
            float s = UiFit.CoverScaleAbout(authored, tall, f);
            Assert.AreEqual(1.2f, s, Eps);
            // 키운 뒤 네 변이 모두 캔버스를 덮는다.
            Assert.LessOrEqual(f.x + s * (authored.xMin - f.x), tall.xMin + Eps);
            Assert.GreaterOrEqual(f.x + s * (authored.xMax - f.x), tall.xMax - Eps);
            Assert.LessOrEqual(f.y + s * (authored.yMin - f.y), tall.yMin + Eps);
            Assert.GreaterOrEqual(f.y + s * (authored.yMax - f.y), tall.yMax - Eps);
        }

        [Test]
        public void MinZoomToCover_ArenaFraming()
        {
            // 1080p, 1칸 96px: 보이는 20×11.25칸 < 전장 24×14 → 1 미만(이미 덮는다).
            Assert.Less(UiFit.MinZoomToCover(1920f, 1080f, 96f, 24f, 14f), 1f);
            // 보스 줌 0.8이면 25칸 > 24 → 최소 0.833배는 돼야 덮는다.
            Assert.AreEqual(1920f / (24f * 96f), UiFit.MinZoomToCover(1920f, 1080f, 96f, 24f, 14f), Eps);
            // 21:9(2560×1080): 26.7칸 → 1.11배로 당겨야 덮는다.
            Assert.AreEqual(2560f / (24f * 96f), UiFit.MinZoomToCover(2560f, 1080f, 96f, 24f, 14f), Eps);
            Assert.AreEqual(0f, UiFit.MinZoomToCover(0f, 1080f, 96f, 24f, 14f));
        }

        // ---------------------------------------------------------------- UiFullBleed (월드 공간 캔버스)

        GameObject _canvasGo;

        [TearDown]
        public void TearDown()
        {
            if (_canvasGo != null) Object.DestroyImmediate(_canvasGo);
        }

        /// <summary>창 w×h(캔버스 단위)를 흉내 낸 캔버스와 그 가운데 1920×1080 안전 영역 루트.</summary>
        RectTransform SafeRoot(float w, float h)
        {
            _canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            _canvasGo.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var crt = (RectTransform)_canvasGo.transform;
            crt.sizeDelta = new Vector2(w, h);
            var root = new GameObject("Root", typeof(RectTransform));
            var rrt = (RectTransform)root.transform;
            rrt.SetParent(crt, false);
            rrt.anchorMin = rrt.anchorMax = rrt.pivot = new Vector2(0.5f, 0.5f);
            rrt.sizeDelta = new Vector2(UiScale.RefWidth, UiScale.RefHeight);
            return rrt;
        }

        static void SetCanvas(RectTransform root, float w, float h) => ((RectTransform)root.parent).sizeDelta = new Vector2(w, h);

        [Test]
        public void FullBleed_FillCoversWindow_AndRefitsOnResize()
        {
            var root = SafeRoot(2560f, 1080f);
            var bg = UiKit.Node(root, "Bg", 0f, 0f, UiScale.RefWidth, UiScale.RefHeight);
            var b = UiFullBleed.Fill(bg);
            Assert.AreEqual(2560f, bg.rect.width, Eps);
            Assert.AreEqual(1080f, bg.rect.height, Eps);
            Assert.AreEqual(0f, bg.localPosition.x, Eps);

            SetCanvas(root, 1920f, 1440f); // 4:3로 바뀜
            b.Refit();
            Assert.AreEqual(1920f, bg.rect.width, Eps);
            Assert.AreEqual(1440f, bg.rect.height, Eps);
        }

        [Test]
        public void FullBleed_TopBandStretchesSidewaysAndUp_ContentUntouched()
        {
            var root = SafeRoot(2560f, 1440f);
            var band = UiKit.Node(root, "TopBarBg", 0f, 0f, UiScale.RefWidth, 80f);
            var content = UiKit.Node(root, "TopBar", 0f, 0f, UiScale.RefWidth, 80f);
            UiFullBleed.Fill(band, UiBleedEdges.Horizontal | UiBleedEdges.Top);
            // 판: 좌우 2560, 위는 캔버스 끝(720)까지, 아래 변은 안전 영역 그대로(540 − 80 = 460).
            Vector2 lp = band.localPosition;
            Assert.AreEqual(2560f, band.rect.width, Eps);
            Assert.AreEqual(720f, lp.y + band.rect.yMax, Eps);
            Assert.AreEqual(460f, lp.y + band.rect.yMin, Eps);
            // 글·단추 자리는 안전 영역 그대로.
            Assert.AreEqual(1920f, content.rect.width, Eps);
        }

        [Test]
        public void FullBleed_CoverImageKeepsAspect()
        {
            var root = SafeRoot(2560f, 1080f);
            var img = UiKit.Node(root, "Art", 0f, 0f, UiScale.RefWidth, UiScale.RefHeight);
            UiFullBleed.Cover(img, Aspect169);
            Assert.AreEqual(2560f, img.rect.width, Eps);
            Assert.AreEqual(1440f, img.rect.height, Eps);
            Assert.AreEqual(Aspect169, img.rect.width / img.rect.height, Eps);
        }

        [Test]
        public void FullBleed_ContainKeepsWholeFrameVisible()
        {
            var root = SafeRoot(2560f, 1440f);
            var frame = UiKit.Node(root, "Video", 0f, 0f, UiScale.RefWidth, UiScale.RefHeight);
            UiFullBleed.Contain(frame, Aspect169);
            Assert.AreEqual(2560f, frame.rect.width, Eps);
            Assert.AreEqual(1440f, frame.rect.height, Eps);
        }

        [Test]
        public void FullBleed_PinnedEdgeStaysThin()
        {
            var root = SafeRoot(2560f, 1440f);
            var left = UiKit.Node(root, "EdgeLeft", 0f, 0f, 4f, UiScale.RefHeight);
            UiFullBleed.Fill(left, UiBleedEdges.Vertical, UiBleedEdges.Left);
            Vector2 lp = left.localPosition;
            Assert.AreEqual(4f, left.rect.width, Eps);
            Assert.AreEqual(1440f, left.rect.height, Eps);
            Assert.AreEqual(-1280f, lp.x + left.rect.xMin, Eps);
        }

        [Test]
        public void FullBleed_ScaleCoverScalesSceneUniformly()
        {
            var root = SafeRoot(2560f, 1080f);
            var scene = UiKit.Node(root, "Scene", 0f, 0f, UiScale.RefWidth, UiScale.RefHeight);
            UiFullBleed.ScaleCover(scene, new Vector2(0.5f, 0.5f));
            Assert.AreEqual(1920f, scene.rect.width, Eps); // 크기는 그대로, 배율로 키운다
            Assert.AreEqual(1280f / 960f, scene.localScale.x, Eps);
            Assert.AreEqual(scene.localScale.x, scene.localScale.y, Eps);
        }

        [Test]
        public void FullBleed_WithoutCanvasKeepsAuthoredRect()
        {
            var go = new GameObject("NoCanvas", typeof(RectTransform));
            try
            {
                var p = (RectTransform)go.transform;
                p.sizeDelta = new Vector2(1920f, 1080f);
                var bg = UiKit.Node(p, "Bg", 100f, 50f, 300f, 200f);
                UiFullBleed.Fill(bg);
                Assert.AreEqual(300f, bg.rect.width, Eps);
                Assert.AreEqual(200f, bg.rect.height, Eps);
                // 왼쪽 위 원점 (100, 50) → 부모 local 가운데 기준.
                Vector2 lp = bg.localPosition;
                Assert.AreEqual(-960f + 100f, lp.x + bg.rect.xMin, Eps);
                Assert.AreEqual(540f - 50f, lp.y + bg.rect.yMax, Eps);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}

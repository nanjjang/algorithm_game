#nullable enable
using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UguiText = UnityEngine.UI.Text;   // Wizard.Text 이름공간과 겹치지 않게
using Wizard.Unity.UI.Common;
using Wizard.ViewLogic.Common;
using Wizard.ViewLogic.Stack;

namespace Wizard.Unity.Tests.EditMode.UI.Common
{
    // 수락 1: 판, 버튼, 탭, 목록, 칸, 글자, 선택 괄호를 코드로 만든다(프리팹·장면 편집 없음).
    // 수락 4: 글자는 이름 표 열쇠로 받고, 이름 표가 없으면 열쇠 문자열을 그대로 찍는다.
    public sealed class UiWidgetTests
    {
        private UiCanvas _canvas = null!;
        private UiViewContext _plain = null!;

        [SetUp]
        public void SetUp()
        {
            _canvas = new UiCanvas();
            _plain = new UiViewContext(new UiTextSource(), new InputModeTracker(), _canvas.Stage);
        }

        [TearDown]
        public void TearDown() => _canvas.Dispose();

        [Test]
        public void Widget_IsPlacedTopLeftInWholePixels()
        {
            var w = new UiWidget("W", _canvas.Stage, new PixelRect(48, 96, 192, 48));
            Assert.That(w.Transform.anchorMin, Is.EqualTo(new Vector2(0f, 1f)));
            Assert.That(w.Transform.anchorMax, Is.EqualTo(new Vector2(0f, 1f)));
            Assert.That(w.Transform.pivot, Is.EqualTo(new Vector2(0f, 1f)));
            Assert.That(w.Transform.anchoredPosition, Is.EqualTo(new Vector2(48f, -96f)));
            Assert.That(w.Transform.sizeDelta, Is.EqualTo(new Vector2(192f, 48f)));
            Assert.That(w.Transform.parent, Is.SameAs(_canvas.Stage));
        }

        [Test]
        public void Widget_AbsoluteRect_AddsParentOffsets()
        {
            var outer = new UiWidget("Outer", _canvas.Stage, new PixelRect(100, 200, 800, 400));
            var inner = new UiWidget("Inner", outer, new PixelRect(16, 24, 64, 32));
            var leaf = new UiWidget("Leaf", inner, new PixelRect(8, 8, 8, 8));
            Assert.That(leaf.AbsoluteRect.X, Is.EqualTo(124));
            Assert.That(leaf.AbsoluteRect.Y, Is.EqualTo(232));
            Assert.That(leaf.AbsoluteRect.Width, Is.EqualTo(8));
            outer.Visible = false;
            Assert.That(leaf.IsShown, Is.False);
            Assert.That(leaf.Visible, Is.True);
        }

        [Test]
        public void Panel_HasInkBorderWoodFillAndTopLine()
        {
            var p = new UiPanel("P", _canvas.Stage, new PixelRect(0, 0, 320, 160));
            Assert.That(p.GameObject.GetComponent<Image>(), Is.Not.Null);
            Assert.That(p.BorderColor, Is.EqualTo(UiPalette.Ink));
            Assert.That(p.FillColor, Is.EqualTo(UiPalette.Wood));
            Assert.That(p.TopLineColor, Is.EqualTo(UiPalette.WoodLight));
            Assert.That(p.InnerRect.X, Is.EqualTo(UiPanel.BorderPx));
            Assert.That(p.InnerRect.Width, Is.EqualTo(320 - 2 * UiPanel.BorderPx));
        }

        [Test]
        public void Label_WithoutNameTable_PrintsTheKeyVerbatim()
        {
            var root = new UiWidget("R", _canvas.Stage, PixelRect.FromSize(1920, 1080));
            var label = new UiLabel(_plain, root, new PixelRect(0, 0, 1024, 24));
            label.SetKey("str.common.yes");
            Assert.That(label.ShownText, Is.EqualTo("str.common.yes"));
            Assert.That(label.Key, Is.EqualTo("str.common.yes"));
            Assert.That(label.GameObject.GetComponent<UguiText>().text, Is.EqualTo("str.common.yes"));
        }

        [Test]
        public void Label_WithNameTable_PrintsTheResolvedText()
        {
            var ctx = new UiViewContext(new UiTextSource(new FakeTextResolver().Add("str.common.yes", "예")), new InputModeTracker(), _canvas.Stage);
            var root = new UiWidget("R", _canvas.Stage, PixelRect.FromSize(1920, 1080));
            var label = new UiLabel(ctx, root, new PixelRect(0, 0, 256, 24));
            label.SetKey("str.common.yes");
            Assert.That(label.ShownText, Is.EqualTo("예"));
            label.SetKey("str.missing.key");
            Assert.That(label.ShownText, Is.EqualTo("str.missing.key"));
        }

        [Test]
        public void Label_Overflow_CutsWithEllipsisAndKeepsFullText()
        {
            // spec 13 1.4: 영문 한 자 = 글자 크기의 절반(F-16 → 8px), "…"도 반 자(8px). 64px 칸: 7자(56) + "…"(8) = 64
            var root = new UiWidget("R", _canvas.Stage, PixelRect.FromSize(1920, 1080));
            var label = new UiLabel(_plain, root, new PixelRect(0, 0, 64, 24), UiFontId.F16);
            label.SetRaw("ABCDEFGHIJ");
            Assert.That(label.ShownText, Is.EqualTo("ABCDEFG…"));
            Assert.That(label.FullText, Is.EqualTo("ABCDEFGHIJ"));
            Assert.That(label.IsTruncated, Is.True);
            label.SetRect(new PixelRect(0, 0, 80, 24));
            Assert.That(label.ShownText, Is.EqualTo("ABCDEFGHIJ"));
            Assert.That(label.IsTruncated, Is.False);
        }

        [Test]
        public void Label_HangulIsFullWidth()
        {
            // 한글 한 자 = 글자 크기(F-16 → 16px). 40px 칸에 "가나다라"(64px)는 두 자(32) + "…"(8) = 40
            var root = new UiWidget("R", _canvas.Stage, PixelRect.FromSize(1920, 1080));
            var label = new UiLabel(_plain, root, new PixelRect(0, 0, 40, 24), UiFontId.F16);
            label.SetRaw("가나다라");
            Assert.That(label.ShownText, Is.EqualTo("가나…"));
        }

        [Test]
        public void Label_FontSizeIsOneOfTheSix()
        {
            var root = new UiWidget("R", _canvas.Stage, PixelRect.FromSize(1920, 1080));
            foreach (UiFontId id in new[] { UiFontId.F12, UiFontId.F16, UiFontId.F24, UiFontId.F32, UiFontId.F48, UiFontId.F64 })
            {
                var label = new UiLabel(_plain, root, new PixelRect(0, 0, 256, 80), id);
                Assert.That(label.GameObject.GetComponent<UguiText>().fontSize, Is.EqualTo((int)id));
            }
            Assert.Throws<ArgumentOutOfRangeException>(() => new UiLabel(_plain, root, new PixelRect(0, 0, 256, 24), (UiFontId)10));
        }

        [Test]
        public void Canvas_IsOverlayWithIntegerScaleAndNoEventSystem()
        {
            Assert.That(_canvas.Canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            Assert.That(_canvas.Canvas.pixelPerfect, Is.True);
            Assert.That(_canvas.GameObject.GetComponent<GraphicRaycaster>(), Is.Null);
            Assert.That(_canvas.Stage.sizeDelta, Is.EqualTo(new Vector2(1920f, 1080f)));

            // spec 14 4.3: 2560×1440 은 그림 1배에 여백, 3840×2160 은 2배
            Assert.That(_canvas.ApplyWindowSize(2560, 1440), Is.True);
            Assert.That(_canvas.Scale, Is.EqualTo(1));
            Assert.That(_canvas.Origin.X, Is.EqualTo(320));
            Assert.That(_canvas.Origin.Y, Is.EqualTo(180));
            Assert.That(_canvas.GameObject.GetComponent<CanvasScaler>().scaleFactor, Is.EqualTo(1f));
            Assert.That(_canvas.ApplyWindowSize(3840, 2160), Is.True);
            Assert.That(_canvas.Scale, Is.EqualTo(2));
            Assert.That(_canvas.Origin.X, Is.EqualTo(0));
            Assert.That(_canvas.GameObject.GetComponent<CanvasScaler>().scaleFactor, Is.EqualTo(2f));
            Assert.That(_canvas.ApplyWindowSize(3840, 2160), Is.False);
        }

        [Test]
        public void Canvas_ScreenToStage_FlipsYAndUndoesScaleAndMargin()
        {
            _canvas.ApplyWindowSize(1920, 1080);
            PixelPoint p = _canvas.ScreenToStage(0, 1079);
            Assert.That(p.X, Is.EqualTo(0));
            Assert.That(p.Y, Is.EqualTo(0));
            p = _canvas.ScreenToStage(100, 0);
            Assert.That(p.Y, Is.EqualTo(1079));

            _canvas.ApplyWindowSize(2560, 1440);           // 무대는 (320, 180)부터
            p = _canvas.ScreenToStage(320, 1440 - 1 - 180);
            Assert.That(p.X, Is.EqualTo(0));
            Assert.That(p.Y, Is.EqualTo(0));
            p = _canvas.ScreenToStage(319, 1440 - 1 - 180);
            Assert.That(p.X, Is.EqualTo(-1));

            _canvas.ApplyWindowSize(3840, 2160);           // 2배
            p = _canvas.ScreenToStage(201, 2160 - 1 - 101);
            Assert.That(p.X, Is.EqualTo(100));
            Assert.That(p.Y, Is.EqualTo(50));
        }

        [Test]
        public void AllWidgets_AreCodeBuilt_NoPrefabConnections()
        {
            using var bed = new UiTestBed();
            TestMenuView menu = bed.OpenMenu();
            Transform[] all = bed.Ui.Canvas.GameObject.GetComponentsInChildren<Transform>(true);
            Assert.That(all.Length, Is.GreaterThan(50));
            foreach (Transform t in all)
            {
                Assert.That(PrefabUtility.IsPartOfAnyPrefab(t.gameObject), Is.False, t.name);
                foreach (Graphic g in t.GetComponents<Graphic>())
                {
                    Assert.That(g.raycastTarget, Is.False, t.name + " must not be a uGUI raycast target");
                }
            }
            Assert.That(menu.Left, Is.InstanceOf<UiButton>());
            Assert.That(menu.Tabs.Tabs.Count, Is.EqualTo(2));
            Assert.That(menu.List.Rows.Count, Is.EqualTo(TestMenuView.ListItems));
            Assert.That(menu.Cell.Content.FullText, Is.EqualTo("block.fireball"));
            // 64px 칸, F-12(반 자 6px): 9자(54) + "…"(6) = 60 ≤ 64, 10자면 66 > 64
            Assert.That(menu.Cell.Content.ShownText, Is.EqualTo("block.fir…"));
            Assert.That(menu.Left.Bracket, Is.Not.Null);
        }

        [Test]
        public void InteractiveWidgets_SyncTheirSizeIntoTheInteractable()
        {
            using var bed = new UiTestBed();
            TestMenuView menu = bed.OpenMenu();
            Assert.That(menu.Left.Control.WidthPx, Is.EqualTo(192));
            Assert.That(menu.Left.Control.HeightPx, Is.EqualTo(48));
            Assert.That(menu.Left.Control.BracketSize, Is.EqualTo(BracketSize.Medium));
            Assert.That(menu.Cell.Control.BracketSize, Is.EqualTo(BracketSize.Small));
            menu.Left.SetRect(new PixelRect(96, 96, 256, 64));
            Assert.That(menu.Left.Control.BracketSize, Is.EqualTo(BracketSize.Large));
        }

        [Test]
        public void List_RejectsRowsLowerThan48()
        {
            using var bed = new UiTestBed(register: r => r.Register(ScreenId.Records, (s, c) => new BadListView(s, c)));
            bed.Stack.Push(ScreenId.Records, ScreenArgs.None);
            Assert.Throws<ArgumentOutOfRangeException>(() => bed.Host.Sync());
        }

        [Test]
        public void TabBar_RejectsTabsBelowMinimumHitSize()
        {
            using var bed = new UiTestBed(register: r => r.Register(ScreenId.Records, (s, c) => new BadTabView(s, c)));
            bed.Stack.Push(ScreenId.Records, ScreenArgs.None);
            Assert.Throws<ArgumentOutOfRangeException>(() => bed.Host.Sync());
        }

        [Test]
        public void TestMenu_IsLaidOutOnThe8pxGridInsideTheSafeMargin()
        {
            using var bed = new UiTestBed();
            TestMenuView menu = bed.OpenMenu();
            foreach (UiInteractiveWidget w in menu.Interactives)
            {
                PixelRect r = w.AbsoluteRect;
                Assert.That(UiLayout.IsOnGrid(r.X) && UiLayout.IsOnGrid(r.Y) && UiLayout.IsOnGrid(r.Width) && UiLayout.IsOnGrid(r.Height), Is.True, w.Control.Id + " " + r);
                Assert.That(r.Width >= UiLayout.MinHitSizePx && r.Height >= UiLayout.MinHitSizePx, Is.True, w.Control.Id);
                Assert.That(r.X >= UiLayout.SafeMarginPx && r.Y >= UiLayout.SafeMarginPx, Is.True, w.Control.Id);
            }
        }

        private sealed class BadListView : UiScreenView
        {
            public BadListView(IScreen s, UiViewContext c) : base(s, c) { }

            protected override void OnBuild()
            {
                CreateGroup();
                _ = new UiList(this, Root, new PixelRect(96, 96, 256, 256), 40);
            }
        }

        private sealed class BadTabView : UiScreenView
        {
            public BadTabView(IScreen s, UiViewContext c) : base(s, c) { }

            protected override void OnBuild()
            {
                CreateGroup();
                _ = new UiTabBar(this, Root, new PixelRect(96, 96, 256, 48), 24);
            }
        }
    }
}

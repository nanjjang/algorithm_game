#nullable enable
using NUnit.Framework;
using Wizard.Unity.UI.Common;
using Wizard.ViewLogic.Common;

namespace Wizard.Unity.Tests.EditMode.UI.Common
{
    // 선택 괄호 도형(spec 14 4.3 "선택 괄호"): 4px 선, 대상에서 4px 바깥, 팔 12/20/28px, 커서는 2px 벌어진다(6px), 먹색 2px 테.
    // 기대값은 명세의 치수에서 손으로 셈한 것이다.
    public sealed class UiBracketTests
    {
        private UiCanvas _canvas = null!;

        [SetUp]
        public void SetUp() => _canvas = new UiCanvas();

        [TearDown]
        public void TearDown() => _canvas.Dispose();

        private UiBracket BracketFor(int w, int h)
        {
            var target = new UiWidget("T", _canvas.Stage, new PixelRect(200, 200, w, h));
            return new UiBracket(target);
        }

        private static BracketOverlay Overlay(BracketMotion motion, BracketTone tone, bool check, int w, int h) =>
            new BracketOverlay(motion, tone, check, null, Brackets.SizeFor(w, h));

        [Test]
        public void Small_Frame0_TopLeftBarsAre4pxOutsideWithArm12()
        {
            UiBracket b = BracketFor(48, 48);
            b.Apply(Overlay(BracketMotion.Still, BracketTone.Select, false, 48, 48), 0);
            Assert.That(b.CornersShown, Is.True);
            Assert.That(b.GapPx, Is.EqualTo(4));
            // 선의 바깥 변 = -(4 + 4) = -8. 가로 막대 12×4, 세로 막대 4×12
            AssertRect(b.TopLeftHorizontalBar, -8, -8, 12, 4);
            AssertRect(b.TopLeftVerticalBar, -8, -8, 4, 12);
        }

        [Test]
        public void Cursor_PulseFrame1_Opens2pxMore()
        {
            UiBracket b = BracketFor(48, 48);
            b.Apply(Overlay(BracketMotion.Moving, BracketTone.Select, false, 48, 48), 1);
            Assert.That(b.GapPx, Is.EqualTo(6));
            AssertRect(b.TopLeftHorizontalBar, -10, -10, 12, 4);
            b.Apply(Overlay(BracketMotion.Moving, BracketTone.Select, false, 48, 48), 0);
            Assert.That(b.GapPx, Is.EqualTo(4));
        }

        [Test]
        public void Confirmed_StaysStillEvenOnPulseFrame1()
        {
            UiBracket b = BracketFor(48, 48);
            b.Apply(Overlay(BracketMotion.Still, BracketTone.Select, true, 48, 48), 1);
            Assert.That(b.GapPx, Is.EqualTo(4));
            Assert.That(b.CheckShown, Is.True);
        }

        [Test]
        public void Medium_OtherCornersAreMirrors()
        {
            // 100×40 → 긴 변 100 → 보통(팔 20)
            UiBracket b = BracketFor(100, 40);
            b.Apply(Overlay(BracketMotion.Still, BracketTone.Select, false, 100, 40), 0);
            Assert.That(b.Size, Is.EqualTo(BracketSize.Medium));
            AssertRect(b.TopLeftHorizontalBar, -8, -8, 20, 4);
            // 오른쪽 위 가로: 오른쪽 바깥 변 = 100 + 8 → x = 108 - 20 = 88
            AssertRect(b.Mirror(b.TopLeftHorizontalBar, true, false), 88, -8, 20, 4);
            // 오른쪽 아래 세로: x = 100 + 4, 아래 바깥 변 = 40 + 8 → y = 48 - 20 = 28
            AssertRect(b.Mirror(b.TopLeftVerticalBar, true, true), 104, 28, 4, 20);
            // 왼쪽 아래 가로: y = 40 + 4
            AssertRect(b.Mirror(b.TopLeftHorizontalBar, false, true), -8, 44, 20, 4);
        }

        [Test]
        public void Large_Arm28()
        {
            UiBracket b = BracketFor(320, 160);
            b.Apply(Overlay(BracketMotion.Moving, BracketTone.Select, false, 320, 160), 0);
            Assert.That(b.Size, Is.EqualTo(BracketSize.Large));
            AssertRect(b.TopLeftHorizontalBar, -8, -8, 28, 4);
        }

        [Test]
        public void Tone_SelectIsYellow_MutedIsSlate()
        {
            UiBracket b = BracketFor(48, 48);
            b.Apply(Overlay(BracketMotion.Moving, BracketTone.Select, false, 48, 48), 0);
            Assert.That(b.BarColor, Is.EqualTo(UiPalette.SelectYellow));
            b.Apply(Overlay(BracketMotion.Moving, BracketTone.Muted, false, 48, 48), 0);
            Assert.That(b.BarColor, Is.EqualTo(UiPalette.Slate));
        }

        [Test]
        public void None_HidesCornersAndCheck()
        {
            UiBracket b = BracketFor(48, 48);
            b.Apply(Overlay(BracketMotion.Moving, BracketTone.Select, true, 48, 48), 0);
            b.Apply(Overlay(BracketMotion.None, BracketTone.Select, false, 48, 48), 0);
            Assert.That(b.CornersShown, Is.False);
            Assert.That(b.CheckShown, Is.False);
            foreach (UnityEngine.Transform t in b.Transform)
            {
                Assert.That(t.gameObject.activeSelf, Is.False, t.name);
            }
        }

        [Test]
        public void Check_SitsOnTheTopRightCorner()
        {
            UiBracket b = BracketFor(64, 64);
            b.Apply(Overlay(BracketMotion.Still, BracketTone.Select, true, 64, 64), 0);
            PixelRect c = b.CheckRect;
            Assert.That(c.X + c.Width / 2, Is.EqualTo(64));
            Assert.That(c.Y + c.Height / 2, Is.EqualTo(0));
        }

        private static void AssertRect(PixelRect r, int x, int y, int w, int h)
        {
            Assert.That(r.X, Is.EqualTo(x), "x of " + r);
            Assert.That(r.Y, Is.EqualTo(y), "y of " + r);
            Assert.That(r.Width, Is.EqualTo(w), "w of " + r);
            Assert.That(r.Height, Is.EqualTo(h), "h of " + r);
        }
    }
}

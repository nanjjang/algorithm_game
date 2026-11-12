using Wizard.ViewLogic.Common;
using Xunit;

namespace Wizard.ViewLogic.Tests.Common
{
    // spec 14 7.7 TV-148~152, spec 13 TV-241·242. spec 14 4.3절 상태 기계와 그림 우선순위
    public sealed class InteractableVectorTests
    {
        private const string GoldShortKey = "str.test.gold_short";

        private static (InputModeTracker mode, InteractableGroup group, Interactable a, Interactable b) Setup(InputMode initial)
        {
            var mode = new InputModeTracker(initial);
            var group = new InteractableGroup(mode);
            Interactable a = group.Add(new Interactable("a", 192, 48));
            Interactable b = group.Add(new Interactable("b", 192, 48));
            return (mode, group, a, b);
        }

        [Fact]
        public void S14_TV_148__DisabledButtonUnderKeyboardCursorDrawsDisabledSlateBracketAndReason()
        {
            var (_, g, a, _) = Setup(InputMode.Mouse);
            g.SetEnabled(a, false, GoldShortKey);
            g.MoveCursor(a);   // 커서는 꺼진 것에도 갈 수 있다
            Assert.Equal(SkinState.Disabled, g.SkinOf(a));
            BracketOverlay o = g.OverlayOf(a);
            Assert.Equal(BracketMotion.Moving, o.Motion);
            Assert.Equal(BracketTone.Muted, o.Tone);
            Assert.Equal(UiColorTokens.Slate, o.ToneToken);
            Assert.Equal(GoldShortKey, o.ReasonKey);
        }

        [Fact]
        public void S14_TV_149__MouseHoveredButtonDrawsHoverWithoutBracket()
        {
            var (_, g, a, _) = Setup(InputMode.Keyboard);
            g.MouseMove(3, 0, a);
            Assert.Equal(SkinState.Hover, g.SkinOf(a));
            Assert.False(g.OverlayOf(a).HasBracket);
            Assert.Equal(InteractVisual.Hover, g.VisualOf(a));
        }

        [Fact]
        public void S14_TV_150__KeyPressWhileHoveringClearsHoverAndBracketsTheCursorTarget()
        {
            var (mode, g, a, b) = Setup(InputMode.Keyboard);
            g.SetCursor(b);
            g.MouseMove(1, 1, a);
            Assert.Equal(SkinState.Hover, g.SkinOf(a));
            Assert.False(g.OverlayOf(b).HasBracket);   // 마우스 모드: 커서 괄호는 그리지 않는다

            g.KeyAny();
            Assert.Equal(InputMode.Keyboard, mode.Mode);
            Assert.False(a.Hovered);
            Assert.Equal(SkinState.Normal, g.SkinOf(a));
            Assert.Equal(BracketMotion.Moving, g.OverlayOf(b).Motion);
            Assert.Equal(BracketTone.Select, g.OverlayOf(b).Tone);
            Assert.Equal(UiColorTokens.SelectYellow, g.OverlayOf(b).ToneToken);
        }

        [Fact]
        public void S14_TV_151__MouseMoveAfterKeyboardKeepsBracketsOnlyOnPicked()
        {
            var (mode, g, a, b) = Setup(InputMode.Keyboard);
            g.SetPicked(a, true);
            g.MoveCursor(b);
            Assert.Equal(BracketMotion.Moving, g.OverlayOf(b).Motion);
            Assert.Equal(BracketMotion.Still, g.OverlayOf(a).Motion);

            g.MouseMove(5, -2, null);
            Assert.Equal(InputMode.Mouse, mode.Mode);
            Assert.False(g.OverlayOf(b).HasBracket);
            BracketOverlay picked = g.OverlayOf(a);
            Assert.Equal(BracketMotion.Still, picked.Motion);
            Assert.True(picked.ConfirmCheck);
        }

        [Fact]
        public void S14_TV_152__PressedOnTabDrawsPressedOverOn()
        {
            var mode = new InputModeTracker(InputMode.Mouse);
            using var g = new InteractableGroup(mode);
            Interactable tab = g.Add(new Interactable("tab", 192, 48, isToggle: true));
            g.SetToggled(tab, true);
            Assert.Equal(SkinState.On, g.SkinOf(tab));
            g.MouseMove(1, 0, tab);
            Assert.Equal(SkinState.On, g.SkinOf(tab));   // on 이 hover 보다 위
            g.PointerDown(tab);
            Assert.True(tab.Pressed);
            Assert.Equal(SkinState.Pressed, g.SkinOf(tab));
            Assert.Equal(InteractVisual.Pressed | InteractVisual.On, g.VisualOf(tab));
        }

        [Fact]
        public void S13_TV_241__HoverWithoutBracketThenKeyboardClearsHoverAndShowsBracket()
        {
            var mode = new InputModeTracker(InputMode.Keyboard);
            using var g = new InteractableGroup(mode);
            Interactable cell = g.Add(new Interactable("cell", 64, 64));
            g.SetCursor(cell);
            g.MouseMove(2, 2, cell);
            Assert.Equal(SkinState.Hover, g.SkinOf(cell));
            Assert.False(g.OverlayOf(cell).HasBracket);

            g.KeyAny();
            Assert.NotEqual(SkinState.Hover, g.SkinOf(cell));
            Assert.Equal(BracketMotion.Moving, g.OverlayOf(cell).Motion);
            Assert.Equal(BracketSize.Small, g.OverlayOf(cell).Size);
        }

        [Fact]
        public void S13_TV_242__DisabledButtonUnderKeyboardCursorHasSlateBracketAndReasonLine()
        {
            var mode = new InputModeTracker(InputMode.Keyboard);
            using var g = new InteractableGroup(mode);
            Interactable button = g.Add(new Interactable("go", 256, 64));
            g.SetEnabled(button, false, GoldShortKey);
            g.MoveCursor(button);
            BracketOverlay o = g.OverlayOf(button);
            Assert.Equal(BracketMotion.Moving, o.Motion);
            Assert.Equal(UiColorTokens.Slate, o.ToneToken);
            Assert.Equal(GoldShortKey, o.ReasonKey);
            Assert.Equal(InteractVisual.Disabled | InteractVisual.Cursor, g.VisualOf(button));
        }
    }
}

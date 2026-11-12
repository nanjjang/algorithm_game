using System;
using System.Collections.Generic;
using System.Linq;
using Wizard.ViewLogic.Common;
using Xunit;

namespace Wizard.ViewLogic.Tests.Common
{
    // spec 14 4.3절 상태 기계의 전이 하나하나. 벡터가 아닌 전이(꺼짐 누름, 눌린 채 벗어남, 커서 이동 중 취소 등)도 여기서 본다
    public sealed class InteractableGroupTests : IDisposable
    {
        private const string Reason = "str.test.reason";

        private readonly InputModeTracker _mode = new InputModeTracker(InputMode.Mouse);
        private readonly InteractableGroup _g;
        private readonly Interactable _a;
        private readonly Interactable _b;
        private readonly Interactable _c;
        private readonly List<InteractionEvent> _events = new List<InteractionEvent>();

        public InteractableGroupTests()
        {
            _g = new InteractableGroup(_mode);
            _a = _g.Add(new Interactable("a", 192, 48));
            _b = _g.Add(new Interactable("b", 192, 48));
            _c = _g.Add(new Interactable("c", 192, 48));
            _g.Raised += e => _events.Add(e);
        }

        public void Dispose() => _g.Dispose();

        private InteractionEventKind[] Kinds() => _events.Select(e => e.Kind).ToArray();

        [Fact]
        public void PointerClickPressesReleasesPicksAndActivates()
        {
            _g.PointerDown(_a);
            Assert.True(_a.Pressed);
            Assert.Same(_a, _g.PressedTarget);
            _g.PointerUp(_a);
            Assert.False(_a.Pressed);
            Assert.True(_a.Picked);
            Assert.Same(_a, _g.CursorTarget);   // 마우스로 고른 것에도 커서(괄호)가 온다
            Assert.Equal(new[] { InteractionEventKind.Press, InteractionEventKind.Release, InteractionEventKind.ConfirmCheck, InteractionEventKind.Activated }, Kinds());
            Assert.All(_events, e => Assert.Same(_a, e.Target));
        }

        [Fact]
        public void PointerReleasedElsewhereDoesNotActivate()
        {
            _g.PointerDown(_a);
            _g.PointerUp(_b);
            Assert.False(_a.Pressed);
            Assert.False(_a.Picked);
            Assert.False(_b.Picked);
            Assert.Equal(new[] { InteractionEventKind.Press, InteractionEventKind.Release }, Kinds());
            _events.Clear();
            _g.PointerDown(_a);
            _g.PointerUp(null);
            Assert.DoesNotContain(InteractionEventKind.Activated, Kinds());
        }

        [Fact]
        public void PointerUpWithoutPressDoesNothing()
        {
            _g.PointerUp(_a);
            Assert.Empty(_events);
        }

        [Fact]
        public void PointerDownOnDisabledRaisesDisabledPressWithReasonOnly()
        {
            _g.SetEnabled(_a, false, Reason);
            _g.PointerDown(_a);
            Assert.False(_a.Pressed);
            InteractionEvent e = Assert.Single(_events);
            Assert.Equal(InteractionEventKind.DisabledPress, e.Kind);
            Assert.Equal(Reason, e.ReasonKey);
            _g.PointerUp(_a);
            Assert.Single(_events);
        }

        [Fact]
        public void PointerDownOnNothingDoesNothing()
        {
            _g.PointerDown(null);
            Assert.Empty(_events);
            Assert.Null(_g.PressedTarget);
        }

        [Fact]
        public void KeyConfirmPressesWhileHeldAndActivatesOnRelease()
        {
            _g.SetCursor(_b);
            _events.Clear();
            _g.KeyConfirmDown();
            Assert.Equal(InputMode.Keyboard, _mode.Mode);
            Assert.True(_b.Pressed);
            Assert.Equal(SkinState.Pressed, _g.SkinOf(_b));
            _g.KeyConfirmDown();   // 키 반복은 무시
            Assert.Single(_events, e => e.Kind == InteractionEventKind.Press);
            _g.KeyConfirmUp();
            Assert.False(_b.Pressed);
            Assert.True(_b.Picked);
            Assert.Equal(new[] { InteractionEventKind.Press, InteractionEventKind.Release, InteractionEventKind.ConfirmCheck, InteractionEventKind.Activated }, Kinds());
        }

        [Fact]
        public void KeyConfirmOnDisabledCursorRaisesDisabledPress()
        {
            _g.SetEnabled(_b, false, Reason);
            _g.MoveCursor(_b);
            _events.Clear();
            _g.KeyConfirmDown();
            _g.KeyConfirmUp();
            InteractionEvent e = Assert.Single(_events);
            Assert.Equal(InteractionEventKind.DisabledPress, e.Kind);
            Assert.Equal(Reason, e.ReasonKey);
            Assert.False(_b.Picked);
        }

        [Fact]
        public void KeyConfirmWithoutCursorDoesNothingButSwitchesToKeyboard()
        {
            _g.KeyConfirmDown();
            _g.KeyConfirmUp();
            Assert.Empty(_events);
            Assert.Equal(InputMode.Keyboard, _mode.Mode);
        }

        [Fact]
        public void MovingTheCursorCancelsAKeyPress()
        {
            _g.MoveCursor(_a);
            _g.KeyConfirmDown();
            _g.MoveCursor(_b);
            Assert.False(_a.Pressed);
            _g.KeyConfirmUp();
            Assert.False(_a.Picked);
            Assert.False(_b.Picked);
            Assert.DoesNotContain(InteractionEventKind.Activated, Kinds());
        }

        [Fact]
        public void DisablingThePressedTargetCancelsThePress()
        {
            _g.PointerDown(_a);
            _g.SetEnabled(_a, false, Reason);
            Assert.False(_a.Pressed);
            Assert.Null(_g.PressedTarget);
            _g.PointerUp(_a);
            Assert.False(_a.Picked);
            Assert.DoesNotContain(InteractionEventKind.Activated, Kinds());
            _g.SetEnabled(_a, true);
            Assert.Null(_a.DisabledReasonKey);
            Assert.Equal(SkinState.Normal, _g.SkinOf(_a));
        }

        [Fact]
        public void PointerAndKeyPressesDoNotOverlap()
        {
            _g.MoveCursor(_a);
            _g.KeyConfirmDown();
            _g.PointerDown(_b);
            Assert.False(_b.Pressed);
            _g.PointerUp(_b);
            Assert.True(_a.Pressed);
            _g.KeyConfirmUp();
            Assert.True(_a.Picked);

            _events.Clear();
            _g.PointerDown(_c);
            _g.KeyConfirmDown();
            Assert.False(_a.Pressed);
            _g.KeyConfirmUp();   // 포인터로 누른 것은 키를 떼도 끝나지 않는다
            Assert.True(_c.Pressed);
            _g.PointerUp(_c);
            Assert.True(_c.Picked);
        }

        [Fact]
        public void ToggleActivationFlipsOnState()
        {
            var mode = new InputModeTracker(InputMode.Mouse);
            using var g = new InteractableGroup(mode, PickPolicy.Multiple);
            var events = new List<InteractionEventKind>();
            g.Raised += e => events.Add(e.Kind);
            Interactable t = g.Add(new Interactable("filter", 128, 48, isToggle: true));
            g.PointerDown(t);
            g.PointerUp(t);
            Assert.True(t.On);
            Assert.Contains(InteractionEventKind.ToggleOn, events);
            events.Clear();
            g.PointerDown(t);
            g.PointerUp(t);
            Assert.False(t.On);
            Assert.Equal(new[] { InteractionEventKind.Press, InteractionEventKind.Release, InteractionEventKind.ToggleOff, InteractionEventKind.Activated }, events);
        }

        [Fact]
        public void SinglePickPolicyKeepsOnlyTheLatest()
        {
            _g.PointerDown(_a);
            _g.PointerUp(_a);
            _g.PointerDown(_b);
            _g.PointerUp(_b);
            Assert.False(_a.Picked);
            Assert.True(_b.Picked);
            _g.SetPicked(_c, true);
            Assert.False(_b.Picked);
            Assert.True(_c.Picked);
        }

        [Fact]
        public void MultiplePickPolicyKeepsAll()
        {
            var mode = new InputModeTracker(InputMode.Mouse);
            using var g = new InteractableGroup(mode, PickPolicy.Multiple);
            Interactable x = g.Add(new Interactable("x", 64, 64));
            Interactable y = g.Add(new Interactable("y", 64, 64));
            g.PointerDown(x);
            g.PointerUp(x);
            g.PointerDown(y);
            g.PointerUp(y);
            Assert.True(x.Picked);
            Assert.True(y.Picked);
            // 커서와 확정이 한 화면에 같이 있을 수 있다
            g.MoveCursor(x);
            Assert.Equal(BracketMotion.Moving, g.OverlayOf(x).Motion);
            Assert.True(g.OverlayOf(x).ConfirmCheck);
            Assert.Equal(BracketMotion.Still, g.OverlayOf(y).Motion);
        }

        [Fact]
        public void AlreadyPickedTargetDoesNotRaiseConfirmCheckAgain()
        {
            _g.PointerDown(_a);
            _g.PointerUp(_a);
            _events.Clear();
            _g.PointerDown(_a);
            _g.PointerUp(_a);
            Assert.Equal(new[] { InteractionEventKind.Press, InteractionEventKind.Release, InteractionEventKind.Activated }, Kinds());
        }

        [Fact]
        public void HoverEnterAndExitFollowThePointer()
        {
            _g.MouseMove(1, 0, _a);
            _g.MouseMove(1, 0, _b);
            _g.MouseMove(1, 0, null);
            Assert.Equal(new[] { InteractionEventKind.HoverEnter, InteractionEventKind.HoverExit, InteractionEventKind.HoverEnter, InteractionEventKind.HoverExit }, Kinds());
            Assert.Equal(new[] { "a", "a", "b", "b" }, _events.Select(e => e.Target.Id).ToArray());
            Assert.Null(_g.HoveredTarget);
        }

        [Fact]
        public void HoveringADisabledTargetShowsDisabledSkinAndReasonButNoHoverEvent()
        {
            _g.SetEnabled(_a, false, Reason);
            _g.MouseMove(1, 0, _a);
            Assert.True(_a.Hovered);
            Assert.Empty(_events);
            Assert.Equal(SkinState.Disabled, _g.SkinOf(_a));
            Assert.Equal(Reason, _g.OverlayOf(_a).ReasonKey);
            Assert.False(_g.OverlayOf(_a).HasBracket);
        }

        [Fact]
        public void ReasonLineIsHiddenWhenNeitherCursorNorHover()
        {
            _g.SetEnabled(_a, false, Reason);
            Assert.Null(_g.OverlayOf(_a).ReasonKey);
            _g.MoveCursor(_b);
            Assert.Null(_g.OverlayOf(_a).ReasonKey);
        }

        [Fact]
        public void ZeroDeltaMouseMoveKeepsKeyboardModeAndDoesNotHover()
        {
            _g.KeyAny();
            _g.MouseMove(0, 0, _a);
            Assert.Equal(InputMode.Keyboard, _mode.Mode);
            Assert.False(_a.Hovered);
        }

        [Fact]
        public void ZeroDeltaMouseMoveInMouseModeUpdatesTheTargetUnderThePointer()
        {
            _g.MouseMove(1, 0, _a);
            _g.MouseMove(0, 0, _b);   // 그림이 움직여 포인터 아래가 바뀐 경우
            Assert.False(_a.Hovered);
            Assert.True(_b.Hovered);
        }

        [Fact]
        public void KeyboardInAnotherGroupClearsHoverHereToo()
        {
            using var other = new InteractableGroup(_mode);
            Interactable o = other.Add(new Interactable("o", 64, 64));
            _g.MouseMove(1, 0, _a);
            Assert.True(_a.Hovered);
            other.MoveCursor(o);
            Assert.False(_a.Hovered);
            other.MouseMove(1, 0, null);
            Assert.Equal(SkinState.Normal, _g.SkinOf(_a));   // 낡은 올림이 되살아나지 않는다
        }

        [Fact]
        public void DisposedGroupStopsListeningToModeChanges()
        {
            var mode = new InputModeTracker(InputMode.Mouse);
            var g = new InteractableGroup(mode);
            Interactable x = g.Add(new Interactable("x", 64, 64));
            g.MouseMove(1, 0, x);
            g.Dispose();
            g.Dispose();
            mode.OnKeyAny();
            Assert.True(x.Hovered);
        }

        [Fact]
        public void ForeignTargetsAreRejected()
        {
            var stranger = new Interactable("z", 64, 64);
            Assert.Throws<ArgumentException>(() => _g.PointerDown(stranger));
            Assert.Throws<ArgumentException>(() => _g.MoveCursor(stranger));
            Assert.Throws<ArgumentException>(() => _g.SetEnabled(stranger, false));
            Assert.Throws<InvalidOperationException>(() => _g.Add(_a));
        }

        [Fact]
        public void RemoveForgetsCursorHoverAndPress()
        {
            _g.MoveCursor(_a);
            _g.KeyConfirmDown();
            _g.MouseMove(1, 0, _a);
            Assert.True(_g.Remove(_a));
            Assert.Null(_g.CursorTarget);
            Assert.Null(_g.HoveredTarget);
            Assert.Null(_g.PressedTarget);
            Assert.False(_a.Cursor);
            Assert.False(_a.Pressed);
            Assert.False(_g.Remove(_a));
            _g.KeyConfirmUp();
            Assert.DoesNotContain(InteractionEventKind.Activated, Kinds());
        }

        [Fact]
        public void MoveCursorByStepsInOrderAndStopsAtTheEnds()
        {
            _g.MoveCursorBy(1);
            Assert.Same(_a, _g.CursorTarget);
            _g.MoveCursorBy(1);
            Assert.Same(_b, _g.CursorTarget);
            _g.MoveCursorBy(5);
            Assert.Same(_c, _g.CursorTarget);
            _g.MoveCursorBy(-9);
            Assert.Same(_a, _g.CursorTarget);
            Assert.Equal(InputMode.Keyboard, _mode.Mode);
            var cursorMoves = _events.Where(e => e.Kind == InteractionEventKind.CursorMove).Select(e => e.Target.Id).ToArray();
            Assert.Equal(new[] { "a", "b", "c", "a" }, cursorMoves);
            Assert.All(_events.Where(e => e.Kind == InteractionEventKind.CursorMove), e => Assert.Equal(BracketSize.Medium, e.BracketSize));

            using var empty = new InteractableGroup(_mode);
            empty.MoveCursorBy(1);
            Assert.Null(empty.CursorTarget);
        }

        [Fact]
        public void SetCursorToSameTargetIsQuiet()
        {
            _g.SetCursor(_a);
            _events.Clear();
            _g.SetCursor(_a);
            Assert.Empty(_events);
            _g.SetCursor(null);
            Assert.Null(_g.CursorTarget);
            Assert.False(_a.Cursor);
        }

        [Fact]
        public void OnSkinIsSkippedWhenTheSpriteHasNoOnState()
        {
            var mode = new InputModeTracker(InputMode.Mouse);
            using var g = new InteractableGroup(mode);
            Interactable sw = g.Add(new Interactable("sw", 48, 24, isToggle: true, hasOnSkin: false));
            g.SetToggled(sw, true);
            Assert.Equal(SkinState.Normal, g.SkinOf(sw));
            g.MouseMove(1, 0, sw);
            Assert.Equal(SkinState.Hover, g.SkinOf(sw));
            Assert.Equal(InteractVisual.Hover | InteractVisual.On, g.VisualOf(sw));
        }

        [Fact]
        public void PickedDisabledTargetHasMutedStillBracket()
        {
            _g.SetPicked(_a, true);
            _g.SetEnabled(_a, false, Reason);
            BracketOverlay o = _g.OverlayOf(_a);
            Assert.Equal(BracketMotion.Still, o.Motion);
            Assert.Equal(BracketTone.Muted, o.Tone);
            Assert.Equal(InteractVisual.Disabled | InteractVisual.Confirmed, _g.VisualOf(_a));
        }

        [Fact]
        public void SkinTokensMatchManUiStateTokens()
        {
            Assert.Equal("normal", SkinStateToken.Of(SkinState.Normal));
            Assert.Equal("hover", SkinStateToken.Of(SkinState.Hover));
            Assert.Equal("pressed", SkinStateToken.Of(SkinState.Pressed));
            Assert.Equal("on", SkinStateToken.Of(SkinState.On));
            Assert.Equal("disabled", SkinStateToken.Of(SkinState.Disabled));
        }
    }
}

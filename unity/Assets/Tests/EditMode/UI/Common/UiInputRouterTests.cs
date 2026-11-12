#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Wizard.Client;
using Wizard.Unity.UI.Common;
using Wizard.ViewLogic.Common;
using Wizard.ViewLogic.Common.Dialogs;
using Wizard.ViewLogic.Stack;

namespace Wizard.Unity.Tests.EditMode.UI.Common
{
    // 수락 3: 키보드와 마우스 둘 다로 모든 위젯을 조작할 수 있고 입력 모드 전환이 spec 14 4.3절과 같다.
    // 입력은 물리 키(자리)와 무대 좌표의 포인터로 UiInputRouter 에 넣는다(Input System 에서 옮기는 일은 조립 루트의 몫).
    public sealed class UiInputRouterTests
    {
        private UiTestBed _bed = null!;
        private TestMenuView _menu = null!;

        [SetUp]
        public void SetUp()
        {
            _bed = new UiTestBed();
            _menu = _bed.OpenMenu();
        }

        [TearDown]
        public void TearDown() => _bed.Dispose();

        private InteractableGroup G => _menu.Group!;

        // ── 키보드로 모든 위젯 ──

        [Test]
        public void Keyboard_ArrowsMoveTheCursor_JPressesAndReleases()
        {
            Assert.That(G.CursorTarget, Is.SameAs(_menu.Left.Control));
            _bed.Input.KeyDown(PhysicalKey.RightArrow);
            Assert.That(G.CursorTarget, Is.SameAs(_menu.Middle.Control));
            _bed.Input.KeyUp(PhysicalKey.RightArrow);

            _bed.Input.KeyDown(PhysicalKey.J);
            Assert.That(_menu.Middle.SkinState, Is.EqualTo(SkinState.Pressed));
            Assert.That(_menu.Middle.Skin.LocalRect.Y, Is.EqualTo(UiInteractiveWidget.PressedShiftPx));
            Assert.That(_menu.Activations, Is.Empty, "activation happens on release (spec 14 4.3 KeyConfirm)");
            _bed.Input.KeyUp(PhysicalKey.J);
            Assert.That(_menu.Activations, Is.EqualTo(new[] { "middle" }));
            Assert.That(_menu.Middle.Control.Picked, Is.True);
            Assert.That(_menu.Middle.SkinState, Is.EqualTo(SkinState.Normal));
            Assert.That(_menu.Middle.Skin.LocalRect.Y, Is.EqualTo(0));
        }

        [Test]
        public void Keyboard_WasdAndEnterWorkToo()
        {
            _bed.Tap(PhysicalKey.D);
            _bed.Tap(PhysicalKey.D);
            Assert.That(G.CursorTarget, Is.SameAs(_menu.Right.Control));
            _bed.Tap(PhysicalKey.A);
            Assert.That(G.CursorTarget, Is.SameAs(_menu.Middle.Control));
            _bed.Tap(PhysicalKey.Enter);
            Assert.That(_menu.Activations, Is.EqualTo(new[] { "middle" }));
        }

        [Test]
        public void Keyboard_ReachesTabsCellAndListRows()
        {
            _bed.Tap(PhysicalKey.DownArrow);                       // 왼쪽 버튼 → 첫 탭
            Assert.That(G.CursorTarget, Is.SameAs(_menu.Tabs.Tabs[0].Control));
            _bed.Tap(PhysicalKey.RightArrow);
            Assert.That(G.CursorTarget, Is.SameAs(_menu.Tabs.Tabs[1].Control));
            _bed.Tap(PhysicalKey.J);
            Assert.That(_menu.TabEvents, Is.EqualTo(new[] { 1 }));
            Assert.That(_menu.Tabs.Tabs[1].Control.On, Is.True);
            Assert.That(_menu.Tabs.Tabs[0].Control.On, Is.False);
            Assert.That(_menu.Tabs.Tabs[1].OnLineShown, Is.True);
            _bed.Tap(PhysicalKey.J);                                // 켜진 탭을 다시 눌러도 켜진 채(탭 줄은 하나만 켠다)
            Assert.That(_menu.Tabs.Tabs[1].Control.On, Is.True);

            _bed.Tap(PhysicalKey.LeftArrow);
            _bed.Tap(PhysicalKey.DownArrow);                        // 첫 탭 → 칸
            Assert.That(G.CursorTarget, Is.SameAs(_menu.Cell.Control));
            _bed.Tap(PhysicalKey.J);
            Assert.That(_menu.Activations, Does.Contain("cell"));

            _bed.Tap(PhysicalKey.DownArrow);                        // 칸 → 목록 첫 줄
            Assert.That(G.CursorTarget, Is.SameAs(_menu.List.Rows[0].Control));
            for (int i = 0; i < 6; i++)
            {
                _bed.Tap(PhysicalKey.DownArrow);
            }
            // 창은 다섯 줄. 여섯째 줄 너머(7번 줄)로 가면 창이 따라 내려간다
            Assert.That(G.CursorTarget, Is.SameAs(_menu.List.Rows[6].Control));
            Assert.That(_menu.List.ScrollOffset, Is.EqualTo(2));
            Assert.That(_menu.List.Rows[6].IsShown, Is.True);
            Assert.That(_menu.List.Rows[1].IsShown, Is.False);
            _bed.Tap(PhysicalKey.J);
            Assert.That(_menu.Activations, Does.Contain("row6"));
            for (int i = 0; i < 6; i++)
            {
                _bed.Tap(PhysicalKey.UpArrow);
            }
            Assert.That(G.CursorTarget, Is.SameAs(_menu.List.Rows[0].Control));
            Assert.That(_menu.List.ScrollOffset, Is.EqualTo(0));
            _bed.Tap(PhysicalKey.UpArrow);                          // 맨 위에서 위 → 목록 밖(칸)
            Assert.That(G.CursorTarget, Is.SameAs(_menu.Cell.Control));
        }

        [Test]
        public void Keyboard_OtherKeysGoToTheFocusedScreen()
        {
            _bed.Tap(PhysicalKey.K);
            _bed.Tap(PhysicalKey.Tab);
            Assert.That(_menu.Keys.Count, Is.EqualTo(2));
            Assert.That(_menu.Keys[0].Action, Is.EqualTo(UiKeyAction.Secondary));
            Assert.That(_menu.Keys[1].Key, Is.EqualTo(PhysicalKey.Tab));
            Assert.That(_menu.Keys[1].Action, Is.EqualTo(UiKeyAction.None));
        }

        // ── 마우스로 모든 위젯 ──

        [Test]
        public void Mouse_ClickActivatesButtonsTabsCellAndRows()
        {
            _bed.Click(TestMenuView.RightRect);
            _bed.Click(TestMenuView.CellRect);
            _bed.Click(_menu.Tabs.Tabs[1].AbsoluteRect);
            _bed.Click(_menu.List.Rows[2].AbsoluteRect);
            Assert.That(_menu.Activations, Is.EqualTo(new[] { "right", "cell", "row2" }));
            Assert.That(_menu.TabEvents, Is.EqualTo(new[] { 1 }));
            Assert.That(_menu.Tabs.Tabs[1].Control.On, Is.True);
            // 마우스로 끝까지 누른 것에도 커서가 온다(spec 13 1.3 "지금 고른 것")
            Assert.That(G.CursorTarget, Is.SameAs(_menu.List.Rows[2].Control));
        }

        [Test]
        public void Mouse_PressThenReleaseElsewhere_DoesNotActivate()
        {
            PixelPoint a = UiTestBed.CenterOf(TestMenuView.LeftRect);
            PixelPoint b = UiTestBed.CenterOf(TestMenuView.MiddleRect);
            _bed.Input.PointerMoveStage(a);
            _bed.Input.PointerDownStage(a);
            Assert.That(_menu.Left.SkinState, Is.EqualTo(SkinState.Pressed));
            _bed.Input.PointerUpStage(b);
            Assert.That(_menu.Activations, Is.Empty);
            Assert.That(_menu.Left.SkinState, Is.Not.EqualTo(SkinState.Pressed));
        }

        [Test]
        public void Mouse_WheelScrollsTheListUnderThePointer()
        {
            _bed.Input.PointerMoveStage(UiTestBed.CenterOf(TestMenuView.ListRect));
            _bed.Input.Scroll(3);
            Assert.That(_menu.List.ScrollOffset, Is.EqualTo(3));
            _bed.Input.Scroll(10);
            Assert.That(_menu.List.ScrollOffset, Is.EqualTo(TestMenuView.ListItems - 5));
            Assert.That(_menu.List.Rows[0].IsShown, Is.False);
            _bed.Click(new PixelRect(TestMenuView.ListRect.X, TestMenuView.ListRect.Y, 64, 48));   // 창의 첫 줄 = 5번 줄
            Assert.That(_menu.Activations, Is.EqualTo(new[] { "row5" }));
            _bed.Input.PointerMoveStage(UiTestBed.CenterOf(TestMenuView.LeftRect));
            _bed.Input.Scroll(-3);
            Assert.That(_menu.List.ScrollOffset, Is.EqualTo(TestMenuView.ListItems - 5), "wheel outside the list does nothing");
        }

        [Test]
        public void Mouse_PointerInWindowPixels_IsMappedToStageAt2x()
        {
            _bed.Ui.Driver.Step(3840, 2160);
            PixelRect r = TestMenuView.MiddleRect;
            int sx = (r.X + 10) * 2;
            int sy = 2160 - 1 - (r.Y + 10) * 2;
            _bed.Input.PointerMove(sx, sy);
            _bed.Input.PointerDown(sx, sy);
            _bed.Input.PointerUp(sx, sy);
            Assert.That(_menu.Activations, Is.EqualTo(new[] { "middle" }));
        }

        // ── 입력 모드 전환(spec 14 4.3, spec 13 1.3) ──

        [Test]
        public void MouseMove_SwitchesToMouse_HoverShows_BracketOnlyOnPicked()
        {
            Assert.That(_bed.Mode.Mode, Is.EqualTo(InputMode.Keyboard));
            Assert.That(_menu.Left.Bracket.CornersShown, Is.True, "keyboard cursor bracket at start");

            _bed.Input.PointerMoveStage(UiTestBed.CenterOf(TestMenuView.MiddleRect));
            Assert.That(_bed.Mode.Mode, Is.EqualTo(InputMode.Mouse));
            Assert.That(_menu.Middle.SkinState, Is.EqualTo(SkinState.Hover));
            Assert.That(_menu.Middle.Skin.BorderColor, Is.EqualTo(UiPalette.WoodLight));
            Assert.That(_menu.Middle.Bracket.CornersShown, Is.False, "hover never gets the bracket");
            Assert.That(_menu.Left.Bracket.CornersShown, Is.False, "in mouse mode only picked items keep a bracket");

            _bed.Click(TestMenuView.RightRect);
            Assert.That(_menu.Right.Bracket.CornersShown, Is.True);
            Assert.That(_menu.Right.Bracket.Motion, Is.EqualTo(BracketMotion.Still));
            Assert.That(_menu.Right.Bracket.CheckShown, Is.True);
            Assert.That(_menu.Middle.SkinState, Is.EqualTo(SkinState.Normal), "hover moved off");
        }

        [Test]
        public void AnyKey_SwitchesToKeyboard_ClearsHover_ShowsCursorBracket()
        {
            _bed.Input.PointerMoveStage(UiTestBed.CenterOf(TestMenuView.MiddleRect));
            Assert.That(_menu.Middle.SkinState, Is.EqualTo(SkinState.Hover));

            _bed.Tap(PhysicalKey.Q);                                // 메뉴 키가 아니어도
            Assert.That(_bed.Mode.Mode, Is.EqualTo(InputMode.Keyboard));
            Assert.That(_menu.Middle.SkinState, Is.EqualTo(SkinState.Normal), "all hover cleared");
            Assert.That(_menu.Middle.Control.Hovered, Is.False);
            Assert.That(_menu.Left.Bracket.CornersShown, Is.True, "cursor target gets the moving bracket");
            Assert.That(_menu.Left.Bracket.Motion, Is.EqualTo(BracketMotion.Moving));
        }

        [Test]
        public void ZeroMouseMove_DoesNotLeaveKeyboardMode()
        {
            PixelPoint p = UiTestBed.CenterOf(TestMenuView.MiddleRect);
            _bed.Input.PointerMoveStage(p);
            _bed.Tap(PhysicalKey.DownArrow);
            Assert.That(_bed.Mode.Mode, Is.EqualTo(InputMode.Keyboard));
            _bed.Input.PointerMoveStage(p);
            _bed.Input.SyncHover();
            Assert.That(_bed.Mode.Mode, Is.EqualTo(InputMode.Keyboard));
            Assert.That(_menu.Middle.SkinState, Is.EqualTo(SkinState.Normal));
        }

        [Test]
        public void CursorAndConfirmed_CanCoexistInKeyboardMode()
        {
            _bed.Tap(PhysicalKey.J);                                // 왼쪽 확정
            _bed.Tap(PhysicalKey.RightArrow);                       // 커서는 가운데
            Assert.That(_menu.Left.Bracket.Motion, Is.EqualTo(BracketMotion.Still));
            Assert.That(_menu.Left.Bracket.CheckShown, Is.True);
            Assert.That(_menu.Middle.Bracket.Motion, Is.EqualTo(BracketMotion.Moving));
        }

        [Test]
        public void Disabled_CursorCanGo_ShowsMutedBracketAndReason_PressDoesNothing()
        {
            _bed.Tap(PhysicalKey.RightArrow);
            _bed.Tap(PhysicalKey.RightArrow);
            _bed.Tap(PhysicalKey.RightArrow);
            Assert.That(G.CursorTarget, Is.SameAs(_menu.Disabled.Control));
            Assert.That(_menu.Disabled.SkinState, Is.EqualTo(SkinState.Disabled));
            Assert.That(_menu.Disabled.Bracket.BarColor, Is.EqualTo(UiPalette.Slate));
            Assert.That(_menu.Disabled.ReasonLine.Visible, Is.True);
            Assert.That(_menu.Disabled.ReasonLine.ShownText, Is.EqualTo(TestMenuView.DisabledReason));
            _bed.Tap(PhysicalKey.J);
            Assert.That(_menu.Activations, Is.Empty);
            _bed.Click(TestMenuView.DisabledRect);
            Assert.That(_menu.Activations, Is.Empty);
            Assert.That(_menu.Disabled.SkinState, Is.EqualTo(SkinState.Disabled), "a disabled item never shows hover or pressed");
            Assert.That(_menu.Disabled.ReasonLine.Visible, Is.True, "mouse over a disabled item shows the reason");
        }

        // ── 스택을 따라 보내기 ──

        [Test]
        public void Modal_TakesKeyboardAndBlocksPointerBelow_EscChoosesNo()
        {
            DialogButtonRole? chosen = null;
            _bed.Stack.Push(ScreenId.ConfirmDialog, ScreenArgs.WithPayload(new ConfirmRequest("str.q", null, false, r => chosen = r)));
            _bed.Host.Sync();
            var dialog = (ConfirmDialogView)_bed.Host.ViewOf(ScreenId.ConfirmDialog)!;

            _bed.Click(TestMenuView.LeftRect);                      // 모달 아래는 눌리지 않는다
            Assert.That(_menu.Activations, Is.Empty);
            _bed.Tap(PhysicalKey.RightArrow);                       // 키보드는 맨 위(창)로
            Assert.That(dialog.Group!.CursorTarget, Is.SameAs(dialog.ButtonOf(DialogButtonRole.No)!.Control));
            Assert.That(G.CursorTarget, Is.SameAs(_menu.Left.Control));

            _bed.Tap(PhysicalKey.Escape);                           // Esc → 모델의 OnBack → [아니오]
            Assert.That(chosen, Is.EqualTo(DialogButtonRole.No));
            Assert.That(_bed.Host.ViewOf(ScreenId.ConfirmDialog), Is.Null);
            Assert.That(dialog.IsDisposed, Is.True);
            Assert.That(_bed.Stack.TopId, Is.EqualTo(ScreenId.MainMenu));
        }

        [Test]
        public void ConfirmDialog_ChosenByMouseOrKeyboard()
        {
            DialogButtonRole? chosen = null;
            _bed.Stack.Push(ScreenId.ConfirmDialog, ScreenArgs.WithPayload(new ConfirmRequest("str.q", null, false, r => chosen = r)));
            _bed.Host.Sync();
            var dialog = (ConfirmDialogView)_bed.Host.ViewOf(ScreenId.ConfirmDialog)!;
            _bed.Click(dialog.ButtonOf(DialogButtonRole.Yes)!.AbsoluteRect);
            Assert.That(chosen, Is.EqualTo(DialogButtonRole.Yes));
            Assert.That(_bed.Stack.TopId, Is.EqualTo(ScreenId.MainMenu));

            chosen = null;
            _bed.Stack.Push(ScreenId.ConfirmDialog, ScreenArgs.WithPayload(new ConfirmRequest("str.q", null, false, r => chosen = r)));
            _bed.Host.Sync();
            _bed.Tap(PhysicalKey.J);                                // 처음 커서 = [예]
            Assert.That(chosen, Is.EqualTo(DialogButtonRole.Yes));
            Assert.That(_bed.Host.ViewOf(ScreenId.ConfirmDialog), Is.Null);
            Assert.That(_menu.Activations, Is.Empty, "the screen below never saw the key");
        }

        [Test]
        public void NonBlockingPanel_PointerPassesOnlyOutsideItsBody()
        {
            _bed.Factory.Layer(ScreenId.Settings, ScreenLayer.Panel, blocksInput: false);
            _bed.Stack.Push(ScreenId.Settings, ScreenArgs.None);
            _bed.Host.Sync();
            _bed.Click(TestMenuView.LeftRect);                      // 판 밖: 아래 화면이 받는다
            Assert.That(_menu.Activations, Is.EqualTo(new[] { "left" }));
            PixelRect body = PlaceholderScreenView.CenterPanelRect;
            Assert.That(_bed.Input.Under(UiTestBed.CenterOf(body), out UiScreenView? v), Is.Null);
            Assert.That(v, Is.Null);
            _bed.Tap(PhysicalKey.RightArrow);                       // 키보드는 포커스(맨 위 판)만
            Assert.That(G.CursorTarget, Is.SameAs(_menu.Left.Control));
        }

        [Test]
        public void Toast_NeverTakesInput()
        {
            _bed.Factory.Layer(ScreenId.RunSubmitPending, ScreenLayer.Toast);
            _bed.Stack.Push(ScreenId.RunSubmitPending, ScreenArgs.None);
            _bed.Host.Sync();
            _bed.Tap(PhysicalKey.RightArrow);
            Assert.That(G.CursorTarget, Is.SameAs(_menu.Middle.Control));
            _bed.Tap(PhysicalKey.Escape);                            // 맨 아래 화면은 Esc 로 닫히지 않는다. 토스트도 그대로
            Assert.That(_bed.Stack.TopId, Is.EqualTo(ScreenId.MainMenu));
            Assert.That(_bed.Stack.Toasts.Count, Is.EqualTo(1));
        }

        [Test]
        public void Esc_BacksOutOfAPushedScreen_UnlessTheModelConsumesIt()
        {
            _bed.Stack.Push(ScreenId.Records, ScreenArgs.None);
            var records = (TestScreen)_bed.Stack.Top!;
            records.ConsumeBack = true;
            _bed.Host.Sync();
            _bed.Tap(PhysicalKey.Escape);
            Assert.That(records.BackCount, Is.EqualTo(1));
            Assert.That(_bed.Stack.TopId, Is.EqualTo(ScreenId.Records));
            records.ConsumeBack = false;
            _bed.Tap(PhysicalKey.Escape);
            Assert.That(_bed.Stack.TopId, Is.EqualTo(ScreenId.MainMenu));
            Assert.That(_bed.Host.ViewOf(ScreenId.Records), Is.Null);
        }

        [Test]
        public void HoverFollowsANewScreenUnderAStillPointer()
        {
            _bed.Input.PointerMoveStage(UiTestBed.CenterOf(TestMenuView.LeftRect));
            Assert.That(_menu.Left.SkinState, Is.EqualTo(SkinState.Hover));
            _bed.Stack.Push(ScreenId.Records, ScreenArgs.None);   // 전체 화면이 덮는다
            _bed.Ui.Driver.Step(1920, 1080);
            Assert.That(_menu.Left.Control.Hovered, Is.False);
            _bed.Stack.Pop();
            _bed.Ui.Driver.Step(1920, 1080);
            Assert.That(_menu.Left.Control.Hovered, Is.True);
        }

        [Test]
        public void ViewHasNoRules_OnlyTheModelsStateMachineChangesState()
        {
            // 뷰를 거치지 않고 모델(묶음)을 바꿔도 뷰는 그대로 그린다: 뷰가 따로 쥔 상태가 없다
            G.MoveCursor(_menu.Right.Control);
            G.SetPicked(_menu.Middle.Control, true);
            _bed.Host.Sync();
            Assert.That(_menu.Right.Bracket.Motion, Is.EqualTo(BracketMotion.Moving));
            Assert.That(_menu.Middle.Bracket.Motion, Is.EqualTo(BracketMotion.Still));
            Assert.That(_menu.Left.Bracket.CornersShown, Is.False);
            var seen = new List<InteractionEventKind>();
            G.Raised += e => seen.Add(e.Kind);
            _bed.Click(TestMenuView.LeftRect);
            Assert.That(seen, Does.Contain(InteractionEventKind.Activated));
        }
    }
}

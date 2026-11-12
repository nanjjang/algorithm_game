#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wizard.Unity.UI.Common;
using Wizard.ViewLogic.Common;
using Wizard.ViewLogic.Common.Dialogs;
using Wizard.ViewLogic.Stack;

namespace Wizard.Unity.Tests.EditMode.UI.Common
{
    // 수락 2: ScreenHost 가 ScreenStack 을 따라 뷰를 만들고 없앤다. 뷰는 모델을 읽고 조작을 모델에 넘기기만 한다
    public sealed class ScreenHostTests
    {
        private UiTestBed _bed = null!;

        [SetUp]
        public void SetUp() => _bed = new UiTestBed();

        [TearDown]
        public void TearDown() => _bed.Dispose();

        [Test]
        public void Push_CreatesAndShowsAView_Pop_DestroysIt()
        {
            _bed.Stack.Push(ScreenId.Title, ScreenArgs.None);
            UiScreenView? view = _bed.Host.ViewOf(ScreenId.Title);
            Assert.That(view, Is.Not.Null);
            Assert.That(view!.Screen, Is.SameAs(_bed.Stack.Top));
            _bed.Host.Sync();
            Assert.That(view.IsBuilt, Is.True);
            Assert.That(view.Shown, Is.True);
            Assert.That(view.Root.Transform.parent, Is.SameAs(_bed.Ui.Canvas.Stage));
            GameObject go = view.Root.GameObject;

            _bed.Stack.Pop();
            Assert.That(_bed.Host.Views.Count, Is.EqualTo(0));
            Assert.That(view.IsDisposed, Is.True);
            Assert.That(go == null, Is.True, "the view's uGUI objects are destroyed with it");
        }

        [Test]
        public void UnregisteredScreen_GetsAPlaceholderShowingItsId()
        {
            _bed.Stack.Push(ScreenId.Records, ScreenArgs.None);
            _bed.Host.Sync();
            var view = (PlaceholderScreenView)_bed.Host.ViewOf(ScreenId.Records)!;
            Assert.That(view.Title!.ShownText, Is.EqualTo("Records"));
        }

        [Test]
        public void Fullscreen_HidesWhatIsBelow_PanelAndToastStayOnTop()
        {
            _bed.Factory.Layer(ScreenId.Settings, ScreenLayer.Panel).Layer(ScreenId.RunSubmitPending, ScreenLayer.Toast);
            _bed.Stack.Push(ScreenId.Title, ScreenArgs.None);
            _bed.Stack.Push(ScreenId.Records, ScreenArgs.None);
            _bed.Host.Sync();
            UiScreenView title = _bed.Host.ViewOf(ScreenId.Title)!;
            UiScreenView records = _bed.Host.ViewOf(ScreenId.Records)!;
            Assert.That(title.Shown, Is.False);
            Assert.That(records.Shown, Is.True);

            _bed.Stack.Push(ScreenId.RunSubmitPending, ScreenArgs.None);
            _bed.Stack.Push(ScreenId.Settings, ScreenArgs.None);
            _bed.Host.Sync();
            UiScreenView settings = _bed.Host.ViewOf(ScreenId.Settings)!;
            UiScreenView toast = _bed.Host.ViewOf(ScreenId.RunSubmitPending)!;
            Assert.That(records.Shown && settings.Shown && toast.Shown, Is.True);
            Assert.That(title.Shown, Is.False);
            int r = records.Root.Transform.GetSiblingIndex();
            int s = settings.Root.Transform.GetSiblingIndex();
            int t = toast.Root.Transform.GetSiblingIndex();
            Assert.That(r < s && s < t, Is.True, "draw order: below → above, toasts last (" + r + ", " + s + ", " + t + ")");

            _bed.Stack.Pop();                                // Settings
            _bed.Stack.Pop();                                // Records → Title 이 드러난다
            _bed.Host.Sync();
            Assert.That(title.Shown, Is.True);
            Assert.That(toast.Shown, Is.True);
            _bed.Stack.DismissToast(ScreenId.RunSubmitPending);
            Assert.That(_bed.Host.ViewOf(ScreenId.RunSubmitPending), Is.Null);
        }

        [Test]
        public void Replace_And_ResetTo_SwapViews()
        {
            _bed.Stack.Push(ScreenId.Title, ScreenArgs.None);
            _bed.Stack.Push(ScreenId.Records, ScreenArgs.None);
            _bed.Stack.Replace(ScreenId.Settings, ScreenArgs.None);
            Assert.That(_bed.Host.ViewOf(ScreenId.Records), Is.Null);
            Assert.That(_bed.Host.ViewOf(ScreenId.Settings), Is.Not.Null);
            _bed.Stack.ResetTo(ScreenId.MainMenu, ScreenArgs.None);
            Assert.That(_bed.Host.Views.Count, Is.EqualTo(1));
            Assert.That(_bed.Host.Views[0].Id, Is.EqualTo(ScreenId.MainMenu));
            _bed.Host.Sync();
            Assert.That(_bed.Ui.Canvas.Stage.childCount, Is.EqualTo(1), "old views left no objects behind");
        }

        [Test]
        public void HostCreatedLate_PicksUpScreensAlreadyOpen()
        {
            var mode = new InputModeTracker();
            var factory = new TestScreenFactory { Mode = mode };
            var stack = new ScreenStack(factory);
            factory.Stack = stack;
            stack.Push(ScreenId.Title, ScreenArgs.None);
            stack.Push(ScreenId.ConfirmDialog, ScreenArgs.WithPayload(new ConfirmRequest("str.q")));
            using var ui = new UiRoot(stack, new ScreenViewRegistry().WithCommonDialogs(), mode, null, () => 0);
            Assert.That(ui.Host.Views.Count, Is.EqualTo(2));
            Assert.That(ui.Host.ViewOf(ScreenId.ConfirmDialog), Is.InstanceOf<ConfirmDialogView>());
            Assert.That(ui.Host.Views[1].IsBuilt, Is.True);
        }

        [Test]
        public void ConfirmDialogView_ReadsTheModel_BuiltAfterOnEnter()
        {
            var details = new List<string> { "str.d1", "str.d2" };
            _bed.Stack.Push(ScreenId.ConfirmDialog, ScreenArgs.WithPayload(new ConfirmRequest("str.q", details, caution: true)));
            var view = (ConfirmDialogView)_bed.Host.ViewOf(ScreenId.ConfirmDialog)!;
            Assert.That(view.IsBuilt, Is.False, "built on the next Sync, after the model's OnEnter");
            _bed.Host.Sync();
            Assert.That(view.Question!.ShownText, Is.EqualTo("str.q"));
            Assert.That(view.Details.Count, Is.EqualTo(2));
            Assert.That(view.Details[1].ShownText, Is.EqualTo("str.d2"));
            Assert.That(view.Buttons.Count, Is.EqualTo(2));
            Assert.That(view.Group, Is.SameAs(view.Model.Group), "the view draws the model's own state machine");
            UiButton yes = view.ButtonOf(DialogButtonRole.Yes)!;
            UiButton no = view.ButtonOf(DialogButtonRole.No)!;
            Assert.That(yes.Label.ShownText, Is.EqualTo(CommonTextKeys.Yes));
            Assert.That(yes.Tone, Is.EqualTo(ButtonTone.Caution));
            Assert.That(yes.ShowsWarningMark, Is.True);
            Assert.That(yes.Skin.BorderColor, Is.EqualTo(UiPalette.AlertAmber));
            Assert.That(no.ShowsWarningMark, Is.False);
            // 조심할 일이면 모델이 처음 커서를 [아니오]에 둔다. 뷰는 그것을 바꾸지 않는다
            Assert.That(view.Group!.CursorTarget, Is.SameAs(no.Control));
            Assert.That(no.Bracket.CornersShown, Is.True);
            Assert.That(yes.Bracket.CornersShown, Is.False);
        }

        [Test]
        public void ConfirmDialog_WithNameTable_ShowsResolvedText()
        {
            using var bed = new UiTestBed(new FakeTextResolver().Add("str.common.yes", "예").Add("str.q", "Q?"));
            bed.Stack.Push(ScreenId.ConfirmDialog, ScreenArgs.WithPayload(new ConfirmRequest("str.q")));
            bed.Host.Sync();
            var view = (ConfirmDialogView)bed.Host.ViewOf(ScreenId.ConfirmDialog)!;
            Assert.That(view.Question!.ShownText, Is.EqualTo("Q?"));
            Assert.That(view.ButtonOf(DialogButtonRole.Yes)!.Label.ShownText, Is.EqualTo("예"));
            Assert.That(view.ButtonOf(DialogButtonRole.No)!.Label.ShownText, Is.EqualTo(CommonTextKeys.No));
        }

        [Test]
        public void ModelStateChange_IsRedrawnOnTheNextSync()
        {
            TestMenuView menu = _bed.OpenMenu();
            Assert.That(menu.Middle.SkinState, Is.EqualTo(SkinState.Normal));
            menu.Group!.SetEnabled(menu.Middle.Control, false, "str.why");
            Assert.That(menu.Middle.SkinState, Is.EqualTo(SkinState.Normal), "the view does not change by itself");
            _bed.Host.Sync();
            Assert.That(menu.Middle.SkinState, Is.EqualTo(SkinState.Disabled));
            Assert.That(menu.Middle.Skin.FillColor, Is.EqualTo(UiPalette.DisabledFill));
            Assert.That(menu.Middle.Label.Color, Is.EqualTo(UiPalette.Slate));
        }

        [Test]
        public void Frame_DrivesUiTicks_CursorBracketPulsesEvery15Ticks()
        {
            TestMenuView menu = _bed.OpenMenu();
            Assert.That(_bed.Mode.Mode, Is.EqualTo(InputMode.Keyboard));
            Assert.That(menu.Group!.CursorTarget, Is.SameAs(menu.Left.Control));
            _bed.Now = 1.0;
            _bed.Ui.Driver.Step(1920, 1080);                 // 처음 프레임은 기준만 잡는다
            Assert.That(menu.Left.Bracket.Motion, Is.EqualTo(BracketMotion.Moving));
            Assert.That(menu.Left.Bracket.GapPx, Is.EqualTo(4));
            _bed.Now = 1.25;                                  // 15t → 프레임 1(6px)
            _bed.Ui.Driver.Step(1920, 1080);
            Assert.That(menu.Group.UiTicksSinceCursorMove, Is.EqualTo(15));
            Assert.That(menu.Left.Bracket.GapPx, Is.EqualTo(6));
            _bed.Now = 1.5;                                   // 30t → 다시 프레임 0
            _bed.Ui.Driver.Step(1920, 1080);
            Assert.That(menu.Left.Bracket.GapPx, Is.EqualTo(4));
            Assert.That(_bed.Host.UiTicks, Is.EqualTo(30));
        }

        [Test]
        public void Registry_RefusesDuplicatesAndForeignViews()
        {
            var reg = new ScreenViewRegistry();
            reg.Register(ScreenId.Title, (s, c) => new PlaceholderScreenView(s, c));
            Assert.Throws<InvalidOperationException>(() => reg.Register(ScreenId.Title, (s, c) => new PlaceholderScreenView(s, c)));
            Assert.Throws<ArgumentException>(() => reg.Register(ScreenId.None, (s, c) => new PlaceholderScreenView(s, c)));

            var other = new TestScreen(ScreenId.Title, ScreenLayer.Fullscreen, false);
            reg.Register(ScreenId.Records, (s, c) => new PlaceholderScreenView(other, c));
            var records = new TestScreen(ScreenId.Records, ScreenLayer.Fullscreen, false);
            Assert.Throws<InvalidOperationException>(() => reg.Create(records, _bed.Host.Context));
        }

        [Test]
        public void Dispose_RemovesEverything()
        {
            _bed.OpenMenu();
            _bed.Stack.Push(ScreenId.Records, ScreenArgs.None);
            _bed.Host.Sync();
            GameObject canvas = _bed.Ui.Canvas.GameObject;
            _bed.Ui.Dispose();
            Assert.That(canvas == null, Is.True);
            Assert.That(_bed.Host.Views.Count, Is.EqualTo(0));
            _bed.Stack.Push(ScreenId.Settings, ScreenArgs.None);  // 떨어진 뒤에는 따라가지 않는다
            Assert.That(_bed.Host.Views.Count, Is.EqualTo(0));
        }
    }
}

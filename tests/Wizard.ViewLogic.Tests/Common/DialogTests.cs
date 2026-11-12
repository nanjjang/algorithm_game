using System;
using System.Collections.Generic;
using System.Linq;
using Wizard.DataBuild.Core;
using Wizard.DataBuild.Names;
using Wizard.TestKit;
using Wizard.ViewLogic.Common;
using Wizard.ViewLogic.Common.Dialogs;
using Wizard.ViewLogic.Stack;
using Xunit;

namespace Wizard.ViewLogic.Tests.Common
{
    // spec 13 1.5절 공용 창. 버튼은 누를 수 있는 것의 상태 기계로 돌고, 고르면 스스로 닫고 요청한 쪽에 알린다
    public sealed class DialogTests
    {
        private const string Question = "str.test.question";

        private sealed class Factory : IScreenFactory
        {
            public ScreenStack? Stack;
            public readonly InputModeTracker Mode = new InputModeTracker(InputMode.Keyboard);
            public readonly List<string> Log = new List<string>();

            public IScreen Create(ScreenId id)
            {
                ScreenStack s = Stack!;
                switch (id)
                {
                    case ScreenId.ConfirmDialog: return new ConfirmDialogModel(s, Mode);
                    case ScreenId.Loading: return new LoadingModel(s, Mode);
                    case ScreenId.Disconnected: return new DisconnectedModel(s, Mode);
                    case ScreenId.RunSubmitPending: return new RunSubmitPendingModel(s, Mode);
                    case ScreenId.SaveRestored: return new SaveRestoredModel(s, Mode);
                    case ScreenId.OpsNotice: return new OpsNoticeModel(s, Mode);
                    case ScreenId.SanctionNotice: return new SanctionNoticeModel(s, Mode);
                    case ScreenId.Tooltip: return new TooltipModel(s, Mode);
                    default: return new FakeScreen(id, ScreenLayer.Fullscreen, false, Log);
                }
            }
        }

        private readonly Factory _f = new Factory();
        private readonly ScreenStack _s;

        public DialogTests()
        {
            _s = new ScreenStack(_f);
            _f.Stack = _s;
            _s.Push(ScreenId.MainMenu, ScreenArgs.None);
        }

        private T Open<T>(ScreenId id, object? payload) where T : DialogScreen
        {
            _s.Push(id, payload == null ? ScreenArgs.None : ScreenArgs.WithPayload(payload));
            return (T)(id == ScreenId.RunSubmitPending ? _s.Toasts[_s.Toasts.Count - 1].Screen : _s.Top!);
        }

        private static void PressJ(DialogScreen d)
        {
            d.Group.KeyConfirmDown();
            d.Group.KeyConfirmUp();
        }

        [Fact]
        public void ConfirmYesByKeyboardClosesAndReports()
        {
            var chosen = new List<DialogButtonRole>();
            var d = Open<ConfirmDialogModel>(ScreenId.ConfirmDialog, new ConfirmRequest(Question, new[] { "str.test.d1", "str.test.d2" }, false, chosen.Add));
            Assert.Equal(ScreenLayer.Modal, d.Layer);
            Assert.True(ScreenStack.Blocks(d));
            Assert.Equal(Question, d.QuestionKey);
            Assert.Equal(2, d.DetailKeys.Count);
            Assert.Equal(new[] { DialogButtonRole.Yes, DialogButtonRole.No }, d.Buttons.Select(b => b.Role).ToArray());
            Assert.Equal(new[] { CommonTextKeys.Yes, CommonTextKeys.No }, d.Buttons.Select(b => b.LabelKey).ToArray());
            Assert.Equal(ButtonTone.Normal, d.FindButton(DialogButtonRole.Yes)!.Tone);
            Assert.Same(d.FindButton(DialogButtonRole.Yes)!.Control, d.Group.CursorTarget);

            PressJ(d);
            Assert.Equal(new[] { DialogButtonRole.Yes }, chosen);
            Assert.Equal(DialogButtonRole.Yes, d.Result);
            Assert.Equal(ScreenId.MainMenu, _s.TopId);
            Assert.False(d.IsOpen);
        }

        [Fact]
        public void CautionConfirmUsesAmberYesWithTriangleAndStartsOnNo()
        {
            var d = Open<ConfirmDialogModel>(ScreenId.ConfirmDialog, new ConfirmRequest(Question, null, caution: true));
            DialogButton yes = d.FindButton(DialogButtonRole.Yes)!;
            Assert.Equal(ButtonTone.Caution, yes.Tone);
            Assert.Equal(UiColorTokens.AlertAmber, yes.ToneToken);
            Assert.True(yes.ShowsWarningTriangle);
            Assert.Null(d.FindButton(DialogButtonRole.No)!.ToneToken);
            Assert.Same(d.FindButton(DialogButtonRole.No)!.Control, d.Group.CursorTarget);
        }

        [Fact]
        public void EscapeOnConfirmChoosesNo()
        {
            DialogButtonRole? got = null;
            var d = Open<ConfirmDialogModel>(ScreenId.ConfirmDialog, new ConfirmRequest(Question, onChosen: r => got = r));
            Assert.True(_s.Back());
            Assert.Equal(DialogButtonRole.No, got);
            Assert.Equal(ScreenId.MainMenu, _s.TopId);
            Assert.False(_s.Back());   // 메인 메뉴는 닫히지 않는다
        }

        [Fact]
        public void MouseClickOnNoChoosesNo()
        {
            var d = Open<ConfirmDialogModel>(ScreenId.ConfirmDialog, new ConfirmRequest(Question));
            Interactable no = d.FindButton(DialogButtonRole.No)!.Control;
            d.Group.MouseMove(2, 0, no);
            d.Group.PointerDown(no);
            d.Group.PointerUp(no);
            Assert.Equal(DialogButtonRole.No, d.Result);
        }

        [Fact]
        public void ChooseOnlyOnceAndNotWhenDisabled()
        {
            int calls = 0;
            var d = Open<ConfirmDialogModel>(ScreenId.ConfirmDialog, new ConfirmRequest(Question, onChosen: _ => calls++));
            d.Group.SetEnabled(d.FindButton(DialogButtonRole.Yes)!.Control, false, "str.test.reason");
            Assert.False(d.Choose(DialogButtonRole.Yes));
            Assert.True(d.IsOpen);
            Assert.True(d.Choose(DialogButtonRole.No));
            Assert.False(d.Choose(DialogButtonRole.No));
            Assert.Equal(1, calls);
        }

        [Fact]
        public void ConfirmRequestRejectsMoreThanTwoDetailLinesAndMissingPayload()
        {
            Assert.Throws<ArgumentException>(() => new ConfirmRequest(Question, new[] { "a.b", "a.c", "a.d" }));
            Assert.Throws<ArgumentException>(() => _s.Push(ScreenId.ConfirmDialog, ScreenArgs.None));
        }

        [Fact]
        public void ChoosingAMissingRoleThrows()
        {
            var d = Open<ConfirmDialogModel>(ScreenId.ConfirmDialog, new ConfirmRequest(Question));
            Assert.Throws<ArgumentException>(() => d.Choose(DialogButtonRole.Retry));
        }

        [Fact]
        public void LoadingHasNoButtonsClampsProgressAndIgnoresEscape()
        {
            var d = Open<LoadingModel>(ScreenId.Loading, new LoadingRequest("str.test.hint"));
            Assert.Equal(ScreenLayer.Fullscreen, d.Layer);
            Assert.Empty(d.Buttons);
            Assert.Equal("str.test.hint", d.HintKey);
            Assert.Equal(0f, d.Progress);
            d.SetProgress(0.5f);
            Assert.Equal(0.5f, d.Progress);
            d.SetProgress(0.25f);
            Assert.Equal(0.5f, d.Progress);   // 뒤로 물러나지 않는다
            d.SetProgress(float.NaN);
            Assert.Equal(0.5f, d.Progress);
            d.SetProgress(7f);
            Assert.Equal(1f, d.Progress);
            Assert.True(_s.Back());
            Assert.Same(d, _s.Top);
            d.SetHint("str.test.hint2");
            Assert.Equal("str.test.hint2", d.HintKey);
        }

        [Fact]
        public void DisconnectedCountsDownAndReportsRetry()
        {
            DialogButtonRole? got = null;
            var d = Open<DisconnectedModel>(ScreenId.Disconnected, new DisconnectedRequest(61, r => got = r));
            Assert.Equal(new[] { DialogButtonRole.Retry, DialogButtonRole.SwitchToOffline }, d.Buttons.Select(b => b.Role).ToArray());
            Assert.Equal(CommonTextKeys.DisconnectedMessage, d.MessageKey);
            Assert.Equal(CommonTextKeys.DisconnectedRunNote, d.RunNoteKey);
            Assert.True(d.IsReconnecting);
            Assert.Equal(2, d.ReconnectRemainingSeconds);   // 61t → 올림 2초
            d.Tick();
            Assert.Equal(1, d.ReconnectRemainingSeconds);
            for (int i = 0; i < 60; i++)
            {
                d.Tick();
            }
            Assert.False(d.IsReconnecting);
            Assert.Equal(0, d.ReconnectRemainingSeconds);
            d.Tick();
            Assert.Equal(0, d.ReconnectRemainingUiTicks);

            Assert.True(_s.Back());   // 취소 버튼이 없다: 소비만
            Assert.Same(d, _s.Top);
            d.SetReconnecting(-5);
            Assert.False(d.IsReconnecting);

            PressJ(d);
            Assert.Equal(DialogButtonRole.Retry, got);
            Assert.Equal(ScreenId.MainMenu, _s.TopId);
        }

        [Fact]
        public void DisconnectedSwitchToOfflineByCursor()
        {
            DialogButtonRole? got = null;
            var d = Open<DisconnectedModel>(ScreenId.Disconnected, new DisconnectedRequest(0, r => got = r));
            Assert.False(d.IsReconnecting);
            d.Group.MoveCursorBy(1);
            PressJ(d);
            Assert.Equal(DialogButtonRole.SwitchToOffline, got);
        }

        [Fact]
        public void RunSubmitPendingIsAToastWithoutInput()
        {
            var d = Open<RunSubmitPendingModel>(ScreenId.RunSubmitPending, null);
            Assert.Equal(ScreenLayer.Toast, d.Layer);
            Assert.False(d.BlocksInput);
            Assert.Empty(d.Buttons);
            Assert.Equal(CommonTextKeys.RunSubmitPendingMessage, d.MessageKey);
            Assert.Equal(ScreenId.MainMenu, _s.TopId);
            Assert.False(_s.ReceivesInput(d));
            Assert.True(_s.DismissToast(ScreenId.RunSubmitPending));
            Assert.False(d.IsOpen);
        }

        [Fact]
        public void SaveRestoredAcceptsOneToThreeGenerationsAndEscapeIsOk()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SaveRestoredRequest(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SaveRestoredRequest(4));
            DialogButtonRole? got = null;
            var d = Open<SaveRestoredModel>(ScreenId.SaveRestored, new SaveRestoredRequest(2, r => got = r));
            Assert.Equal(2, d.GenerationsBack);
            Assert.Equal(CommonTextKeys.SaveRestoredMessage, d.MessageKey);
            Assert.Equal(new[] { DialogButtonRole.Ok }, d.Buttons.Select(b => b.Role).ToArray());
            Assert.True(_s.Back());
            Assert.Equal(DialogButtonRole.Ok, got);
        }

        [Fact]
        public void OpsNoticeScrollsWithinItsLinesAndCloses()
        {
            var lines = Enumerable.Range(1, 5).Select(i => "str.test.line" + i).ToArray();
            DialogButtonRole? got = null;
            var d = Open<OpsNoticeModel>(ScreenId.OpsNotice, new OpsNoticeRequest(lines, 3, r => got = r));
            Assert.Equal(2, d.MaxScrollOffset);
            Assert.False(d.CanScrollUp);
            Assert.True(d.CanScrollDown);
            d.ScrollBy(10);
            Assert.Equal(2, d.ScrollOffset);
            Assert.False(d.CanScrollDown);
            d.ScrollBy(-1);
            Assert.Equal(1, d.ScrollOffset);
            d.ScrollBy(-10);
            Assert.Equal(0, d.ScrollOffset);
            Assert.Throws<ArgumentOutOfRangeException>(() => new OpsNoticeRequest(lines, 0));
            Assert.True(_s.Back());
            Assert.Equal(DialogButtonRole.Close, got);

            var shortNotice = Open<OpsNoticeModel>(ScreenId.OpsNotice, new OpsNoticeRequest(new[] { "str.test.one" }, 3));
            Assert.Equal(0, shortNotice.MaxScrollOffset);
            shortNotice.ScrollBy(1);
            Assert.Equal(0, shortNotice.ScrollOffset);
        }

        [Fact]
        public void SanctionNoticeShowsFeaturesDurationAndReason()
        {
            var d = Open<SanctionNoticeModel>(ScreenId.SanctionNotice,
                new SanctionNoticeRequest(new[] { "str.test.trade" }, 3600, "str.test.reason"));
            Assert.Equal(new[] { "str.test.trade" }, d.BlockedFeatureKeys);
            Assert.Equal(3600L, d.RemainingSeconds);
            Assert.Null(d.PermanentKey);
            Assert.Equal("str.test.reason", d.ReasonKey);
            PressJ(d);
            Assert.Equal(DialogButtonRole.Ok, d.Result);

            var forever = Open<SanctionNoticeModel>(ScreenId.SanctionNotice,
                new SanctionNoticeRequest(new[] { "str.test.trade" }, null, "str.test.reason"));
            Assert.Equal(CommonTextKeys.SanctionPermanent, forever.PermanentKey);
            Assert.Throws<ArgumentOutOfRangeException>(() => new SanctionNoticeRequest(new string[0], -1, "str.test.reason"));
        }

        [Fact]
        public void TooltipIsANonBlockingPanelClosedByEscape()
        {
            var d = Open<TooltipModel>(ScreenId.Tooltip, new TooltipRequest("wand.1", "rare_blue", "str.test.kind", "element.ice",
                new[] { "str.test.v1" }, "str.test.desc", tradable: false));
            Assert.Equal(ScreenLayer.Panel, d.Layer);
            Assert.False(d.BlocksInput);
            Assert.True(_s.ReceivesInput(_s.Entries[0].Screen));   // 아래 메인 메뉴도 입력을 받는다
            Assert.Equal(CommonTextKeys.TooltipUntradable, d.TradableKey);
            Assert.Equal("wand.1", d.Content!.NameKey);
            Assert.True(_s.Back());
            Assert.Equal(ScreenId.MainMenu, _s.TopId);

            var item = Open<TooltipModel>(ScreenId.Tooltip, new TooltipRequest("item.x", tradable: true));
            Assert.Equal(CommonTextKeys.TooltipTradable, item.TradableKey);
            var plain = Open<TooltipModel>(ScreenId.Tooltip, new TooltipRequest("str.test.plain"));
            Assert.Null(plain.TradableKey);
        }

        [Fact]
        public void TooltipLinesAreFittedTo400px()
        {
            Assert.Equal(400, TooltipModel.MaxWidthPx);
            string longLine = new string('가', 30);   // 16px 이면 480px
            string fit = TooltipModel.FitLine(longLine, 16);
            Assert.True(UiText.WidthPx(fit, 16) <= 400);
            Assert.EndsWith("…", fit);
            Assert.Equal("짧은 줄", TooltipModel.FitLine("짧은 줄", 16));
        }

        [Fact]
        public void DialogButtonsUseMediumBrackets()
        {
            var d = Open<ConfirmDialogModel>(ScreenId.ConfirmDialog, new ConfirmRequest(Question));
            Assert.All(d.Buttons, b => Assert.Equal(BracketSize.Medium, b.Control.BracketSize));
            Assert.All(d.Buttons, b => Assert.True(b.Control.WidthPx >= UiLayout.MinHitSizePx && b.Control.HeightPx >= UiLayout.MinHitSizePx));
            Assert.All(d.Buttons, b => Assert.True(UiLayout.IsOnGrid(b.Control.WidthPx) && UiLayout.IsOnGrid(b.Control.HeightPx)));
        }

        [Fact]
        public void EveryCommonTextKeyIsInTheNameTable()
        {
            var diagnostics = new Diagnostics();
            NamesFile? names = NamesFile.LoadFromRepo(TestRepo.Root, diagnostics);
            Assert.NotNull(names);
            foreach (string key in CommonTextKeys.All)
            {
                Assert.True(names!.Contains(key), key + " is missing in " + NamesFile.RepoPath);
            }
            Assert.Equal(CommonTextKeys.All.Length, CommonTextKeys.All.Distinct().Count());
        }
    }
}

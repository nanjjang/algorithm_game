using System.Collections.Generic;
using NUnit.Framework;
using Wizard.Solo.UI;

namespace Wizard.Solo.Tests.UI
{
    /// <summary>화면 스택 규칙(08 요약서 1.1절, 00 문서 8절 전이).</summary>
    public class UiScreenStackTests
    {
        static FakeProvider Provider() => new FakeProvider()
            .Add(ScreenId.Title, ScreenLayer.Fullscreen, true)
            .Add(ScreenId.MainMenu, ScreenLayer.Fullscreen, true)
            .Add(ScreenId.ExpeditionPrep, ScreenLayer.Fullscreen, true)
            .Add(ScreenId.Battle, ScreenLayer.Fullscreen, false)
            .Add(ScreenId.LootPick, ScreenLayer.Panel, true)
            .Add(ScreenId.Camp, ScreenLayer.Panel, true)
            .Add(ScreenId.Pause, ScreenLayer.Panel, true)
            .Add(ScreenId.Settlement, ScreenLayer.Fullscreen, true)
            .Add(ScreenId.WandPicker, ScreenLayer.Fullscreen, true)
            .Add(ScreenId.Enhance, ScreenLayer.Fullscreen, true)
            .Add(ScreenId.Loading, ScreenLayer.Fullscreen, true)
            .Add(ScreenId.ConfirmDialog, ScreenLayer.Modal, true)
            .Add(ScreenId.Tooltip, ScreenLayer.Toast, false)
            .Add(ScreenId.Settings, ScreenLayer.Fullscreen, true);

        [Test]
        public void PushPopReplace_LifecycleOrder()
        {
            var p = Provider();
            var st = p.NewStack();
            st.Push(ScreenId.Title);
            st.Push(ScreenId.MainMenu);
            Assert.AreEqual(ScreenId.MainMenu, st.Top);
            Assert.AreEqual(2, st.Count);
            st.Replace(ScreenId.ExpeditionPrep);
            Assert.AreEqual(ScreenId.ExpeditionPrep, st.Top);
            Assert.AreEqual(2, st.Count);
            st.Pop();
            Assert.AreEqual(ScreenId.Title, st.Top);
            CollectionAssert.AreEqual(new[]
            {
                "enter Title", "enter MainMenu", "exit MainMenu", "enter ExpeditionPrep", "exit ExpeditionPrep"
            }, p.Log);
        }

        [Test]
        public void Push_DefaultEntryIsPreviousTop()
        {
            var p = Provider();
            var st = p.NewStack();
            st.Push(ScreenId.MainMenu);
            st.Push(ScreenId.WandPicker);
            Assert.AreEqual(ScreenId.MainMenu, p.Last(ScreenId.WandPicker).LastArgs.Entry);
        }

        [Test]
        public void Replace_OnEmptyPushes()
        {
            var p = Provider();
            var st = p.NewStack();
            st.Replace(ScreenId.Title);
            Assert.AreEqual(ScreenId.Title, st.Top);
            Assert.AreEqual(1, st.Count);
        }

        [Test]
        public void Unregistered_IsIgnoredWithWarning()
        {
            var p = Provider();
            var st = p.NewStack();
            st.Push(ScreenId.Title);
            st.Push(ScreenId.Shop);
            st.Replace(ScreenId.Records);
            Assert.AreEqual(ScreenId.Title, st.Top);
            Assert.AreEqual(1, st.Count);
            Assert.AreEqual(2, p.Warnings.Count);
            st.Push(ScreenId.None);
            Assert.AreEqual(3, p.Warnings.Count);
        }

        [Test]
        public void Pop_EmptyIsNoOp()
        {
            var p = Provider();
            var st = p.NewStack();
            Assert.DoesNotThrow(() => st.Pop());
            Assert.AreEqual(ScreenId.None, st.Top);
            Assert.AreEqual(1, p.Warnings.Count);
        }

        [Test]
        public void PopTo_UnwindsOrOpensFresh()
        {
            var p = Provider();
            var st = p.NewStack();
            st.Push(ScreenId.MainMenu);
            st.Push(ScreenId.ExpeditionPrep);
            st.Push(ScreenId.Loading);
            st.PopTo(ScreenId.MainMenu);
            Assert.AreEqual(ScreenId.MainMenu, st.Top);
            Assert.AreEqual(1, st.Count);
            // 쌓여 있지 않으면 전부 닫고 연다
            st.Push(ScreenId.Battle);
            st.PopTo(ScreenId.Settlement);
            Assert.AreEqual(ScreenId.Settlement, st.Top);
            Assert.AreEqual(1, st.Count);
            Assert.AreEqual(1, p.Last(ScreenId.MainMenu).Exits);
        }

        [Test]
        public void Modal_BlocksInputAndTicksStopThere()
        {
            var p = Provider();
            var st = p.NewStack();
            st.Push(ScreenId.Battle);
            st.Push(ScreenId.LootPick);
            var recv = new List<IScreen>();
            st.GetInputReceivers(recv);
            Assert.AreEqual(1, recv.Count, "LootPick blocks input");
            st.Pop();
            st.Push(ScreenId.ConfirmDialog);
            st.GetInputReceivers(recv);
            Assert.AreEqual(1, recv.Count);
            Assert.AreEqual(ScreenId.ConfirmDialog, recv[0].Id);
            var ticks = new List<IScreen>();
            st.GetTickReceivers(ticks);
            Assert.AreEqual(1, ticks.Count);
            Assert.IsTrue(st.IsVisible(p.Last(ScreenId.Battle)), "Battle stays visible under the modal");
        }

        [Test]
        public void NonBlockingPanel_PassesInputAndTickDown()
        {
            var p = new FakeProvider().Add(ScreenId.Battle, ScreenLayer.Fullscreen).Add(ScreenId.ControlsView, ScreenLayer.HudOverlay);
            var st = p.NewStack();
            st.Push(ScreenId.Battle);
            st.Push(ScreenId.ControlsView);
            var recv = new List<IScreen>();
            st.GetInputReceivers(recv);
            Assert.AreEqual(2, recv.Count);
            var ticks = new List<IScreen>();
            st.GetTickReceivers(ticks);
            Assert.AreEqual(2, ticks.Count);
        }

        [Test]
        public void Visibility_StopsAtFirstFullscreen()
        {
            var p = Provider();
            var st = p.NewStack();
            st.Push(ScreenId.Battle);
            st.Push(ScreenId.Pause);
            st.Push(ScreenId.Settings);
            Assert.IsFalse(st.IsVisibleAt(0));
            Assert.IsFalse(st.IsVisibleAt(1), "Pause panel hidden under Settings fullscreen");
            Assert.IsTrue(st.IsVisibleAt(2));
            st.Pop();
            Assert.IsTrue(st.IsVisibleAt(0));
            Assert.IsTrue(st.IsVisibleAt(1));
        }

        // 08 1.1절 / 00 8.1절: 진입점이 여럿인 화면은 같은 화면 하나이고 ReturnTo로 돌아갈 곳을 고른다
        [Test]
        public void ReturnTo_FromSettlementEnhanceEscGoesToMainMenu()
        {
            var p = Provider();
            var st = p.NewStack();
            st.Push(ScreenId.MainMenu);
            st.Push(ScreenId.Battle);
            st.Replace(ScreenId.Settlement);
            st.Push(ScreenId.Enhance, ScreenArgs.From(ScreenId.Settlement).With(ScreenArgKeys.ReturnTo, ScreenId.MainMenu));
            Assert.AreEqual(ScreenId.MainMenu, st.ReturnTarget);
            Assert.IsTrue(st.Back());
            Assert.AreEqual(ScreenId.MainMenu, st.Top);
            Assert.AreEqual(1, st.Count);
            Assert.AreEqual(1, p.Last(ScreenId.Settlement).Exits);
        }

        [Test]
        public void ReturnTo_NotInStackClearsAndOpensTarget()
        {
            var p = Provider();
            var st = p.NewStack();
            st.Push(ScreenId.Settlement);
            st.Push(ScreenId.WandPicker, ScreenArgs.From(ScreenId.Settlement).With(ScreenArgKeys.ReturnTo, ScreenId.MainMenu));
            st.Pop();
            Assert.AreEqual(ScreenId.MainMenu, st.Top);
            Assert.AreEqual(1, st.Count);
            Assert.AreEqual(ScreenId.WandPicker, p.Last(ScreenId.MainMenu).LastArgs.Entry);
        }

        [Test]
        public void ReturnTo_FromMainMenuIsPlainPop()
        {
            var p = Provider();
            var st = p.NewStack();
            st.Push(ScreenId.MainMenu);
            st.Push(ScreenId.WandPicker);
            st.Push(ScreenId.Enhance);
            Assert.AreEqual(ScreenId.WandPicker, st.ReturnTarget);
            st.Back();
            Assert.AreEqual(ScreenId.WandPicker, st.Top);
        }

        [Test]
        public void Back_ConsumedByScreenOrBottomStays()
        {
            var p = Provider();
            var st = p.NewStack();
            st.Push(ScreenId.MainMenu);
            Assert.IsFalse(st.Back(), "bottom screen is not closed");
            Assert.AreEqual(ScreenId.MainMenu, st.Top);
            st.Push(ScreenId.Battle);
            p.Last(ScreenId.Battle).BackResult = true;
            Assert.IsTrue(st.Back());
            Assert.AreEqual(ScreenId.Battle, st.Top, "consumed Esc keeps the screen");
            p.Last(ScreenId.Battle).BackResult = false;
            Assert.IsTrue(st.Back());
            Assert.AreEqual(ScreenId.MainMenu, st.Top);
        }

        [Test]
        public void Back_WhenOnBackPushesPauseDoesNotPop()
        {
            // BattleScreen.OnBack은 Pause를 Push하고 true를 준다(00 4.1절). Esc 한 번에 Pause가 두 번 쌓이거나 닫히지 않는다.
            var p = Provider();
            var st = p.NewStack();
            st.Push(ScreenId.MainMenu);
            st.Push(ScreenId.Battle);
            var battle = p.Last(ScreenId.Battle);
            battle.BackResult = true;
            battle.OnBackAction = () => st.Push(ScreenId.Pause);
            st.Back();
            Assert.AreEqual(ScreenId.Pause, st.Top);
            Assert.AreEqual(3, st.Count);
            // Pause의 Esc(false) = Pop → Battle
            st.Back();
            Assert.AreEqual(ScreenId.Battle, st.Top);
        }

        [Test]
        public void ToastLayer_VisibleOverFullscreenTop()
        {
            // 08 요약서 1.1절(층 순서: Toast가 맨 위)·13절(토스트 쪽지는 오른쪽 위에 내려온다): 토스트·툴팁은 어느 화면 위에든 뜬다.
            // 맨 위가 Fullscreen(메인 메뉴)이어도 보여야 한다(통합 단계의 스크린숏에서 안 보이던 것).
            var p = Provider();
            var st = p.NewStack();
            st.Push(ScreenId.MainMenu);
            st.Push(ScreenId.Tooltip, ScreenArgs.From(ScreenId.MainMenu).With(ScreenArgKeys.Text, "x"));
            Assert.AreEqual(1, st.Toasts.Count);
            Assert.IsTrue(st.IsVisible(st.Toasts[0].Screen), "toast over a fullscreen top");
            st.Push(ScreenId.Settings);
            Assert.IsTrue(st.IsVisible(st.Toasts[0].Screen), "toast stays visible when another fullscreen opens");
            Assert.IsFalse(st.IsVisible(st.Entries[0].Screen), "main menu is covered by settings");
        }

        [Test]
        public void Top_IgnoresToastLayer()
        {
            var p = Provider();
            var st = p.NewStack();
            st.Push(ScreenId.Battle);
            st.Push(ScreenId.Tooltip, ScreenArgs.From(ScreenId.Battle).With(ScreenArgKeys.Text, "x"));
            Assert.AreEqual(ScreenId.Battle, st.Top, "battle input gate stays open");
            Assert.AreEqual(1, st.Count);
            Assert.AreEqual(1, st.Toasts.Count);
            Assert.IsTrue(st.IsOpen(ScreenId.Tooltip));
            // Pop은 토스트를 닫지 않는다
            st.Push(ScreenId.Pause);
            st.Pop();
            Assert.AreEqual(1, st.Toasts.Count);
            Assert.IsTrue(st.DismissToast(ScreenId.Tooltip));
            Assert.AreEqual(0, st.Toasts.Count);
            Assert.IsFalse(st.IsOpen(ScreenId.Tooltip));
            // 토스트는 언제나 Tick 받는다
            st.Push(ScreenId.Tooltip);
            st.Push(ScreenId.ConfirmDialog);
            var ticks = new List<IScreen>();
            st.GetTickReceivers(ticks);
            Assert.AreEqual(2, ticks.Count);
            Assert.AreEqual(ScreenId.Tooltip, ticks[1].Id);
        }

        [Test]
        public void Pop_ResultGoesToOnResultAfterExit()
        {
            var p = Provider();
            var st = p.NewStack();
            st.Push(ScreenId.MainMenu);
            object got = null;
            ScreenId topAtResult = ScreenId.None;
            var args = ScreenArgs.From(ScreenId.MainMenu);
            args.OnResult = r => { got = r; topAtResult = st.Top; };
            st.Push(ScreenId.ConfirmDialog, args);
            st.Pop(true);
            Assert.AreEqual(true, got);
            Assert.AreEqual(ScreenId.MainMenu, topAtResult);
        }

        [Test]
        public void OnResult_CanPushNewScreen()
        {
            var p = Provider();
            var st = p.NewStack();
            st.Push(ScreenId.MainMenu);
            var args = ScreenArgs.From(ScreenId.MainMenu);
            args.OnResult = r => { if (r is bool b && b) st.Push(ScreenId.Settings); };
            st.Push(ScreenId.ConfirmDialog, args);
            st.Pop(true);
            Assert.AreEqual(ScreenId.Settings, st.Top);
            Assert.AreEqual(2, st.Count);
        }

        [Test]
        public void Dismiss_MiddleEntryOnly()
        {
            var p = Provider();
            var st = p.NewStack();
            st.Push(ScreenId.MainMenu);
            st.Push(ScreenId.Settings);
            st.Push(ScreenId.ConfirmDialog);
            Assert.IsTrue(st.Dismiss(p.Last(ScreenId.Settings)));
            Assert.AreEqual(ScreenId.ConfirmDialog, st.Top);
            Assert.AreEqual(2, st.Count);
            Assert.IsFalse(st.Dismiss(p.Last(ScreenId.Settings)));
        }

        [Test]
        public void Clear_ExitsAllTopFirst()
        {
            var p = Provider();
            var st = p.NewStack();
            st.Push(ScreenId.MainMenu);
            st.Push(ScreenId.Settings);
            st.Push(ScreenId.Tooltip);
            p.Log.Clear();
            st.Clear();
            CollectionAssert.AreEqual(new[] { "exit Tooltip", "exit Settings", "exit MainMenu" }, p.Log);
            Assert.AreEqual(0, st.Count);
            Assert.AreEqual(ScreenId.None, st.Top);
        }

        [Test]
        public void Replace_ViaLoadingCarriesNextArgs()
        {
            var p = Provider();
            var st = p.NewStack();
            st.Push(ScreenId.ExpeditionPrep);
            var next = ScreenArgs.From(ScreenId.ExpeditionPrep).With(ScreenArgKeys.Character, 3);
            st.Push(ScreenId.Loading, ScreenArgs.ViaLoading(ScreenId.ExpeditionPrep, ScreenId.Battle, next));
            var load = p.Last(ScreenId.Loading);
            var flow = new UiLoadingFlow();
            flow.Begin(load.LastArgs);
            Assert.AreEqual(ScreenId.Battle, flow.Next);
            st.Replace(flow.Next, flow.NextArgs);
            Assert.AreEqual(ScreenId.Battle, st.Top);
            Assert.AreEqual(2, st.Count);
            Assert.AreEqual(3, p.Last(ScreenId.Battle).LastArgs.Get(ScreenArgKeys.Character, 0));
        }

        [Test]
        public void SameScreenTwice_ProviderGivesSeparateInstances()
        {
            var p = Provider();
            var st = p.NewStack();
            st.Push(ScreenId.MainMenu);
            st.Push(ScreenId.ConfirmDialog);
            st.Push(ScreenId.ConfirmDialog);
            Assert.AreEqual(3, st.Count);
            st.Pop();
            Assert.AreEqual(ScreenId.ConfirmDialog, st.Top);
            st.Pop();
            Assert.AreEqual(ScreenId.MainMenu, st.Top);
        }
    }
}

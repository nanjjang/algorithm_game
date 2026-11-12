using System;
using System.Collections.Generic;
using System.Linq;
using Wizard.ViewLogic.Stack;
using Xunit;

namespace Wizard.ViewLogic.Tests.Common
{
    // spec 13 1.1절 화면 관리자: Push/Replace/Pop, ReturnTarget, 층과 입력 가로막기. 흐름 예는 spec 13 4.2, 9.1, 10, 15.3절
    public sealed class ScreenStackTests
    {
        private readonly FakeScreenFactory _f = new FakeScreenFactory()
            .Kind(ScreenId.MainMenu, ScreenLayer.Fullscreen)
            .Kind(ScreenId.WandPicker, ScreenLayer.Fullscreen)
            .Kind(ScreenId.Enhance, ScreenLayer.Fullscreen)
            .Kind(ScreenId.EnhanceTutorial, ScreenLayer.Modal)
            .Kind(ScreenId.PlazaOverlay, ScreenLayer.HudOverlay)
            .Kind(ScreenId.PlazaMenu, ScreenLayer.Panel, blocks: true)
            .Kind(ScreenId.BattleHud, ScreenLayer.HudOverlay)
            .Kind(ScreenId.LootPick, ScreenLayer.Panel, blocks: false)
            .Kind(ScreenId.Pause, ScreenLayer.Modal, blocks: false)
            .Kind(ScreenId.Tooltip, ScreenLayer.Panel, blocks: false)
            .Kind(ScreenId.RunSubmitPending, ScreenLayer.Toast)
            .Kind(ScreenId.Settlement, ScreenLayer.Fullscreen)
            .Kind(ScreenId.Title, ScreenLayer.Fullscreen);

        private ScreenStack NewStack() => new ScreenStack(_f);

        private static ScreenId[] Ids(ScreenStack s) => s.Entries.Select(e => e.Id).ToArray();

        [Fact]
        public void PushStacksAndRemembersTheScreenBelowAsReturnTarget()
        {
            ScreenStack s = NewStack();
            Assert.Equal(ScreenId.None, s.TopId);
            Assert.Null(s.Top);
            s.Push(ScreenId.MainMenu, ScreenArgs.None);
            Assert.Equal(ScreenId.None, s.ReturnTarget);
            s.Push(ScreenId.WandPicker, ScreenArgs.From(ScreenEntry.MainMenu));
            Assert.Equal(new[] { ScreenId.MainMenu, ScreenId.WandPicker }, Ids(s));
            Assert.Equal(ScreenId.WandPicker, s.TopId);
            Assert.Equal(ScreenId.MainMenu, s.ReturnTarget);
            Assert.Equal(0, _f.Last(ScreenId.MainMenu).Exits);   // 덮여도 닫히지 않는다
            Assert.Equal(new[] { "enter MainMenu", "enter WandPicker" }, _f.Log);
        }

        [Fact]
        public void EnhanceFromMainMenuReturnsToMainMenu()
        {
            // spec 13 4.2: 메뉴 "강화" → WandPicker → Enhance(entry=MainMenu). 9.1: Esc → 들어온 곳으로
            ScreenStack s = NewStack();
            s.Push(ScreenId.MainMenu, ScreenArgs.None);
            s.Push(ScreenId.WandPicker, ScreenArgs.From(ScreenEntry.MainMenu));
            s.Replace(ScreenId.Enhance, ScreenArgs.From(ScreenEntry.MainMenu));
            Assert.Equal(new[] { ScreenId.MainMenu, ScreenId.Enhance }, Ids(s));
            Assert.Equal(1, _f.Last(ScreenId.WandPicker).Exits);
            Assert.Equal(ScreenEntry.MainMenu, _f.Last(ScreenId.Enhance).EnteredWith!.Value.Entry);
            Assert.Equal(ScreenId.MainMenu, s.ReturnTarget);
            Assert.True(s.Back());
            Assert.Equal(new[] { ScreenId.MainMenu }, Ids(s));
            Assert.Equal(1, _f.Last(ScreenId.Enhance).Exits);
        }

        [Fact]
        public void EnhanceFromPlazaMenuReturnsToThePlaza()
        {
            // spec 13 15.3: 광장 메뉴 "강화" → WandPicker → Enhance(entry=PlazaMenu) (나가면 광장으로)
            ScreenStack s = NewStack();
            s.Push(ScreenId.PlazaOverlay, ScreenArgs.None);
            s.Push(ScreenId.PlazaMenu, ScreenArgs.None);
            s.Push(ScreenId.WandPicker, ScreenArgs.From(ScreenEntry.PlazaMenu));
            s.Replace(ScreenId.Enhance, ScreenArgs.From(ScreenEntry.PlazaMenu).WithReturnTarget(ScreenId.PlazaOverlay));
            Assert.Equal(ScreenId.PlazaOverlay, s.ReturnTarget);
            // 강화 화면은 광장을 덮는다(spec 13 9.1)
            var visible = new List<IScreen>();
            s.GetVisible(visible);
            Assert.Equal(new[] { ScreenId.Enhance }, visible.Select(v => v.Id).ToArray());

            s.Pop();
            Assert.Equal(new[] { ScreenId.PlazaOverlay }, Ids(s));
            Assert.Equal(1, _f.Last(ScreenId.PlazaMenu).Exits);
            // 위에서부터 닫는다
            Assert.Equal(new[] { "exit WandPicker", "enter Enhance", "exit Enhance", "exit PlazaMenu" },
                _f.Log.Skip(3).ToArray());
        }

        [Fact]
        public void SameScreenTwoEntriesIsOneScreenId()
        {
            ScreenStack s = NewStack();
            s.Push(ScreenId.Enhance, ScreenArgs.From(ScreenEntry.MainMenu));
            s.Push(ScreenId.Enhance, ScreenArgs.From(ScreenEntry.PlazaMenu));
            FakeScreen[] both = _f.Created.Where(c => c.Id == ScreenId.Enhance).ToArray();
            Assert.Equal(2, both.Length);
            Assert.Equal(ScreenEntry.MainMenu, both[0].EnteredWith!.Value.Entry);
            Assert.Equal(ScreenEntry.PlazaMenu, both[1].EnteredWith!.Value.Entry);
        }

        [Fact]
        public void ReturnTargetMissingFromTheStackIsOpenedOnPop()
        {
            // spec 13 10절: 정산 3쪽 [강화 화면으로] → Enhance(entry=MainMenu) [임시: 돌아올 곳 = 메인 메뉴]
            ScreenStack s = NewStack();
            s.Push(ScreenId.Settlement, ScreenArgs.None);
            s.Replace(ScreenId.Enhance, ScreenArgs.From(ScreenEntry.MainMenu).WithReturnTarget(ScreenId.MainMenu));
            Assert.Equal(ScreenId.MainMenu, s.ReturnTarget);
            s.Pop();
            Assert.Equal(new[] { ScreenId.MainMenu }, Ids(s));
            Assert.Equal(new[] { "enter Settlement", "exit Settlement", "enter Enhance", "exit Enhance", "enter MainMenu" }, _f.Log);
        }

        [Fact]
        public void BackAtTheRootWithExplicitReturnTargetStillLeaves()
        {
            ScreenStack s = NewStack();
            s.Push(ScreenId.Enhance, ScreenArgs.From(ScreenEntry.MainMenu).WithReturnTarget(ScreenId.MainMenu));
            Assert.True(s.Back());
            Assert.Equal(new[] { ScreenId.MainMenu }, Ids(s));
        }

        [Fact]
        public void ReplaceKeepsTheReturnTargetOfTheSlot()
        {
            ScreenStack s = NewStack();
            s.Replace(ScreenId.Title, ScreenArgs.None);   // 빈 스택의 Replace = Push
            Assert.Equal(new[] { ScreenId.Title }, Ids(s));
            Assert.Equal(ScreenId.None, s.ReturnTarget);
            s.Replace(ScreenId.MainMenu, ScreenArgs.None);
            Assert.Equal(new[] { ScreenId.MainMenu }, Ids(s));
            Assert.Equal(ScreenId.None, s.ReturnTarget);
            Assert.Equal(new[] { "enter Title", "exit Title", "enter MainMenu" }, _f.Log);
        }

        [Fact]
        public void PopOnEmptyStackThrows()
        {
            Assert.Throws<InvalidOperationException>(() => NewStack().Pop());
        }

        [Fact]
        public void BackAsksTheFocusedScreenFirst()
        {
            ScreenStack s = NewStack();
            s.Push(ScreenId.BattleHud, ScreenArgs.None);
            s.Push(ScreenId.Pause, ScreenArgs.None);
            _f.Last(ScreenId.Pause).ConsumeBack = true;
            Assert.True(s.Back());
            Assert.Equal(ScreenId.Pause, s.TopId);
            _f.Last(ScreenId.Pause).ConsumeBack = false;
            Assert.True(s.Back());
            Assert.Equal(ScreenId.BattleHud, s.TopId);
            // 돌아갈 곳이 없는 맨 아래 화면은 닫지 않는다
            Assert.False(s.Back());
            Assert.Equal(ScreenId.BattleHud, s.TopId);
            Assert.False(NewStack().Back());
        }

        [Fact]
        public void FullscreenCoversEverythingBelow()
        {
            ScreenStack s = NewStack();
            s.Push(ScreenId.PlazaOverlay, ScreenArgs.None);
            s.Push(ScreenId.MainMenu, ScreenArgs.None);
            s.Push(ScreenId.Tooltip, ScreenArgs.None);
            var visible = new List<IScreen>();
            s.GetVisible(visible);
            Assert.Equal(new[] { ScreenId.MainMenu, ScreenId.Tooltip }, visible.Select(v => v.Id).ToArray());
            Assert.False(s.IsVisible(_f.Last(ScreenId.PlazaOverlay)));
            var input = new List<IScreen>();
            s.GetInputReceivers(input);
            Assert.Equal(new[] { ScreenId.Tooltip, ScreenId.MainMenu }, input.Select(v => v.Id).ToArray());
            Assert.False(s.ReceivesInput(_f.Last(ScreenId.PlazaOverlay)));
        }

        [Fact]
        public void PanelsShowTheScreenBelowAndPassInputUnlessTheyBlock()
        {
            ScreenStack s = NewStack();
            s.Push(ScreenId.BattleHud, ScreenArgs.None);
            s.Push(ScreenId.LootPick, ScreenArgs.None);   // 가로막지 않는 판
            var list = new List<IScreen>();
            s.GetVisible(list);
            Assert.Equal(new[] { ScreenId.BattleHud, ScreenId.LootPick }, list.Select(v => v.Id).ToArray());
            s.GetInputReceivers(list);
            Assert.Equal(new[] { ScreenId.LootPick, ScreenId.BattleHud }, list.Select(v => v.Id).ToArray());
            Assert.Same(_f.Last(ScreenId.LootPick), s.Focus);

            s.Pop();
            s.Push(ScreenId.PlazaMenu, ScreenArgs.None);   // 가로막는 판
            s.GetInputReceivers(list);
            Assert.Equal(new[] { ScreenId.PlazaMenu }, list.Select(v => v.Id).ToArray());
            s.GetVisible(list);
            Assert.Equal(new[] { ScreenId.BattleHud, ScreenId.PlazaMenu }, list.Select(v => v.Id).ToArray());
        }

        [Fact]
        public void ModalAlwaysBlocksInputEvenIfItSaysOtherwise()
        {
            ScreenStack s = NewStack();
            s.Push(ScreenId.BattleHud, ScreenArgs.None);
            s.Push(ScreenId.Pause, ScreenArgs.None);   // Kind: Modal, BlocksInput = false
            var list = new List<IScreen>();
            s.GetInputReceivers(list);
            Assert.Equal(new[] { ScreenId.Pause }, list.Select(v => v.Id).ToArray());
            Assert.True(ScreenStack.Blocks(_f.Last(ScreenId.Pause)));
            s.GetVisible(list);
            Assert.Equal(new[] { ScreenId.BattleHud, ScreenId.Pause }, list.Select(v => v.Id).ToArray());
        }

        [Fact]
        public void ToastsFloatAboveAndNeverTakeInputOrBack()
        {
            ScreenStack s = NewStack();
            s.Push(ScreenId.MainMenu, ScreenArgs.None);
            s.Push(ScreenId.RunSubmitPending, ScreenArgs.None);
            Assert.Equal(new[] { ScreenId.MainMenu }, Ids(s));
            Assert.Single(s.Toasts);
            Assert.Equal(ScreenId.MainMenu, s.TopId);
            Assert.True(s.Contains(ScreenId.RunSubmitPending));
            s.Push(ScreenId.Enhance, ScreenArgs.None);
            Assert.Equal(ScreenId.MainMenu, s.ReturnTarget);   // 토스트는 돌아갈 곳이 되지 않는다

            var list = new List<IScreen>();
            s.GetVisible(list);
            Assert.Equal(new[] { ScreenId.Enhance, ScreenId.RunSubmitPending }, list.Select(v => v.Id).ToArray());
            s.GetInputReceivers(list);
            Assert.DoesNotContain(list, v => v.Id == ScreenId.RunSubmitPending);
            Assert.False(s.ReceivesInput(_f.Last(ScreenId.RunSubmitPending)));
            Assert.True(s.IsVisible(_f.Last(ScreenId.RunSubmitPending)));

            s.Pop();
            Assert.Single(s.Toasts);
            s.ResetTo(ScreenId.Title, ScreenArgs.None);
            Assert.Single(s.Toasts);   // 화면을 바꿔도 남는다
            Assert.True(s.DismissToast(ScreenId.RunSubmitPending));
            Assert.Empty(s.Toasts);
            Assert.False(s.DismissToast(ScreenId.RunSubmitPending));
            Assert.Equal(1, _f.Last(ScreenId.RunSubmitPending).Exits);
        }

        [Fact]
        public void ReplacingWithAToastIsRejected()
        {
            ScreenStack s = NewStack();
            s.Push(ScreenId.MainMenu, ScreenArgs.None);
            Assert.Throws<InvalidOperationException>(() => s.Replace(ScreenId.RunSubmitPending, ScreenArgs.None));
            Assert.Equal(new[] { ScreenId.MainMenu }, Ids(s));
            Assert.Equal(0, _f.Last(ScreenId.MainMenu).Exits);
        }

        [Fact]
        public void ResetToClosesEverythingFromTheTop()
        {
            ScreenStack s = NewStack();
            s.Push(ScreenId.PlazaOverlay, ScreenArgs.None);
            s.Push(ScreenId.PlazaMenu, ScreenArgs.None);
            s.ResetTo(ScreenId.MainMenu, ScreenArgs.None);
            Assert.Equal(new[] { ScreenId.MainMenu }, Ids(s));
            Assert.Equal(new[] { "enter PlazaOverlay", "enter PlazaMenu", "exit PlazaMenu", "exit PlazaOverlay", "enter MainMenu" }, _f.Log);
        }

        [Fact]
        public void CloseRemovesOneScreenWherever()
        {
            ScreenStack s = NewStack();
            s.Push(ScreenId.BattleHud, ScreenArgs.None);
            s.Push(ScreenId.LootPick, ScreenArgs.None);
            s.Push(ScreenId.Tooltip, ScreenArgs.None);
            s.Push(ScreenId.RunSubmitPending, ScreenArgs.None);
            Assert.True(s.Close(_f.Last(ScreenId.LootPick)));
            Assert.Equal(new[] { ScreenId.BattleHud, ScreenId.Tooltip }, Ids(s));
            Assert.True(s.Close(_f.Last(ScreenId.Tooltip)));
            Assert.Equal(new[] { ScreenId.BattleHud }, Ids(s));
            Assert.True(s.Close(_f.Last(ScreenId.RunSubmitPending)));
            Assert.Empty(s.Toasts);
            Assert.False(s.Close(_f.Last(ScreenId.Tooltip)));
        }

        [Fact]
        public void LifecycleEventsWrapEnterAndExit()
        {
            ScreenStack s = NewStack();
            var log = _f.Log;
            s.Opened += x => log.Add("opened " + x.Id);
            s.Closed += x => log.Add("closed " + x.Id);
            s.Push(ScreenId.MainMenu, ScreenArgs.None);
            s.Replace(ScreenId.Enhance, ScreenArgs.None);
            s.Pop();
            Assert.Equal(new[]
            {
                "opened MainMenu", "enter MainMenu",
                "exit MainMenu", "closed MainMenu", "opened Enhance", "enter Enhance",
                "exit Enhance", "closed Enhance",
            }, log);
        }

        [Fact]
        public void ScreensMayChangeTheStackFromOnEnter()
        {
            // 강화 화면 첫 안내(spec 13 9.12)처럼 열리자마자 위에 창을 띄우는 경우
            _f.OnCreate = c =>
            {
                if (c.Id == ScreenId.Enhance)
                {
                    c.WhenEntered = _ => _stack!.Push(ScreenId.EnhanceTutorial, ScreenArgs.None);
                }
            };
            ScreenStack s = NewStack();
            _stack = s;
            s.Push(ScreenId.MainMenu, ScreenArgs.None);
            s.Push(ScreenId.Enhance, ScreenArgs.From(ScreenEntry.MainMenu));
            Assert.Equal(new[] { ScreenId.MainMenu, ScreenId.Enhance, ScreenId.EnhanceTutorial }, Ids(s));
            Assert.Equal(ScreenId.Enhance, s.ReturnTarget);
            s.Pop();
            Assert.Equal(ScreenId.Enhance, s.TopId);
        }

        private ScreenStack? _stack;

        [Fact]
        public void FactoryMistakesAreRejected()
        {
            ScreenStack s = NewStack();
            Assert.Throws<ArgumentException>(() => s.Push(ScreenId.None, ScreenArgs.None));
            _f.Override = _ => new FakeScreen(ScreenId.Title, ScreenLayer.Fullscreen, false, _f.Log);
            Assert.Throws<InvalidOperationException>(() => s.Push(ScreenId.MainMenu, ScreenArgs.None));
            _f.Override = _ => null;
            Assert.Throws<InvalidOperationException>(() => s.Push(ScreenId.MainMenu, ScreenArgs.None));
            Assert.Equal(0, s.Count);
            Assert.Throws<ArgumentNullException>(() => new ScreenStack(null!));
        }

        [Fact]
        public void ArgsCarryEntryVariantPayloadAndReturnTarget()
        {
            var payload = new object();
            var a = new ScreenArgs(ScreenEntry.Camp, ScreenId.MainMenu, 3, payload);
            Assert.Equal(ScreenEntry.Camp, a.Entry);
            Assert.Equal(ScreenId.MainMenu, a.ReturnTarget);
            Assert.Equal(3, a.Variant);
            Assert.Same(payload, a.Payload);
            ScreenArgs b = a.WithReturnTarget(ScreenId.PlazaOverlay);
            Assert.Equal(ScreenId.PlazaOverlay, b.ReturnTarget);
            Assert.Equal(3, b.Variant);
            Assert.Same(payload, ScreenArgs.WithPayload(payload).Payload);
            Assert.Equal(ScreenEntry.None, ScreenArgs.None.Entry);
        }

        [Fact]
        public void ScreenIdTableMatchesSpec13Section11()
        {
            // spec 13 1.1절 표의 79개, 더하기만 한다. None = 0
            ScreenId[] all = (ScreenId[])Enum.GetValues(typeof(ScreenId));
            Assert.Equal(80, all.Length);
            Assert.Equal(0, (int)ScreenId.None);
            Assert.Equal(1, (int)ScreenId.FirstRunPrompt);
            Assert.Equal(14, (int)ScreenId.MainMenu);
            Assert.Equal(41, (int)ScreenId.Enhance);
            Assert.Equal(79, (int)ScreenId.DebtList);
            Assert.Equal(Enumerable.Range(0, 80), all.Select(v => (int)v).OrderBy(v => v));
        }
    }
}

using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using Wizard.Solo.Art;
using Wizard.Solo.UI;
using UImage = UnityEngine.UI.Image;

namespace Wizard.Solo.Tests.UI
{
    /// <summary>불러오기 흐름, 조작 안내 글, 위젯(캔버스 없이 GameObject만 — EditMode에서 Update는 돌지 않는다).</summary>
    public class UiWidgetTests
    {
        GameObject _root;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("TestRoot", typeof(RectTransform));
            ((RectTransform)_root.transform).sizeDelta = new Vector2(1920f, 1080f);
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
        }

        RectTransform Root => (RectTransform)_root.transform;

        static GameContext Ctx(FakeSound sound = null, FakeNames names = null) =>
            new GameContext { Sound = sound, Names = names };

        // ---------------------------------------------------------------- Loading (00 8.1절)

        [Test]
        public void Loading_ReplacesWithNextAfterOneDrawnFrame()
        {
            var next = ScreenArgs.From(ScreenId.ExpeditionPrep).With(ScreenArgKeys.Character, 2);
            var args = ScreenArgs.ViaLoading(ScreenId.ExpeditionPrep, ScreenId.Battle, next);
            var flow = new UiLoadingFlow();
            var m = new FakeScreenManager();
            flow.Begin(args);
            Assert.AreEqual(0f, flow.Progress);
            Assert.IsFalse(flow.Step(m), "first frame only draws");
            Assert.IsEmpty(m.Calls);
            Assert.IsTrue(flow.Step(m));
            CollectionAssert.AreEqual(new[] { "replace Battle" }, m.Calls);
            Assert.AreSame(next, m.LastArgs);
            Assert.AreEqual(1f, flow.Progress);
            Assert.IsFalse(flow.Step(m), "hands off only once");
            Assert.AreEqual(1, m.Calls.Count);
        }

        [Test]
        public void Loading_WithoutNextPops()
        {
            var flow = new UiLoadingFlow();
            var m = new FakeScreenManager();
            flow.Begin(ScreenArgs.From(ScreenId.MainMenu));
            flow.Step(m);
            flow.Step(m);
            CollectionAssert.AreEqual(new[] { "pop" }, m.Calls);
            flow.Begin(null);
            flow.Step(m);
            flow.Step(m);
            CollectionAssert.AreEqual(new[] { "pop", "pop" }, m.Calls);
        }

        [Test]
        public void Loading_ViaLoadingDefaultsNextArgs()
        {
            var args = ScreenArgs.ViaLoading(ScreenId.Tutorial, ScreenId.Battle, null);
            var flow = new UiLoadingFlow();
            flow.Begin(args);
            Assert.AreEqual(ScreenId.Battle, flow.Next);
            Assert.IsNotNull(flow.NextArgs);
            Assert.AreEqual(ScreenId.Tutorial, flow.NextArgs.Entry);
        }

        // ---------------------------------------------------------------- 조작 안내

        [Test]
        public void KeyHint_DefaultLine()
        {
            var names = new FakeNames()
                .Set(UiNameKeys.HintItem, "{key} {action}").Set(UiNameKeys.HintSep, " · ").Set(UiNameKeys.HintPick, "PICK")
                .Set(UiNameKeys.CommonBack, "BACK").Set(UiNameKeys.CommonExit, "EXIT").Set("str.ui.key.escape", "Esc");
            Assert.AreEqual("J PICK · K BACK · Esc EXIT", UiKeyHint.Compose(names, UiKeyHint.Defaults));
            Assert.AreEqual(string.Empty, UiKeyHint.Compose(names, null));
        }

        [Test]
        public void KeyLabels_LettersDigitsAndNames()
        {
            var names = new FakeNames().Set("str.ui.key.leftshift", "Shift");
            Assert.AreEqual("J", UiKeyLabels.Of(names, Key.J));
            Assert.AreEqual("A", UiKeyLabels.Of(null, Key.A));
            Assert.AreEqual("Z", UiKeyLabels.Of(null, Key.Z));
            Assert.AreEqual("1", UiKeyLabels.Of(null, Key.Digit1));
            Assert.AreEqual("0", UiKeyLabels.Of(null, Key.Digit0));
            Assert.AreEqual("Shift", UiKeyLabels.Of(names, Key.LeftShift));
            Assert.AreEqual("str.ui.key.escape", UiKeyLabels.NameKey(Key.Escape));
        }

        // ---------------------------------------------------------------- 위젯

        [Test]
        public void Button_EnabledClicksWithSound_DisabledShowsReason()
        {
            var sound = new FakeSound();
            var names = new FakeNames().Set("error.not_enough_gold", "NO GOLD");
            var ctx = Ctx(sound, names);
            int clicks = 0;
            var b = UiKit.Button(ctx, Root, "OK", 0f, 0f, 200f, 64f, () => clicks++, UiButtonStyle.Large);
            b.Activate();
            Assert.AreEqual(1, clicks);
            CollectionAssert.Contains(sound.Played, UiSounds.Press + "@" + SoundBus.Ui);
            Assert.AreEqual(UiVisualState.Pressed, b.VisualState, "keyboard press shows pressed briefly");

            b.SetDisabled("error.not_enough_gold");
            sound.Played.Clear();
            b.Activate();
            Assert.AreEqual(1, clicks, "disabled does nothing");
            CollectionAssert.AreEqual(new[] { UiSounds.DisabledPress + "@" + SoundBus.Ui }, sound.Played);
            Assert.AreEqual(UiVisualState.Disabled, b.VisualState);
            var reason = b.transform.Find("DisabledReason");
            Assert.IsNotNull(reason, "reason line is shown again on press");
            Assert.IsTrue(reason.gameObject.activeSelf);
            Assert.AreEqual("NO GOLD", reason.GetComponent<UiLabel>().FullText);
        }

        [Test]
        public void Button_CustomOrNoPressSound()
        {
            var sound = new FakeSound();
            var ctx = Ctx(sound);
            var b = UiKit.Button(ctx, Root, "A", 0f, 0f, 100f, 48f, null, UiButtonStyle.Medium, null, "ui.camp.depart");
            b.Activate();
            CollectionAssert.AreEqual(new[] { "ui.camp.depart@" + SoundBus.Ui }, sound.Played);
            sound.Played.Clear();
            var silent = UiKit.Button(ctx, Root, "B", 0f, 0f, 100f, 48f, null, UiButtonStyle.Medium, null, "");
            silent.Activate();
            Assert.IsEmpty(sound.Played);
        }

        [Test]
        public void Button_CautionHasAmberFrameAndTriangle_NoArtFallsBackToSolid()
        {
            var b = UiKit.Button(Ctx(), Root, "GIVE UP", 0f, 0f, 240f, 48f, null, UiButtonStyle.Caution);
            Assert.IsTrue(b.IsCaution);
            Assert.IsNotNull(b.CautionFrame);
            Assert.AreEqual(UiTheme.Amber, b.CautionFrame.Color);
            Assert.IsNotNull(b.CautionMark);
            Assert.IsTrue(b.CautionMark.InnerMark);
            Assert.IsFalse(UiTheme.IsDangerColor(b.CautionMark.color));
            Assert.IsNull(b.Background.sprite, "ctx.Art null -> solid color");
            Assert.AreEqual(UiTheme.Wood, b.Background.color);
        }

        [Test]
        public void Button_BracketFollowsCursorAndPicked()
        {
            var b = UiKit.Button(Ctx(), Root, "X", 0f, 0f, 64f, 64f, null);
            var bracket = b.GetComponentInChildren<UiSelectionBracket>(true);
            Assert.IsNotNull(bracket);
            Assert.IsFalse(bracket.gameObject.activeSelf);
            b.Cursor = true; // 관리자 없음 → Keyboard 모드
            Assert.IsTrue(bracket.gameObject.activeSelf);
            Assert.IsTrue(bracket.Visual.Pulsing);
            b.Cursor = false;
            b.Picked = true;
            Assert.IsTrue(bracket.gameObject.activeSelf);
            Assert.IsFalse(bracket.Visual.Pulsing);
            Assert.IsTrue(bracket.Visual.ConfirmCheck);
            b.Enabled = false;
            Assert.AreEqual(UiBracketTone.Disabled, bracket.Visual.Tone);
        }

        [Test]
        public void Button_OnShowsChalkBar()
        {
            var b = UiKit.Button(Ctx(), Root, "TAB", 0f, 0f, 160f, 48f, null, UiButtonStyle.Tab);
            Assert.IsFalse(b.OnBar.gameObject.activeSelf);
            b.On = true;
            Assert.AreEqual(UiVisualState.On, b.VisualState);
            Assert.IsTrue(b.OnBar.gameObject.activeSelf);
            Assert.AreEqual(UiTheme.Chalk, b.OnBar.color);
        }

        [Test]
        public void FocusGroup_MovesAndActivates()
        {
            var sound = new FakeSound();
            var ctx = Ctx(sound);
            var g = new UiFocusGroup(new UiInputModeTracker(), sound) { Columns = 1 };
            int a = 0, c = 0;
            var b1 = UiKit.Button(ctx, Root, "1", 0f, 0f, 100f, 48f, () => a++, UiButtonStyle.Medium, g);
            var b2 = UiKit.Button(ctx, Root, "2", 0f, 60f, 100f, 48f, () => c++, UiButtonStyle.Medium, g);
            Assert.AreSame(b1, g.Current);
            Assert.IsTrue(b1.Cursor);
            Assert.IsTrue(g.HandleKey(MenuKey.Down));
            Assert.AreSame(b2, g.Current);
            Assert.IsFalse(b1.Cursor);
            CollectionAssert.Contains(sound.Played, UiSounds.CursorMove + "@" + SoundBus.Ui);
            Assert.IsTrue(g.HandleKey(MenuKey.Confirm));
            Assert.AreEqual(1, c);
            Assert.IsFalse(g.HandleKey(MenuKey.Cancel));
            // 꺼진 칸에도 커서는 간다
            b1.SetDisabled("error.x");
            g.HandleKey(MenuKey.Up);
            Assert.AreSame(b1, g.Current);
            g.HandleKey(MenuKey.Confirm);
            Assert.AreEqual(0, a);
        }

        [Test]
        public void FocusGroup_HoverMovesCursorOnlyInMouseMode()
        {
            var mode = new UiInputModeTracker();
            var g = new UiFocusGroup(mode) { Columns = 1 };
            var ctx = Ctx();
            var b1 = UiKit.Button(ctx, Root, "1", 0f, 0f, 100f, 48f, null, UiButtonStyle.Medium, g);
            var b2 = UiKit.Button(ctx, Root, "2", 0f, 60f, 100f, 48f, null, UiButtonStyle.Medium, g);
            g.OnPointerHover(b2);
            Assert.AreSame(b1, g.Current, "keyboard mode: hover does not steal the cursor");
            mode.OnMouseMove(1f, 1f);
            g.OnPointerHover(b2);
            Assert.AreSame(b2, g.Current);
        }

        [Test]
        public void Toggle_FlipsAndPlaysOnOff()
        {
            var sound = new FakeSound();
            bool? got = null;
            var t = UiKit.Toggle(Ctx(sound), Root, "T", 0f, 0f, 400f, 32f, false, v => got = v);
            t.Activate();
            Assert.IsTrue(t.Value);
            Assert.AreEqual(true, got);
            CollectionAssert.AreEqual(new[] { UiSounds.ToggleOn + "@" + SoundBus.Ui }, sound.Played);
            Assert.IsTrue(t.HandleKey(MenuKey.Left));
            Assert.IsFalse(t.Value);
            Assert.IsTrue(t.HandleKey(MenuKey.Left), "already off: consumed, no change");
            Assert.IsFalse(t.Value);
            CollectionAssert.AreEqual(new[] { UiSounds.ToggleOn + "@" + SoundBus.Ui, UiSounds.ToggleOff + "@" + SoundBus.Ui }, sound.Played);
        }

        [Test]
        public void Slider_ClampsStepsAndKeys()
        {
            int last = -1;
            var s = UiKit.Slider(Ctx(new FakeSound(), new FakeNames()), Root, 0f, 0f, 400f, 32f, 50, v => last = v, 0, 100, 5);
            Assert.AreEqual(50, s.Value);
            Assert.AreEqual(-1, last, "initial value does not notify");
            s.HandleKey(MenuKey.Right);
            Assert.AreEqual(55, s.Value);
            Assert.AreEqual(55, last);
            s.SetValue(200, true);
            Assert.AreEqual(100, s.Value);
            s.SetValue(-3, false);
            Assert.AreEqual(0, s.Value);
            s.HandleKey(MenuKey.Left);
            Assert.AreEqual(0, s.Value);
            Assert.AreEqual("0", s.ValueLabel.FullText);
            Assert.AreEqual(0f, s.FillRect.anchorMax.x, 1e-5f);
            // 정해진 값 사이만(telegraph_fill 70/80/90)
            var st = UiKit.Slider(Ctx(), Root, 0f, 40f, 400f, 32f, 70, null, 70, 90, 1, null, new[] { 70, 80, 90 });
            st.HandleKey(MenuKey.Right);
            Assert.AreEqual(80, st.Value);
            st.HandleKey(MenuKey.Right);
            st.HandleKey(MenuKey.Right);
            Assert.AreEqual(90, st.Value);
            st.SetValue(74, false);
            Assert.AreEqual(70, st.Value);
        }

        [Test]
        public void Tabs_QESkipsDisabledAndWraps()
        {
            int sel = -1;
            var tabs = UiKit.Tabs(Ctx(), Root, new[] { "a", "b", "c" }, 0f, 0f, 160f, 48f, i => sel = i);
            Assert.AreEqual(0, tabs.Index);
            Assert.IsTrue(tabs.Buttons[0].On);
            tabs.Buttons[1].SetDisabled("error.x");
            Assert.IsTrue(tabs.HandleKey(MenuKey.NextTab));
            Assert.AreEqual(2, tabs.Index);
            Assert.AreEqual(2, sel);
            Assert.IsFalse(tabs.Buttons[0].On);
            Assert.IsTrue(tabs.Buttons[2].On);
            tabs.HandleKey(MenuKey.NextTab);
            Assert.AreEqual(0, tabs.Index, "wraps");
            tabs.HandleKey(MenuKey.PrevTab);
            Assert.AreEqual(2, tabs.Index);
            Assert.IsFalse(tabs.HandleKey(MenuKey.Confirm));
        }

        [Test]
        public void Label_EllipsizesToRectWidth()
        {
            var l = UiKit.Label(Ctx(), Root, "가나다라마바사아자", UiFontId.F16, 0f, 0f, 128f, 24f);
            Assert.IsTrue(l.Truncated);
            StringAssert.EndsWith(UiTextFit.Ellipsis, l.Text.text);
            Assert.AreEqual("가나다라마바사아자", l.FullText);
            Assert.IsNotNull(l.GetComponent<UiTooltipTrigger>(), "full text goes to a tooltip");
            l.SetText("가나다");
            Assert.IsFalse(l.Truncated);
            Assert.AreEqual("가나다", l.Text.text);
            Assert.IsFalse(l.GetComponent<UiTooltipTrigger>().enabled);
        }

        [Test]
        public void Place_TopLeftOrigin()
        {
            var n = UiKit.Node(Root, "n", 10f, 20f, 30f, 40f);
            Assert.AreEqual(new Vector2(0f, 1f), n.anchorMin);
            Assert.AreEqual(new Vector2(0f, 1f), n.pivot);
            Assert.AreEqual(new Vector2(10f, -20f), n.anchoredPosition);
            Assert.AreEqual(new Vector2(30f, 40f), n.sizeDelta);
        }

        [Test]
        public void Panel_NoArtUsesThemeColor()
        {
            var p = UiKit.Panel(Ctx(), Root, UiPanelKind.Paper, 0f, 0f, 100f, 100f);
            Assert.IsNull(p.sprite);
            Assert.AreEqual(UiTheme.Paper, p.color);
            UImage d = UiKit.Divider(Ctx(), Root, 0f, 0f, 200f);
            Assert.AreEqual(new Vector2(200f, 4f), d.rectTransform.sizeDelta);
        }

        [Test]
        public void ProgressBar_FillAnchor()
        {
            var bar = UiKit.ProgressBar(Ctx(), Root, 0f, 0f, 640f, 16f, 0.25f);
            Assert.AreEqual(0.25f, bar.Fill.rectTransform.anchorMax.x, 1e-5f);
            bar.Value01 = 2f;
            Assert.AreEqual(1f, bar.Value01);
        }
    }
}

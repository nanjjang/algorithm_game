using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.Art;
using Wizard.Solo.UI;

namespace Wizard.Solo.Tests.UI
{
    /// <summary>글자 폭·자르기, 상태 규약, 괄호, 입력 장치 모드, 커서 이동(08 요약서 1.3·1.4절).</summary>
    public class UiRulesTests
    {
        // ---------------------------------------------------------------- 1.4 글자

        // TV-238: 이름 칸 한글 8자 / 9자, 16px, 칸 128px → 그대로 / 끝 "…"
        [Test]
        public void S13_TV_238__NameField8FitsAnd9Ellipsizes()
        {
            Assert.AreEqual(128, UiTextFit.NameFieldWidthPx(16));
            const string eight = "가나다라마바사아";
            const string nine = "가나다라마바사아자";
            Assert.AreEqual(eight, UiTextFit.FitName(eight, 16));
            Assert.AreEqual(eight, UiTextFit.Fit(eight, 16, 128));
            string cut = UiTextFit.FitName(nine, 16);
            StringAssert.EndsWith(UiTextFit.Ellipsis, cut);
            Assert.AreNotEqual(nine, cut);
            Assert.LessOrEqual(UiTextFit.WidthPx(cut, 16), 128);
            StringAssert.StartsWith("가나다라마바사", cut);
            Assert.IsTrue(UiTextFit.IsTruncated(nine, 16, 128));
            Assert.IsFalse(UiTextFit.IsTruncated(eight, 16, 128));
        }

        // TV-239: "48,200" 16px → 48px
        [Test]
        public void S13_TV_239__DigitsAndCommaAreHalfWidth()
        {
            Assert.AreEqual(48, UiTextFit.WidthPx("48,200", 16));
        }

        // TV-240: 48200 → "48,200"
        [Test]
        public void S13_TV_240__FormatThousands()
        {
            Assert.AreEqual("48,200", UiTextFit.FormatThousands(48200));
            Assert.AreEqual("0", UiTextFit.FormatThousands(0));
            Assert.AreEqual("999", UiTextFit.FormatThousands(999));
            Assert.AreEqual("1,000", UiTextFit.FormatThousands(1000));
            Assert.AreEqual("-1,234,567", UiTextFit.FormatThousands(-1234567));
            Assert.AreEqual("-9,223,372,036,854,775,808", UiTextFit.FormatThousands(long.MinValue));
        }

        [Test]
        public void TextWidth_HangulAndSymbolFullAsciiHalf()
        {
            Assert.AreEqual(16, UiTextFit.WidthPx("가", 16));
            Assert.AreEqual(8, UiTextFit.WidthPx("A", 16));
            Assert.AreEqual(8, UiTextFit.WidthPx(" ", 16));
            Assert.AreEqual(16, UiTextFit.WidthPx(UiTextFit.Ellipsis, 16));
            Assert.AreEqual(24, UiTextFit.WidthPx("가1", 16));
            Assert.AreEqual(0, UiTextFit.WidthPx(null, 16));
            Assert.AreEqual(36, UiTextFit.WidthPx("가나다", 12));
        }

        [Test]
        public void Fit_DoesNotSplitSurrogatesAndHandlesTinyWidth()
        {
            string s = "ab\U0001F600cd";
            string cut = UiTextFit.Fit(s, 16, 40);
            Assert.LessOrEqual(UiTextFit.WidthPx(cut, 16), 40);
            StringAssert.EndsWith(UiTextFit.Ellipsis, cut);
            string body = cut.Substring(0, cut.Length - UiTextFit.Ellipsis.Length);
            Assert.IsFalse(body.Length > 0 && char.IsHighSurrogate(body[body.Length - 1]), "dangling surrogate");
            Assert.AreEqual(UiTextFit.Ellipsis, UiTextFit.Fit("가나다", 16, 10));
            Assert.AreEqual(string.Empty, UiTextFit.Fit(null, 16, 10));
        }

        // ---------------------------------------------------------------- 1.3 상태

        [Test]
        public void StatePriority_DisabledPressedOnHoverNormal()
        {
            var K = UiInputMode.Keyboard;
            var M = UiInputMode.Mouse;
            Assert.AreEqual(UiVisualState.Disabled, UiStateRules.Resolve(false, true, true, true, M));
            Assert.AreEqual(UiVisualState.Pressed, UiStateRules.Resolve(true, true, true, true, M));
            Assert.AreEqual(UiVisualState.On, UiStateRules.Resolve(true, false, true, true, M));
            Assert.AreEqual(UiVisualState.Hover, UiStateRules.Resolve(true, false, false, true, M));
            Assert.AreEqual(UiVisualState.Normal, UiStateRules.Resolve(true, false, false, false, M));
            // Hover는 마우스 전용
            Assert.AreEqual(UiVisualState.Normal, UiStateRules.Resolve(true, false, false, true, K));
            Assert.AreEqual(UiVisualState.On, UiStateRules.Resolve(true, false, true, true, K));
        }

        [Test]
        public void StateTokens_MatchFileNameRule()
        {
            Assert.IsNull(UiStateRules.StateToken(UiVisualState.Normal));
            Assert.AreEqual("hover", UiStateRules.StateToken(UiVisualState.Hover));
            Assert.AreEqual("pressed", UiStateRules.StateToken(UiVisualState.Pressed));
            Assert.AreEqual("on", UiStateRules.StateToken(UiVisualState.On));
            Assert.AreEqual("disabled", UiStateRules.StateToken(UiVisualState.Disabled));
        }

        // TV-241: 마우스로 칸 위 → Hover(괄호 없음) / 키보드 입력 후 → Hover 해제 + 괄호
        [Test]
        public void S13_TV_241__HoverInMouseModeBracketAfterKey()
        {
            var mode = new UiInputModeTracker();
            Assert.IsTrue(mode.OnMouseMove(3f, 0f));
            Assert.AreEqual(UiInputMode.Mouse, mode.Mode);
            // 마우스가 칸 위에 있고, 커서도 그 칸(마우스가 옮겨 놓음)
            Assert.AreEqual(UiVisualState.Hover, UiStateRules.Resolve(true, false, false, true, mode.Mode));
            Assert.IsFalse(UiStateRules.Bracket(true, true, false, mode.Mode).Visible);
            Assert.IsTrue(mode.OnKeyAny());
            Assert.AreEqual(UiVisualState.Normal, UiStateRules.Resolve(true, false, false, true, mode.Mode));
            var b = UiStateRules.Bracket(true, true, false, mode.Mode);
            Assert.IsTrue(b.Visible);
            Assert.IsTrue(b.Pulsing);
            Assert.AreEqual(UiBracketTone.Select, b.Tone);
        }

        // TV-242: 꺼진 버튼에 키보드 커서 → 괄호 회청 + 이유 한 줄
        [Test]
        public void S13_TV_242__DisabledWithCursorShowsGrayBracketAndReason()
        {
            var b = UiStateRules.Bracket(false, true, false, UiInputMode.Keyboard);
            Assert.IsTrue(b.Visible);
            Assert.AreEqual(UiBracketTone.Disabled, b.Tone);
            Assert.IsTrue(UiStateRules.ShowsDisabledReason(false, true, false, UiInputMode.Keyboard, true));
            Assert.IsFalse(UiStateRules.ShowsDisabledReason(true, true, false, UiInputMode.Keyboard, true));
            Assert.IsFalse(UiStateRules.ShowsDisabledReason(false, false, false, UiInputMode.Keyboard, true));
            Assert.IsFalse(UiStateRules.ShowsDisabledReason(false, true, false, UiInputMode.Keyboard, false));
        }

        [Test]
        public void Bracket_MouseModeOnlyPickedAndPickedHasCheck()
        {
            var m = UiInputMode.Mouse;
            Assert.IsFalse(UiStateRules.Bracket(true, true, false, m).Visible);
            var picked = UiStateRules.Bracket(true, false, true, m);
            Assert.IsTrue(picked.Visible);
            Assert.IsFalse(picked.Pulsing);
            Assert.IsTrue(picked.ConfirmCheck);
            // 커서와 확정이 한 화면에 동시에(캐릭터 선택): 커서 칸은 맥동, 확정 칸은 멈춤
            var k = UiInputMode.Keyboard;
            Assert.IsTrue(UiStateRules.Bracket(true, true, false, k).Pulsing);
            Assert.IsFalse(UiStateRules.Bracket(true, false, true, k).Pulsing);
            Assert.IsFalse(UiStateRules.Bracket(true, false, false, k).Visible);
        }

        // TV-243: 대상 64×64 / 65×40 / 240×336 / 336×376 → Small / Medium / Large / Large
        [Test]
        public void S13_TV_243__BracketSize()
        {
            Assert.AreEqual(UiBracketSize.Small, UiBrackets.SizeFor(64, 64));
            Assert.AreEqual(UiBracketSize.Medium, UiBrackets.SizeFor(65, 40));
            Assert.AreEqual(UiBracketSize.Large, UiBrackets.SizeFor(240, 336));
            Assert.AreEqual(UiBracketSize.Large, UiBrackets.SizeFor(336, 376));
            Assert.AreEqual(UiBracketSize.Medium, UiBrackets.SizeFor(240, 240));
            Assert.AreEqual(12, UiBrackets.ArmPx(UiBracketSize.Small));
            Assert.AreEqual(20, UiBrackets.ArmPx(UiBracketSize.Medium));
            Assert.AreEqual(28, UiBrackets.ArmPx(UiBracketSize.Large));
        }

        // 08 1.3절: 커서 괄호는 0.5초(30t) 주기로 2px 벌어졌다 오므라든다(2프레임, 프레임당 15t)
        [Test]
        public void BracketPulse_TwoFramesOf15Ticks()
        {
            Assert.AreEqual(0, UiBrackets.PulseFrame(0));
            Assert.AreEqual(0, UiBrackets.PulseFrame(14));
            Assert.AreEqual(1, UiBrackets.PulseFrame(15));
            Assert.AreEqual(1, UiBrackets.PulseFrame(29));
            Assert.AreEqual(0, UiBrackets.PulseFrame(30));
            Assert.AreEqual(0, UiBrackets.PulseFrame(-5));
            Assert.AreEqual(4, UiBrackets.GapAt(0));
            Assert.AreEqual(6, UiBrackets.GapAt(1));
            Assert.AreEqual(1, UiBrackets.PulseFrameAt(0.3f));
            Assert.AreEqual(0, UiBrackets.PulseFrameAt(0.55f));
        }

        [Test]
        public void InputMode_ZeroDeltaDoesNotSwitch()
        {
            var mode = new UiInputModeTracker();
            Assert.AreEqual(UiInputMode.Keyboard, mode.Mode);
            Assert.IsFalse(mode.OnMouseMove(0f, 0f));
            Assert.AreEqual(UiInputMode.Keyboard, mode.Mode);
            int changes = 0;
            mode.Changed += _ => changes++;
            mode.OnMouseMove(0f, -1f);
            mode.OnMouseMove(2f, 0f);
            mode.OnKeyAny();
            mode.OnKeyAny();
            Assert.AreEqual(2, changes);
        }

        // ---------------------------------------------------------------- 1.5 색

        [Test]
        public void Theme_MenuColorsAreNotDangerColors()
        {
            var menu = new[]
            {
                UiTheme.Ink, UiTheme.WoodDark, UiTheme.Wood, UiTheme.WoodLight, UiTheme.SlateBlue, UiTheme.Chalk, UiTheme.Paper,
                UiTheme.PaperShade, UiTheme.PaperText, UiTheme.SelectYellow, UiTheme.Gold, UiTheme.RareBlue, UiTheme.CommonGray,
                UiTheme.Amber, UiTheme.Good, UiTheme.Mutation, UiTheme.ArcaneTeal
            };
            foreach (var c in menu) Assert.IsFalse(UiTheme.IsDangerColor(c), c.ToString());
            Assert.IsTrue(UiTheme.IsDangerColor(UiTheme.Hex(0xFF3B30)));
            Assert.IsTrue(UiTheme.IsDangerColor(UiTheme.Hex(0xFF2E88)));
            Assert.AreEqual(0.6f, UiTheme.ModalDim.a, 1e-6f);
            Assert.AreEqual(UiTheme.Hex(0x12131C).r, UiTheme.ModalDim.r, 1e-6f);
        }

        [Test]
        public void Theme_LineHeights()
        {
            Assert.AreEqual(16, UiTheme.LineHeight(UiFontId.F12));
            Assert.AreEqual(24, UiTheme.LineHeight(UiFontId.F16));
            Assert.AreEqual(32, UiTheme.LineHeight(UiFontId.F24));
            Assert.AreEqual(40, UiTheme.LineHeight(UiFontId.F32));
            Assert.AreEqual(56, UiTheme.LineHeight(UiFontId.F48));
            Assert.AreEqual(72, UiTheme.LineHeight(UiFontId.F64));
        }

        // ---------------------------------------------------------------- 커서 이동

        [Test]
        public void FocusNav_VerticalListAndWrap()
        {
            // y 위가 +: 0이 맨 위
            var c = new List<Vector2> { new Vector2(0, 300), new Vector2(0, 200), new Vector2(0, 100) };
            Assert.AreEqual(1, UiFocusNav.Next(c, 0, MenuKey.Down, false));
            Assert.AreEqual(2, UiFocusNav.Next(c, 1, MenuKey.Down, false));
            Assert.AreEqual(2, UiFocusNav.Next(c, 2, MenuKey.Down, false));
            Assert.AreEqual(0, UiFocusNav.Next(c, 2, MenuKey.Down, true));
            Assert.AreEqual(2, UiFocusNav.Next(c, 0, MenuKey.Up, true));
            Assert.AreEqual(0, UiFocusNav.Next(c, 0, MenuKey.Left, false));
        }

        [Test]
        public void FocusNav_PrefersAlignedNeighbour()
        {
            // 2×2 격자 + 오른쪽 아래로 치우친 칸
            var c = new List<Vector2> { new Vector2(0, 100), new Vector2(100, 100), new Vector2(0, 0), new Vector2(100, 0) };
            Assert.AreEqual(1, UiFocusNav.Next(c, 0, MenuKey.Right, false));
            Assert.AreEqual(2, UiFocusNav.Next(c, 0, MenuKey.Down, false));
            Assert.AreEqual(3, UiFocusNav.Next(c, 1, MenuKey.Down, false));
            Assert.AreEqual(3, UiFocusNav.Next(c, 2, MenuKey.Right, false));
        }

        [Test]
        public void FocusNav_Grid()
        {
            // 5칸, 3열: 0 1 2 / 3 4
            Assert.AreEqual(1, UiFocusNav.NextInGrid(5, 3, 0, MenuKey.Right, false));
            Assert.AreEqual(2, UiFocusNav.NextInGrid(5, 3, 2, MenuKey.Right, false));
            Assert.AreEqual(0, UiFocusNav.NextInGrid(5, 3, 2, MenuKey.Right, true));
            Assert.AreEqual(3, UiFocusNav.NextInGrid(5, 3, 0, MenuKey.Down, false));
            Assert.AreEqual(4, UiFocusNav.NextInGrid(5, 3, 2, MenuKey.Down, false));
            Assert.AreEqual(1, UiFocusNav.NextInGrid(5, 3, 4, MenuKey.Up, false));
            Assert.AreEqual(4, UiFocusNav.NextInGrid(5, 3, 4, MenuKey.Right, false));
            Assert.AreEqual(1, UiFocusNav.NextInGrid(5, 3, 4, MenuKey.Down, true));
        }

        [Test]
        public void GridCell_RowMajor()
        {
            Assert.AreEqual(new Vector2(0, 0), UiKit.GridCell(0, 3, 64, 64, 8, 8));
            Assert.AreEqual(new Vector2(144, 0), UiKit.GridCell(2, 3, 64, 64, 8, 8));
            Assert.AreEqual(new Vector2(72, 72), UiKit.GridCell(4, 3, 64, 64, 8, 8));
        }
    }
}

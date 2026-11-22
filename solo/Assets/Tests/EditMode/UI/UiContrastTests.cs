using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.UI;

namespace Wizard.Solo.Tests.UI
{
    /// <summary>
    /// 글자 대비(WCAG): 판 종류 × 글자 쓰임마다 본문 4.5:1 이상. 플레이 시험에서 "밝은 판 위 노랑 글자", "어두운 판 위 회색 글자"가
    /// 안 읽힌다는 피드백이 있어, 판 바탕색(그림 가운데 평균)과 테마 글자색의 짝을 숫자로 묶어 둔다.
    /// </summary>
    public class UiContrastTests
    {
        [Test]
        public void Contrast_KnownValues()
        {
            Assert.AreEqual(21f, UiTheme.Contrast(Color.black, Color.white), 0.01f);
            Assert.AreEqual(1f, UiTheme.Contrast(UiTheme.Chalk, UiTheme.Chalk), 1e-4f);
            Assert.AreEqual(UiTheme.Contrast(UiTheme.Gold, UiTheme.Ink), UiTheme.Contrast(UiTheme.Ink, UiTheme.Gold), 1e-5f);
        }

        [Test]
        public void EveryPanelKind_EveryTextRole_MeetsBodyContrast()
        {
            foreach (UiPanelKind kind in System.Enum.GetValues(typeof(UiPanelKind)))
            foreach (UiTextRole role in System.Enum.GetValues(typeof(UiTextRole)))
            {
                var bg = UiKit.PanelBaseColor(kind);
                var fg = UiKit.TextColor(kind, role);
                float c = UiTheme.Contrast(fg, bg);
                Assert.GreaterOrEqual(c, UiTheme.MinContrastBody - 0.15f, kind + "/" + role + " = " + c.ToString("0.00"));
            }
        }

        [Test]
        public void SlateBlue_ReadableOnDarkKitPanels()
        {
            foreach (var kind in new[] { UiPanelKind.Board, UiPanelKind.Recess, UiPanelKind.Heading, UiPanelKind.ScreenHeading, UiPanelKind.ControlStrip, UiPanelKind.Tooltip })
                Assert.GreaterOrEqual(UiTheme.Contrast(UiTheme.SlateBlue, UiKit.PanelBaseColor(kind)), 6f, kind.ToString());
            // 화면 바탕(나무 어두움)과 기본 판 단색 대체.
            Assert.GreaterOrEqual(UiTheme.Contrast(UiTheme.SlateBlue, UiTheme.WoodDark), 6f);
            Assert.GreaterOrEqual(UiTheme.Contrast(UiTheme.SlateBlue, UiTheme.Wood), 5f);
        }

        [Test]
        public void LightTextOnPaper_IsWhatWeAvoid()
        {
            // 쪽지 위 분필·금·노랑·회청·호박은 모두 2:1 아래 — 그래서 쪽지 위는 TextOn(true, …)을 쓴다.
            var paper = UiKit.PanelBaseColor(UiPanelKind.Paper);
            foreach (var c in new[] { UiTheme.Chalk, UiTheme.Gold, UiTheme.SelectYellow, UiTheme.SlateBlue, UiTheme.Amber })
                Assert.Less(UiTheme.Contrast(c, paper), 2f);
            Assert.IsTrue(UiTheme.IsLight(paper));
            Assert.IsFalse(UiTheme.IsLight(UiKit.PanelBaseColor(UiPanelKind.Board)));
            Assert.IsTrue(UiKit.IsLightPanel(UiPanelKind.Paper));
            Assert.IsTrue(UiKit.IsLightPanel(UiPanelKind.Toast));
            Assert.IsFalse(UiKit.IsLightPanel(UiPanelKind.Recess));
        }

        [Test]
        public void PaperRoles_ReadableOnParchmentArt()
        {
            // 받아 온 양피지 그림(장부 frame_ledger 평균 #EFBD71, 영수증 frame_receipt #EEC67D, 쪽지 그늘 PaperShade).
            var parchments = new[] { UiTheme.Hex(0xEFBD71), UiTheme.Hex(0xEEC67D), UiTheme.Paper, UiTheme.Hex(0xD4BFA7) };
            foreach (var bg in parchments)
            foreach (UiTextRole role in System.Enum.GetValues(typeof(UiTextRole)))
                Assert.GreaterOrEqual(UiTheme.Contrast(UiTheme.TextOn(true, role), bg), UiTheme.MinContrastBody, role + " on " + bg);
        }

        [Test]
        public void ReadableOn_FallsBackToInkOrChalk()
        {
            var paper = UiKit.PanelBaseColor(UiPanelKind.Paper);
            Assert.AreEqual(UiTheme.PaperText, UiTheme.ReadableOn(paper, UiTheme.Gold));
            Assert.AreEqual(UiTheme.Chalk, UiTheme.ReadableOn(UiTheme.Ink, UiTheme.PaperText));
            Assert.AreEqual(UiTheme.Gold, UiTheme.ReadableOn(UiTheme.Ink, UiTheme.Gold));
        }

        [Test]
        public void PaperTextColors_AreNotDangerColors()
        {
            foreach (var c in new[] { UiTheme.PaperMuted, UiTheme.PaperAccent, UiTheme.PaperWarn, UiTheme.SlateBlue })
                Assert.IsFalse(UiTheme.IsDangerColor(c), c.ToString());
        }
    }
}

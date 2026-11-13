using System.Collections.Generic;
using System.Linq;
using Wizard.AssetCheck.Color;
using Wizard.ViewLogic.Presentation.Limits;
using Xunit;

namespace Wizard.AssetCheck.Tests
{
    // spec 05 11.18절 색과 대비(TV-COL-01~18). 대비 허용오차는 spec 05 C-05(대비 ±0.1 은 원본·계산 차이, 표에 적힌 자리는 ±0.05),
    // 색 채널 ±1, 명도 ±1%p
    public sealed class EnemyColorVectorTests
    {
        private const int Red = 0xFF3B30;
        private const int White = 0xFFFFFF;
        private const int Black = 0x000000;
        private const double Ratio = 0.05;

        // spec 05 4.1~4.12 (마) 겉모습의 팔레트, 12종
        public static readonly int[][] EnemyPalettes =
        {
            new[] { 0x9CCB6A, 0x5E8F4A, 0x9AA39A, 0x5C6670, 0xFFF2A0, 0x1B2440 },
            new[] { 0xA89C8A, 0x6A6258, 0x98BF6E, 0xE6F2C0, 0xFFFFFF },
            new[] { 0xA9AEB8, 0x5E6678, 0x98BF6E, 0xFFF2A0, 0x1B2440 },
            new[] { 0xCDB27E, 0x8A6F48, 0xEDE3C8, 0x7A6248, 0x3A2E2A },
            new[] { 0x8E9E7A, 0x56664E, 0xE9D98A, 0xB8A560, 0x3A463E },
            new[] { 0x93B87F, 0x587A56, 0xE6F2C0, 0xD9C7A3, 0x3A2E2A },
            new[] { 0xC09A78, 0x7A5A44, 0xC9B8A0, 0x8A7A66, 0x98BF6E, 0xA8875A },
            new[] { 0x8496C8, 0xA9B8E0, 0x56648C, 0xFFFFFF },
            new[] { 0xFFF0A8, 0xD8C070, 0x7F8FA8, 0x4E5A72, 0xFFFFFF },
            new[] { 0x98BF6E, 0x5E8F4A, 0xF2E38C, 0xEDE3C8 },
            new[] { 0xB08A6E, 0x6E5444, 0x5E5A66, 0xF0E6C8 },
            new[] { 0xA9B86A, 0x6A7A44, 0xE6D9A8, 0x7A8A4E, 0xF4F1E6 },
        };

        private static IEnumerable<int> AllEnemyColors => EnemyPalettes.SelectMany(p => p);

        [Fact]
        public void S05_TV_COL_01__RedIsDangerColor()
        {
            Assert.True(ColorRules.IsDangerColor(Red));
            Hsv c = Hsv.FromRgb(Red);
            Assert.InRange(c.H, 3.2 - Ratio, 3.2 + Ratio);
            Assert.InRange(c.S, 80.5, 81.5);
            Assert.InRange(c.V, 99.5, 100);
        }

        [Fact]
        public void S05_TV_COL_02__MagentaIsDangerColor()
        {
            Assert.True(ColorRules.IsDangerColor(0xFF2E88));
            Hsv c = Hsv.FromRgb(0xFF2E88);
            Assert.InRange(c.H, 334.2 - Ratio, 334.2 + Ratio);
            Assert.InRange(c.S, 81.5, 82.5);
            Assert.InRange(c.V, 99.5, 100);
        }

        [Fact]
        public void S05_TV_COL_03__BulletOutlineA3004EIsNotDangerValue63_9()
        {
            Assert.False(ColorRules.IsDangerColor(0xA3004E));
            Assert.InRange(Hsv.FromRgb(0xA3004E).V, 63.9 - Ratio, 63.9 + Ratio);
        }

        [Fact]
        public void S05_TV_COL_04__TwelveEnemyPalettesHaveNoDangerColor()
        {
            Assert.Equal(12, EnemyPalettes.Length);
            Assert.DoesNotContain(AllEnemyColors, c => ColorRules.IsDangerColor(c));
        }

        [Fact]
        public void S05_TV_COL_05__TwelveEnemyPalettesHaveNoTeal()
        {
            Assert.DoesNotContain(AllEnemyColors, c => ColorRules.IsTeal(c));
            Assert.DoesNotContain(AllEnemyColors, c => ColorRules.CheckEnemyColor(c) == EnemyColorVerdict.Teal);
        }

        [Fact]
        public void S05_TV_COL_06__TwelveEnemyPalettesSaturationAtMost50()
        {
            // 외곽선 #1B2440 은 몸 색이 아니라 MON 1.4 의 고정 외곽선이라 COL-2 대상에서 뺀다(검사기도 허용 색으로 둔다)
            int[] body = AllEnemyColors.Where(c => c != ColorRules.EnemyOutline).Distinct().ToArray();
            Assert.All(body, c => Assert.True(Hsv.FromRgb(c).S <= ColorRules.EnemyMaxSaturation, ColorRules.Hex(c)));
            int max = body.OrderByDescending(c => Hsv.FromRgb(c).S).First();
            Assert.Equal(0x5E8F4A, max);
            Assert.InRange(Hsv.FromRgb(0x5E8F4A).S, 48.3 - Ratio, 48.3 + Ratio);
            Assert.InRange(Hsv.FromRgb(0xD8C070).S, 48.1 - Ratio, 48.1 + Ratio);
            Assert.DoesNotContain(body, c => ColorRules.CheckEnemyColor(c) == EnemyColorVerdict.OverSaturated);
        }

        [Fact]
        public void S05_TV_COL_07__3A2E2AHue15IsCol4BoundaryWarningOnly()
        {
            Hsv c = Hsv.FromRgb(0x3A2E2A);
            Assert.Equal(15.0, c.H, 9);
            EnemyColorVerdict v = ColorRules.CheckEnemyColor(0x3A2E2A);
            Assert.Equal(EnemyColorVerdict.ForbiddenHueEdge, v);
            Assert.False(ColorRules.IsEnemyError(v));
            Assert.False(ColorRules.IsDangerColor(0x3A2E2A));
        }

        private static void AssertContrast(int bg, double red, double white, double black)
        {
            Assert.InRange(ColorRules.ContrastRatio(bg, Red), red - Ratio, red + Ratio);
            Assert.InRange(ColorRules.ContrastRatio(bg, White), white - Ratio, white + Ratio);
            Assert.InRange(ColorRules.ContrastRatio(bg, Black), black - Ratio, black + Ratio);
        }

        [Fact]
        public void S05_TV_COL_08__ContrastOnDayGrass()
        {
            AssertContrast(0x2F4A3A, 2.7, 9.7, 2.2);
        }

        [Fact]
        public void S05_TV_COL_09__ContrastOnNight()
        {
            AssertContrast(0x161F3A, 4.6, 16.3, 1.3);
        }

        [Fact]
        public void S05_TV_COL_10__ContrastOnLightning()
        {
            AssertContrast(0xFFF36B, 3.1, 1.1, 18.3);
        }

        [Fact]
        public void S05_TV_COL_11__ContrastOnFrostAndFire()
        {
            AssertContrast(0x9FDCFF, 2.4, 1.5, 14.2);
            AssertContrast(0xFF8A1F, 1.5, 2.4, 8.9);
        }

        [Fact]
        public void S05_TV_COL_12__ContrastOnKilnWhiteIs7_4To7_5()
        {
            Assert.InRange(ColorRules.ContrastRatio(0x6E4F30, Red), 2.1 - Ratio, 2.1 + Ratio);
            Assert.InRange(ColorRules.ContrastRatio(0x6E4F30, White), 7.4, 7.5);
            Assert.InRange(ColorRules.ContrastRatio(0x6E4F30, Black), 2.8 - Ratio, 2.8 + Ratio);
        }

        [Fact]
        public void S05_TV_COL_13__RedAgainstWhiteAndBlack()
        {
            Assert.InRange(ColorRules.ContrastRatio(Red, White), 3.5 - Ratio, 3.5 + Ratio);
            Assert.InRange(ColorRules.ContrastRatio(Red, Black), 5.9 - Ratio, 5.9 + Ratio);
        }

        private static void AssertChannelsNear(int expected, int actual)
        {
            for (int shift = 0; shift <= 16; shift += 8)
            {
                int e = (expected >> shift) & 0xFF;
                int a = (actual >> shift) & 0xFF;
                Assert.InRange(a, e - 1, e + 1);
            }
        }

        [Fact]
        public void S05_TV_COL_14__DayGrassDarkened40ThenRed70()
        {
            int dark = ColorRules.Darken(0x2F4A3A, 0.4);
            AssertChannelsNear(0x1C2C23, dark);
            AssertChannelsNear(0xBB372C, ColorRules.Over(dark, Red, 0.7));
        }

        [Fact]
        public void S05_TV_COL_15__NightDarkened40ThenRed70()
        {
            int dark = ColorRules.Darken(0x161F3A, 0.4);
            AssertChannelsNear(0x0D1323, dark);
            AssertChannelsNear(0xB62F2C, ColorRules.Over(dark, Red, 0.7));
        }

        [Fact]
        public void S05_TV_COL_16__FilledColorsMeetDangerConditions()
        {
            foreach (int bg in new[] { 0x2F4A3A, 0x161F3A })
            {
                int filled = ColorRules.Over(ColorRules.Darken(bg, 0.4), Red, 0.7);
                Hsv c = Hsv.FromRgb(filled);
                Assert.InRange(c.S, 75 - 1, 78 + 1);
                Assert.InRange(c.V, 71 - 1, 75 + 1);
                Assert.True(ColorRules.IsDangerColor(filled), ColorRules.Hex(filled));
            }
        }

        [Fact]
        public void S05_TV_COL_17__TelegraphFill60PercentIsRejected()
        {
            Assert.False(TelegraphFillSetting.IsAllowed(60));
            Assert.True(TelegraphFillSetting.IsAllowed(TelegraphFillSetting.MinPercent));
            Assert.Equal(70, TelegraphFillSetting.MinPercent);
            Assert.True(TelegraphFillSetting.IsAllowed(80));
            Assert.True(TelegraphFillSetting.IsAllowed(90));
        }

        private const double ScreenPx = 1920.0 * 1080.0;

        [Fact]
        public void S05_TV_COL_18__FlashAreaOver518400InOneSecondTurnsNextFlashIntoBorder()
        {
            var limiter = new TelegraphFlashLimiter();
            MotionSettings full = MotionSettings.Default;
            Assert.Equal(FlashOutcome.Flash, limiter.Submit((float)(518401 / ScreenPx), 0, full));
            Assert.Equal(FlashOutcome.BorderHighlight, limiter.Submit(0.01f, 500, full));

            // 경계: 정확히 25% (518,400px)는 넘지 않았으므로 다음 번쩍은 그대로다
            var atLimit = new TelegraphFlashLimiter();
            Assert.Equal(FlashOutcome.Flash, atLimit.Submit((float)(518400 / ScreenPx), 0, full));
            Assert.Equal(FlashOutcome.Flash, atLimit.Submit(0.01f, 500, full));
        }
    }
}

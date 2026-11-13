using Wizard.AssetCheck.Color;
using Xunit;

namespace Wizard.AssetCheck.Tests
{
    // spec 14 7.6절 위험색 검사기(TV-110~127). 기대값은 명세 표의 값이다(소수 자리는 표가 적은 만큼만 맞춘다)
    public sealed class DangerColorVectorTests
    {
        private static void AssertHsv(int rgb, double h, double s, double v, double tol)
        {
            Hsv c = Hsv.FromRgb(rgb);
            Assert.InRange(c.H, h - tol, h + tol);
            Assert.InRange(c.S, s - tol, s + tol);
            Assert.InRange(c.V, v - tol, v + tol);
        }

        // 두 자리까지 적힌 값은 ±0.005, 한 자리까지는 ±0.05, 정수는 ±0.5
        private const double Two = 0.005;
        private const double One = 0.05;

        [Fact]
        public void S14_TV_110__RedFF3B30IsDangerAndProper()
        {
            AssertHsv(0xFF3B30, 3.19, 81.18, 100, Two);
            Assert.True(ColorRules.InDangerRange(0xFF3B30));
            Assert.True(ColorRules.IsProperDangerUse(0xFF3B30));
        }

        [Fact]
        public void S14_TV_111__MagentaFF2E88IsDangerAndProper()
        {
            AssertHsv(0xFF2E88, 334.16, 81.96, 100, Two);
            Assert.True(ColorRules.InDangerRange(0xFF2E88));
            Assert.True(ColorRules.IsProperDangerUse(0xFF2E88));
        }

        [Fact]
        public void S14_TV_112__EnemyBulletOutlineA3004EIsNotDangerValueBelow70()
        {
            AssertHsv(0xA3004E, 331.29, 100, 63.92, Two);
            Assert.False(ColorRules.InDangerRange(0xA3004E));
            Assert.False(ColorRules.IsProperDangerUse(0xA3004E));
        }

        [Fact]
        public void S14_TV_113__Char5Hood7A336AIsNotDanger()
        {
            Hsv c = Hsv.FromRgb(0x7A336A);
            Assert.InRange(c.H, 313.52 - Two, 313.52 + Two);
            Assert.InRange(c.S, 58.2 - One, 58.2 + One);
            Assert.InRange(c.V, 47.84 - Two, 47.84 + Two);
            Assert.False(ColorRules.InDangerRange(0x7A336A));
        }

        [Fact]
        public void S14_TV_114__Char5Boot704456HueInRangeButLowSaturationAndValue()
        {
            AssertHsv(0x704456, 335.45, 39.29, 43.92, Two);
            Assert.True(Hsv.FromRgb(0x704456).H >= ColorRules.DangerHueFrom);
            Assert.False(ColorRules.InDangerRange(0x704456));
        }

        [Fact]
        public void S14_TV_115__Char5HoodLightB25A9BIsNotDanger()
        {
            Hsv c = Hsv.FromRgb(0xB25A9B);
            Assert.InRange(c.H, 315.68 - Two, 315.68 + Two);
            Assert.InRange(c.S, 49.44 - Two, 49.44 + Two);
            Assert.InRange(c.V, 69.8 - One, 69.8 + One);
            Assert.False(ColorRules.InDangerRange(0xB25A9B));
        }

        [Fact]
        public void S14_TV_116__FireOrangeFF8A1FIsNotDangerAndHueAbove25()
        {
            AssertHsv(0xFF8A1F, 28.66, 87.84, 100, Two);
            Assert.False(ColorRules.InDangerRange(0xFF8A1F));
            Assert.True(Hsv.FromRgb(0xFF8A1F).H >= 25);
        }

        [Fact]
        public void S14_TV_117__FF4040IsDangerButNotProperSaturationBelow80()
        {
            Hsv c = Hsv.FromRgb(0xFF4040);
            Assert.InRange(c.H, 0, Two);
            Assert.InRange(c.S, 74.9 - One, 74.9 + One);
            Assert.Equal(100, c.V, 9);
            Assert.True(ColorRules.InDangerRange(0xFF4040));
            Assert.False(ColorRules.IsProperDangerUse(0xFF4040));
        }

        [Fact]
        public void S14_TV_118__FF6040IsDangerButNotProper()
        {
            Hsv c = Hsv.FromRgb(0xFF6040);
            Assert.InRange(c.H, 10.05 - Two, 10.05 + Two);
            Assert.InRange(c.S, 74.9 - One, 74.9 + One);
            Assert.True(ColorRules.InDangerRange(0xFF6040));
            Assert.False(ColorRules.IsProperDangerUse(0xFF6040));
        }

        [Fact]
        public void S14_TV_119__FF2659IsDangerAndProper()
        {
            Hsv c = Hsv.FromRgb(0xFF2659);
            Assert.InRange(c.H, 345.9 - One, 345.9 + One);
            Assert.InRange(c.S, 85.1 - One, 85.1 + One);
            Assert.True(ColorRules.InDangerRange(0xFF2659));
            Assert.True(ColorRules.IsProperDangerUse(0xFF2659));
        }

        [Fact]
        public void S14_TV_120__FF4D4DSaturation69_8IsNotDanger()
        {
            Hsv c = Hsv.FromRgb(0xFF4D4D);
            Assert.InRange(c.S, 69.8 - One, 69.8 + One);
            Assert.True(c.S < ColorRules.DangerMinSaturation);
            Assert.False(ColorRules.InDangerRange(0xFF4D4D));
        }

        [Fact]
        public void S14_TV_121__GrayB3B3B3HasNoHueAndIsNotDanger()
        {
            Hsv c = Hsv.FromRgb(0xB3B3B3);
            Assert.Equal(0, c.H, 9);
            Assert.Equal(0, c.S, 9);
            Assert.InRange(c.V, 70.2 - One, 70.2 + One);
            Assert.False(c.HasHue);
            Assert.False(ColorRules.InDangerRange(0xB3B3B3));
        }

        [Fact]
        public void S14_TV_122__WhiteIsNotDanger()
        {
            AssertHsv(0xFFFFFF, 0, 0, 100, Two);
            Assert.False(ColorRules.InDangerRange(0xFFFFFF));
        }

        [Fact]
        public void S14_TV_123__AlertAmberF2A33AIsNotDanger()
        {
            AssertHsv(0xF2A33A, 34.24, 76.03, 94.9, One);
            Assert.False(ColorRules.InDangerRange(0xF2A33A));
        }

        [Theory]
        [InlineData(0xFFE94A)]
        [InlineData(0xFFD24A)]
        [InlineData(0xB8F04A)]
        [InlineData(0x4FE3C1)]
        [InlineData(0x9FDCFF)]
        [InlineData(0x8A5CFF)]
        public void S14_TV_124__TokenColorsAreNotDanger(int rgb)
        {
            Assert.False(ColorRules.InDangerRange(rgb));
        }

        // world-characters.md 4.1~4.5(spec 14 4.2절 표): 달구 12, 초롱 12, 갈피 12, 성에 12, 그믐 16 = 64색
        public static readonly int[] Char1Palette = { 0x2A1A20, 0x7A4322, 0xB5692F, 0xE39A4E, 0xFFD79A, 0x16655F, 0x1F8C82, 0x4FC2B0, 0xF2CFA0, 0x5A3A2A, 0xE8E4DA, 0xF4F4F4 };
        public static readonly int[] Char2Palette = { 0x1C1830, 0x3A2A6A, 0x5A45A0, 0x8068C8, 0x8A6A14, 0xC89A22, 0xEBC04A, 0x6FE0C0, 0xF0CFA8, 0xFFF36B, 0xFFFFFF, 0x4A3A2A };
        public static readonly int[] Char3Palette = { 0x1A2030, 0x5A3A2E, 0x8A5A3E, 0xB88A5A, 0xEDE3C8, 0x3A4658, 0x55657C, 0x7C8EA6, 0xF2D2AC, 0x4FE3C1, 0xD6FFF3, 0xC8A24A };
        public static readonly int[] Char4Palette = { 0x1E2238, 0xB9A8E8, 0xE4DAFF, 0xC9B88A, 0xF3E7C0, 0xFFF8E2, 0x2E55B8, 0x4F7CE0, 0x9FDCFF, 0xF4FBFF, 0xF4D6B4, 0x7A5A3A };
        public static readonly int[] Char5Palette = { 0x1B0D1B, 0x561F4C, 0x7A336A, 0xB25A9B, 0xE2ACFA, 0x222641, 0x3E4675, 0x6271AE, 0xCA8F44, 0xF8C062, 0x704456, 0xA47384, 0x854972, 0x8539BF, 0xD68CF9, 0xFDF6FF };

        [Fact]
        public void S14_TV_125__FiveCharacterPalettes64ColorsHaveNoDangerColor()
        {
            int total = 0, danger = 0;
            foreach (int[] p in new[] { Char1Palette, Char2Palette, Char3Palette, Char4Palette, Char5Palette })
            {
                foreach (int c in p)
                {
                    total++;
                    if (ColorRules.InDangerRange(c))
                    {
                        danger++;
                    }
                }
            }
            Assert.Equal(64, total);
            Assert.Equal(0, danger);
        }

        [Fact]
        public void S14_TV_125__Char5PaletteInRulesFileIsTheConfirmedSixteen()
        {
            // 반입 검사가 쓰는 원본 팔레트(asset_rules.json)가 명세의 16색과 같다
            var rules = Wizard.AssetCheck.Check.AssetRules.Parse(System.IO.File.ReadAllText(Wizard.TestKit.TestRepo.Path(Wizard.AssetCheck.Check.AssetRules.RepoPath)));
            Assert.Equal(Char5Palette, rules.Palette.Palette);
        }

        [Theory]
        [InlineData(0xAEB6C2)]
        [InlineData(0xF4F1E6)]
        public void S14_TV_126__CrackedMirrorShardColorsAreNotDanger(int rgb)
        {
            Assert.False(ColorRules.InDangerRange(rgb));
        }

        [Theory]
        [InlineData(0x8E9AAE)]
        [InlineData(0x1F2130)]
        public void S14_TV_127__PlaceholderColorsAreNotDanger(int rgb)
        {
            Assert.False(ColorRules.InDangerRange(rgb));
        }
    }
}

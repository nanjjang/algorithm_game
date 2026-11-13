using System.Linq;
using Wizard.AssetCheck.Check;
using Wizard.AssetCheck.Color;
using Wizard.AssetCheck.Png;
using Wizard.TestKit;
using Wizard.ViewLogic.Assets;
using Wizard.ViewLogic.Common;
using Wizard.ViewLogic.Presentation;
using Xunit;

namespace Wizard.AssetCheck.Tests
{
    // 수락 조건: char.5(그믐) 그림의 고유 색이 16 이하이고 원본 팔레트의 부분집합, 지팡이 +0 머리가 보라 계열(DECISIONS 2.2)
    public sealed class Char5PaletteTests
    {
        private static readonly int[] Palette = DangerColorVectorTests.Char5Palette;
        private static readonly PackTextureSettings PixelExact = new PackTextureSettings(true, false, false, true);

        private static AssetRegistry CharRegistry(string variant, string file)
        {
            var reg = new AssetRegistry();
            reg.Add(new AssetRecord(new AssetKey(AssetPack.Character, "char.5", variant), file, new PixelSize(64, 64), new[] { new PixelRect(0, 0, 64, 64) }));
            reg.SetSettings(AssetPack.Character, PixelExact);
            return reg;
        }

        [Fact]
        public void BodyUsingOnlyThePaletteIsImported()
        {
            using (var sb = new Sandbox())
            {
                sb.WritePng("players/x/body.png", TestPng.Rgba(64, 64, (x, y) => TestPng.Opaque(Palette[(x + y) % Palette.Length])));
                ImportCheckResult r = ImportChecker.Run(ImportCheckTests.InputFor(sb, CharRegistry("idle_down", "players/x/body.png")));
                Assert.DoesNotContain(r.Errors, f => f.Check == CheckNames.Palette);
            }
        }

        [Fact]
        public void BodyWithAColorOutsideThePaletteFails()
        {
            using (var sb = new Sandbox())
            {
                sb.WritePng("players/x/body.png", TestPng.Rgba(64, 64, (x, y) => TestPng.Opaque(x == 3 && y == 3 ? 0x2C3050 : Palette[x % 4])));
                ImportCheckResult r = ImportChecker.Run(ImportCheckTests.InputFor(sb, CharRegistry("idle_down", "players/x/body.png")));
                Finding f = Assert.Single(r.Errors, e => e.Check == CheckNames.Palette);
                Assert.Contains("#2C3050", f.Message);
                Assert.Contains("not in the original palette", f.Message);
            }
        }

        [Fact]
        public void BodyWithSeventeenColorsFails()
        {
            using (var sb = new Sandbox())
            {
                int[] colors = Palette.Concat(new[] { 0x3A3A3A }).ToArray();
                sb.WritePng("players/x/body.png", TestPng.Rgba(64, 64, (x, y) => TestPng.Opaque(colors[(y * 64 + x) % colors.Length])));
                ImportCheckResult r = ImportChecker.Run(ImportCheckTests.InputFor(sb, CharRegistry("idle_down", "players/x/body.png")));
                Assert.Contains(r.Errors, f => f.Check == CheckNames.Palette && f.Message.Contains("17 colors"));
            }
        }

        [Fact]
        public void SelectionIsExcludedWithAWarning()
        {
            using (var sb = new Sandbox())
            {
                sb.WritePng("players/x/selection_192x192.png", TestPng.Rgba(64, 64, (x, y) => TestPng.Opaque((x << 16) | (y << 8))));
                ImportCheckResult r = ImportChecker.Run(ImportCheckTests.InputFor(sb, CharRegistry("selection", "players/x/selection_192x192.png")));
                Assert.DoesNotContain(r.Errors, f => f.Check == CheckNames.Palette);
                Assert.Contains(r.Findings, f => !f.IsError && f.Check == CheckNames.Palette && f.Message.Contains("excluded"));
            }
        }

        private static AssetRegistry WandRegistry(string file)
        {
            var reg = new AssetRegistry();
            var hand = new SheetGrid(file, new PixelSize(32, 64), 1, 6);
            var rows = new[] { "down_hold", "down_aim", "right_hold", "right_aim", "up_hold", "up_aim" };
            var stages = new[] { new WandStagePoints(0, new PixelPoint(16, 37), new[] { new PixelPoint(12, 40), new PixelPoint(20, 40) }, new PixelPoint(18, 49)) };
            reg.AddWand(new WandSheet("wand.5", new PixelPoint(16, 48), hand, rows, new SheetGrid("w/i.png", new PixelSize(192, 192), 1, 1),
                new SheetGrid("w/c.png", new PixelSize(64, 64), 1, 1), stages));
            return reg;
        }

        // 머리 점(16, 37) 위에 결정을 그린다. 외곽선은 #1B0D1B, 나무는 #704456
        private static byte[] WandSheet(int crystal)
        {
            return TestPng.Rgba(32, 384, (x, y) =>
            {
                if (y >= 64 || x < 12 || x > 20)
                {
                    return 0;
                }
                if (y >= 24 && y < 33)
                {
                    return TestPng.Opaque(x == 12 || x == 20 ? 0x1B0D1B : crystal);
                }
                if (y >= 33 && y < 64)
                {
                    return TestPng.Opaque(0x704456);
                }
                return 0;
            });
        }

        [Fact]
        public void PurpleWandHeadPasses()
        {
            using (var sb = new Sandbox())
            {
                sb.WritePng("w/hand.png", WandSheet(0x8A5CFF));
                ImportCheckResult r = ImportChecker.Run(ImportCheckTests.InputFor(sb, WandRegistry("w/hand.png")));
                Assert.DoesNotContain(r.Errors, f => f.Check == CheckNames.Palette);
            }
        }

        [Theory]
        [InlineData(0x4FE3C1)]   // 청록
        [InlineData(0x7A336A)]   // 그믐 후드의 자주(313도)는 보라 결정이 아니다
        public void NonPurpleWandHeadFails(int crystal)
        {
            using (var sb = new Sandbox())
            {
                sb.WritePng("w/hand.png", WandSheet(crystal));
                ImportCheckResult r = ImportChecker.Run(ImportCheckTests.InputFor(sb, WandRegistry("w/hand.png")));
                Assert.Contains(r.Errors, f => f.Check == CheckNames.Palette && f.Message.Contains("head is not purple"));
            }
        }

        [Fact]
        public void PaletteCrystalColorsArePurple()
        {
            PaletteRules rules = AssetRules.Parse(System.IO.File.ReadAllText(TestRepo.Path(AssetRules.RepoPath))).Palette;
            Assert.True(ImportChecker.IsPurple(Hsv.FromRgb(0x8539BF), rules));
            Assert.True(ImportChecker.IsPurple(Hsv.FromRgb(0xD68CF9), rules));
            Assert.False(ImportChecker.IsPurple(Hsv.FromRgb(0x7A336A), rules));
        }

        // 지금 저장소의 원본으로 검사가 돈다(수락 조건): 원본 기준 그림이 있고, char.5 몸·초상과 wand.5 를 실제로 읽는다
        [Fact]
        public void RunsOnTheRepositoryOriginals()
        {
            AssetCheckInput input = AssetCheckRepo.Load(TestRepo.Root);
            Assert.True(input.ReferenceExists);
            Assert.Equal("art/player/dark_wizard/idle-reference-details.png", input.Rules.Palette.Reference);
            Assert.True(PngImage.TryReadSize(TestRepo.Path(input.Rules.Palette.Reference), out int rw, out int rh));
            Assert.Equal(64, rw);
            Assert.Equal(64, rh);
            Assert.NotEmpty(input.Registry.RecordsOf(AssetPack.Character, "char.5"));
            Assert.True(input.Registry.TryGetWand("wand.5", out _));

            ImportCheckResult r = ImportChecker.Run(input);
            // 검사가 char.5 그림을 보았다는 표시: 뺀 그림의 경고가 나온다
            Assert.Contains(r.Findings, f => f.Check == CheckNames.Palette && f.File.EndsWith("/selection_192x192.png") && f.File.Contains("players/"));
            // DECISIONS 2.2: 그믐 지팡이 +0 의 머리는 보라 결정이다
            Assert.DoesNotContain(r.Errors, f => f.Check == CheckNames.Palette && f.Message.Contains("head is not purple"));
            // 16색 이하(DECISIONS 2.2)
            Assert.DoesNotContain(r.Errors, f => f.Check == CheckNames.Palette && f.Message.Contains(" colors (at most"));
        }
    }
}

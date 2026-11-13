using System.Collections.Generic;
using System.IO;
using System.Linq;
using Wizard.AssetCheck.Check;
using Wizard.AssetCheck.Report;
using Wizard.AssetCheck.Slots;
using Wizard.TestKit;
using Wizard.ViewLogic.Assets;
using Wizard.ViewLogic.Common;
using Wizard.ViewLogic.Presentation;
using Xunit;

namespace Wizard.AssetCheck.Tests
{
    // spec 14 5.7.1절의 다섯 검사(+ 치장 그림의 위험색, TV-238). 일부러 틀린 입력을 넣고 정해진 진단으로 실패하는지 본다
    public sealed class ImportCheckTests
    {
        private const int Grass = 0x5E8F4A;
        private const int DangerRed = 0xFF3B30;
        private static readonly PackTextureSettings PixelExact = new PackTextureSettings(true, false, false, true);

        internal static AssetCheckInput InputFor(Sandbox sb, AssetRegistry reg)
        {
            var problems = new List<AssetLoadProblem>();
            AssetAliases aliases = AssetAliases.Parse(File.ReadAllText(TestRepo.Path(AssetAliases.RepoPath)), problems);
            AssetRules rules = AssetRules.Parse(File.ReadAllText(TestRepo.Path(AssetRules.RepoPath)));
            return new AssetCheckInput(reg, aliases, new AssetLoadProblem[0], AssetSlotTable.Parse("{ \"slots\": [] }"), rules,
                new ImageCache(sb.Art), true);
        }

        private static AssetRecord Strip(AssetKey key, string file, int w, int h, int frames)
        {
            var rects = new PixelRect[frames];
            for (int i = 0; i < frames; i++)
            {
                rects[i] = new PixelRect(i * w, 0, w, h);
            }
            return new AssetRecord(key, file, new PixelSize(w, h), rects);
        }

        private static byte[] Solid(int w, int h, int rgb) => TestPng.Rgba(w, h, (x, y) => TestPng.Opaque(rgb));

        private static byte[] WithOnePixel(int w, int h, int rgb, int dot) =>
            TestPng.Rgba(w, h, (x, y) => x == w / 2 && y == h / 2 ? TestPng.Opaque(dot) : TestPng.Opaque(rgb));

        private static AssetRegistry Registry(params AssetRecord[] records)
        {
            var reg = new AssetRegistry();
            foreach (AssetRecord r in records)
            {
                Assert.True(reg.Add(r));
                reg.SetSettings(r.Key.Pack, PixelExact);
            }
            return reg;
        }

        private static readonly AssetKey Cosmetic = new AssetKey(AssetPack.Character, "char.1", "cosmetic/head/mirror_shard");

        [Fact]
        public void S14_TV_238__CosmeticPictureWithOneDangerPixelFailsTheImportCheck()
        {
            using (var sb = new Sandbox())
            {
                sb.WritePng("players/cosmetic/head_mirror_shard_24x24.png", WithOnePixel(24, 24, 0xAEB6C2, DangerRed));
                AssetRegistry reg = Registry(Strip(Cosmetic, "players/cosmetic/head_mirror_shard_24x24.png", 24, 24, 1));
                ImportCheckResult result = ImportChecker.Run(InputFor(sb, reg));

                Finding f = Assert.Single(result.Errors, e => e.Check == CheckNames.Danger);
                Assert.Equal("art/production/players/cosmetic/head_mirror_shard_24x24.png", f.File);
                Assert.Contains("cosmetic", f.Message);
                Assert.Contains("1 danger-color pixel", f.Message);
                Assert.False(result.Ok);
                SlotReport slot = AssetReport.Build(result, new ApprovalLedger()).Find("Character:char.1/cosmetic/head/mirror_shard")!;
                Assert.Equal(SlotStatus.Failed, slot.Status);
            }
        }

        [Fact]
        public void S14_TV_238__SameCosmeticWithoutDangerPixelIsImported()
        {
            using (var sb = new Sandbox())
            {
                sb.WritePng("players/cosmetic/head_mirror_shard_24x24.png", WithOnePixel(24, 24, 0xAEB6C2, 0xF4F1E6));
                AssetRegistry reg = Registry(Strip(Cosmetic, "players/cosmetic/head_mirror_shard_24x24.png", 24, 24, 1));
                ImportCheckResult result = ImportChecker.Run(InputFor(sb, reg));
                Assert.DoesNotContain(result.Errors, e => e.Check == CheckNames.Danger);
                Assert.Equal(SlotStatus.Imported, AssetReport.Build(result, new ApprovalLedger()).Find("Character:char.1/cosmetic/head/mirror_shard")!.Status);
            }
        }

        [Fact]
        public void TransparentDangerPixelIsNotCounted()
        {
            using (var sb = new Sandbox())
            {
                sb.WritePng("ui/kit/k-99_test_8x8.png", TestPng.Rgba(8, 8, (x, y) => x == 0 ? 0xFF3B3000u : TestPng.Opaque(Grass)));
                AssetRegistry reg = Registry(Strip(new AssetKey(AssetPack.Ui, "K-99", "test", "normal"), "ui/kit/k-99_test_8x8.png", 8, 8, 1));
                Assert.DoesNotContain(ImportChecker.Run(InputFor(sb, reg)).Errors, e => e.Check == CheckNames.Danger);
            }
        }

        [Theory]
        [InlineData(AssetPack.Ui, "K-99", true)]
        [InlineData(AssetPack.Wand, "wand.1", true)]
        [InlineData(AssetPack.Character, "char.2", true)]
        [InlineData(AssetPack.Fx, "player/fireball", true)]
        [InlineData(AssetPack.Fx, "ritual/seal_fire", true)]
        [InlineData(AssetPack.Fx, "loot/coin", true)]
        [InlineData(AssetPack.Fx, "enemy/round_small", false)]
        public void DangerPixelsArePlayerSideFailuresExceptEnemyFx(AssetPack pack, string id, bool fails)
        {
            using (var sb = new Sandbox())
            {
                sb.WritePng("x/pic_8x8.png", WithOnePixel(8, 8, Grass, DangerRed));
                AssetRegistry reg = Registry(Strip(new AssetKey(pack, id, "v"), "x/pic_8x8.png", 8, 8, 1));
                bool failed = ImportChecker.Run(InputFor(sb, reg)).Errors.Any(e => e.Check == CheckNames.Danger);
                Assert.Equal(fails, failed);
            }
        }

        [Fact]
        public void MonsterDangerAndTealAreErrorsOtherEnemyRulesAreWarnings()
        {
            using (var sb = new Sandbox())
            {
                sb.WritePng("monsters/enemy.a.png", WithOnePixel(8, 8, Grass, DangerRed));
                sb.WritePng("monsters/enemy.b.png", WithOnePixel(8, 8, Grass, 0x4FE3C1));
                sb.WritePng("monsters/enemy.c.png", WithOnePixel(8, 8, Grass, 0x3A2E2A));
                AssetRegistry reg = Registry(
                    Strip(new AssetKey(AssetPack.Monster, "enemy.a", "move"), "monsters/enemy.a.png", 8, 8, 1),
                    Strip(new AssetKey(AssetPack.Monster, "enemy.b", "move"), "monsters/enemy.b.png", 8, 8, 1),
                    Strip(new AssetKey(AssetPack.Monster, "enemy.c", "move"), "monsters/enemy.c.png", 8, 8, 1));
                ImportCheckResult r = ImportChecker.Run(InputFor(sb, reg));
                Assert.Contains(r.Errors, e => e.Check == CheckNames.EnemyColor && e.File.EndsWith("enemy.a.png") && e.Message.Contains("danger"));
                Assert.Contains(r.Errors, e => e.Check == CheckNames.EnemyColor && e.File.EndsWith("enemy.b.png") && e.Message.Contains("teal"));
                Assert.DoesNotContain(r.Errors, e => e.File.EndsWith("enemy.c.png"));
                Assert.Contains(r.Findings, e => !e.IsError && e.File.EndsWith("enemy.c.png") && e.Message.Contains("COL-4 boundary"));
                Assert.DoesNotContain(r.Errors, e => e.Check == CheckNames.Danger);
            }
        }

        [Fact]
        public void MissingFileFails()
        {
            using (var sb = new Sandbox())
            {
                AssetRegistry reg = Registry(Strip(new AssetKey(AssetPack.Fx, "player/x"), "effects/player/x_8x8_f1.png", 8, 8, 1));
                ImportCheckResult r = ImportChecker.Run(InputFor(sb, reg));
                Finding f = Assert.Single(r.Errors);
                Assert.Equal(CheckNames.File, f.Check);
                Assert.Contains("not found", f.Message);
            }
        }

        [Fact]
        public void UnreadablePngFails()
        {
            using (var sb = new Sandbox())
            {
                sb.WriteText("art/production/effects/player/x_8x8_f1.png", "not a png");
                AssetRegistry reg = Registry(Strip(new AssetKey(AssetPack.Fx, "player/x"), "effects/player/x_8x8_f1.png", 8, 8, 1));
                Finding f = Assert.Single(ImportChecker.Run(InputFor(sb, reg)).Errors);
                Assert.Equal(CheckNames.File, f.Check);
                Assert.Contains("PNG", f.Message);
            }
        }

        [Fact]
        public void UiStripMustBeWidthTimesFramesByHeight()
        {
            using (var sb = new Sandbox())
            {
                sb.WritePng("ui/kit/k-99_test_8x8_f3.png", Solid(16, 8, Grass));
                AssetRegistry reg = Registry(Strip(new AssetKey(AssetPack.Ui, "K-99", "test", "normal"), "ui/kit/k-99_test_8x8_f3.png", 8, 8, 3));
                Finding f = Assert.Single(ImportChecker.Run(InputFor(sb, reg)).Errors);
                Assert.Equal(CheckNames.Size, f.Check);
                Assert.Contains("16x8", f.Message);
                Assert.Contains("24x8", f.Message);
            }
        }

        [Fact]
        public void FrameRectOutsideTheSheetFails()
        {
            using (var sb = new Sandbox())
            {
                sb.WritePng("monsters/enemy.a.png", Solid(64, 64, Grass));
                AssetRegistry reg = Registry(Strip(new AssetKey(AssetPack.Monster, "enemy.a", "move"), "monsters/enemy.a.png", 64, 64, 2));
                Finding f = Assert.Single(ImportChecker.Run(InputFor(sb, reg)).Errors);
                Assert.Equal(CheckNames.Size, f.Check);
                Assert.Contains("frame 1", f.Message);
            }
        }

        [Theory]
        [InlineData(false, false, false, "filter")]
        [InlineData(true, true, false, "mipmaps")]
        [InlineData(true, false, true, "compression")]
        public void TextureSettingsMustBePointNoMipmapsNoCompression(bool point, bool mipmaps, bool compression, string word)
        {
            using (var sb = new Sandbox())
            {
                sb.WritePng("effects/player/x_8x8_f1.png", Solid(8, 8, Grass));
                var reg = new AssetRegistry();
                reg.Add(Strip(new AssetKey(AssetPack.Fx, "player/x"), "effects/player/x_8x8_f1.png", 8, 8, 1));
                reg.SetSettings(AssetPack.Fx, new PackTextureSettings(point, mipmaps, compression, true));
                ImportCheckResult r = ImportChecker.Run(InputFor(sb, reg));
                Finding f = Assert.Single(r.Errors);
                Assert.Equal(CheckNames.Settings, f.Check);
                Assert.Contains(word, f.Message);
                Assert.Equal("art/production/effects/manifest.json", f.File);
                Assert.Equal(SlotStatus.Failed, AssetReport.Build(r, new ApprovalLedger()).Find("Fx:player/x")!.Status);
            }
        }

        [Fact]
        public void PackWithoutDeclaredSettingsFails()
        {
            using (var sb = new Sandbox())
            {
                sb.WritePng("effects/player/x_8x8_f1.png", Solid(8, 8, Grass));
                var reg = new AssetRegistry();
                reg.Add(Strip(new AssetKey(AssetPack.Fx, "player/x"), "effects/player/x_8x8_f1.png", 8, 8, 1));
                Assert.Contains(ImportChecker.Run(InputFor(sb, reg)).Errors, f => f.Check == CheckNames.Settings);
            }
        }

        [Fact]
        public void BackgroundPackedIntoAnAtlasFails()
        {
            using (var sb = new Sandbox())
            {
                sb.WritePng("ui/screen/menu/s-0999_test_background_1920x1080.png", Solid(1920, 1080, 0x1F2130));
                sb.WritePng("ui/atlas/menu-0.png", Solid(2048, 2048, 0x1F2130));
                sb.WritePng("ui/kit/k-99_small_8x8.png", Solid(8, 8, 0x1F2130));
                sb.WriteText("art/production/ui/manifest.json", "{ \"sampling\": \"nearest\", \"mipmaps\": false, \"texture_compression\": false, \"assets\": ["
                    + "{ \"id\": \"S-0999\", \"file\": \"screen/menu/s-0999_test_background_1920x1080.png\", \"width\": 1920, \"height\": 1080, \"frames\": 1, \"atlas\": { \"page\": \"atlas/menu-0.png\", \"rect\": [0, 0, 1920, 1080] } },"
                    + "{ \"id\": \"K-99\", \"file\": \"kit/k-99_small_8x8.png\", \"width\": 8, \"height\": 8, \"frames\": 1, \"atlas\": { \"page\": \"atlas/menu-0.png\", \"rect\": [0, 1080, 8, 8] } } ] }");
                ImportCheckResult r = ImportChecker.Run(sb.Load());
                Finding f = Assert.Single(r.Errors, e => e.Check == CheckNames.Atlas);
                Assert.Contains("s-0999", f.File);
                Assert.DoesNotContain(r.Errors, e => e.Check == CheckNames.Atlas && e.File.Contains("k-99"));
            }
        }

        [Fact]
        public void AtlasRectOutsideThePageFails()
        {
            using (var sb = new Sandbox())
            {
                sb.WritePng("ui/atlas/kit-0.png", Solid(16, 16, 0x1F2130));
                sb.WritePng("ui/kit/k-99_small_8x8.png", Solid(8, 8, 0x1F2130));
                sb.WriteText("art/production/ui/manifest.json", "{ \"sampling\": \"nearest\", \"mipmaps\": false, \"texture_compression\": false, \"assets\": ["
                    + "{ \"id\": \"K-99\", \"file\": \"kit/k-99_small_8x8.png\", \"width\": 8, \"height\": 8, \"frames\": 1, \"atlas\": { \"page\": \"atlas/kit-0.png\", \"rect\": [12, 0, 8, 8] } } ] }");
                ImportCheckResult r = ImportChecker.Run(sb.Load());
                Finding f = Assert.Single(r.Errors, e => e.Check == CheckNames.Size);
                Assert.Equal("art/production/ui/atlas/kit-0.png", f.File);
            }
        }

        [Fact]
        public void ContentIdsWithoutPicturesAreReportedMissingNotFailed()
        {
            using (var sb = new Sandbox())
            {
                ImportCheckResult r = ImportChecker.Run(InputFor(sb, new AssetRegistry()));
                Assert.True(r.Ok);
                var ids = r.Missing.Select(m => m.SlotId).ToList();
                Assert.Contains("Ui:I-B1/block_fireball", ids);
                Assert.Contains("Ui:I-B2/block_fireball", ids);
                Assert.Contains("Ui:I-E2/element_fire", ids);
                Assert.DoesNotContain("Ui:I-E2/element_neutral", ids);   // spec 14 5.2절: I-E2 는 무 없음(asset_rules.json not_required)
                Assert.Contains("Ui:I-E1/element_neutral", ids);
                Assert.Contains("Character:char.5", ids);
                Assert.Contains("Wand:wand.5", ids);
                Assert.Contains("Monster:enemy.slime_moss", ids);
                Assert.Contains("Monster:boss.kiln_boar", ids);
                Assert.Contains("Fx:player/fireball", ids);
                Assert.All(r.Missing, m => Assert.Contains(r.Findings, f => !f.IsError && f.Check == CheckNames.Content && f.Slot == m.SlotId));
                AssetReport report = AssetReport.Build(r, new ApprovalLedger());
                Assert.All(report.Slots, s => Assert.Equal(SlotStatus.Missing, s.Status));
            }
        }

        [Fact]
        public void AllContentIdsComeFromContentIdsAndAreSorted()
        {
            IReadOnlyList<string> ids = ImportChecker.AllContentIds();
            Assert.Contains("block.fireball", ids);
            Assert.Contains("char.1", ids);
            Assert.Contains("currency.gold", ids);
            Assert.Equal(ids.OrderBy(s => s, System.StringComparer.Ordinal), ids);
            Assert.Equal(ids.Count, ids.Distinct().Count());
        }
    }
}

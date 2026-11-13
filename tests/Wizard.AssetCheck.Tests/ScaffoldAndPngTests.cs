using System.IO;
using System.Linq;
using Wizard.AssetCheck.Png;
using Wizard.AssetCheck.Scaffold;
using Wizard.TestKit;
using Xunit;

namespace Wizard.AssetCheck.Tests
{
    // 수락 조건: assetcheck scaffold <png> 가 파일 이름 규칙에서 매니페스트 항목 초안을 만들어 보여 준다(자동으로 쓰지 않는다)
    public sealed class ScaffoldAndPngTests
    {
        private static byte[] Solid(int w, int h) => TestPng.Rgba(w, h, (x, y) => TestPng.Opaque(0x2A2D40));

        private static string[] Files(Sandbox sb) =>
            Directory.GetFiles(sb.Root, "*", SearchOption.AllDirectories).Select(f => f + ":" + File.GetLastWriteTimeUtc(f).Ticks).OrderBy(s => s, System.StringComparer.Ordinal).ToArray();

        private static int Run(Sandbox sb, string artPath, out string stdout, out string stderr)
        {
            var o = new StringWriter();
            var e = new StringWriter();
            int code = Program.Run(new[] { "scaffold", sb.PathOf("art/production/" + artPath), "--root", sb.Root }, o, e);
            stdout = o.ToString();
            stderr = e.ToString();
            return code;
        }

        [Fact]
        public void UiFileNameGivesAnEntryAndWritesNothing()
        {
            using (var sb = new Sandbox())
            {
                sb.WritePng("ui/kit/k-20_button_large_48x64_hover.png", Solid(48, 64));
                string[] before = Files(sb);
                Assert.Equal(Program.ExitOk, Run(sb, "ui/kit/k-20_button_large_48x64_hover.png", out string o, out _));
                Assert.Contains("\"id\": \"K-20\"", o);
                Assert.Contains("\"file\": \"kit/k-20_button_large_48x64_hover.png\"", o);
                Assert.Contains("\"width\": 48, \"height\": 64, \"frames\": 1", o);
                Assert.Contains("button_large # hover", o);
                Assert.Contains("art/production/ui/manifest.json", o);
                Assert.Contains("nothing was written", o);
                Assert.Equal(before, Files(sb));
            }
        }

        [Fact]
        public void UiFramesComeFromTheSuffixAndSizeMismatchIsNoted()
        {
            using (var sb = new Sandbox())
            {
                sb.WritePng("ui/kit/k-40_selection_corner_24x24_f2.png", Solid(24, 24));
                Assert.Equal(Program.ExitOk, Run(sb, "ui/kit/k-40_selection_corner_24x24_f2.png", out string o, out _));
                Assert.Contains("\"frames\": 2", o);
                Assert.Contains("frame_ms", o);
                Assert.Contains("the import check will fail", o);
            }
        }

        [Fact]
        public void FxFileNameGivesFrameRects()
        {
            using (var sb = new Sandbox())
            {
                sb.WritePng("effects/player/hit_fire_32x32_f3.png", Solid(96, 32));
                Assert.Equal(Program.ExitOk, Run(sb, "effects/player/hit_fire_32x32_f3.png", out string o, out _));
                Assert.Contains("\"hit_fire\": { \"path\": \"player/hit_fire_32x32_f3.png\", \"size\": [32, 32], \"frames\": [[0, 0, 32, 32], [32, 0, 32, 32], [64, 0, 32, 32]]", o);
                Assert.Contains(ManifestScaffold.FillMarker, o);
                Assert.DoesNotContain("import check will fail", o);
            }
        }

        [Fact]
        public void MonsterAndWandAndPlayerNamesAreRecognised()
        {
            using (var sb = new Sandbox())
            {
                sb.WritePng("monsters/enemy.slime_moss.png", Solid(256, 64));
                sb.WritePng("wands/x/hand.png", Solid(448, 384));
                sb.WritePng("players/x/body.png", Solid(768, 832));
                Assert.Equal(Program.ExitOk, Run(sb, "monsters/enemy.slime_moss.png", out string mo, out _));
                Assert.Contains("\"enemy.slime_moss\": { \"path\": \"enemy.slime_moss.png\"", mo);
                Assert.Equal(Program.ExitOk, Run(sb, "wands/x/hand.png", out string wo, out _));
                Assert.Contains("\"cell\": [32, 64], \"columns\": 14", wo);
                Assert.DoesNotContain("spec 14 5.4 expects", wo);
                Assert.Equal(Program.ExitOk, Run(sb, "players/x/body.png", out string po, out _));
                Assert.Contains("\"body\": \"x/body.png\"", po);
            }
        }

        [Theory]
        [InlineData("ui/kit/button.png")]
        [InlineData("effects/player/hit_fire.png")]
        [InlineData("monsters/slime.png")]
        [InlineData("backgrounds/title.png")]
        [InlineData("wands/x/other.png")]
        public void NamesOutsideTheRulesFail(string artPath)
        {
            using (var sb = new Sandbox())
            {
                sb.WritePng(artPath, Solid(8, 8));
                Assert.Equal(Program.ExitFail, Run(sb, artPath, out _, out string e));
                Assert.Contains("FAIL", e);
            }
        }

        [Fact]
        public void FileOutsideArtProductionFails()
        {
            using (var sb = new Sandbox())
            {
                TestPng.Write(sb.PathOf("art/player/x.png"), Solid(8, 8));
                var e = new StringWriter();
                Assert.Equal(Program.ExitFail, Program.Run(new[] { "scaffold", sb.PathOf("art/player/x.png"), "--root", sb.Root }, new StringWriter(), e));
                Assert.Contains("is not under", e.ToString());
            }
        }

        [Fact]
        public void AlreadyListedFileIsNoted()
        {
            using (var sb = new Sandbox())
            {
                sb.WritePng("effects/player/hit_fire_32x32_f1.png", Solid(32, 32));
                sb.WriteText("art/production/effects/manifest.json", "{ \"notes\": { \"filter\": \"point\", \"mipmaps\": false }, \"player\": {"
                    + " \"hit_fire\": { \"path\": \"player/hit_fire_32x32_f1.png\", \"size\": [32, 32], \"frames\": [[0, 0, 32, 32]], \"fps\": 12, \"loop\": false } } }");
                Assert.Equal(Program.ExitOk, Run(sb, "effects/player/hit_fire_32x32_f1.png", out string o, out _));
                Assert.Contains("already in the manifest as Fx:player/hit_fire", o);
            }
        }

        [Fact]
        public void PngRoundTripThroughAllFilters()
        {
            byte[] png = TestPng.Rgba(13, 7, (x, y) => (uint)((x * 19) << 24 | (y * 31) << 16 | ((x + y) * 7) << 8 | (x * y % 256)));
            PngImage img = PngImage.Decode(png);
            Assert.Equal(13, img.Width);
            Assert.Equal(7, img.Height);
            for (int y = 0; y < 7; y++)
            {
                for (int x = 0; x < 13; x++)
                {
                    uint expected = (uint)((x * 19) << 24 | (y * 31) << 16 | ((x + y) * 7) << 8 | (x * y % 256));
                    Assert.Equal(expected, img.PixelAt(x, y));
                }
            }
        }

        [Fact]
        public void PalettePngWithTransparency()
        {
            byte[] png = TestPng.Palette(4, 2, new[] { 0xFF3B30FFu, 0x00000000u }, (x, y) => (byte)(x % 2));
            PngImage img = PngImage.Decode(png);
            Assert.Equal(0xFF3B30FFu, img.PixelAt(0, 0));
            Assert.Equal(0, img.AlphaAt(1, 0));
        }

        [Fact]
        public void BadPngIsRejected()
        {
            Assert.Throws<PngFormatException>(() => PngImage.Decode(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 }));
            byte[] png = TestPng.Rgba(4, 4, (x, y) => 0xFFFFFFFFu);
            png[41] = 0xFF;   // IDAT 자료의 첫 바이트(zlib 머리말: 8 + IHDR 25 + 길이·종류 8 = 41)를 망가뜨린다
            Assert.Throws<PngFormatException>(() => PngImage.Decode(png));
        }

        [Fact]
        public void DecodesEveryRepositoryPicture()
        {
            // 저장소의 그림이 이 읽개로 다 읽힌다(반입 검사가 조용히 건너뛰지 않는다)
            var files = Directory.GetFiles(TestRepo.Path("art/production"), "*.png", SearchOption.AllDirectories);
            Assert.NotEmpty(files);
            foreach (string f in files)
            {
                PngImage img = PngImage.Load(f);
                Assert.True(PngImage.TryReadSize(f, out int w, out int h));
                Assert.Equal(img.Width, w);
                Assert.Equal(img.Height, h);
            }
        }
    }
}

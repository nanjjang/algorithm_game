using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Wizard.Solo.Editor;

namespace Wizard.Solo.Tests.Art
{
    /// <summary>그림 대응표(artsync.json)와 임포트 설정 값. 00 문서 10절의 제외 규칙, 원본 존재, 최대 크기.</summary>
    public class ArtSyncTests
    {
        static ArtSyncRules _rules;
        static List<ArtSyncRules.Entry> _entries;
        static List<string> _missing;

        static ArtSyncRules Rules => _rules ?? (_rules = ArtSyncRules.Load(ArtTestUtil.ProjectRoot));

        static List<ArtSyncRules.Entry> Entries
        {
            get
            {
                if (_entries == null)
                {
                    _missing = new List<string>();
                    _entries = Rules.Resolve(ArtTestUtil.ProjectRoot, _missing);
                }
                return _entries;
            }
        }

        static string ArtRoot => Path.Combine(ArtTestUtil.ProjectRoot, "Assets", "Resources", "Art");

        /// <summary>keep 규칙에 걸리고(원본에서 복사하지 않음) Resources에 실제로 있는 장.</summary>
        static bool IsKeptOnDisk(string dest) => Rules.IsKept(dest) && File.Exists(Path.Combine(ArtRoot, dest));

        static int KeptOnDisk(string prefix)
        {
            var dests = new HashSet<string>(Entries.Select(e => e.Dest));
            return Rules.Keep.Where(k => k.StartsWith(prefix) && k.EndsWith(".png") && k.IndexOf('*') < 0 && !dests.Contains(k))
                .Count(k => File.Exists(Path.Combine(ArtRoot, k)));
        }

        [TestCase("**/*.py", "a/b/c.py", true)]
        [TestCase("**/*.py", "c.py", true)]
        [TestCase("ui/font/*.png", "ui/font/f-12.png", true)]
        [TestCase("ui/font/*.png", "ui/font/sub/f-12.png", false)]
        [TestCase("ui/atlas/**", "ui/atlas/kit-0.png", true)]
        [TestCase("**/preview*.png", "monsters/preview-bosses.png", true)]
        [TestCase("**/preview*.png", "monsters/boss.kiln_boar.png", false)]
        [TestCase("**/previews/**", "supplement/enhancement/previews/a.png", true)]
        [TestCase("*/body.png", "dalgu/body.png", true)]
        [TestCase("*/body.png", "source/x/body.png", false)]
        [TestCase("kit/**/*.png", "kit/k-01_panel_48x48.png", true)]
        [TestCase("a?c.json", "abc.json", true)]
        [TestCase("a?c.json", "a/c.json", false)]
        [TestCase("field.wall.*.png", "field_wall_x_png", false)]
        public void Glob_Semantics(string glob, string path, bool expect)
        {
            Assert.AreEqual(expect, ArtSyncRules.GlobMatch(glob, path));
        }

        [Test]
        public void Table_AllSourceFoldersAndFilesExist()
        {
            var entries = Entries;
            Assert.IsEmpty(_missing, "없는 원본 폴더");
            Assert.Greater(entries.Count, 0);
            foreach (var e in Entries) FileAssert.Exists(e.Source);
        }

        [Test]
        public void Table_ExclusionRules_00Section10()
        {
            foreach (var e in Entries)
            {
                string d = e.Dest;
                StringAssert.DoesNotStartWith("ui/atlas/", d);
                StringAssert.DoesNotStartWith("players/source/", d);
                Assert.IsFalse(d.StartsWith("ui/font/") && d.EndsWith(".png"), d);
                Assert.IsFalse(d.EndsWith("_glyphs.json"), d);
                string name = Path.GetFileName(d);
                Assert.IsFalse(name.StartsWith("preview"), d);
                Assert.IsFalse(name.EndsWith("_preview.png") || name.EndsWith("-preview.png") || name == "combat_preview.png", d);
                Assert.IsFalse(d.Contains("/previews/"), d);
                Assert.AreNotEqual("ritual.gif", name, d);
                Assert.IsFalse(d.EndsWith(".py") || d.EndsWith(".md"), d);
                Assert.IsFalse(d.Contains("/frames/"), d);
            }
        }

        [Test]
        public void Table_ContainsManifestsFontsAndCanonicalSets()
        {
            var dests = new HashSet<string>(Entries.Select(e => e.Dest));
            foreach (var m in new[] { "players", "wands", "monsters", "effects", "ui", "world", "supplement/layers", "supplement/enhancement" })
                Assert.IsTrue(dests.Contains(m + "/manifest.json"), m);
            Assert.IsTrue(dests.Contains("ui/font/Galmuri11.ttf"));
            Assert.IsTrue(dests.Contains("ui/font/neodgm.ttf"));
            Assert.IsTrue(dests.Contains("ui/font/Galmuri-OFL.txt"));
            Assert.IsTrue(dests.Contains("ui/font/neodgm-OFL.txt"));
            Assert.IsTrue(dests.Contains("backgrounds/title_1920x1080.png"));
            Assert.IsTrue(dests.Contains("supplement/enhancement/background/workbench.png"));
            // UI 낱장: manifest 627 − 글꼴 그림 6 = 621장이 전부 있어야 한다. 사용자가 새 시트(빠진 그림 보충)를 icon·kit·screen 폴더에
            // 더하면 표에 manifest 밖의 장이 늘어나므로 "정확히"가 아니라 "이상"으로 본다(없는 것이 있으면 아래 Table_EveryManifest…가 잡는다).
            Assert.GreaterOrEqual(Entries.Count(e => e.Dest.StartsWith("ui/") && e.Dest.EndsWith(".png")), 621);
            // 효과 303항목 = 파일 303장 이상(사용자가 새 효과 시트를 supplement·wand 폴더에 더하면 manifest 밖의 장이 늘어난다)
            // keep 목록의 효과 장(solo가 고친 그림을 원본 대신 쓰는 것)은 복사하지 않지만 Resources에 있으므로 같이 센다.
            Assert.GreaterOrEqual(Entries.Count(e => e.Dest.StartsWith("effects/") && e.Dest.EndsWith(".png")) + KeptOnDisk("effects/"), 303);
        }

        [Test]
        public void Table_EveryManifestReferencedSheetIsInTable()
        {
            var dests = new HashSet<string>(Entries.Select(e => e.Dest));
            var effects = Wizard.Solo.Art.ManifestParser.ParseEffects(ArtTestUtil.ReadArt("effects/manifest.json"), "effects/");
            foreach (var g in effects.Groups) foreach (var e in g.Entries) Assert.IsTrue(dests.Contains(e.Path) || IsKeptOnDisk(e.Path), e.Path);
            var ui = Wizard.Solo.Art.ManifestParser.ParseUi(ArtTestUtil.ReadArt("ui/manifest.json"));
            foreach (var e in ui.Assets)
                if (!e.Id.StartsWith("F-")) Assert.IsTrue(dests.Contains("ui/" + e.File), e.File);
            var mon = Wizard.Solo.Art.ManifestParser.ParseMonsters(ArtTestUtil.ReadArt("monsters/manifest.json"));
            foreach (var e in mon.Entries) Assert.IsTrue(dests.Contains("monsters/" + e.Path), e.Path);
            var world = Wizard.Solo.Art.ManifestParser.ParseWorld(ArtTestUtil.ReadArt("world/manifest.json"));
            foreach (var e in world.Assets.Values)
            {
                if (e.IsTile) foreach (var v in e.Variants.Values) Assert.IsTrue(dests.Contains("world/" + v.File), v.File);
                else Assert.IsTrue(dests.Contains("world/" + e.File), e.File);
            }
        }

        [Test]
        public void Table_NoResourcePathCollisions()
        {
            Assert.IsEmpty(ArtSyncRules.ResourceCollisions(Entries));
        }

        [Test]
        public void Import_MaxTextureSizeCoversLargestImage()
        {
            int largest = 0;
            var over2048 = new List<string>();
            foreach (var e in Entries)
            {
                if (!e.Dest.EndsWith(".png")) continue;
                Assert.IsTrue(PngSize.TryRead(e.Source, out int w, out int h), e.Source);
                int m = w > h ? w : h;
                if (m > largest) largest = m;
                if (m > 2048) over2048.Add(e.Dest);
            }
            CollectionAssert.IsSupersetOf(over2048, new[] { "world/sprites/field.weather.rain.png", "world/sprites/field.tower.png", "effects/ritual/screen_crack_1920x1080_f3.png" });
            Assert.AreEqual(8192, ArtImportRules.MaxTextureSize);
            Assert.GreaterOrEqual(ArtImportRules.MaxTextureSize, largest);
            Assert.AreEqual(64, ArtImportRules.PixelsPerUnit);
            Assert.IsTrue(ArtImportRules.AppliesTo("Assets/Resources/Art/players/dalgu/body.png"));
            Assert.IsFalse(ArtImportRules.AppliesTo("Assets/Resources/Data/sim/blocks.json"));
        }

        [Test]
        public void Table_LayoutReadableByArtsyncSh()
        {
            // Tools/artsync.sh가 줄 단위로 읽는다: maps는 한 줄에 하나, exclude·keep은 한 줄 배열, 글롭에 ] 없음.
            var lines = File.ReadAllLines(Path.Combine(ArtTestUtil.ProjectRoot, ArtSyncRules.TableAssetPath));
            int mapLines = lines.Count(l => l.TrimStart().StartsWith("{ \"src\"") || l.TrimStart().StartsWith("{\"src\""));
            Assert.AreEqual(Rules.Maps.Count, mapLines);
            Assert.AreEqual(1, lines.Count(l => l.TrimStart().StartsWith("\"exclude\"") && l.Contains("]")));
            Assert.AreEqual(1, lines.Count(l => l.TrimStart().StartsWith("\"keep\"") && l.Contains("]")));
            foreach (var g in Rules.Exclude.Concat(Rules.Keep).Concat(Rules.Maps.SelectMany(m => m.Include.Concat(m.Exclude))))
                Assert.IsFalse(g.Contains("]") || g.Contains("\""), g);
            CollectionAssert.Contains(Rules.Keep, "solo_presentation.json");
        }

        [Test]
        public void Table_HasNoCharacterWorkNames()
        {
            // 작업용 폴더 이름은 ArtAliases 표 한 곳에만(대응표는 글롭으로 폴더를 가리킨다).
            string text = File.ReadAllText(Path.Combine(ArtTestUtil.ProjectRoot, ArtSyncRules.TableAssetPath));
            for (int n = 1; n <= Ids.Char.Count; n++)
                StringAssert.DoesNotContain("\"" + Wizard.Solo.Art.ArtAliases.FolderOf(Ids.Char.Of(n)) + "/", text);
        }
    }
}

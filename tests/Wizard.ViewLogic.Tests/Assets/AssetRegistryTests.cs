using System.Collections.Generic;
using System.Linq;
using Wizard.ViewLogic.Assets;
using Wizard.ViewLogic.Common;
using Wizard.ViewLogic.Presentation;
using Xunit;

namespace Wizard.ViewLogic.Tests.Assets
{
    // M9-AST-01 수락 조건: 다섯 매니페스트 → AssetKey 레지스트리, 파일 이름 해석 금지(SplitUiName 하나만), 별칭은 asset_aliases.json,
    // 저장소 매니페스트가 오류 없이 읽힘, 없는 열쇠는 대체 기록 + 한 번 기록
    public sealed class AssetRegistryTests
    {
        private sealed class RecordingLog : IAssetLog
        {
            public List<AssetKey> Keys { get; } = new List<AssetKey>();

            public void PlaceholderUsed(AssetKey missing) => Keys.Add(missing);
        }

        [Fact]
        public void RepoManifests_LoadWithoutProblems()
        {
            AssetLoadResult r = RepoAssets.Load();
            Assert.True(r.Ok, string.Join("\n", r.Problems.Select(p => p.ToString())));
            // 다섯 묶음이 모두 들어왔다
            foreach (AssetPack pack in new[] { AssetPack.Ui, AssetPack.Character, AssetPack.Wand, AssetPack.Monster, AssetPack.Fx })
            {
                Assert.NotEmpty(r.Registry.IdsOf(pack));
                Assert.True(r.Registry.TryGetSettings(pack, out _), pack.ToString());
            }
            Assert.Equal(5, r.Registry.WandIds.Count);
        }

        [Fact]
        public void RepoLoad_IsRepeatable_SameKeysInSameOrder()
        {
            AssetRegistry a = RepoAssets.Load().Registry;
            AssetRegistry b = RepoAssets.Load().Registry;
            Assert.Equal(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.True(AssetKeyComparer.Instance.Equals(a.Records[i].Key, b.Records[i].Key));
                Assert.Equal(a.Records[i].File, b.Records[i].File);
                Assert.Equal(a.Records[i].Frames, b.Records[i].Frames);
            }
        }

        [Fact]
        public void MissingKey_ReturnsPlaceholder_AndLogsOncePerKey()
        {
            var log = new RecordingLog();
            AssetRegistry reg = RepoAssets.Load().Registry;
            reg.SetLog(log);
            var missing = new AssetKey(AssetPack.Ui, "I-ZZ", "nothing", "normal");

            AssetRecord first = reg.Resolve(missing);
            AssetRecord again = reg.Resolve(missing, new PixelSize(48, 48));
            AssetRecord other = reg.Resolve(new AssetKey(AssetPack.Fx, "player/nothing"));

            Assert.True(first.IsPlaceholder);
            Assert.Equal("I-ZZ", first.PlaceholderLabel);
            Assert.Equal(AssetPlaceholder.DefaultSize.Width, first.FrameSize.Width);
            Assert.Equal(1, first.FrameCount);
            Assert.Equal(48, again.FrameSize.Width);   // 원하는 크기의 바둑판
            Assert.True(other.IsPlaceholder);
            Assert.Equal(2, log.Keys.Count);           // 같은 열쇠는 두 번째에 기록하지 않는다
            Assert.Equal("I-ZZ", log.Keys[0].Id);
            Assert.Equal("player/nothing", log.Keys[1].Id);
            Assert.Equal(2, reg.PlaceholderLogCount);
        }

        [Fact]
        public void ExistingKey_IsNotLogged()
        {
            var log = new RecordingLog();
            AssetRegistry reg = RepoAssets.Load().Registry;
            reg.SetLog(log);
            AssetRecord r = reg.Resolve(new AssetKey(AssetPack.Fx, "player/hit_fire"));
            Assert.False(r.IsPlaceholder);
            Assert.Empty(log.Keys);
        }

        [Fact]
        public void PlaceholderStyle_UsesOnlySlateAndWoodDarkWithF12Label()
        {
            Assert.Equal(UiColorTokens.Slate, AssetPlaceholder.LightToken);
            Assert.Equal("wood_dark", AssetPlaceholder.DarkToken);
            Assert.Equal(8, AssetPlaceholder.CheckerPx);
            Assert.Equal("F-12", AssetPlaceholder.LabelFontId);
        }

        [Fact]
        public void ExternalWithoutFile_IsMarkedMissing_AndResolvesToPlaceholder()
        {
            AssetRegistry reg = RepoAssets.Registry;
            var s2405 = new AssetKey(AssetPack.Ui, "S-2405", "external", null);
            // 그림이 들어온 뒤라 S-2405·S-2407 은 더는 빠진 것으로 표시되지 않는다
            Assert.False(reg.IsMarkedMissing(s2405));
            Assert.True(reg.Contains(s2405));
            Assert.True(reg.Contains(new AssetKey(AssetPack.Ui, "S-2407", "external", null)));
            // 빈 등록부는 무엇이든 자리표시로 풀린다
            Assert.True(new AssetRegistry().Resolve(s2405).IsPlaceholder);
            // 파일이 있는 S-0201 은 들어왔다
            Assert.True(reg.TryGet(new AssetKey(AssetPack.Ui, "S-0201", "external", null), out AssetRecord bg));
            Assert.Equal("backgrounds/title_1920x1080.png", bg.File);
        }

        [Fact]
        public void Loader_ReadsOnlyTheFiveManifests_AndAsksExistenceOnlyForExternals()
        {
            MemoryAssetSource src = MemoryAssetSource.Minimal();
            AssetLoadResult r = AssetManifestLoader.LoadAll(src, MemoryAssetSource.MinimalAliases);
            Assert.True(r.Ok, string.Join("\n", r.Problems.Select(p => p.ToString())));
            Assert.Equal(new[] { "ui/manifest.json", "players/manifest.json", "wands/manifest.json", "monsters/manifest.json", "effects/manifest.json" }, src.Reads);
            Assert.Equal(new[] { "backgrounds/missing_1920x1080.png" }, src.ExistsQueries);
            Assert.True(r.Registry.IsMarkedMissing(new AssetKey(AssetPack.Ui, "S-9001", "external", null)));
        }

        [Fact]
        public void FrameCountAndSizeComeFromTheManifest_NotFromTheFileName()
        {
            // 효과 파일 이름은 _f3 이지만 매니페스트의 frames 는 둘이다 → 둘
            AssetLoadResult r = AssetManifestLoader.LoadAll(MemoryAssetSource.Minimal(), MemoryAssetSource.MinimalAliases);
            Assert.True(r.Registry.TryGet(new AssetKey(AssetPack.Fx, "player/hit_fire"), out AssetRecord fx));
            Assert.Equal(2, fx.FrameCount);
            Assert.Equal("effects/player/hit_fire_32x32_f3.png", fx.File);
            Assert.Null(fx.Pivot);
            Assert.Equal(12, fx.Fps);
            Assert.False(fx.Loop);
        }

        [Fact]
        public void UiEntry_UsesAtlasRect_NineSlice_AndElementAlias()
        {
            AssetRegistry reg = AssetManifestLoader.LoadAll(MemoryAssetSource.Minimal(), MemoryAssetSource.MinimalAliases).Registry;
            Assert.True(reg.TryGet(new AssetKey(AssetPack.Ui, "K-20", "button_large", "hover"), out AssetRecord k20));
            Assert.Equal("ui/kit/k-20_button_large_48x64_hover.png", k20.File);
            Assert.Equal("ui/atlas/kit-0.png", k20.AtlasPage);
            Assert.Equal("ui/atlas/kit-0.png", k20.TextureFile);
            Assert.Equal(new PixelRect(100, 200, 48, 64), k20.FrameRect(0));
            Assert.Equal(12, k20.NineSlice!.Value.Left);
            Assert.True(reg.TryGet(new AssetKey(AssetPack.Ui, "I-B1", "block_fireball", "normal"), out AssetRecord icon));
            Assert.Null(icon.AtlasPage);
            Assert.Equal(new PixelRect(0, 0, 48, 48), icon.FrameRect(0));
            Assert.Equal("ice", icon.Element);   // frost → ice
        }

        [Fact]
        public void CharacterAndMonsterRecords_CarryPerFrameOrigins()
        {
            AssetRegistry reg = AssetManifestLoader.LoadAll(MemoryAssetSource.Minimal(), MemoryAssetSource.MinimalAliases).Registry;
            Assert.True(reg.TryGet(new AssetKey(AssetPack.Character, "char.1", "idle_down"), out AssetRecord idle));
            Assert.Equal("players/folder_a/body.png", idle.File);
            Assert.Equal(7, idle.Fps);
            Assert.True(idle.Loop);
            Assert.Equal(new PixelPoint(22, 43).ToString(), idle.CharacterPoints![1].Hand.ToString());
            Assert.True(reg.TryGet(new AssetKey(AssetPack.Character, "char.1", "portrait"), out AssetRecord portrait));
            Assert.Equal(4, portrait.FrameCount);
            Assert.Equal(new PixelRect(192, 0, 64, 64), portrait.FrameRect(3));

            Assert.True(reg.TryGet(new AssetKey(AssetPack.Monster, "enemy.slime_moss", "move"), out AssetRecord move));
            Assert.Equal("(32, 58)", move.OriginOf(0).ToString());
            Assert.Equal("(32, 54)", move.OriginOf(1).ToString());
            Assert.Equal("(32, 58)", move.Pivot!.Value.ToString());
        }

        [Fact]
        public void UnknownFolder_UnknownElement_AndDuplicates_AreReportedNotThrown()
        {
            string ui = MemoryAssetSource.MinimalUi
                .Replace("\"element\": \"frost\"", "\"element\": \"plasma\"")
                .Replace("\"assets\": [", "\"assets\": [ { \"id\": \"K-20\", \"file\": \"kit/k-20_button_large_48x64_hover.png\", \"width\": 48, \"height\": 64, \"frames\": 1 },");
            string players = MemoryAssetSource.MinimalPlayers.Replace("\"folder_a\": {", "\"folder_z\": {");
            MemoryAssetSource src = MemoryAssetSource.Minimal().Text("ui/manifest.json", ui).Text("players/manifest.json", players);
            AssetLoadResult r = AssetManifestLoader.LoadAll(src, MemoryAssetSource.MinimalAliases);
            Assert.False(r.Ok);
            Assert.Contains(r.Problems, p => p.File == "ui/manifest.json" && p.Message.Contains("unknown element"));
            Assert.Contains(r.Problems, p => p.File == "ui/manifest.json" && p.Message.Contains("duplicate"));
            Assert.Contains(r.Problems, p => p.File == "players/manifest.json" && p.Message.Contains("folder_z"));
            // 문제가 있어도 읽을 수 있는 것은 들어 있다
            Assert.True(r.Registry.Contains(new AssetKey(AssetPack.Fx, "player/hit_fire")));
        }

        [Fact]
        public void MissingManifest_AndBrokenJson_AreProblems()
        {
            MemoryAssetSource src = MemoryAssetSource.Minimal().Text("effects/manifest.json", "{ \"player\": ");
            var partial = new MemoryAssetSource()
                .Text("ui/manifest.json", MemoryAssetSource.MinimalUi);
            AssetLoadResult broken = AssetManifestLoader.LoadAll(src, MemoryAssetSource.MinimalAliases);
            AssetLoadResult missing = AssetManifestLoader.LoadAll(partial, MemoryAssetSource.MinimalAliases);
            Assert.Contains(broken.Problems, p => p.File == "effects/manifest.json" && p.Message.StartsWith("not JSON"));
            Assert.Contains(missing.Problems, p => p.File == "monsters/manifest.json" && p.Message == "manifest not found");
        }

        [Fact]
        public void PlayerFramesMustEqualClipSum()
        {
            string players = MemoryAssetSource.MinimalPlayers.Replace("\"frames\": 2,", "\"frames\": 3,");
            AssetLoadResult r = AssetManifestLoader.LoadAll(MemoryAssetSource.Minimal().Text("players/manifest.json", players), MemoryAssetSource.MinimalAliases);
            Assert.Contains(r.Problems, p => p.Message.Contains("sum of clip frames"));
        }

        [Fact]
        public void AliasTargetsMustExist()
        {
            string aliases = MemoryAssetSource.MinimalAliases.Replace("\"player/hit_fire\"", "\"player/nope\"").Replace("[\"I-B1\"]", "[\"I-B9\"]");
            AssetLoadResult r = AssetManifestLoader.LoadAll(MemoryAssetSource.Minimal(), aliases);
            Assert.Contains(r.Problems, p => p.File == AssetAliases.RepoPath && p.Message.Contains("player/nope"));
            Assert.Contains(r.Problems, p => p.File == AssetAliases.RepoPath && p.Message.Contains("I-B9"));
            // _note 는 항목이 아니다
            Assert.DoesNotContain(r.Problems, p => p.Pointer.Contains("_note"));
        }

        [Fact]
        public void SameKeyDifferentSizes_AreKeptApart_AndLookupBySizeWorks()
        {
            AssetRegistry reg = RepoAssets.Registry;
            var key = new AssetKey(AssetPack.Ui, "K-40", "selection_corner", "normal");
            IReadOnlyList<AssetRecord> all = reg.GetAll(key);
            Assert.Equal(new[] { 16, 24, 32 }, all.Select(r => r.FrameSize.Width).ToArray());
            Assert.True(reg.TryGet(key, new PixelSize(32, 32), out AssetRecord big));
            Assert.Equal(32, big.FrameSize.Height);
            Assert.False(reg.TryGet(key, new PixelSize(40, 40), out _));
            Assert.True(reg.Resolve(key, new PixelSize(40, 40)).IsPlaceholder);
        }

        // DECISIONS-TECH T-58: 크기 없는 조회는 가장 작은 기록이다. 매니페스트의 줄 순서(I-L4 는 128, 24, 48 순)에 기대지 않는다
        [Fact]
        public void SameKeyDifferentSizes_LookupWithoutSize_ReturnsSmallestWhateverTheManifestOrder()
        {
            AssetRegistry reg = RepoAssets.Registry;
            var laurel = new AssetKey(AssetPack.Ui, "I-L4", "league_laurel_heir", "normal");
            Assert.Equal(new[] { 128, 24, 48 }, reg.GetAll(laurel).Select(r => r.FrameSize.Width).ToArray());
            Assert.True(reg.TryGet(laurel, out AssetRecord smallest));
            Assert.Equal(24, smallest.FrameSize.Width);
            Assert.Equal(24, smallest.FrameSize.Height);
            Assert.Equal(24, reg.Resolve(laurel).FrameSize.Width);

            var corner = new AssetKey(AssetPack.Ui, "K-40", "selection_corner", "normal");
            Assert.True(reg.TryGet(corner, out AssetRecord c));
            Assert.Equal(16, c.FrameSize.Width);

            // 넣는 순서를 뒤집어도 같다. 높이가 같으면 폭이 작은 것
            var made = new AssetRegistry();
            var key = new AssetKey(AssetPack.Ui, "X-1", "v", "normal");
            foreach ((int w, int h) in new[] { (48, 48), (32, 16), (16, 16), (24, 24) })
            {
                Assert.True(made.Add(new AssetRecord(key, "x.png", new PixelSize(w, h), new[] { new PixelRect(0, 0, w, h) })));
            }
            Assert.True(made.TryGet(key, out AssetRecord m));
            Assert.Equal("16x16", m.FrameSize.ToString());
        }

        [Fact]
        public void AssetPaths_RejectEscapesAndNormalizeParents()
        {
            Assert.True(AssetPaths.TryCombine("ui", "../backgrounds/title.png", out string p));
            Assert.Equal("backgrounds/title.png", p);
            Assert.False(AssetPaths.TryCombine("ui", "../../etc/passwd", out _));
            Assert.False(AssetPaths.TryCombine("ui", "/abs.png", out _));
            Assert.False(AssetPaths.TryCombine("ui", "a\\b.png", out _));
            Assert.False(new DirectoryAssetSource(RepoAssets.Source.Root).Exists("../README.md"));
        }

        [Fact]
        public void UiIconRule_MapsContentIdsToVariants()
        {
            AssetAliases a = RepoAssets.Aliases;
            Assert.True(a.TryUiIcon("block.fireball", out UiIconRule block, out string v1));
            Assert.Equal("block_fireball", v1);
            Assert.Equal(new[] { "I-B1", "I-B2" }, block.Icons);
            Assert.True(a.TryUiIcon("element.ice", out _, out string v2));
            Assert.Equal("element_frost", v2);
            Assert.True(a.TryUiIcon("element.dark", out _, out string v3));
            Assert.Equal("element_shadow", v3);
            Assert.True(a.TryUiIcon("art.kiln", out _, out string v4));
            Assert.Equal("kiln", v4);
            Assert.False(a.TryUiIcon("boss.kiln_boar", out _, out _));
        }

        [Fact]
        public void BlockProjectileFx_AliasesPointAtEffects()
        {
            AssetAliases a = RepoAssets.Aliases;
            Assert.True(a.TryBlockProjectileFx("block.magic_bolt", out string bolt));
            Assert.Equal("player/mana_bolt", bolt);
            Assert.True(a.TryBlockProjectileFx("block.ice_spike", out string ice));
            Assert.Equal("player/ice_lance", ice);
            Assert.False(a.TryBlockProjectileFx("block.boomerang", out _));   // 탄 그림 없음(5.6절)
            Assert.True(RepoAssets.Registry.TryGet(new AssetKey(AssetPack.Fx, ice), out AssetRecord lance));
            Assert.Equal(2, lance.FrameCount);
            Assert.Equal(12, lance.FrameSize.Width);
            Assert.Equal(28, lance.FrameSize.Height);
        }
    }
}

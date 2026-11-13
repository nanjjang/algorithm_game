using System;
using System.Collections.Generic;
using System.Linq;
using Wizard.Content;
using Wizard.Core;
using Wizard.ViewLogic.Assets;
using Wizard.ViewLogic.Presentation;
using Xunit;

namespace Wizard.ViewLogic.Tests.Assets
{
    // spec 14 7.8 TV-212~235: MAN-mo, MAN-fx 검산, 배율 환산, 텍스처 설정, 아이콘과 콘텐츠 id 의 1:1
    public sealed class ManifestMonsterFxVectorTests
    {
        private static AssetRegistry Reg => RepoAssets.Registry;

        private static int MonsterFrames(string id) => Reg.RecordsOf(AssetPack.Monster, id).Sum(r => r.FrameCount);

        private static IEnumerable<string> MonsterIds => Reg.IdsOf(AssetPack.Monster);

        // spec 14 3.6절의 일반 적 12종(새싹은 보스 쪽에 센다)
        private static IEnumerable<string> NormalEnemyIds =>
            Enum.GetValues(typeof(EnemyTypeId)).Cast<EnemyTypeId>()
                .Where(e => e != EnemyTypeId.None && e != EnemyTypeId.SporeSeedling)
                .Select(e => ContentIds.ToId(e));

        [Fact]
        public void S14_TV_212__TwelveNormalEnemiesHave100Frames()
        {
            string[] ids = NormalEnemyIds.ToArray();
            Assert.Equal(12, ids.Length);
            Assert.All(ids, id => Assert.NotEmpty(Reg.RecordsOf(AssetPack.Monster, id)));
            Assert.Equal(100, ids.Sum(MonsterFrames));
        }

        [Fact]
        public void S14_TV_213__BossRelatedFramesAre135IncludingPhantom21AndSeedling()
        {
            string[] ids = MonsterIds.Where(id => id.StartsWith("boss.", StringComparison.Ordinal)).Concat(new[] { "enemy.spore_seedling" }).ToArray();
            Assert.Equal(21, MonsterFrames("boss.mirror_phantom"));
            Assert.Equal(135, ids.Sum(MonsterFrames));
        }

        [Fact]
        public void S14_TV_214__EliteRelatedFramesAre17()
        {
            string[] elite = MonsterIds.Where(id => id.StartsWith("elite.", StringComparison.Ordinal)).ToArray();
            string[] overlays = elite.Where(id => id.EndsWith(".overlay", StringComparison.Ordinal)).ToArray();
            Assert.Equal(12, overlays.Length);
            Assert.Equal(12, overlays.Sum(MonsterFrames));
            Assert.Equal(1, MonsterFrames("elite.horn"));
            Assert.Equal(1, MonsterFrames("elite.bat_dusk.dive"));
            Assert.Equal(3, MonsterFrames("elite.wisp_stray.twin"));
            Assert.Equal(17, elite.Sum(MonsterFrames));
            // 세 묶음이 37개 항목을 남김없이 나눈다
            Assert.Equal(37, MonsterIds.Count());
        }

        [Fact]
        public void S14_TV_215__BugLanternMove2Ignite2NoDeath()
        {
            Assert.Equal(2, Reg.Resolve(new AssetKey(AssetPack.Monster, "enemy.bug_lantern", "move")).FrameCount);
            Assert.Equal(2, Reg.Resolve(new AssetKey(AssetPack.Monster, "enemy.bug_lantern", "ignite")).FrameCount);
            Assert.False(Reg.Contains(new AssetKey(AssetPack.Monster, "enemy.bug_lantern", "death")));
        }

        [Fact]
        public void S14_TV_216__MirrorPhantomHasFirstMeetingCrack2AndLook1()
        {
            Assert.Equal(2, Reg.Resolve(new AssetKey(AssetPack.Monster, "boss.mirror_phantom", "crack")).FrameCount);
            Assert.Equal(1, Reg.Resolve(new AssetKey(AssetPack.Monster, "boss.mirror_phantom", "look")).FrameCount);
        }

        [Fact]
        public void S14_TV_217__BatDuskDeathFrameUsesItsOwnOrigin()
        {
            Assert.True(Reg.TryGet(new AssetKey(AssetPack.Monster, "enemy.bat_dusk", "death"), out AssetRecord death));
            Assert.Equal("(32, 58)", death.Pivot!.Value.ToString());   // 기본값
            Assert.Equal("(32, 54)", death.OriginOf(0).ToString());    // 프레임 값
        }

        [Fact]
        public void S14_TV_218__SporeSeedlingIs32x32()
        {
            Assert.All(Reg.RecordsOf(AssetPack.Monster, "enemy.spore_seedling"), r => Assert.Equal("32x32", r.FrameSize.ToString()));
        }

        [Fact]
        public void S14_TV_219__Fx303EntriesInSevenGroups()
        {
            IReadOnlyList<string> ids = Reg.IdsOf(AssetPack.Fx);
            Assert.Equal(303, ids.Count);
            var expected = new Dictionary<string, int>
            {
                ["player"] = 55, ["special"] = 89, ["ritual"] = 77, ["enemy"] = 31, ["loot"] = 11, ["wand"] = 15, ["status"] = 25,
            };
            foreach (KeyValuePair<string, int> g in expected)
            {
                Assert.Equal(g.Value, ids.Count(id => id.StartsWith(g.Key + "/", StringComparison.Ordinal)));
            }
            Assert.Equal(1201, ids.Sum(id => Reg.Resolve(new AssetKey(AssetPack.Fx, id)).FrameCount));   // 5.6절 프레임 합
        }

        private static int FxFrames(string id)
        {
            Assert.True(Reg.TryGet(new AssetKey(AssetPack.Fx, id), out AssetRecord r), id);
            return r.FrameCount;
        }

        [Fact]
        public void S14_TV_220__IcePillarSixFrames()
        {
            Assert.Equal(2 + 1 + 3, FxFrames("enemy/ice_pillar"));
        }

        [Fact]
        public void S14_TV_221__RockFiveFrames()
        {
            Assert.Equal(2 + 1 + 2, FxFrames("enemy/rock"));
        }

        [Fact]
        public void S14_TV_222__IceScreenHorizontalTenFrames()
        {
            Assert.Equal(3 + 1 + 2 + 4, FxFrames("special/ice_screen_horizontal"));
        }

        [Fact]
        public void S14_TV_223__MutationVine24Frames()
        {
            Assert.Equal(8 + 4 + 6 + 6, FxFrames("ritual/mutation_vine"));
        }

        [Fact]
        public void S14_TV_224__FifteenCastSeals()
        {
            Assert.Equal(15, Reg.IdsOf(AssetPack.Fx).Count(id => id.StartsWith("special/cast_seal_", StringComparison.Ordinal)));
        }

        [Fact]
        public void S14_TV_225__Arena24x14CellsIs1536x896Px()
        {
            Assert.Equal(1536, ArtUnits.WholeCellsToPx(24));
            Assert.Equal(896, ArtUnits.WholeCellsToPx(14));
            Assert.True(ArtUnits.TryCellsToWholePx(Fix64.FromInt(24), out int w));
            Assert.Equal(1536, w);
        }

        [Fact]
        public void S14_TV_226__HitRadius015CellsIs9_6PxAndRaw644245094()
        {
            Fix64 r = Fix64.FromRatio(15, 100);
            Assert.Equal(644_245_094L, r.Raw);
            Assert.Equal(9.6f, ArtUnits.CellsToPx(r), 4);
            Assert.False(ArtUnits.TryCellsToWholePx(r, out _));   // 정수 px 에 떨어지지 않는다
        }

        [Fact]
        public void S14_TV_227__OnePixelIsRaw67108864()
        {
            Assert.Equal(67_108_864L, ArtUnits.RawPerPixel);
            Assert.Equal(67_108_864L, ArtUnits.PxToCells(1).Raw);
            Assert.Equal(Fix64.One.Raw, ArtUnits.PxToCells(64).Raw);
        }

        [Fact]
        public void S14_TV_228__HitCenter04CellsIs25_6PxAndRaw1717986918()
        {
            Fix64 up = Fix64.FromRatio(4, 10);
            Assert.Equal(1_717_986_918L, up.Raw);
            Assert.Equal(25.6f, ArtUnits.CellsToPx(up), 4);
        }

        [Fact]
        public void S14_TV_229__StoneyardStartsAt384_448And1152_448()
        {
            Assert.True(ArtUnits.TryCellsToWholePx(Fix64.FromInt(6), out int x1));
            Assert.True(ArtUnits.TryCellsToWholePx(Fix64.FromInt(7), out int y1));
            Assert.True(ArtUnits.TryCellsToWholePx(Fix64.FromInt(18), out int x2));
            Assert.Equal((384, 448), (x1, y1));
            Assert.Equal((1152, 448), (x2, y1));
        }

        [Fact]
        public void S14_TV_230__StoneyardCenterLineX12Is768()
        {
            Assert.True(ArtUnits.TryCellsToWholePx(Fix64.FromInt(12), out int x));
            Assert.Equal(768, x);
            Assert.Equal(768f, ArtUnits.CellsToPx(Fix64.FromInt(12)));
        }

        [Fact]
        public void S14_TV_231__AllPacksArePointFilterNoMipmapsNoCompression()
        {
            foreach (AssetPack pack in new[] { AssetPack.Ui, AssetPack.Character, AssetPack.Wand, AssetPack.Monster, AssetPack.Fx })
            {
                Assert.True(Reg.TryGetSettings(pack, out PackTextureSettings s), pack.ToString());
                Assert.True(s.PointFilter, pack + " filter");
                Assert.False(s.Mipmaps, pack + " mipmaps");
                Assert.False(s.Compression, pack + " compression");
                Assert.True(s.IsPixelExact);
            }
            Assert.True(Reg.TryGetSettings(AssetPack.Ui, out PackTextureSettings ui));
            Assert.True(ui.CompressionDeclared);   // MAN-ui 만 압축을 적었다. 나머지는 규격값(4.4절)으로 읽는다
        }

        private static string[] IconVariants(string iconId) =>
            Reg.RecordsOf(AssetPack.Ui, iconId).Select(r => r.Key.Variant!).OrderBy(v => v, StringComparer.Ordinal).ToArray();

        private static string[] MappedVariants(IEnumerable<string> contentIds) =>
            contentIds.Select(id =>
            {
                Assert.True(RepoAssets.Aliases.TryUiIcon(id, out _, out string v), id);
                return v;
            }).OrderBy(v => v, StringComparer.Ordinal).ToArray();

        [Fact]
        public void S14_TV_232__BlockIconsB1AndB2Are16EachOneToOneWithBlockIds()
        {
            string[] blocks = Enum.GetValues(typeof(BlockId)).Cast<BlockId>().Where(b => b != BlockId.None).Select(b => ContentIds.ToId(b)).ToArray();
            Assert.Equal(16, blocks.Length);
            string[] expected = MappedVariants(blocks);
            Assert.Equal(16, expected.Distinct().Count());
            foreach (string icon in new[] { "I-B1", "I-B2" })
            {
                Assert.Equal(16, Reg.RecordsOf(AssetPack.Ui, icon).Count);
                Assert.Equal(expected, IconVariants(icon));
            }
        }

        [Fact]
        public void S14_TV_233__ArtIconsT1AndT2Are15EachOneToOneWithArtIds()
        {
            string[] arts = Enum.GetValues(typeof(ArtId)).Cast<ArtId>().Where(a => a != ArtId.None).Select(a => ContentIds.ToId(a)).ToArray();
            Assert.Equal(15, arts.Length);
            string[] expected = MappedVariants(arts);
            foreach (string icon in new[] { "I-T1", "I-T2" })
            {
                Assert.Equal(15, Reg.RecordsOf(AssetPack.Ui, icon).Count);
                Assert.Equal(expected, IconVariants(icon));
            }
        }

        [Fact]
        public void S14_TV_234__ElementIconE2HasFiveWithoutNeutral()
        {
            Assert.Equal(5, Reg.RecordsOf(AssetPack.Ui, "I-E2").Count);
            string[] fiveElements = { "element.fire", "element.lightning", "element.arcane", "element.ice", "element.dark" };
            Assert.Equal(MappedVariants(fiveElements), IconVariants("I-E2"));
            Assert.True(RepoAssets.Aliases.TryUiIcon("element.neutral", out _, out string neutral));
            Assert.DoesNotContain(neutral, IconVariants("I-E2"));
            Assert.Contains(neutral, IconVariants("I-E1"));   // 무는 I-E1·I-E3 에만
        }

        [Fact]
        public void S14_TV_235__CurrencyIconsC1ToC3AreFourEach()
        {
            // 골드, 원정 지갑, 시즌 주화, 별빛(spec 14 3.11. 원정 지갑은 표시 전용 id)
            var currencies = Enum.GetValues(typeof(CurrencyCode)).Cast<CurrencyCode>().Select(c => ContentIds.ToId(c)).ToList();
            currencies.Add("currency.expedition_purse");
            Assert.Equal(4, currencies.Count);
            string[] expected = MappedVariants(currencies);
            foreach (string icon in new[] { "I-C1", "I-C2", "I-C3" })
            {
                Assert.Equal(4, Reg.RecordsOf(AssetPack.Ui, icon).Count);
                Assert.Equal(expected, IconVariants(icon));
            }
        }
    }
}

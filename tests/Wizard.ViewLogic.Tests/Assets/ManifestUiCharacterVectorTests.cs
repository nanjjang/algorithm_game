using System.Collections.Generic;
using System.Linq;
using Wizard.ViewLogic.Assets;
using Wizard.ViewLogic.Common;
using Wizard.ViewLogic.Presentation;
using Xunit;

namespace Wizard.ViewLogic.Tests.Assets
{
    // spec 14 7.8 TV-180~211: MAN-ui, MAN-pl, MAN-wd 검산과 칸 고르기·합성 순서.
    // 기대값은 명세의 표에서 가져왔다. 실제 값은 저장소 매니페스트를 레지스트리로 읽은 것(PNG 크기는 시험이 머리를 직접 읽는다)
    public sealed class ManifestUiCharacterVectorTests
    {
        private static AssetRegistry Reg => RepoAssets.Registry;

        private static IEnumerable<AssetRecord> UiFiles => Reg.Records.Where(r => r.Key.Pack == AssetPack.Ui && r.Key.Variant != "external");

        private static readonly string[] CharIds = { "char.1", "char.2", "char.3", "char.4", "char.5" };
        private static readonly string[] WandIds = { "wand.1", "wand.2", "wand.3", "wand.4", "wand.5" };
        private static readonly string[] SheetVariants = { "portrait", "selection", "silhouette" };

        private static IEnumerable<AssetRecord> Clips(string charId) =>
            Reg.RecordsOf(AssetPack.Character, charId).Where(r => !SheetVariants.Contains(r.Key.Variant));

        [Fact]
        public void S14_TV_180__UiHas199UniqueIds()
        {
            Assert.Equal(197, UiFiles.Select(r => r.Key.Id).Distinct().Count());
            Assert.Equal(199, Reg.IdsOf(AssetPack.Ui).Count);
            // 코드로 만드는 7줄은 그림이 없다
            foreach (string codeOnly in new[] { "K-10", "K-37", "K-47", "K-72", "I-B4", "I-C4", "I-M8" })
            {
                Assert.Empty(Reg.RecordsOf(AssetPack.Ui, codeOnly));
            }
        }

        [Fact]
        public void S14_TV_181__Ui627FilesOn23AtlasPages()
        {
            Assert.Equal(627, UiFiles.Count());
            Assert.Equal(23, UiFiles.Where(r => r.AtlasPage != null).Select(r => r.AtlasPage).Distinct().Count());
        }

        [Fact]
        public void S14_TV_182__SixteenUiEntriesHaveNoAtlas()
        {
            AssetRecord[] loose = UiFiles.Where(r => r.AtlasPage == null).ToArray();
            Assert.Equal(16, loose.Length);
            Assert.Equal(6, loose.Count(r => r.Key.Id.StartsWith("F-")));                     // 글꼴 6
            Assert.Equal(4, loose.Count(r => r.Key.Id == "S-0203"));                          // 런처 아이콘 4
            Assert.Equal(6, loose.Count(r => r.FrameSize.Width == 1920 && r.FrameSize.Height == 1080));   // 큰 배경 6
        }

        [Fact]
        public void S14_TV_183__SevenFullScreenBackgroundsIncludingExternalS0201()
        {
            string[] inManifest = UiFiles.Where(r => r.FrameSize.Width == 1920 && r.FrameSize.Height == 1080).Select(r => r.Key.Id).OrderBy(x => x, System.StringComparer.Ordinal).ToArray();
            Assert.Equal(new[] { "S-0501", "S-0901", "S-1602", "S-2307", "S-2506", "S-2601" }, inManifest);
            Assert.True(Reg.TryGet(new AssetKey(AssetPack.Ui, "S-0201", "external", null), out AssetRecord ext));
            Assert.Equal(1920, ext.FrameSize.Width);
            Assert.Equal(1080, ext.FrameSize.Height);
            PixelSize png = RepoAssets.PngSize(ext.File);
            Assert.Equal(1920, png.Width);
            Assert.Equal(1080, png.Height);
            Assert.Equal(7, inManifest.Length + 1);
            // 큰 배경은 아틀라스에 넣지 않는다(5.7.1 4번)
            Assert.All(UiFiles.Where(r => r.FrameSize.Width == 1920), r => Assert.Null(r.AtlasPage));
        }

        [Fact]
        public void S14_TV_184__K20NineSlice12AndFourStates()
        {
            AssetRecord[] k20 = Reg.RecordsOf(AssetPack.Ui, "K-20").ToArray();
            Assert.Equal(new[] { "disabled", "hover", "normal", "pressed" }, k20.Select(r => r.Key.State).OrderBy(s => s, System.StringComparer.Ordinal).ToArray());
            Assert.All(k20, r =>
            {
                Assert.Equal("button_large", r.Key.Variant);
                NineSlice n = r.NineSlice!.Value;
                Assert.Equal(new[] { 12, 12, 12, 12 }, new[] { n.Left, n.Top, n.Right, n.Bottom });
            });
        }

        [Fact]
        public void S14_TV_185__K01NineSlice16()
        {
            NineSlice n = Reg.RecordsOf(AssetPack.Ui, "K-01").Single().NineSlice!.Value;
            Assert.Equal(new[] { 16, 16, 16, 16 }, new[] { n.Left, n.Top, n.Right, n.Bottom });
        }

        [Fact]
        public void S14_TV_186__K04NineSlice24x12()
        {
            NineSlice n = Reg.RecordsOf(AssetPack.Ui, "K-04").Single().NineSlice!.Value;
            Assert.Equal(new[] { 24, 12, 24, 12 }, new[] { n.Left, n.Top, n.Right, n.Bottom });
        }

        [Fact]
        public void S14_TV_187__K44SixFramesOf100msLoopIs36Ticks()
        {
            AssetRecord k44 = Reg.RecordsOf(AssetPack.Ui, "K-44").Single();
            Assert.Equal(6, k44.FrameCount);
            Assert.Equal(100, k44.FrameMs);
            Assert.Equal(600, k44.CycleMs);
            Assert.Equal(36, UiTime.MsToUiTicks(k44.CycleMs));
            Assert.True(k44.Loop);
        }

        [Fact]
        public void S14_TV_188__SplitButtonLargeHover()
        {
            Assert.True(UiAssetName.TrySplit("K-20", "k-20_button_large_48x64_hover.png", 48, 64, out string v, out string s));
            Assert.Equal("button_large", v);
            Assert.Equal("hover", s);
            Assert.True(Reg.Contains(new AssetKey(AssetPack.Ui, "K-20", "button_large", "hover")));
        }

        [Fact]
        public void S14_TV_189__SplitBlockFireballIsNormal()
        {
            Assert.True(UiAssetName.TrySplit("I-B1", "i-b1_block_fireball_48x48.png", 48, 48, out string v, out string s));
            Assert.Equal("block_fireball", v);
            Assert.Equal("normal", s);
            Assert.True(Reg.Contains(new AssetKey(AssetPack.Ui, "I-B1", "block_fireball", "normal")));
        }

        [Fact]
        public void S14_TV_190__SplitSelectionCornerDropsFrameToken()
        {
            Assert.True(UiAssetName.TrySplit("K-40", "k-40_selection_corner_24x24_f2.png", 24, 24, out string v, out string s));
            Assert.Equal("selection_corner", v);
            Assert.Equal("normal", s);
            Assert.True(Reg.TryGet(new AssetKey(AssetPack.Ui, "K-40", "selection_corner", "normal"), new PixelSize(24, 24), out AssetRecord r));
            Assert.Equal(2, r.FrameCount);
        }

        [Fact]
        public void S14_TV_190__SplitRejectsNamesThatDoNotMatchTheEntry()
        {
            Assert.False(UiAssetName.TrySplit("K-20", "k-21_button_48x64.png", 48, 64, out _, out _));   // 다른 Id
            Assert.False(UiAssetName.TrySplit("K-20", "k-20_button_32x32.png", 48, 64, out _, out _));   // 매니페스트 크기와 다름
            Assert.False(UiAssetName.TrySplit("K-20", "k-20_48x64.png", 48, 64, out _, out _));          // 변형 없음
            Assert.True(UiAssetName.TrySplit("I-L6", "i-l6_promotion_24x24_on_f2.png", 24, 24, out string v, out string s));
            Assert.Equal("promotion", v);
            Assert.Equal("on", s);
        }

        [Fact]
        public void S14_TV_191__EachCharacterHas147FramesAcrossClips()
        {
            foreach (string c in CharIds)
            {
                AssetRecord[] clips = Clips(c).ToArray();
                Assert.Equal(37, clips.Length);
                Assert.Equal(147, clips.Sum(r => r.FrameCount));
            }
        }

        [Fact]
        public void S14_TV_192__EveryCharacterFrameHasFeetAt30_60()
        {
            foreach (string c in CharIds)
            {
                foreach (AssetRecord clip in Clips(c))
                {
                    Assert.Equal("(30, 60)", clip.Pivot!.Value.ToString());
                    for (int i = 0; i < clip.FrameCount; i++)
                    {
                        Assert.Equal("(30, 60)", clip.CharacterPoints![i].Feet.ToString());
                        Assert.Equal("(30, 60)", clip.OriginOf(i).ToString());
                    }
                }
            }
        }

        [Fact]
        public void S14_TV_193__BodySheetIs768x832With156CellsForAll147Frames()
        {
            foreach (string c in CharIds)
            {
                AssetRecord any = Clips(c).First();
                PixelSize png = RepoAssets.PngSize(any.File);
                Assert.Equal(768, png.Width);
                Assert.Equal(832, png.Height);
                int cols = png.Width / any.FrameSize.Width;
                int rows = png.Height / any.FrameSize.Height;
                Assert.Equal(12, cols);
                Assert.Equal(13, rows);
                Assert.True(cols * rows >= 147);
                var cells = new HashSet<string>();
                foreach (AssetRecord clip in Clips(c))
                {
                    Assert.Equal(any.File, clip.File);
                    foreach (PixelRect r in clip.Frames)
                    {
                        Assert.True(r.X % 64 == 0 && r.Y % 64 == 0 && r.Right <= png.Width && r.Bottom <= png.Height, r.ToString());
                        cells.Add(r.ToString());
                    }
                }
                Assert.Equal(147, cells.Count);   // 프레임마다 다른 칸
            }
        }

        [Fact]
        public void S14_TV_194__PortraitStripIs256x64FourFramesOf64()
        {
            foreach (string c in CharIds)
            {
                Assert.True(Reg.TryGet(new AssetKey(AssetPack.Character, c, "portrait"), out AssetRecord p));
                Assert.Equal(4, p.FrameCount);
                Assert.Equal(64, p.FrameSize.Width);
                Assert.Equal(64, p.FrameSize.Height);
                PixelSize png = RepoAssets.PngSize(p.File);
                Assert.Equal(256, png.Width);
                Assert.Equal(64, png.Height);
                Assert.Equal(png.Width, p.Frames.Last().Right);
            }
        }

        [Fact]
        public void S14_TV_195__BodyTableSumsTo147PerCharacterAnd735InAll()
        {
            // WC 10.1 표의 줄(동작 → 클립 이름)과 장수
            var table = new (string[] Clips, int Count)[]
            {
                (new[] { "idle_down", "idle_right", "idle_up" }, 12),
                (new[] { "walk_down", "walk_right", "walk_up" }, 18),
                (new[] { "stance_walk_down", "stance_walk_right", "stance_walk_up" }, 12),
                (new[] { "fire_down", "fire_right", "fire_up" }, 9),
                (new[] { "special_down", "special_right", "special_up" }, 12),
                (new[] { "signature_down", "signature_right", "signature_up" }, 18),
                (new[] { "hit_down", "hit_right", "hit_up" }, 6),
                (new[] { "death_down" }, 6),
                (new[] { "sit_down" }, 2),
                (new[] { "wave_down", "clap_down", "taunt_down", "bow_down", "sit_down_down", "dance_down", "lift_wand_down", "frustrated_down" }, 32),
                (new[] { "upgrade_wait_down", "upgrade_charge_down", "upgrade_success_down", "upgrade_fail_down" }, 12),
                (new[] { "duel_win_down", "duel_loss_down" }, 8),
            };
            Assert.Equal(147, table.Sum(t => t.Count));
            int all = 0;
            foreach (string c in CharIds)
            {
                int perChar = 0;
                foreach ((string[] clips, int count) in table)
                {
                    int got = clips.Sum(clip => Reg.Resolve(new AssetKey(AssetPack.Character, c, clip)).FrameCount);
                    Assert.Equal(count, got);
                    perChar += got;
                }
                Assert.Equal(147, perChar);
                Assert.Equal(Clips(c).Count(), table.Sum(t => t.Clips.Length));   // 표 밖의 클립이 없다
                all += perChar;
            }
            Assert.Equal(735, all);
        }

        [Fact]
        public void S14_TV_197__PortraitSelectionSilhouetteAre30Pictures()
        {
            int total = 0;
            foreach (string c in CharIds)
            {
                AssetRecord portrait = Reg.Resolve(new AssetKey(AssetPack.Character, c, "portrait"));
                AssetRecord selection = Reg.Resolve(new AssetKey(AssetPack.Character, c, "selection"));
                AssetRecord silhouette = Reg.Resolve(new AssetKey(AssetPack.Character, c, "silhouette"));
                Assert.False(portrait.IsPlaceholder || selection.IsPlaceholder || silhouette.IsPlaceholder);
                Assert.Equal(4 + 1 + 1, portrait.FrameCount + selection.FrameCount + silhouette.FrameCount);
                PixelSize sel = RepoAssets.PngSize(selection.File);
                PixelSize sil = RepoAssets.PngSize(silhouette.File);
                Assert.Equal("192x192", sel.ToString());
                Assert.Equal("32x32", sil.ToString());
                Assert.Equal(sel.ToString(), selection.FrameSize.ToString());
                Assert.Equal(sil.ToString(), silhouette.FrameSize.ToString());
                total += portrait.FrameCount + selection.FrameCount + silhouette.FrameCount;
            }
            Assert.Equal(30, total);
        }

        [Fact]
        public void S14_TV_198__HandSheetIs448x384()
        {
            foreach (string w in WandIds)
            {
                Assert.True(Reg.TryGetWand(w, out WandSheet s));
                Assert.Equal("448x384", s.Hand.SheetSize.ToString());
                Assert.Equal(14, s.Hand.Columns);
                Assert.Equal(6, s.Hand.Rows);
                Assert.Equal("32x64", s.Hand.Cell.ToString());
                Assert.Equal("448x384", RepoAssets.PngSize(s.Hand.File).ToString());
                Assert.Equal(84, Reg.Resolve(new AssetKey(AssetPack.Wand, w, "hand")).FrameCount);
            }
        }

        [Fact]
        public void S14_TV_199__InspectionSheetIs1344x384For14Stages()
        {
            foreach (string w in WandIds)
            {
                Assert.True(Reg.TryGetWand(w, out WandSheet s));
                Assert.Equal("1344x384", s.Inspection.SheetSize.ToString());
                Assert.Equal(7, s.Inspection.Columns);
                Assert.Equal(2, s.Inspection.Rows);
                Assert.Equal(14, s.StageCount);
                Assert.Equal("1344x384", RepoAssets.PngSize(s.Inspection.File).ToString());
            }
        }

        [Fact]
        public void S14_TV_200__IconSheetIs896x64()
        {
            foreach (string w in WandIds)
            {
                Assert.True(Reg.TryGetWand(w, out WandSheet s));
                Assert.Equal("896x64", s.Icon.SheetSize.ToString());
                Assert.Equal("896x64", RepoAssets.PngSize(s.Icon.File).ToString());
            }
        }

        private static WandSheet AnyWand()
        {
            Assert.True(Reg.TryGetWand("wand.1", out WandSheet s));
            return s;
        }

        [Fact]
        public void S14_TV_201__HandRectStage13UpAim()
        {
            Assert.Equal(new PixelRect(416, 320, 32, 64), AnyWand().HandRect(13, SpriteFacing.Up, WandPose.Aim));
        }

        [Fact]
        public void S14_TV_202__HandRectStage0DownHold()
        {
            Assert.Equal(new PixelRect(0, 0, 32, 64), AnyWand().HandRect(0, SpriteFacing.Down, WandPose.Hold));
        }

        [Fact]
        public void S14_TV_203__HandRectStage5RightAim_LeftUsesTheRightRow()
        {
            Assert.Equal(new PixelRect(160, 192, 32, 64), AnyWand().HandRect(5, SpriteFacing.Right, WandPose.Aim));
            Assert.Equal(new PixelRect(160, 192, 32, 64), AnyWand().HandRect(5, SpriteFacing.Left, WandPose.Aim));
            Assert.Equal(31 - 20, AnyWand().MirrorX(20));
        }

        [Fact]
        public void S14_TV_204__InspectionRectStage8()
        {
            Assert.Equal(new PixelRect(192, 192, 192, 192), AnyWand().InspectionRect(8));
        }

        [Fact]
        public void S14_TV_205__InspectionRectStage6()
        {
            Assert.Equal(new PixelRect(1152, 0, 192, 192), AnyWand().InspectionRect(6));
        }

        [Fact]
        public void S14_TV_206__IconRectStage13()
        {
            Assert.Equal(new PixelRect(832, 0, 64, 64), AnyWand().IconRect(13));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => AnyWand().IconRect(14));
        }

        [Fact]
        public void S14_TV_207__Wand2HasThreeMutationPointsOthersTwo()
        {
            foreach (string w in WandIds)
            {
                Assert.True(Reg.TryGetWand(w, out WandSheet s));
                int expected = w == "wand.2" ? 3 : 2;
                Assert.Equal(14, s.Stages.Count);
                Assert.All(s.Stages, st => Assert.Equal(expected, st.Mutations.Count));
            }
        }

        [Fact]
        public void S14_TV_208__AttachWandHand34_44Grip16_48IsTopLeft18_Minus4()
        {
            Assert.Equal("(18, -4)", WandSheet.AttachTopLeft(new PixelPoint(34, 44), new PixelPoint(16, 48)).ToString());
            Assert.Equal("(16, 48)", AnyWand().Grip.ToString());   // MAN-wd grip
            Assert.Equal("(18, -4)", AnyWand().AttachTopLeft(new PixelPoint(34, 44)).ToString());
        }

        private static string Describe(CompositeLayer[] layers) =>
            string.Join(",", layers.Select(l => l.IsWholeSprite ? l.Kind.ToString() : l.Kind + "[" + l.RowFirst + ".." + l.RowLast + "+" + l.OffsetX + "]"));

        [Fact]
        public void S14_TV_209__Char1UpPutsWandUnderTheBody()
        {
            Assert.True(Reg.TryGetWand("wand.1", out WandSheet s));
            Assert.Equal("Wand,Body,ReactiveParts,WildStain,Cosmetics",
                Describe(CharacterComposite.Order("wand.1", SpriteFacing.Up, RepoAssets.Aliases, s.Hand.Cell.Height)));
        }

        [Fact]
        public void S14_TV_210__Wand2UpSplitsAtRow28AndShifts6()
        {
            Assert.True(Reg.TryGetWand("wand.2", out WandSheet s));
            Assert.Equal("Wand[28..63+6],Body,ReactiveParts,WildStain,Cosmetics,Wand[0..27+6]",
                Describe(CharacterComposite.Order("wand.2", SpriteFacing.Up, RepoAssets.Aliases, s.Hand.Cell.Height)));
            // 나누는 줄은 손잡이 기준점에서 위로 20px
            Assert.Equal(28, s.Grip.Y - 20);
        }

        [Fact]
        public void S14_TV_211__Char3DownPutsWandOnTop()
        {
            Assert.True(Reg.TryGetWand("wand.3", out WandSheet s));
            Assert.Equal("Body,ReactiveParts,WildStain,Cosmetics,Wand",
                Describe(CharacterComposite.Order("wand.3", SpriteFacing.Down, RepoAssets.Aliases, s.Hand.Cell.Height)));
            Assert.Equal("Body,ReactiveParts,WildStain,Cosmetics,Wand",
                Describe(CharacterComposite.Order("wand.2", SpriteFacing.Left, RepoAssets.Aliases, s.Hand.Cell.Height)));
        }
    }
}

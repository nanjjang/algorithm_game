using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.Art;

namespace Wizard.Solo.Tests.Art
{
    /// <summary>엔진 텍스처 없이 도는 순수 규칙: 좌표 변환, UI 이름, 상태 우선순위, 9조각 순서, 지팡이 칸, 클립·아이콘 대응, 코드 그림.</summary>
    public class ArtRulesTests
    {
        // ---- RectMath (00 문서 10절, 09 1절) ----

        [Test]
        public void RectMath_TopLeftRectToUnityRect()
        {
            // 시트 768×832(캐릭터 몸), 칸 i = 13 → (64, 64, 64, 64) → Unity y = 832 − 64 − 64 = 704
            Assert.AreEqual(new Rect(64, 704, 64, 64), RectMath.ToUnityRect(64, 64, 64, 64, 832));
            Assert.AreEqual(new Rect(0, 768, 64, 64), RectMath.ToUnityRect(0, 0, 64, 64, 832));
            // 여러 줄 격자: screen_crack 1920×3240의 셋째 칸 [0, 2160, 1920, 1080] → y 0
            Assert.AreEqual(new Rect(0, 0, 1920, 1080), RectMath.ToUnityRect(new PixelRect(0, 2160, 1920, 1080), 3240));
        }

        [Test]
        public void RectMath_PivotFromTopLeftPoint()
        {
            // 캐릭터 발밑 (30, 60) in 64×64 → (30/64, 4/64)
            var p = RectMath.ToPivot(30, 60, 64, 64);
            Assert.AreEqual(30f / 64f, p.x, 1e-6f);
            Assert.AreEqual(4f / 64f, p.y, 1e-6f);
            // 지팡이 손잡이 (16, 48) in 32×64 → (0.5, 0.25)
            var g = RectMath.ToPivot(new PixelPoint(16, 48), 32, 64);
            Assert.AreEqual(0.5f, g.x, 1e-6f);
            Assert.AreEqual(0.25f, g.y, 1e-6f);
            // 보스 kiln_boar origin (96,122) in 192×128
            var b = RectMath.ToPivot(96, 122, 192, 128);
            Assert.AreEqual(0.5f, b.x, 1e-6f);
            Assert.AreEqual(6f / 128f, b.y, 1e-6f);
        }

        [Test]
        public void RectMath_BorderLtrbToUnityLbrt()
        {
            // K-04 [24, 12, 24, 12] (l, t, r, b) → Unity (L, B, R, T) = (24, 12, 24, 12)
            Assert.AreEqual(new Vector4(24, 12, 24, 12), RectMath.LtrbToUnityBorder(new[] { 24, 12, 24, 12 }));
            // 비대칭 [1, 2, 3, 4] → (1, 4, 3, 2)
            Assert.AreEqual(new Vector4(1, 4, 3, 2), RectMath.LtrbToUnityBorder(new[] { 1, 2, 3, 4 }));
            Assert.AreEqual(Vector4.zero, RectMath.LtrbToUnityBorder(null));
        }

        [Test]
        public void RectMath_FitsAndGrid()
        {
            Assert.IsTrue(RectMath.Fits(new PixelRect(704, 768, 64, 64), 768, 832));
            Assert.IsFalse(RectMath.Fits(new PixelRect(705, 768, 64, 64), 768, 832));
            Assert.IsFalse(RectMath.Fits(new PixelRect(0, 0, 0, 64), 768, 832));
            var c = RectMath.GridCell(9, 8, 96, 96); // master_chair 8열: 9 → (1, 1)
            Assert.AreEqual(96, c.X);
            Assert.AreEqual(96, c.Y);
            var s = RectMath.StripFrame(3, 64, 64);
            Assert.AreEqual(192, s.X);
            Assert.AreEqual(0, s.Y);
        }

        // ---- UiAssetName (S14 TV-188~190) ----

        [Test]
        public void S14_TV_188__SplitButtonLargeHover()
        {
            Assert.IsTrue(UiAssetName.TrySplit("K-20", "k-20_button_large_48x64_hover.png", 48, 64, out var v, out var s));
            Assert.AreEqual("button_large", v);
            Assert.AreEqual("hover", s);
        }

        [Test]
        public void S14_TV_189__SplitBlockFireballIsNormal()
        {
            Assert.IsTrue(UiAssetName.TrySplit("I-B1", "i-b1_block_fireball_48x48.png", 48, 48, out var v, out var s));
            Assert.AreEqual("block_fireball", v);
            Assert.AreEqual("normal", s);
        }

        [Test]
        public void S14_TV_190__SplitSelectionCornerDropsFrameToken()
        {
            Assert.IsTrue(UiAssetName.TrySplit("K-40", "k-40_selection_corner_24x24_f2.png", 24, 24, out var v, out var s));
            Assert.AreEqual("selection_corner", v);
            Assert.AreEqual("normal", s);
        }

        [Test]
        public void S14_TV_190__SplitRejectsNamesThatDoNotMatchTheEntry()
        {
            Assert.IsFalse(UiAssetName.TrySplit("K-20", "k-21_button_48x64.png", 48, 64, out _, out _));
            Assert.IsFalse(UiAssetName.TrySplit("K-20", "k-20_button_32x32.png", 48, 64, out _, out _));
            Assert.IsFalse(UiAssetName.TrySplit("K-20", "k-20_48x64.png", 48, 64, out _, out _));
            Assert.IsTrue(UiAssetName.TrySplit("I-L6", "i-l6_promotion_24x24_on_f2.png", 24, 24, out var v, out var s));
            Assert.AreEqual("promotion", v);
            Assert.AreEqual("on", s);
            // 상태 토큰이 변형 안에 있는 꼴(K-27 toggle_off) = 변형으로 읽힌다
            Assert.IsTrue(UiAssetName.TrySplit("K-27", "k-27_toggle_off_48x24.png", 48, 24, out v, out s));
            Assert.AreEqual("toggle_off", v);
            Assert.AreEqual("normal", s);
        }

        // ---- 상태 우선순위 (S14 4.3, 09 6.1절): disabled > pressed > on > hover > normal ----

        [TestCase(false, true, true, true, "disabled")]
        [TestCase(true, true, true, true, "pressed")]
        [TestCase(true, false, true, true, "on")]
        [TestCase(true, false, false, true, "hover")]
        [TestCase(true, false, false, false, "normal")]
        public void UiState_Priority(bool enabled, bool pressed, bool on, bool hover, string expected)
        {
            Assert.AreEqual(expected, UiStates.Resolve(enabled, pressed, on, hover));
        }

        [Test]
        public void UiSelect_FallsBackWhenStateMissing_AndPicksSize()
        {
            var list = new List<UiEntry>
            {
                new UiEntry { Id = "K-26", Variant = "tab_vertical", State = "normal", Width = 48, Height = 56 },
                new UiEntry { Id = "K-26", Variant = "tab_vertical", State = "hover", Width = 48, Height = 56 },
                new UiEntry { Id = "K-26", Variant = "tab_vertical", State = "on", Width = 48, Height = 56 },
            };
            Assert.AreEqual("normal", UiSelect.Pick(list, "tab_vertical", "disabled").State);
            Assert.AreEqual("hover", UiSelect.Pick(list, "tab_vertical", "pressed").State);
            Assert.AreEqual("on", UiSelect.Pick(list, null, "on").State);
            Assert.IsNull(UiSelect.Pick(list, "nope", null));

            var sizes = new List<UiEntry>
            {
                new UiEntry { Variant = "selection_corner", State = "normal", Width = 16, Height = 16 },
                new UiEntry { Variant = "selection_corner", State = "normal", Width = 24, Height = 24 },
                new UiEntry { Variant = "selection_corner", State = "normal", Width = 32, Height = 32 },
            };
            Assert.AreEqual(24, UiSelect.Pick(sizes, "selection_corner", null, 24).Height);
            Assert.AreEqual(32, UiSelect.Pick(sizes, "selection_corner", null, 30).Height);
            Assert.AreEqual(32, UiSelect.Pick(sizes, "selection_corner", null, 100).Height);
            Assert.AreEqual(16, UiSelect.Pick(sizes, "selection_corner", null, 0).Height);
        }

        // ---- 지팡이 칸 (S14 5.4, TV-201~208) ----

        [Test]
        public void S14_TV_201_206__WandCells()
        {
            AssertRect(416, 320, WandSheet.HandRect(13, Facing3.Up, true));
            AssertRect(0, 0, WandSheet.HandRect(0, Facing3.Down, false));
            AssertRect(160, 192, WandSheet.HandRect(5, Facing3.Right, true));
            AssertRect(192, 192, WandSheet.InspectionRect(8));
            AssertRect(1152, 0, WandSheet.InspectionRect(6));
            AssertRect(832, 0, WandSheet.IconRect(13));
        }

        [Test]
        public void WandInspection_ColumnIsStageMod7_RowIsStageDiv7()
        {
            for (int n = 0; n <= WandSheet.MaxStage; n++)
            {
                var r = WandSheet.InspectionRect(n);
                Assert.AreEqual((n % 7) * 192, r.X, "stage " + n);
                Assert.AreEqual((n / 7) * 192, r.Y, "stage " + n);
                Assert.AreEqual(192, r.W);
            }
        }

        [Test]
        public void S14_TV_208__WandAttachTopLeftIsHandMinusGrip()
        {
            var p = WandSheet.AttachTopLeft(new PixelPoint(34, 44));
            Assert.AreEqual(18, p.X);
            Assert.AreEqual(-4, p.Y);
            Assert.AreEqual(31 - 20, WandSheet.MirrorX(20));
        }

        [Test]
        public void WandStageBand()
        {
            Assert.AreEqual("wand_tier_0_to_3", WandSheet.StageBandVariant(3));
            Assert.AreEqual("wand_tier_4_to_6", WandSheet.StageBandVariant(4));
            Assert.AreEqual("wand_tier_7_to_9", WandSheet.StageBandVariant(9));
            Assert.AreEqual("wand_tier_10_to_12", WandSheet.StageBandVariant(12));
            Assert.AreEqual("wand_tier_13", WandSheet.StageBandVariant(13));
        }

        static void AssertRect(int x, int y, PixelRect r)
        {
            Assert.AreEqual(x, r.X, "x of " + r);
            Assert.AreEqual(y, r.Y, "y of " + r);
        }

        // ---- 대응표(별칭) ----

        [Test]
        public void Aliases_CharFolderRoundTrip()
        {
            for (int n = 1; n <= Ids.Char.Count; n++)
            {
                string id = Ids.Char.Of(n);
                string folder = ArtAliases.FolderOf(id);
                Assert.IsNotNull(folder, id);
                Assert.AreEqual(id, ArtAliases.CharOfFolder(folder));
            }
            Assert.IsNull(ArtAliases.FolderOf("char.6"));
            Assert.AreEqual("reactive/" + ArtAliases.FolderOf("char.1") + "/steam", ArtAliases.ReplaceCharSegments("reactive/char.1/steam"));
        }

        [Test]
        public void Aliases_ElementTodAndPaths()
        {
            Assert.AreEqual("frost", ArtAliases.ElementUiKey(Ids.Element.Ice));
            Assert.AreEqual("shadow", ArtAliases.ElementUiKey(Ids.Element.Dark));
            Assert.AreEqual("ice", ArtAliases.ElementFxKey(Ids.Element.Ice));
            Assert.AreEqual("day", ArtAliases.TodVariant(Ids.Tod.Day));
            Assert.AreEqual("dusk", ArtAliases.TodVariant(Ids.Tod.Dusk));
            Assert.AreEqual("night", ArtAliases.TodVariant(Ids.Tod.Boss));
            Assert.AreEqual("day", ArtAliases.TodVariant(null));
            Assert.AreEqual("Art/players/manifest", ArtAliases.ToResourcePath("players/manifest.json"));
            Assert.AreEqual("Art/world/sprites/field.wall.n.01", ArtAliases.ToResourcePath("world/sprites/field.wall.n.01.png"));
            Assert.AreEqual("backgrounds/title_1920x1080.png", ArtAliases.Normalize("ui/../backgrounds/title_1920x1080.png"));
        }

        [Test]
        public void Aliases_CharacterClipResolution()
        {
            var clips = new HashSet<string> { "idle_down", "idle_right", "idle_up", "death_down", "sit_down", "sit_down_down", "walk_right" };
            Assert.AreEqual("idle_right", ArtAliases.ResolveCharacterClip(clips, "idle", Facing3.Right));
            Assert.AreEqual("death_down", ArtAliases.ResolveCharacterClip(clips, "death", Facing3.Up));
            Assert.AreEqual("sit_down", ArtAliases.ResolveCharacterClip(clips, "sit", Facing3.Down));
            Assert.AreEqual("sit_down_down", ArtAliases.ResolveCharacterClip(clips, "sit_down", Facing3.Down));
            Assert.AreEqual("idle_up", ArtAliases.ResolveCharacterClip(clips, "walk", Facing3.Up));
            Assert.AreEqual("idle_up", ArtAliases.ResolveCharacterClip(clips, "nope", Facing3.Up));
        }

        [Test]
        public void Aliases_MonsterKeyForElite()
        {
            var have = new HashSet<string> { "enemy.wolf_bramble", "elite.horn" };
            Assert.AreEqual("enemy.wolf_bramble", ArtAliases.MonsterEntryKey("elite.wolf_bramble", have.Contains));
            Assert.AreEqual("elite.horn", ArtAliases.MonsterEntryKey("elite.horn", have.Contains));
            Assert.IsNull(ArtAliases.MonsterEntryKey("enemy.unknown", have.Contains));
        }

        [Test]
        public void Aliases_IconRules_09Section8()
        {
            Check(Ids.Block.Fireball, "I-B1", "block_fireball");
            Check(Ids.Art.ThunderSpear, "I-T1", "thunder_spear");
            Check(Ids.Element.Ice, "I-E1", "element_frost");
            Check(Ids.Element.Dark, "I-E3", "element_shadow");
            Check(Ids.Passive.Overflow, "I-S2", "overflow");
            Check(Ids.WandTrait.OnCredit, "I-S3", "on_credit");
            Check(Ids.Item.Gold, "I-C2", "gold");
            Check(Ids.Item.StoneMid, "I-M1", "stone_mid");
            Check(Ids.Item.Charm, "I-M3", "safeguard_charm");
            Check(Ids.Item.CrowBundle, "I-M4", "crow_bundle");
            Check(Ids.Item.WandBox, "I-M6", "wand_box");
            Check(Ids.Item.LibraryExpansion, "I-M9", "library_expansion");
            Check("cosmetic.headwear.cracked_mirror_shard", "I-M7", "cosmetic_headwear");
            Assert.IsFalse(ArtAliases.TryIconRule(Ids.Tod.Day, out _, out _));
            Assert.IsFalse(ArtAliases.TryIconRule(Ids.Whim.Crowding, out _, out _));
            Assert.IsTrue(ArtAliases.IsHiddenIcon("currency.starlight"));
        }

        static void Check(string contentId, string expectId, string expectVariant)
        {
            Assert.IsTrue(ArtAliases.TryIconRule(contentId, out var ids, out var v), contentId);
            CollectionAssert.Contains(ids, expectId, contentId);
            Assert.AreEqual(expectVariant, v, contentId);
        }

        [Test]
        public void D39_MutationIconIndexFollowsCodeOrder()
        {
            for (int i = 0; i < Ids.Mutation.All.Length; i++)
                Assert.AreEqual(i, ArtAliases.MutationIconIndex(Ids.Mutation.All[i]));
            Assert.AreEqual(-1, ArtAliases.MutationIconIndex("mutation.none"));
        }

        // ---- 코드 그림 ----

        [Test]
        public void Placeholder_8pxCheckerOfSlateAndWoodDark()
        {
            Assert.AreEqual(GeneratedArt.Slate, GeneratedArt.PlaceholderPixel(0, 0));
            Assert.AreEqual(GeneratedArt.Slate, GeneratedArt.PlaceholderPixel(7, 7));
            Assert.AreEqual(GeneratedArt.WoodDark, GeneratedArt.PlaceholderPixel(8, 0));
            Assert.AreEqual(GeneratedArt.WoodDark, GeneratedArt.PlaceholderPixel(0, 15));
            Assert.AreEqual(GeneratedArt.Slate, GeneratedArt.PlaceholderPixel(8, 8));
            Assert.AreEqual(new Color32(0x8E, 0x9A, 0xAE, 0xFF), GeneratedArt.Slate);
            Assert.AreEqual(new Color32(0x1F, 0x21, 0x30, 0xFF), GeneratedArt.WoodDark);
        }

        [Test]
        public void Circle_CoverageAndRing()
        {
            // 반지름 8: 가운데는 칠함, 귀퉁이는 비움
            Assert.IsTrue(GeneratedArt.CircleCovers(8, 8, 8, false, 0));
            Assert.IsFalse(GeneratedArt.CircleCovers(0, 0, 8, false, 0));
            // 고리 폭 2: 가운데는 비움, 가장자리 안쪽 한 칸은 칠함
            Assert.IsFalse(GeneratedArt.CircleCovers(8, 8, 8, true, 2));
            Assert.IsTrue(GeneratedArt.CircleCovers(8, 0, 8, true, 2));
            Assert.IsTrue(GeneratedArt.CircleCovers(0, 8, 8, true, 2));
        }
    }
}

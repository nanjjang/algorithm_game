using NUnit.Framework;
using Wizard.Solo.Art;

namespace Wizard.Solo.Tests.Art
{
    public class DiscipleThreeAssetPackTests
    {
        [Test]
        public void ResourcesResolveNewArtInSelectionCombatAndEnhancement()
        {
            using (var art = new ArtLibrary())
            {
                const string character = Ids.Char.C3;
                Assert.AreEqual("portraits_64x64_f4_v2", art.CharacterPortrait(character, 2).texture.name);
                Assert.AreEqual("silhouette_32x32_v2", art.CharacterSilhouette(character).texture.name);
                for (int stage = 0; stage <= 13; stage++)
                {
                    Assert.AreEqual("inspection_v2", art.WandInspection(character, stage).texture.name);
                    Assert.AreEqual("icon_v2", art.WandIcon(character, stage).texture.name);
                    foreach (var facing in new[] { Facing3.Down, Facing3.Right, Facing3.Up })
                    {
                        Assert.AreEqual("hand_v2", art.WandHand(character, stage, facing, false).texture.name);
                        Assert.AreEqual("hand_v2", art.WandHand(character, stage, facing, true).texture.name);
                    }
                }

                Assert.AreEqual("i-t1_copy_circle_48x48_v2", art.Icon(Ids.Art.CopyCircle, 48).texture.name);
                Assert.AreEqual("arcane_copy_96x96_f4_v2", art.Effect("special", "arcane_copy").Frames[3].texture.name);
                Assert.AreEqual("cast_seal_13_96x96_f4_v2", art.Effect("special", "cast_seal_13").Frames[3].texture.name);
                Assert.AreEqual("scattered_pages_16x16_f3_v2", art.Effect("special", "scattered_pages").Frames[2].texture.name);
                Assert.AreEqual("sheet_v2", art.Effect(ArtLibrary.LayersGroup, "reactive/char.3/bookmark:up").Frames[3].texture.name);
                Assert.IsEmpty(art.Warnings);
            }
        }
    }
}

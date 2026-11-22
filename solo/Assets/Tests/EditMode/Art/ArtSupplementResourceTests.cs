using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.Art;

namespace Wizard.Solo.Tests.Art
{
    /// <summary>09 요약서 9절의 빠진 그림을 채운 솔로 보충 그림(supplement/solo, Tools/art_supplement.py)과 타이틀 배경.</summary>
    public class ArtSupplementResourceTests
    {
        static void AssertReal(Sprite s, string texture, int w, int h)
        {
            Assert.That(s, Is.Not.Null);
            Assert.AreEqual(texture, s.texture.name);
            Assert.AreEqual(new Vector2(w, h), s.rect.size);
            Assert.AreEqual(FilterMode.Point, s.texture.filterMode);
        }

        [Test]
        public void WaveIconsComeFromSupplementSheets()
        {
            using (var art = new ArtLibrary())
            {
                foreach (var id in new[] { Ids.Tod.Day, Ids.Tod.Dusk, Ids.Tod.Night, Ids.Tod.Boss })
                    AssertReal(art.Icon(id, 32), "tod_32x32_f4", 32, 32);
                foreach (var id in new[] { Ids.Whim.Crowding, Ids.Whim.ThickHide, Ids.Whim.BigPack, Ids.Whim.ChiefParade,
                             Ids.Whim.LastBreath, Ids.Whim.SlowBarrage, Ids.Whim.LongNight, Ids.Whim.NarrowField })
                    AssertReal(art.Icon(id, 32), "whim_32x32_f8", 32, 32);
                foreach (var id in new[] { Ids.Frame.Swarm, Ids.Frame.HuntingParty, Ids.Frame.Battery, Ids.Frame.Ambush, Ids.Frame.Mixed })
                    AssertReal(art.Icon(id, 32), "frame_32x32_f5", 32, 32);
                // 칸이 서로 다르다(같은 칸을 돌려주지 않는다)
                Assert.AreNotEqual(art.Icon(Ids.Tod.Day).rect.x, art.Icon(Ids.Tod.Boss).rect.x);
                Assert.AreNotEqual(art.Icon(Ids.Whim.Crowding).rect, art.Icon(Ids.Whim.NarrowField).rect);
                Assert.IsEmpty(art.Warnings);
            }
        }

        [Test]
        public void MissingUiIdsFallBackToSupplement()
        {
            using (var art = new ArtLibrary())
            {
                AssertReal(art.Ui("I-M10", "borrowed_tag").Sprite, "borrowed_tag_32x16", 32, 16);
                AssertReal(art.Icon("ui:I-M10/borrowed_tag", 32), "borrowed_tag_32x16", 32, 16);
                foreach (var v in new[] { "bag_slot_empty", "bag_slot_filled", "bag_slot_hero" })
                    AssertReal(art.Ui("I-M12", v).Sprite, "bag_slot_32x32_f3", 32, 32);
                Assert.That(art.UiAnim("I-M11", "bundle_open").Frames, Has.Length.EqualTo(5));
                Assert.IsEmpty(art.Warnings);
            }
        }

        [Test]
        public void MissingEffectsAreRegistered()
        {
            using (var art = new ArtLibrary())
            {
                (string group, string key, int frames, int w, int h)[] all =
                {
                    ("player", "boomerang", 4, 32, 32), ("special", "thunderlance_core", 2, 64, 16), ("special", "spear_scratch", 1, 64, 16),
                    ("special", "interrupt_plate", 3, 64, 24), ("ambient", "firefly", 2, 4, 4), ("loot", "crow_pack_open", 5, 64, 64),
                    ("wand", "mutation_ring", 2, 32, 32), ("ritual", "break_pieces_b_fire", 18, 64, 64), ("ritual", "break_pieces_b_dark", 18, 64, 64),
                };
                foreach (var e in all)
                {
                    var a = art.Effect(e.group, e.key);
                    Assert.That(a.Frames, Has.Length.EqualTo(e.frames), e.key);
                    Assert.AreEqual(new Vector2(e.w, e.h), a.Frames[0].rect.size, e.key);
                    StringAssert.DoesNotStartWith("placeholder_", a.Frames[0].texture.name, e.key);
                }
                // 기존 묶음에 더해졌을 뿐 원본 열쇠는 그대로다
                Assert.That(art.Effect("player", "mana_bolt").Frames, Has.Length.EqualTo(2));
                Assert.IsEmpty(art.Warnings);
            }
        }

        [Test]
        public void TitleUsesSoloReplacementBackground()
        {
            using (var art = new ArtLibrary())
            {
                var title = art.Background(ArtLibrary.TitleKey);
                Assert.AreEqual("title_1920x1080_v2", title.texture.name);
                Assert.AreEqual(new Vector2(1920, 1080), title.rect.size);
                Assert.IsEmpty(art.Warnings);
            }
        }

        [Test]
        public void CommonAssetsLoadInFrameOrder()
        {
            using (var art = new ArtLibrary())
            {
                var spark = art.CommonFrames("Combat/hit_spark_ice_32x32_f4");
                Assert.That(spark, Has.Length.EqualTo(4));
                for (int i = 0; i < spark.Length; i++) StringAssert.EndsWith("_0" + i, spark[i].name);
                Assert.That(art.CommonSprite("HUD/panel_9slice_48x48"), Is.Not.Null);
                Assert.That(art.CommonFrames("Combat/no_such_sheet"), Is.Empty);
            }
        }
    }
}

using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.Art;

namespace Wizard.Solo.Tests.Art
{
    public class DiscipleArtResourceTests
    {
        [Test]
        public void DiscipleFourLoadsItsCombatLayersAndStageThirteenArt()
        {
            using (var art = new ArtLibrary())
            {
                Assert.That(art.CharacterSelection(Ids.Char.C4).texture.name, Is.EqualTo("selection_192x192_v2"));
                var body = art.Effect(ArtLibrary.LayersGroup, "reactive/char.4/body");
                Assert.That(body.Frames, Has.Length.EqualTo(147));
                Assert.That(body.Frames[0].pivot, Is.EqualTo(new Vector2(30, 4)));
                Assert.That(art.Effect(ArtLibrary.LayersGroup, "reactive/char.4/crown_shard").Frames, Has.Length.EqualTo(15));
                Assert.That(art.WandHand(Ids.Char.C4, 13, Facing3.Up, true).texture.name, Is.EqualTo("hand_v2"));
                Assert.That(art.WandInspection(Ids.Char.C4, 13).texture.name, Is.EqualTo("inspection_v2"));
                Assert.That(art.WandDetachedPart(Ids.Char.C4).Frames, Has.Length.EqualTo(8));

                foreach (string orientation in new[] { "horizontal", "vertical" })
                {
                    string key = "ice_screen_" + orientation;
                    var wall = art.Effect("special", key);
                    Assert.That(wall.Frames, Has.Length.EqualTo(10));
                    Assert.That(wall.Frames[9].texture.name, Is.EqualTo(key + "_192x96_f10_v2"));
                    Assert.That(wall.Frames[9].texture.filterMode, Is.EqualTo(FilterMode.Point));
                }
            }
        }

        [Test]
        public void DiscipleThreeUsesNewSelectionAndCombatArt()
        {
            using (var art = new ArtLibrary())
            {
                var selection = art.CharacterSelection(Ids.Char.C3);
                Assert.AreEqual("selection_192x192_v2", selection.texture.name);
                Assert.AreEqual(FilterMode.Point, selection.texture.filterMode);

                foreach (var facing in new[] { Facing3.Down, Facing3.Right, Facing3.Up })
                {
                    var fire = art.CharacterClip(Ids.Char.C3, "fire", facing);
                    var frames = art.CharacterClipInfo(Ids.Char.C3, "fire", facing).Frames;
                    Assert.That(fire.Frames.Length, Is.EqualTo(frames.Count));
                    for (int i = 0; i < frames.Count; i++)
                    {
                        Assert.AreEqual("body_v2", fire.Frames[i].texture.name);
                        Assert.AreEqual(FilterMode.Point, fire.Frames[i].texture.filterMode);
                        Assert.AreEqual(new Vector2(30, 4), fire.Frames[i].pivot);
                    }
                    Assert.That(frames[1].Hand.X, Is.GreaterThan(frames[0].Hand.X));
                }

                Assert.IsEmpty(art.Warnings);
            }
        }

        [Test]
        public void DiscipleOneUsesArtVariantsAtDoubleResolution()
        {
            using (var art = new ArtLibrary())
            {
                // 선택·초상·실루엣(ArtVariants/char.1 → players/dalgu/*_v2, Tools/art_disciple1.py)
                Assert.AreEqual("selection_192x192_v2", art.CharacterSelection(Ids.Char.C1).texture.name);
                var portrait = art.CharacterPortrait(Ids.Char.C1, 3);
                Assert.AreEqual("portraits_128x128_f4_v2", portrait.texture.name);
                Assert.AreEqual(new Vector2(128, 128), portrait.rect.size);
                Assert.AreEqual(new Vector2(384, 0), portrait.rect.position);
                Assert.AreEqual("silhouette_32x32_v2", art.CharacterSilhouette(Ids.Char.C1).texture.name);

                // 몸: 37클립 모두 새 시트. 128칸을 그대로 자르고(pixelScale 2) PPU 128이라 화면 크기는 64칸과 같다.
                foreach (var clip in new[] { "idle", "walk", "stance_walk", "fire", "special", "signature", "hit" })
                    foreach (var facing in new[] { Facing3.Down, Facing3.Right, Facing3.Up })
                    {
                        var anim = art.CharacterClip(Ids.Char.C1, clip, facing);
                        var info = art.CharacterClipInfo(Ids.Char.C1, clip, facing);
                        Assert.That(info, Is.Not.Null, clip + " " + facing);
                        Assert.That(anim.Frames.Length, Is.EqualTo(info.Frames.Count));
                        foreach (var f in anim.Frames)
                        {
                            Assert.AreEqual("body_v2", f.texture.name);
                            Assert.AreEqual(FilterMode.Point, f.texture.filterMode);
                            Assert.AreEqual(new Vector2(128, 128), f.rect.size);
                            Assert.AreEqual(128f, f.pixelsPerUnit);
                            Assert.AreEqual(new Vector2(60, 8), f.pivot); // 발 (30,60) 논리 = (60,120) 그림
                            Assert.AreEqual(new Vector2(1f, 1f), (Vector2)f.bounds.size);
                        }
                        foreach (var f in info.Frames) Assert.AreEqual(new PixelPoint(30, 60), f.Feet);
                    }
                // 클립 길이(프레임 수 ÷ fps)는 원본과 같다 — 걷기 6칸 10fps = 0.6초, 강화 반응 성공 4칸 8fps = 0.5초.
                Assert.That(art.CharacterClip(Ids.Char.C1, "walk", Facing3.Right).Duration, Is.EqualTo(0.6f).Within(0.01f));
                Assert.That(art.CharacterClip(Ids.Char.C1, "upgrade_success", Facing3.Down).Duration, Is.EqualTo(0.5f).Within(0.01f));
                Assert.AreEqual("body_v2", art.CharacterClip(Ids.Char.C1, "death", Facing3.Down).Frames[5].texture.name);

                // 지팡이 손·검사·아이콘(크기가 같아 덮어쓰기 표)
                Assert.AreEqual("hand_v2", art.WandHand(Ids.Char.C1, 13, Facing3.Up, true).texture.name);
                Assert.AreEqual("inspection_v2", art.WandInspection(Ids.Char.C1, 7).texture.name);
                Assert.AreEqual("icon_v2", art.WandIcon(Ids.Char.C1, 0).texture.name);

                // 원본 자리와 크기가 다른 효과는 묶음 "char.1"(불가마 불티는 16칸 = 논리 8칸, 시전 문양은 128칸 = 논리 96칸)
                var ember = art.Effect(Ids.Char.C1, "kiln_ember");
                Assert.That(ember.Frames, Has.Length.EqualTo(2));
                Assert.AreEqual(new Vector2(16, 16), ember.Frames[1].rect.size);
                Assert.AreEqual(new Vector2(8f / 64f, 8f / 64f), (Vector2)ember.Frames[1].bounds.size);
                var seal = art.Effect(Ids.Char.C1, "cast_seal");
                Assert.That(seal.Frames, Has.Length.EqualTo(4));
                Assert.AreEqual(new Rect(384, 0, 128, 128), seal.Frames[3].rect);
                Assert.That(seal.Frames[3].bounds.size.x, Is.EqualTo(96f / 64f).Within(1e-4f));
                Assert.That(art.Effect(Ids.Char.C1, "kiln_border").Frames, Has.Length.EqualTo(4));
                Assert.That(art.Effect(Ids.Char.C1, "overflow_steam_arc").Frames, Has.Length.EqualTo(5));
                Assert.IsEmpty(art.Warnings);
            }
        }

        [Test]
        public void DiscipleTwoUsesNewSelectionCombatWandAndLanternArt()
        {
            using (var art = new ArtLibrary())
            {
                var selection = art.CharacterSelection(Ids.Char.C2);
                Assert.AreEqual("selection_192x192_v2", selection.texture.name);
                Assert.AreEqual(FilterMode.Point, selection.texture.filterMode);
                Assert.AreEqual("portraits_64x64_f4_v2", art.CharacterPortrait(Ids.Char.C2, 0).texture.name);
                Assert.AreEqual("silhouette_32x32_v2", art.CharacterSilhouette(Ids.Char.C2).texture.name);

                // body_64(37클립 147칸)를 쓴다 — 전투 클립과 강화 반응 클립이 모두 새 시트에서 나온다.
                foreach (var clip in new[] { "idle", "walk", "fire", "special", "signature", "hit" })
                    foreach (var facing in new[] { Facing3.Down, Facing3.Right, Facing3.Up })
                    {
                        var anim = art.CharacterClip(Ids.Char.C2, clip, facing);
                        var info = art.CharacterClipInfo(Ids.Char.C2, clip, facing);
                        Assert.That(info, Is.Not.Null, clip + " " + facing);
                        Assert.That(anim.Frames.Length, Is.EqualTo(info.Frames.Count));
                        foreach (var f in anim.Frames)
                        {
                            Assert.AreEqual("body_v2", f.texture.name);
                            Assert.AreEqual(new Vector2(30, 4), f.pivot);
                        }
                    }
                Assert.That(art.CharacterClipInfo(Ids.Char.C2, "upgrade_success", Facing3.Down), Is.Not.Null);
                Assert.AreEqual("body_v2", art.CharacterClip(Ids.Char.C2, "upgrade_success", Facing3.Down).Frames[0].texture.name);

                // 깜빡이 등불대(+0~+13): 손·검사·아이콘은 solo_texture_overrides로 _v2를 읽는다.
                Assert.AreEqual("hand_v2", art.WandHand(Ids.Char.C2, 13, Facing3.Right, true).texture.name);
                Assert.AreEqual("inspection_v2", art.WandInspection(Ids.Char.C2, 7).texture.name);
                Assert.AreEqual("icon_v2", art.WandIcon(Ids.Char.C2, 0).texture.name);

                // 머리 등불(9칸 한 줄)과 등불 뺀 몸
                var lantern = art.Effect(ArtLibrary.LayersGroup, "reactive/char.2/head_lantern");
                Assert.That(lantern.Frames, Has.Length.EqualTo(9));
                Assert.AreEqual("sheet_v2", lantern.Frames[8].texture.name);
                Assert.That(art.Effect(ArtLibrary.LayersGroup, "reactive/char.2/head_lantern:flash").Frames, Has.Length.EqualTo(3));

                // 새 효과(원본 자리 없음)는 묶음 "char.2"
                var trail = art.Effect(Ids.Char.C2, "fx/bolt_dash_trail");
                Assert.That(trail.Frames, Has.Length.EqualTo(3));
                Assert.AreEqual(new Vector2(256, 64), trail.Frames[0].rect.size);
                Assert.That(art.Effect(Ids.Char.C2, "enhancement/transition_12_13").Frames, Has.Length.EqualTo(12));
                Assert.IsEmpty(art.Warnings);
            }
        }
    }
}

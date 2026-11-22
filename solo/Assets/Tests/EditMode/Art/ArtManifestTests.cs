using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Wizard.Solo.Art;

namespace Wizard.Solo.Tests.Art
{
    /// <summary>복사된 manifest(Assets/Resources/Art)를 ManifestParser로 읽어 09 요약서·S14 벡터와 맞춘다. 기대값은 요약서에서.</summary>
    public class ArtManifestTests
    {
        static PlayerManifest _players;
        static WandManifest _wands;
        static MonsterManifest _monsters;
        static EffectManifest _effects;
        static UiManifest _ui;
        static WorldManifest _world;

        static PlayerManifest Players => _players ?? (_players = ManifestParser.ParsePlayers(ArtTestUtil.ReadArt("players/manifest.json")));
        static WandManifest Wands => _wands ?? (_wands = ManifestParser.ParseWands(ArtTestUtil.ReadArt("wands/manifest.json")));
        static MonsterManifest Monsters => _monsters ?? (_monsters = ManifestParser.ParseMonsters(ArtTestUtil.ReadArt("monsters/manifest.json")));
        static EffectManifest Effects => _effects ?? (_effects = ManifestParser.ParseEffects(ArtTestUtil.ReadArt("effects/manifest.json"), "effects/"));
        static UiManifest Ui => _ui ?? (_ui = ManifestParser.ParseUi(ArtTestUtil.ReadArt("ui/manifest.json")));
        static WorldManifest World => _world ?? (_world = ManifestParser.ParseWorld(ArtTestUtil.ReadArt("world/manifest.json")));

        // ---- 캐릭터 (09 2절) ----

        // 09 요약서 2.2절 클립 표: (클립, 시작 칸, 프레임 수, fps, 반복)
        static readonly object[] ClipTable =
        {
            new object[] { "idle_down", 0, 4, 7, true }, new object[] { "idle_right", 4, 4, 7, true }, new object[] { "idle_up", 8, 4, 7, true },
            new object[] { "walk_down", 12, 6, 10, true }, new object[] { "walk_right", 18, 6, 10, true }, new object[] { "walk_up", 24, 6, 10, true },
            new object[] { "stance_walk_down", 30, 4, 9, true }, new object[] { "stance_walk_right", 34, 4, 9, true }, new object[] { "stance_walk_up", 38, 4, 9, true },
            new object[] { "fire_down", 42, 3, 14, false }, new object[] { "fire_right", 45, 3, 14, false }, new object[] { "fire_up", 48, 3, 14, false },
            new object[] { "special_down", 51, 4, 12, false }, new object[] { "special_right", 55, 4, 12, false }, new object[] { "special_up", 59, 4, 12, false },
            new object[] { "signature_down", 63, 6, 12, false }, new object[] { "signature_right", 69, 6, 12, false }, new object[] { "signature_up", 75, 6, 12, false },
            new object[] { "hit_down", 81, 2, 12, false }, new object[] { "hit_right", 83, 2, 12, false }, new object[] { "hit_up", 85, 2, 12, false },
            new object[] { "death_down", 87, 6, 10, false }, new object[] { "sit_down", 93, 2, 4, true }, new object[] { "wave_down", 95, 4, 7, false },
            new object[] { "clap_down", 99, 4, 8, false }, new object[] { "taunt_down", 103, 4, 7, false }, new object[] { "bow_down", 107, 4, 8, false },
            new object[] { "sit_down_down", 111, 4, 8, false }, new object[] { "dance_down", 115, 4, 9, false }, new object[] { "lift_wand_down", 119, 4, 8, false },
            new object[] { "frustrated_down", 123, 4, 6, false }, new object[] { "upgrade_wait_down", 127, 2, 5, false },
            new object[] { "upgrade_charge_down", 129, 2, 5, false }, new object[] { "upgrade_success_down", 131, 4, 8, false },
            new object[] { "upgrade_fail_down", 135, 4, 8, false }, new object[] { "duel_win_down", 139, 4, 8, false }, new object[] { "duel_loss_down", 143, 4, 8, false },
        };

        [TestCaseSource(nameof(ClipTable))]
        public void S09_2_2__CharacterClipTable(string clip, int startCell, int frames, int fps, bool loop)
        {
            Assert.AreEqual(5, Players.Characters.Count);
            foreach (var pc in Players.Characters.Values)
            {
                Assert.IsTrue(pc.ClipByName.TryGetValue(clip, out var c), pc.Folder + " " + clip);
                Assert.AreEqual(frames, c.Frames.Count, clip);
                Assert.AreEqual(fps, c.Fps, 1e-6f, clip);
                Assert.AreEqual(loop, c.Loop, clip);
                for (int k = 0; k < frames; k++)
                {
                    int i = startCell + k;
                    var r = c.Frames[k].Rect;
                    Assert.AreEqual((i % 12) * 64, r.X, clip + " frame " + k);
                    Assert.AreEqual((i / 12) * 64, r.Y, clip + " frame " + k);
                    Assert.AreEqual(64, r.W);
                    Assert.AreEqual(64, r.H);
                }
            }
        }

        [Test]
        public void S14_TV_191__ThirtySevenClips147Frames()
        {
            foreach (var pc in Players.Characters.Values)
            {
                Assert.AreEqual(37, pc.Clips.Count, pc.Folder);
                Assert.AreEqual(147, pc.Clips.Sum(c => c.Frames.Count), pc.Folder);
                Assert.AreEqual(147, pc.FrameCount, pc.Folder);
            }
            Assert.AreEqual(ClipTable.Length, 37);
        }

        [Test]
        public void S14_TV_192__AllFeetAt30_60()
        {
            foreach (var pc in Players.Characters.Values)
                foreach (var c in pc.Clips)
                    foreach (var f in c.Frames)
                    {
                        Assert.AreEqual(30, f.Feet.X);
                        Assert.AreEqual(60, f.Feet.Y);
                    }
            Assert.IsTrue(Players.MirrorRightForLeft);
        }

        [Test]
        public void Players_FoldersMatchAliasTable()
        {
            foreach (var folder in Players.Characters.Keys)
                Assert.IsNotNull(ArtAliases.CharOfFolder(folder), folder);
        }

        // ---- 지팡이 (09 3절) ----

        [Test]
        public void Wands_SheetLayoutFromManifest()
        {
            Assert.AreEqual(16, Wands.Grip.X);
            Assert.AreEqual(48, Wands.Grip.Y);
            Assert.AreEqual(5, Wands.Sprites.Count);
            foreach (var w in Wands.Sprites.Values)
            {
                Assert.AreEqual(32, w.HandCell.X);
                Assert.AreEqual(64, w.HandCell.Y);
                Assert.AreEqual(14, w.HandColumns);
                CollectionAssert.AreEqual(new[] { "down_hold", "down_aim", "right_hold", "right_aim", "up_hold", "up_aim" }, w.HandRows);
                Assert.AreEqual(7, w.InspectionColumns);
                Assert.AreEqual(2, w.InspectionRows);
                Assert.AreEqual(14, w.Stages.Count);
                // 손 행 이름 순서가 공식(방향 × 2 + 자세)과 같다
                Assert.AreEqual(WandSheet.HandRow(Facing3.Right, true), w.HandRows.IndexOf("right_aim"));
                Assert.AreEqual(WandSheet.HandRow(Facing3.Up, false), w.HandRows.IndexOf("up_hold"));
            }
        }

        [Test]
        public void S14_TV_207__Wand2HasThreeMutationPoints()
        {
            string folder = ArtAliases.FolderOf(Ids.Char.C2);
            foreach (var st in Wands.Sprites[folder].Stages) Assert.AreEqual(3, st.Mutations.Count);
            foreach (var kv in Wands.Sprites)
                if (kv.Key != folder) foreach (var st in kv.Value.Stages) Assert.AreEqual(2, st.Mutations.Count);
        }

        [Test]
        public void Wands_DetachedParts()
        {
            Assert.AreEqual(3, Wands.Detached.Count);
            foreach (var d in Wands.Detached.Values) Assert.Greater(d.Frames, 0);
        }

        // ---- 몬스터 (09 4절) ----

        [Test]
        public void S14_TV_212__TwelveRegularEnemiesHave100Frames()
        {
            var regular = Monsters.Entries.Where(e => e.Id.StartsWith("enemy.") && e.Id != Ids.Enemy.SporeSeedling).ToList();
            Assert.AreEqual(12, regular.Count);
            Assert.AreEqual(100, regular.Sum(e => e.Clips.Sum(c => c.Frames.Count)));
        }

        [Test]
        public void S14_TV_214__EliteAssetsHave17Frames()
        {
            var elites = Monsters.Entries.Where(e => e.Id.StartsWith("elite.")).ToList();
            Assert.AreEqual(17, elites.Sum(e => e.Clips.Sum(c => c.Frames.Count)));
        }

        [Test]
        public void S14_TV_215__BugLanternHasNoDeathClip()
        {
            Assert.IsNull(Monsters.ById[Ids.Enemy.BugLantern].Clip("death"));
        }

        [Test]
        public void S14_TV_217__BatDeathFirstFrameOrigin32_54()
        {
            var f = Monsters.ById[Ids.Enemy.BatDusk].Clip("death").Frames;
            Assert.AreEqual(32, f[0].Origin.X);
            Assert.AreEqual(54, f[0].Origin.Y);
            Assert.AreEqual(58, f[1].Origin.Y);
        }

        [Test]
        public void Monsters_BossUiForAllSixBosses()
        {
            foreach (var b in new[] { Ids.Boss.KilnBoar, Ids.Boss.MirrorPhantom, Ids.Boss.SporeQueen, Ids.Boss.StormOwl, Ids.Boss.FrostGoat, Ids.Boss.NightSerpent })
            {
                Assert.IsTrue(Monsters.BossUi.ContainsKey(b), b);
                Assert.IsTrue(Monsters.ById.ContainsKey(b), b);
                Assert.AreEqual(96, Monsters.BossUi[b].PortraitSize.X);
                Assert.AreEqual(32, Monsters.BossUi[b].HudSize.X);
            }
        }

        // ---- 효과 (09 5절) ----

        [Test]
        public void S14_TV_219__Effects303InSevenGroups()
        {
            Assert.AreEqual(303, Effects.Count);
            var expect = new Dictionary<string, int> { { "player", 55 }, { "special", 89 }, { "ritual", 77 }, { "enemy", 31 }, { "loot", 11 }, { "wand", 15 }, { "status", 25 } };
            Assert.AreEqual(7, Effects.Groups.Count);
            foreach (var kv in expect) Assert.AreEqual(kv.Value, Effects.ByName[kv.Key].Entries.Count, kv.Key);
        }

        [Test]
        public void S09_5__SevenMultiRowGridsUseFrameRects()
        {
            var multi = new List<string>();
            foreach (var g in Effects.Groups)
                foreach (var e in g.Entries)
                    if (e.Frames.Select(f => f.Y).Distinct().Count() > 1) multi.Add(g.Name + "/" + e.Key);
            CollectionAssert.AreEquivalent(new[]
            {
                "ritual/mutation_vine", "ritual/screen_crack", "ritual/max_sky_band", "ritual/title_ribbon_normal",
                "ritual/title_ribbon_gold", "ritual/plaza_notice_normal", "ritual/plaza_notice_gold"
            }, multi);
            var crack = Effects.ByName["ritual"].ByKey["screen_crack"];
            Assert.AreEqual(3, crack.Frames.Count);
            Assert.AreEqual(2160, crack.Frames[2].Y);
            Assert.AreEqual("effects/ritual/screen_crack_1920x1080_f3.png", crack.Path);
        }

        [Test]
        public void S14_TV_222__IceScreenHasTenFrames()
        {
            var e = Effects.ByName["special"].ByKey["ice_screen_horizontal"];
            Assert.AreEqual(10, e.Frames.Count);
            Assert.AreEqual(192, e.Size.X);
            Assert.AreEqual(96, e.Size.Y);
        }

        [Test]
        public void S14_TV_223__MutationVineHas24Frames()
        {
            Assert.AreEqual(24, Effects.ByName["ritual"].ByKey["mutation_vine"].Frames.Count);
        }

        // ---- UI (09 6절) ----

        [Test]
        public void S14_TV_181__Ui627Entries()
        {
            Assert.AreEqual(627, Ui.Assets.Count);
        }

        [Test]
        public void S14_TV_184__K20NineSlice12AndFourStates()
        {
            var k20 = Ui.Of("K-20");
            CollectionAssert.AreEquivalent(new[] { "disabled", "hover", "normal", "pressed" }, k20.Select(e => e.State).ToArray());
            foreach (var e in k20)
            {
                Assert.AreEqual("button_large", e.Variant);
                CollectionAssert.AreEqual(new[] { 12, 12, 12, 12 }, e.NineSliceLtrb);
            }
        }

        [Test]
        public void S14_TV_186__K04NineSlice24x12_ToUnityOrder()
        {
            var e = Ui.Of("K-04").Single();
            CollectionAssert.AreEqual(new[] { 24, 12, 24, 12 }, e.NineSliceLtrb);
            var b = RectMath.LtrbToUnityBorder(e.NineSliceLtrb);
            Assert.AreEqual(24f, b.x); // L
            Assert.AreEqual(12f, b.y); // B
            Assert.AreEqual(24f, b.z); // R
            Assert.AreEqual(12f, b.w); // T
        }

        [Test]
        public void S14_TV_187__K44SixFramesOf100ms()
        {
            var e = Ui.Of("K-44").Single();
            Assert.AreEqual(6, e.Frames);
            Assert.AreEqual(100, e.FrameMs);
        }

        [Test]
        public void Ui_AllEntriesParseToVariant()
        {
            foreach (var e in Ui.Assets)
                if (!e.Id.StartsWith("F-")) Assert.IsNotEmpty(e.Variant, e.File);
            Assert.AreEqual("../backgrounds/title_1920x1080.png", Ui.External["S-0201"]);
        }

        // ---- 월드 (09 7절) ----

        [Test]
        public void World_TilesHaveThreeTodsAndSpritesHaveOrigins()
        {
            var grass = World.Assets["field.tile.grass.01"];
            Assert.IsTrue(grass.IsTile);
            CollectionAssert.AreEquivalent(new[] { "day", "dusk", "night" }, grass.Variants.Keys.ToArray());
            var rain = World.Assets["field.weather.rain"];
            Assert.AreEqual(1920, rain.FrameSize.X);
            Assert.AreEqual(4, rain.FrameCount);
            var chair = World.Assets["facility.master_chair"];
            Assert.AreEqual(8, chair.Columns);
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, chair.PoseFrames["sleep"]);
            var dummy = World.Assets["facility.training_dummy"];
            Assert.AreEqual(32, dummy.Origin.X);
            Assert.AreEqual(94, dummy.Origin.Y);
        }

        // ---- 보강 묶음 ----

        [Test]
        public void Supplement_EnhancementAndLayersParse()
        {
            var enh = ManifestParser.ParseEnhancement(ArtTestUtil.ReadArt("supplement/enhancement/manifest.json"), "enhance", "supplement/enhancement/", out var bg);
            Assert.AreEqual("supplement/enhancement/background/workbench.png", bg);
            Assert.AreEqual(6, enh.ByKey["mutation_icons"].Frames.Count);
            Assert.AreEqual(3, enh.ByKey["protected_stamp"].Frames.Count);
            Assert.IsTrue(enh.ByKey["master_sleep"].HasPivot);
            var layers = ManifestParser.ParseLayers(ArtTestUtil.ReadArt("supplement/layers/manifest.json"), "layers", "supplement/layers/");
            Assert.AreEqual(36, layers.Entries.Count);
            string steam = "reactive/" + ArtAliases.FolderOf(Ids.Char.C1) + "/steam";
            Assert.AreEqual(12, layers.ByKey[steam].Frames.Count);
            Assert.AreEqual("normal_00", layers.ByKey[steam].FrameKeys[0]);
            // 모든 시트가 복사돼 있다
            foreach (var e in enh.Entries.Concat(layers.Entries))
                FileAssert.Exists(ArtTestUtil.ArtRoot + "/" + e.Path, e.Key);
        }

        [Test]
        public void Presentation_D37_MonsterFps8()
        {
            var p = ArtPresentation.Parse(ArtTestUtil.ReadArt("solo_presentation.json"));
            Assert.AreEqual(8f, p.MonsterClipFps);
            Assert.IsFalse(p.MonsterClipLoops("death"));
            Assert.IsTrue(p.MonsterClipLoops("move"));
        }
    }
}

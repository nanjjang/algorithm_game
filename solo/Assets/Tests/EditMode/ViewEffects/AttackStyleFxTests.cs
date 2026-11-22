using System.Collections.Generic;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Wizard.Solo.Art;
using Wizard.Solo.Audio;
using Wizard.Solo.Data;
using Wizard.Solo.Screens.Hints;
using Wizard.Solo.Sim;
using Wizard.Solo.View.Effects;

namespace Wizard.Solo.Tests.ViewEffects
{
    /// <summary>
    /// 기본 공격 유형 15종·고유 비기 강화의 그림·소리 연결(Docs/12-attack-styles.md 3·4절): styles 묶음 읽기(기준점·구간), 유형 아이콘,
    /// 폭발 두 번 그리지 않기, 몸 없는 동작, 소리 사건 → 실제 클립, 원정 준비 미리보기 표의 그림, 도감 분류.
    /// </summary>
    public sealed class AttackStyleFxTests
    {
        static PresentationData D => ViewEffectsTestData.Shared;

        static readonly string[] Cores =
        {
            "block.core.ember_mortar", "block.core.flame_jet", "block.core.bouncing_ember", "block.core.chain_bolt", "block.core.thunder_orb",
            "block.core.sky_strike", "block.core.page_flurry", "block.core.tome_throw", "block.core.rune_mine", "block.core.frost_lance",
            "block.core.blizzard", "block.core.icicle_fall", "block.core.shadow_wave", "block.core.shadow_scythe", "block.core.eye_seeker"
        };

        // ------------------------------------------------------------ 그림

        [Test]
        public void Manifest__PivotOnlyWhenAsked_ClipsAlways()
        {
            const string json = @"{ ""notes"": {}, ""g"": { ""a"": { ""path"": ""a.png"", ""size"": [48, 64], ""frames"": [[0,0,48,64],[48,0,48,64]],
                ""pivot"": [24, 58], ""clips"": { ""rise"": [0, 1], ""bad"": [3] } } } }";
            var plain = ManifestParser.ParseEffects(json, "x/");
            var e = plain.ByName["g"].ByKey["a"];
            Assert.IsFalse(e.HasPivot, "기존 묶음은 pivot 칸이 있어도 가운데 기준(그림이 옮겨지지 않게)");
            Assert.AreEqual(new[] { 0, 1 }, e.Clips["rise"]);
            Assert.IsFalse(e.Clips.ContainsKey("bad"), "끝이 없는 구간은 버린다");
            var styled = ManifestParser.ParseEffects(json, "x/", true).ByName["g"].ByKey["a"];
            Assert.IsTrue(styled.HasPivot);
            Assert.AreEqual(24, styled.Pivot.X);
            Assert.AreEqual(58, styled.Pivot.Y);
            Assert.AreEqual("x/a.png", styled.Path);
        }

        [Test]
        public void ArtLibrary__StyleGroupsLoad_WithPivotAndClips_NoWarnings()
        {
            using (var art = new ArtLibrary())
            {
                (string group, string key, int frames)[] all =
                {
                    ("styles.char.1", "mortar_shell", 4), ("styles.char.1", "mortar_blast", 6), ("styles.char.1", "fire_floor", 6), ("styles.char.1", "flame_jet", 6),
                    ("styles.char.1", "fire_pillar", 8), ("styles.char.2", "chain_link", 4), ("styles.char.2", "ball_lightning", 6), ("styles.char.2", "strike_marker", 4),
                    ("styles.char.2", "strike_bolt", 4), ("styles.char.2", "strike_blast", 5), ("styles.char.3", "book_spin", 8), ("styles.char.3", "book_open", 6),
                    ("styles.char.3", "book_flip", 4), ("styles.char.3", "page_vortex", 8), ("styles.char.3", "rune_trap", 8), ("styles.char.3", "rune_burst", 5),
                    ("styles.char.4", "ice_spear", 2), ("styles.char.4", "blizzard", 6), ("styles.char.4", "icicle", 2), ("styles.char.4", "icicle_marker", 4),
                    ("styles.char.4", "ice_spike", 8), ("styles.char.5", "shadow_wave", 5), ("styles.char.5", "giant_hand", 8),
                    ("styles.common", "ground_slam", 6), ("styles.common", "ground_crack", 4), ("styles.common", "dust_skirt", 5), ("styles.common", "debris_burst", 6),
                    ("styles.reuse", "page_large", 4), ("styles.reuse", "page_small", 4), ("styles.reuse", "hit_page", 4), ("styles.reuse", "floating_eye", 3),
                };
                foreach (var (g, k, n) in all)
                {
                    Assert.IsNotNull(art.EffectInfo(g, k), g + "/" + k);
                    var a = art.Effect(g, k);
                    Assert.AreEqual(n, a.Frames.Length, g + "/" + k);
                    StringAssert.DoesNotStartWith("placeholder", a.Frames[0].texture.name, g + "/" + k);
                }
                // 기준점: 불꽃 방사는 왼쪽 끝(지팡이 끝) — 프레임 왼쪽 위 기준 (5, 24) → Unity 기준점(왼쪽 아래 원점) (5, 48 − 24)
                var jet = art.Effect("styles.char.1", "flame_jet").Frames[0];
                Assert.AreEqual(new Vector2(5f, 24f), jet.pivot);
                var bolt = art.Effect("styles.char.2", "strike_bolt").Frames[0];
                Assert.AreEqual(new Vector2(24f, 192f - 188f), bolt.pivot, "벼락 아래 끝 = 착지점");
                Assert.AreEqual(new[] { 0, 2 }, art.EffectInfo("styles.char.4", "ice_spike").Clips["rise"]);
                Assert.AreEqual(new[] { 5, 6 }, art.EffectInfo("styles.char.5", "giant_hand").Clips["slam"]);
                // 기존 효과 묶음은 그대로 가운데 기준
                var hit = art.Effect("player", "hit_fire").Frames[0];
                Assert.AreEqual(hit.rect.size * 0.5f, hit.pivot);
                Assert.IsEmpty(art.Warnings, string.Join("\n", art.Warnings));
            }
        }

        [Test]
        public void ArtLibrary__EveryStyleCoreHasARealIcon()
        {
            using (var art = new ArtLibrary())
            {
                var seen = new HashSet<string>();
                foreach (var id in Cores)
                {
                    var s = art.Icon(id, 64);
                    Assert.IsNotNull(s, id);
                    StringAssert.DoesNotStartWith("placeholder", s.texture.name, id);
                    Assert.IsTrue(seen.Add(s.texture.name + s.rect), id + ": 유형마다 다른 그림");
                }
            }
        }

        [Test]
        public void StylePreview__EveryAnimExists_AndEveryCoreHasAPreview()
        {
            var root = JObject.Parse(ViewEffectsTestData.Read("DataSolo/StylePreview.json"));
            var styles = (JObject)root["styles"];
            using (var art = new ArtLibrary())
            {
                foreach (var id in Cores)
                {
                    var arr = styles[id] as JArray;
                    Assert.IsNotNull(arr, id);
                    Assert.IsNotEmpty(arr, id);
                    foreach (var step in arr)
                    {
                        string spec = (string)step["anim"];
                        int slash = spec.IndexOf('/');
                        Assert.IsNotNull(art.EffectInfo(spec.Substring(0, slash), spec.Substring(slash + 1)), id + ": " + spec);
                    }
                }
            }
        }

        // ------------------------------------------------------------ 두 번 그리지 않기 · 몸 없는 동작

        [Test]
        public void Rules__StyleExplosionFiltered_BodylessBehaviorsHidden()
        {
            Assert.IsTrue(StyleFxRules.IsStyleExplosion(new SimEvent { Type = SimEventType.Explosion, B = 1 }));
            Assert.IsFalse(StyleFxRules.IsStyleExplosion(new SimEvent { Type = SimEventType.Explosion, B = 0 }), "화염구·적 폭발은 그대로");
            var db = TestContent.Db;
            string[] hidden = { "block.core.ember_mortar", "block.core.flame_jet", "block.core.chain_bolt", "block.core.sky_strike", "block.core.rune_mine",
                "block.core.blizzard", "block.core.icicle_fall", "block.core.shadow_wave", "block.core.shadow_scythe" };
            string[] bodies = { "block.core.bouncing_ember", "block.core.thunder_orb", "block.core.page_flurry", "block.core.tome_throw", "block.core.frost_lance",
                "block.core.eye_seeker", "block.core.frost_lance_shard" };
            foreach (var id in hidden)
                Assert.IsTrue(StyleFxRules.BodyHidden(D.Tuning, new ProjectileEntity { Block = db.Block(id) }), id);
            foreach (var id in bodies)
                Assert.IsFalse(StyleFxRules.BodyHidden(D.Tuning, new ProjectileEntity { Block = db.Block(id) }), id);
            Assert.IsFalse(StyleFxRules.BodyHidden(D.Tuning, new ProjectileEntity { Block = db.Block(Ids.Block.MagicBolt) }));
        }

        // ------------------------------------------------------------ 소리

        static List<SoundCue> Map(SimEvent e)
        {
            var w = new SimWorld(TestContent.Db, SimModuleSet.Stubs(), 1);
            w.Players[0].Active = true;
            var m = new SoundEventMap(D.Bindings, D.Tuning, (a, b) => 0f);
            var l = new List<SoundCue>();
            m.Map(w, e, l);
            return l;
        }

        static string Ids1(List<SoundCue> c) { var s = new List<string>(); foreach (var x in c) s.Add(x.Id); return string.Join(",", s); }

        [Test]
        public void Sound__StyleEventsMapToStyleIds_ExplosionNotTwice()
        {
            Assert.AreEqual("style.lob.land", Ids1(Map(new SimEvent { Type = SimEventType.StyleBlast, B = (int)StyleBlastKind.MortarLand, A = 1 })));
            Assert.AreEqual("style.strike.land", Ids1(Map(new SimEvent { Type = SimEventType.StyleBlast, B = (int)StyleBlastKind.SkyStrike })));
            Assert.AreEqual("style.icicle.land", Ids1(Map(new SimEvent { Type = SimEventType.StyleBlast, B = (int)StyleBlastKind.IcicleDrop })));
            Assert.AreEqual("style.rune.burst", Ids1(Map(new SimEvent { Type = SimEventType.StyleBlast, B = (int)StyleBlastKind.RuneBurst })));
            Assert.AreEqual("", Ids1(Map(new SimEvent { Type = SimEventType.StyleBlast, B = (int)StyleBlastKind.MortarFloorTick })), "불바닥 틱마다는 소리 없음(시작에 지글)");
            Assert.AreEqual("", Ids1(Map(new SimEvent { Type = SimEventType.Explosion, B = 1 })), "유형 폭발의 속성 폭발 소리는 내지 않는다");
            Assert.AreEqual("explosion.detonated", Ids1(Map(new SimEvent { Type = SimEventType.Explosion, B = 0 })));
            Assert.AreEqual("style.floor.start", Ids1(Map(new SimEvent { Type = SimEventType.StyleGroundSpawned, B = 0 })));
            Assert.AreEqual("style.rune.arm", Ids1(Map(new SimEvent { Type = SimEventType.StyleMineArmed })));
            Assert.AreEqual("style.shatter", Ids1(Map(new SimEvent { Type = SimEventType.StyleShatter })));
            Assert.AreEqual("style.split", Ids1(Map(new SimEvent { Type = SimEventType.StyleSplit })));
            Assert.AreEqual("style.ricochet", Ids1(Map(new SimEvent { Type = SimEventType.StyleRicochet })));
            Assert.AreEqual("style.zap", Ids1(Map(new SimEvent { Type = SimEventType.StyleZap })));
            Assert.AreEqual("style.art.kiln.slam,style.art.kiln.pillar",
                Ids1(Map(new SimEvent { Type = SimEventType.ArtStage, Id = Ids.Art.Kiln, A = (int)ArtStageKind.Rise, B = 0 })));
            Assert.AreEqual("style.art.kiln.pillar", Ids1(Map(new SimEvent { Type = SimEventType.ArtStage, Id = Ids.Art.Kiln, A = (int)ArtStageKind.Rise, B = 1 })));
            Assert.AreEqual("style.art.copy.open", Ids1(Map(new SimEvent { Type = SimEventType.ArtStage, Id = Ids.Art.CopyCircle, A = (int)ArtStageKind.Pulse, B = 0 })));
            Assert.AreEqual("style.art.copy.pulse", Ids1(Map(new SimEvent { Type = SimEventType.ArtStage, Id = Ids.Art.CopyCircle, A = (int)ArtStageKind.Pulse, B = 3 })));
            Assert.AreEqual("style.art.hand.crush", Ids1(Map(new SimEvent { Type = SimEventType.ArtStage, Id = Ids.Art.ShadowHand, A = (int)ArtStageKind.Crush })));
            Assert.AreEqual("style.art.ice.slam,style.art.ice.spike",
                Ids1(Map(new SimEvent { Type = SimEventType.ArtStage, Id = Ids.Art.IceScreen, A = (int)ArtStageKind.Rise, B = 0 })));
            Assert.AreEqual("style.art.bolt_dash.land", Ids1(Map(new SimEvent { Type = SimEventType.ArtStage, Id = Ids.Art.BoltDash, A = (int)ArtStageKind.Land, B = 2 })));
        }

        [Test]
        public void Sound__EveryStyleEventResolvesToARealClip_LoopsAreLoops()
        {
            var bank = SoundBank.Parse(Resources.Load<TextAsset>(SoundBank.Resource).text);
            var events = new List<string>
            {
                "style.lob.land", "style.floor.start", "style.strike.land", "style.icicle.land", "style.rune.arm", "style.rune.burst", "style.ricochet",
                "style.zap", "style.shatter", "style.split", "style.art.kiln.slam", "style.art.kiln.pillar", "style.art.bolt_dash.land",
                "style.art.copy.open", "style.art.copy.pulse", "style.art.copy.end", "style.art.ice.slam", "style.art.ice.spike",
                "style.art.hand.grip", "style.art.hand.crush"
            };
            foreach (var id in Cores)
                if (id != "block.core.flame_jet" && id != "block.core.blizzard") events.Add("heat.shot_block." + id.Substring("block.".Length));
            var into = new List<ResolvedClip>();
            foreach (var ev in events)
            {
                into.Clear();
                bank.Resolve(ev, null, "char.1", 0, 0f, n => 0, into);
                Assert.IsNotEmpty(into, ev);
                foreach (var c in into) Assert.IsNotNull(Resources.Load<AudioClip>(c.Path), ev + " → " + c.Path);
            }
            foreach (var ev in new[] { "style.flame.loop", "style.blizzard.loop", "style.orb.loop" })
            {
                into.Clear();
                bank.Resolve(ev, null, "char.1", 0, 0f, n => 0, into);
                Assert.AreEqual(1, into.Count, ev);
                Assert.IsTrue(into[0].Loop, ev);
                Assert.IsNotNull(Resources.Load<AudioClip>(into[0].Path), ev);
            }
            // 불꽃 방사·눈보라는 한 발마다 소리를 내지 않는다(반복음이 대신한다 — 초당 10번 겹치지 않게)
            into.Clear();
            bank.Resolve("heat.shot_block.core.flame_jet", null, "char.1", 0, 0f, n => 0, into);
            Assert.IsEmpty(into);
            var loops = D.Tuning.StringMap("sound", "style_loops");
            Assert.AreEqual("style.flame.loop", loops["block.core.flame_jet"]);
            Assert.AreEqual("style.orb.loop", loops["block.core.thunder_orb"]);
        }

        // ------------------------------------------------------------ 도감

        [Test]
        public void Codex__StylesSectionLists15_WithCharacterAndSlot()
        {
            var db = TestContent.Db;
            var names = TestContent.Names;
            var args = CodexModel.Args(db, db.BaselineCharacter, null, k => k.ToString());
            var rows = CodexModel.Rows(CodexModel.Styles, names, db, Ids.Char.C1, args);
            int heads = 0;
            foreach (var r in rows)
            {
                if (r.Head == null) continue;
                heads++;
                Assert.IsNotNull(r.IconId);
                Assert.IsFalse(string.IsNullOrEmpty(r.Body), r.Head);
                Assert.IsFalse(r.Body.StartsWith("str."), r.Body);
                StringAssert.DoesNotContain("{", r.Side);
            }
            Assert.AreEqual(15, heads, "캐릭터마다 셋 × 5");
            CollectionAssert.Contains(CodexModel.Sections, CodexModel.Styles);
        }
    }
}

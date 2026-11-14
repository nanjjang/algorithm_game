using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.Art;
using Wizard.Solo.View.Effects;

namespace Wizard.Solo.Tests.ViewEffects
{
    /// <summary>캐릭터 교체 묶음 고르기(G16 character_fx + 효과 묶음 char.N)와 보충 그림 연결 값.</summary>
    public class CharacterFxTests
    {
        static EffectsTuning Tuning => EffectsTuning.Parse(ViewEffectsTestData.TuningJson);

        [Test]
        public void StageBandFollowsWandStage()
        {
            var t = Tuning;
            // 제자 2 지팡이 띠: 0~3, 4~6, 7~9, 10~12, 13(교체 묶음 manifest wand.stageBands)
            Assert.AreEqual(0, CharacterFx.Band(t, Ids.Char.C2, 0));
            Assert.AreEqual(0, CharacterFx.Band(t, Ids.Char.C2, 3));
            Assert.AreEqual(1, CharacterFx.Band(t, Ids.Char.C2, 4));
            Assert.AreEqual(2, CharacterFx.Band(t, Ids.Char.C2, 9));
            Assert.AreEqual(3, CharacterFx.Band(t, Ids.Char.C2, 12));
            Assert.AreEqual(4, CharacterFx.Band(t, Ids.Char.C2, 13));
            Assert.AreEqual("fx/trail_band_2", CharacterFx.Key(t, Ids.Char.C2, "wand", "trail_lightning", 8));
            Assert.AreEqual("fx/impact_band_4", CharacterFx.Key(t, Ids.Char.C2, "player", "hit_lightning", 13));
            // 다른 캐릭터·없는 열쇠는 바꾸지 않는다
            Assert.IsNull(CharacterFx.Key(t, Ids.Char.C3, "wand", "trail_lightning", 8));
            Assert.IsNull(CharacterFx.Key(t, Ids.Char.C2, "wand", "trail_fire", 8));
        }

        [Test]
        public void CharacterBundlesReplaceSharedArtOnlyForThatCharacter()
        {
            var t = Tuning;
            using (var art = new ArtLibrary())
            {
                // 제자 2: 단계별 꼬리·적중·발사, 벼락 질주, 피뢰침
                var trail = CharacterFx.Resolve(art, t, Ids.Char.C2, "wand", "trail_lightning", 13);
                Assert.AreSame(art.Effect(Ids.Char.C2, "fx/trail_band_4"), trail);
                Assert.AreSame(art.Effect(Ids.Char.C2, "fx/bolt_dash_trail"), CharacterFx.Resolve(art, t, Ids.Char.C2, "special", "lightning_dash"));
                Assert.AreSame(art.Effect(Ids.Char.C2, "fx/lightning_rod"), CharacterFx.Resolve(art, t, Ids.Char.C2, "player", "graze_strike"));
                Assert.That(CharacterFx.Own(art, Ids.Char.C2, "fx/muzzle_band_0").Frames, Has.Length.EqualTo(3));
                // 같은 공용 열쇠도 다른 캐릭터면 공용 그림
                Assert.AreSame(art.Effect("wand", "trail_lightning"), CharacterFx.Resolve(art, t, Ids.Char.C5, "wand", "trail_lightning", 13));
                // 제자 1: 불가마 테두리·불티·시전 문양·끓어넘침
                Assert.AreSame(art.Effect(Ids.Char.C1, "kiln_border"), CharacterFx.Resolve(art, t, Ids.Char.C1, "special", "kiln_border"));
                Assert.AreSame(art.Effect(Ids.Char.C1, "kiln_ember"), CharacterFx.Resolve(art, t, Ids.Char.C1, "special", "kiln_ember"));
                Assert.AreSame(art.Effect(Ids.Char.C1, "cast_seal"), CharacterFx.Resolve(art, t, Ids.Char.C1, "special", "cast_seal_11"));
                Assert.AreSame(art.Effect(Ids.Char.C1, "overflow_steam_arc"), CharacterFx.Resolve(art, t, Ids.Char.C1, "player", "overflow_steam_arc"));
                Assert.IsNull(CharacterFx.Own(art, Ids.Char.C1, "no_such_key"));
                Assert.IsEmpty(art.Warnings);
            }
        }

        [Test]
        public void SupplementArtKeysInTuningPointAtRealArt()
        {
            var t = Tuning;
            using (var art = new ArtLibrary())
            {
                // 긁힌 자국(ground_marks), 뇌광창 흰 심(art.fx), "끊음!" 판(interrupt)
                var scratch = art.EffectInfo(t.S("ground_marks", "scratch_group"), t.S("ground_marks", "scratch_key"));
                Assert.That(scratch, Is.Not.Null);
                Assert.AreEqual(new PixelPoint(64, 16), scratch.Size);
                Assert.That(art.EffectInfo("special", t.S("art", "fx", "art.thunder_spear", "core")), Is.Not.Null);
                Assert.That(art.EffectInfo(t.S("interrupt", "plate_group"), t.S("interrupt", "plate_key")), Is.Not.Null);
                // 제자 2 벼락 질주 잔광·도착
                Assert.That(CharacterFx.Own(art, Ids.Char.C2, t.S(CharacterFx.Section, Ids.Char.C2, "bolt_dash", "afterglow")), Is.Not.Null);
                Assert.That(CharacterFx.Own(art, Ids.Char.C2, t.S(CharacterFx.Section, Ids.Char.C2, "bolt_dash", "arrival")), Is.Not.Null);
                Assert.IsEmpty(art.Warnings);
            }
        }
    }
}

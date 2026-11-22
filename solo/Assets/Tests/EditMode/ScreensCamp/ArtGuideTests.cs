using System.Collections.Generic;
using NUnit.Framework;
using Wizard.Solo.Data;
using Wizard.Solo.Screens.Camp;
using Wizard.Solo.Screens.Hints;

namespace Wizard.Solo.Tests.ScreensCamp
{
    /// <summary>
    /// 비기 풀이(ArtGuide — 플레이 피드백 "비기 같은데 설명 더 필요하면 좋을듯"): 모든 비기에 내는 법·효과·비용·배우는 법이 채워지고,
    /// 수치는 arts.json·tries.json 값이며(문구에 숫자 없음), 키 이름은 넘겨 준 키 배치를 따르고, 다음 가지는 선행 표대로인지 본다.
    /// </summary>
    public sealed class ArtGuideTests
    {
        static readonly System.Func<LogicalKey, string> Keys = k => k == LogicalKey.Stance ? "L" : k == LogicalKey.Skill ? "K" : k.ToString();

        static string Num(float v) => v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

        static bool HasDigit(string s)
        {
            int depth = 0;
            foreach (char c in s)
            {
                if (c == '{') depth++;
                else if (c == '}') depth--;
                else if (depth == 0 && c >= '0' && c <= '9') return true;
            }
            return false;
        }

        [Test]
        public void ArtGuide__EveryTrieArtOfEveryCharacter_HasFilledLines()
        {
            var db = TestContent.Db;
            var names = TestContent.Names;
            for (int n = 1; n <= Ids.Char.Count; n++)
            {
                string ch = Ids.Char.Of(n);
                foreach (var id in db.TrieArts(ch))
                {
                    var g = ArtGuide.Describe(names, db, ch, id, Keys);
                    foreach (var line in new[] { g.Effect, g.HowTo, g.Short, g.Cost, g.Unlock })
                    {
                        Assert.IsNotNull(line, ch + " " + id);
                        Assert.IsFalse(line.Contains("{"), "채우지 않은 자리: " + id + " = " + line);
                    }
                    foreach (var line in new[] { g.Gap, g.Fallback, g.Next, g.Upgrade })
                        if (line != null) Assert.IsFalse(line.Contains("{"), id + " = " + line);
                    StringAssert.Contains("K", g.HowTo, "비기 키 이름: " + id);
                }
            }
        }

        [Test]
        public void ArtGuide__TextsHaveNoDigits_ValuesComeFromData()
        {
            var nt = (NameTable)TestContent.Names;
            foreach (var key in new[]
            {
                ArtGuide.HowToTapKey, ArtGuide.HowToKey, ArtGuide.HowToJustKey, ArtGuide.ShortKey, ArtGuide.GapKey, ArtGuide.CostKey,
                ArtGuide.FallbackKey, ArtGuide.UnlockStartKey, ArtGuide.UnlockUniqueKey, ArtGuide.UnlockPrereqKey, ArtGuide.UnlockPrereqWaveKey,
                ArtGuide.NextKey, ArtGuide.UpgradeKey, ArtGuide.MirrorKey, ArtGuide.NoCreditKey
            })
            {
                Assert.IsTrue(nt.Has(key), key);
                Assert.IsFalse(HasDigit(nt.Get(key)), "문구에 숫자를 적지 않는다: " + key);
            }
            var db = TestContent.Db;
            var parser = db.Tries.Parser;
            var spear = ArtGuide.Describe(nt, db, Ids.Char.C1, Ids.Art.Spear, Keys);
            StringAssert.Contains(Num(parser.ArtCooldownSeconds), spear.Cost, "대기 = tries.json art_cooldown_seconds");
            StringAssert.Contains(db.Art(Ids.Art.Spear).CostInt.ToString(), spear.Cost, "결정 비용 = arts.json cost");
            var thunder = ArtGuide.Describe(nt, db, Ids.Char.C1, Ids.Art.ThunderSpear, Keys);
            StringAssert.Contains(Num(parser.JustWindowSeconds), thunder.HowTo, "섬 창 = tries.json just_window_seconds");
            StringAssert.Contains(db.Art(Ids.Art.ThunderSpear).Unlock.MinWave.ToString(), thunder.Unlock, "웨이브 조건 = arts.json min_wave");
            StringAssert.Contains(nt.Get(Ids.Art.Spear), thunder.Unlock, "선행 = 꿰뚫는 창");
        }

        [Test]
        public void ArtGuide__HowTo_UsesKeyNamesAndGlyphs_BurstIsJustTheArtKey()
        {
            var db = TestContent.Db;
            var names = TestContent.Names;
            var burst = ArtGuide.Describe(names, db, Ids.Char.C1, Ids.Art.Burst, Keys);
            Assert.AreEqual("K", burst.Short, "마력 방출 = 비기 키 하나");
            Assert.IsFalse(burst.HowTo.Contains("L"), "자세 키가 필요 없다");
            Assert.IsNull(burst.Gap, "방향이 없으면 간격 줄이 없다");

            var spear = ArtGuide.Describe(names, db, Ids.Char.C1, Ids.Art.Spear, k => k == LogicalKey.Stance ? "Shift" : k == LogicalKey.Skill ? "X" : "?");
            string up = names.Get(ArtTrieModel.GlyphKey(CmdDir.Forward));
            StringAssert.Contains("Shift", spear.HowTo, "지금 배치의 자세 키");
            StringAssert.Contains("X", spear.HowTo, "지금 배치의 비기 키");
            StringAssert.Contains(up + " " + up, spear.HowTo, "앞, 앞");

            var wings = ArtGuide.Describe(names, db, Ids.Char.C1, Ids.Art.Wings, Keys);
            StringAssert.Contains(names.Get(ArtGuide.MirrorKey), wings.HowTo, "좌우 대칭 경로");
        }

        [Test]
        public void ArtGuide__TightestGap_FollowsParserEdgeRules()
        {
            var db = TestContent.Db;
            var p = db.Tries.Parser;
            Assert.AreEqual(p.GapDefaultSeconds, ArtGuide.TightestGapSeconds(db.Art(Ids.Art.Spear), Ids.Char.C1, p), 1e-6, "앞, 앞 = 기본");
            Assert.AreEqual(p.GapShortSeconds, ArtGuide.TightestGapSeconds(db.Art(Ids.Art.Octastar), Ids.Char.C1, p), 1e-6, "앞, 뒤 = 짧은 간격");
            Assert.AreEqual(p.GapSameSideSeconds, ArtGuide.TightestGapSeconds(db.Art(Ids.Art.Afterimage), Ids.Char.C1, p), 1e-6, "같은 옆 두 번");
            Assert.AreEqual(p.GapFullBloomSeconds, ArtGuide.TightestGapSeconds(db.Art(Ids.Art.Bloom), Ids.Char.C1, p), 1e-6, "만개 = 가장 짧은 만개 간격");
            Assert.AreEqual(p.GapShortSeconds, ArtGuide.TightestGapSeconds(db.Art(Ids.Art.Kiln), Ids.Char.C1, p), 1e-6, "char.1 뒤, 뒤 = 짧은 간격");
            Assert.AreEqual(0f, ArtGuide.TightestGapSeconds(db.Art(Ids.Art.Fan), Ids.Char.C1, p), "방향 하나 = 간격 없음");
        }

        [Test]
        public void ArtGuide__NextBranches_FollowPrereqTable_UniqueAndStart()
        {
            var db = TestContent.Db;
            var names = TestContent.Names;
            CollectionAssert.AreEquivalent(new[] { Ids.Art.Fan, Ids.Art.Wings }, ArtGuide.NextArts(db, Ids.Char.C1, Ids.Art.Burst));
            CollectionAssert.AreEquivalent(new[] { Ids.Art.Spear, Ids.Art.Octastar }, ArtGuide.NextArts(db, Ids.Char.C1, Ids.Art.Fan));
            CollectionAssert.AreEquivalent(new[] { Ids.Art.ThunderSpear }, ArtGuide.NextArts(db, Ids.Char.C1, Ids.Art.Spear));
            Assert.IsEmpty(ArtGuide.NextArts(db, Ids.Char.C1, Ids.Art.Bloom), "만개 뒤에는 가지가 없다");
            Assert.IsNull(ArtGuide.Describe(names, db, Ids.Char.C1, Ids.Art.Bloom, Keys).Next);

            var kiln = ArtGuide.Describe(names, db, Ids.Char.C1, Ids.Art.Kiln, Keys);
            Assert.AreEqual(names.Get(ArtGuide.UnlockUniqueKey), kiln.Unlock, "고유 비기");
            StringAssert.Contains(names.Get(Ids.Art.Recoil), kiln.Fallback, "결정이 모자라면 반동 사격");
            Assert.AreEqual(names.Get(ArtGuide.UnlockStartKey), ArtGuide.Describe(names, db, Ids.Char.C1, Ids.Art.Recoil, Keys).Unlock);
            Assert.IsNull(ArtGuide.Describe(names, db, Ids.Char.C1, Ids.Art.Spear, Keys).Fallback, "공용 비기는 대신 나가는 비기가 없다");
        }

        [Test]
        public void ArtGuide__NoCredit_OnlyForCreditWandCharacter()
        {
            var db = TestContent.Db;
            var names = TestContent.Names;
            string nc = names.Get(ArtGuide.NoCreditKey);
            Assert.IsFalse(db.Art(Ids.Art.Bloom).Credit, "만개는 외상 불가(arts.json)");
            StringAssert.Contains(nc, ArtGuide.Describe(names, db, Ids.Char.C5, Ids.Art.Bloom, Keys).Cost, "외상 지팡이 캐릭터에게만 뜻이 있다");
            Assert.IsFalse(ArtGuide.Describe(names, db, Ids.Char.C1, Ids.Art.Bloom, Keys).Cost.Contains(nc));
            Assert.IsFalse(ArtGuide.Describe(names, db, Ids.Char.C5, Ids.Art.Spear, Keys).Cost.Contains(nc), "외상 되는 비기");
        }

        [Test]
        public void ArtGuide__NullInputs_AreSafe()
        {
            var g = ArtGuide.Describe(null, TestContent.Db, Ids.Char.C1, Ids.Art.Spear, Keys);
            Assert.IsNull(g.HowTo);
            Assert.IsNull(ArtGuide.Describe(TestContent.Names, null, Ids.Char.C1, Ids.Art.Spear, Keys).Effect);
            Assert.IsNull(ArtGuide.Describe(TestContent.Names, TestContent.Db, Ids.Char.C1, "art.nope", Keys).Effect);
            Assert.IsEmpty(ArtGuide.NextArts(null, Ids.Char.C1, Ids.Art.Burst));
            Assert.AreEqual(0f, ArtGuide.TightestGapSeconds(null, Ids.Char.C1, TestContent.Db.Tries.Parser));
        }

        // ------------------------------------------------------------ 처음 비기 쪽지

        [Test]
        public void FirstArtsHint__OnArtTrieAndCamp_NotInBattleOrMenus()
        {
            Assert.Contains(HintIds.FirstArts, new List<string>(HintIds.FirstTime));
            Assert.IsTrue(HintContexts.Accepts(HintIds.FirstArts, Wizard.Solo.UI.ScreenId.ArtTrie));
            Assert.IsTrue(HintContexts.Accepts(HintIds.FirstArts, Wizard.Solo.UI.ScreenId.Camp));
            foreach (var s in new[] { Wizard.Solo.UI.ScreenId.Battle, Wizard.Solo.UI.ScreenId.MainMenu, Wizard.Solo.UI.ScreenId.Settlement, Wizard.Solo.UI.ScreenId.Title })
                Assert.IsFalse(HintContexts.Accepts(HintIds.FirstArts, s), s.ToString());
            var t = HintTuning.From(TestContent.Db);
            var spot = t.SpotFor(Wizard.Solo.UI.ScreenId.ArtTrie);
            float trieRight = (1920f - ArtTrieView.W) * 0.5f + ArtTrieView.W;
            Assert.GreaterOrEqual(spot.X, trieRight, "쪽지가 트라이 판(가운데 912)을 가리지 않는다");
            Assert.LessOrEqual(spot.X + spot.W, 1920f);
        }

        [Test]
        public void FirstArtsHint__BodyFilledFromCharacterData()
        {
            var names = TestContent.Names;
            var ch = TestContent.Db.BaselineCharacter;
            string body = names.Format(HintIds.BodyKey(HintIds.FirstArts), "stance", "L", "art", "K", "n", ch.ComboPerCrystal, "cap", ch.CrystalCap);
            Assert.IsFalse(body.Contains("{"), body);
            StringAssert.Contains(ch.ComboPerCrystal.ToString(), body);
            StringAssert.Contains(ch.CrystalCap.ToString(), body);
        }
    }
}

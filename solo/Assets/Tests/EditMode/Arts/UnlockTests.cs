using System.Collections.Generic;
using NUnit.Framework;
using Wizard.Solo.Sim;
using Wizard.Solo.Sim.Arts;

namespace Wizard.Solo.Tests.Arts
{
    /// <summary>해금: 04 요약서 11절(U-R2~R8), 14.2절 B17~B26, 08 요약서 8.4절(TV-078~080).</summary>
    public class UnlockTests
    {
        const int Seeds = 40;
        static readonly string[] None = new string[0];
        static readonly string[] CommonEight =
        {
            Ids.Art.Fan, Ids.Art.Wings, Ids.Art.Afterimage, Ids.Art.Spear, Ids.Art.Vent, Ids.Art.Octastar, Ids.Art.ThunderSpear, Ids.Art.Bloom
        };

        static ArtKit Kit(string charId, string[] commons, ulong seed = 1) => new ArtKit(charId, 0, 0, commons, true, seed);

        static List<string> Offer(ArtKit kit, int turn) => kit.Arts.BuildOffer(kit.W, kit.P, turn).Candidates;

        /// <summary>여러 씨앗에서 그 차례에 한 번이라도 후보로 나온 비기의 합집합(= 적격 집합이 2개를 넘어도 전부 드러난다).</summary>
        static HashSet<string> OfferedOverSeeds(string charId, string[] commons, int turn)
        {
            var seen = new HashSet<string>();
            for (ulong s = 1; s <= Seeds; s++)
            {
                var c = Offer(Kit(charId, commons, s), turn);
                Assert.LessOrEqual(c.Count, TestContent.Db.Camp.ArtCandidates);
                Assert.AreEqual(c.Count, new HashSet<string>(c).Count, "candidates are distinct");
                foreach (var a in c) seen.Add(a);
            }
            return seen;
        }

        [Test]
        public void S03_TV_B17__NewProfileStartsWithThreeArts()
        {
            var kit = Kit(Ids.Char.C3, None);
            CollectionAssert.AreEquivalent(new[] { Ids.Art.Burst, Ids.Art.Recoil, Ids.Art.CopyCircle }, kit.P.UnlockedArts);
        }

        [Test]
        public void S03_U_R2__EveryCharacterStartsWithBurstRecoilAndOwnUnique()
        {
            for (int c = 1; c <= Ids.Char.Count; c++)
            {
                var kit = Kit(Ids.Char.Of(c), None);
                string unique = TestContent.Db.Character(Ids.Char.Of(c)).UniqueArt;
                CollectionAssert.AreEquivalent(new[] { Ids.Art.Burst, Ids.Art.Recoil, unique }, kit.P.UnlockedArts);
            }
        }

        [Test]
        public void S03_TV_B18__FirstTurnOffersTwoOfFanWingsVent()
        {
            var allowed = new[] { Ids.Art.Fan, Ids.Art.Wings, Ids.Art.Vent };
            for (ulong s = 1; s <= Seeds; s++)
            {
                var c = Offer(Kit(Ids.Char.C3, None, s), 1);
                Assert.AreEqual(2, c.Count);
                Assert.AreNotEqual(c[0], c[1]);
                CollectionAssert.IsSubsetOf(c, allowed);
            }
            // 균등 무작위: 세 후보가 모두 나온다.
            CollectionAssert.AreEquivalent(allowed, OfferedOverSeeds(Ids.Char.C3, None, 1));
        }

        [Test]
        public void S03_TV_B19__ThunderSpearOnlyFromTurnFour()
        {
            var commons = new[] { Ids.Art.Fan, Ids.Art.Spear };
            CollectionAssert.DoesNotContain(OfferedOverSeeds(Ids.Char.C1, commons, 3), Ids.Art.ThunderSpear);
            CollectionAssert.Contains(OfferedOverSeeds(Ids.Char.C1, commons, 4), Ids.Art.ThunderSpear);
        }

        [Test]
        public void S03_TV_B20__BloomOnlyFromTurnSix()
        {
            var commons = new[] { Ids.Art.Fan, Ids.Art.Octastar };
            CollectionAssert.DoesNotContain(OfferedOverSeeds(Ids.Char.C1, commons, 5), Ids.Art.Bloom);
            CollectionAssert.Contains(OfferedOverSeeds(Ids.Char.C1, commons, 6), Ids.Art.Bloom);
        }

        [Test]
        public void S03_TV_B21__SingleEligibleShowsOneCandidate()
        {
            var commons = new[]
            {
                Ids.Art.Fan, Ids.Art.Wings, Ids.Art.Afterimage, Ids.Art.Spear, Ids.Art.Vent, Ids.Art.Octastar, Ids.Art.ThunderSpear
            };
            CollectionAssert.AreEqual(new[] { Ids.Art.Bloom }, Offer(Kit(Ids.Char.C1, commons), 6));
        }

        [Test]
        public void S03_TV_B21b__NoEligibleMeansEmptyOffer()
        {
            var commons = new[]
            {
                Ids.Art.Fan, Ids.Art.Wings, Ids.Art.Afterimage, Ids.Art.Spear, Ids.Art.Vent, Ids.Art.Octastar, Ids.Art.ThunderSpear
            };
            var kit = Kit(Ids.Char.C1, commons);
            var offer = kit.Arts.BuildOffer(kit.W, kit.P, 1);
            Assert.AreEqual(1, offer.TurnWave);
            Assert.AreEqual(0, offer.Candidates.Count);
        }

        [Test]
        public void S03_TV_B22__SkippedTurnsOneToSixInOrder()
        {
            // 시작 웨이브 11: 차례 1~6을 차례대로 고른다(차례 줄은 Run의 몫이고 여기서는 후보 규칙만 본다).
            string[] wish = { Ids.Art.Fan, Ids.Art.Spear, Ids.Art.Octastar, Ids.Art.ThunderSpear, Ids.Art.Bloom, Ids.Art.Wings, Ids.Art.Vent, Ids.Art.Afterimage };
            for (ulong s = 1; s <= Seeds; s++)
            {
                var kit = Kit(Ids.Char.C2, None, s);
                for (int turn = 1; turn <= 6; turn++)
                {
                    var c = Offer(kit, turn);
                    Assert.GreaterOrEqual(c.Count, 1, "turn " + turn);
                    Assert.LessOrEqual(c.Count, 2);
                    foreach (var a in c)
                    {
                        var def = TestContent.Db.Art(a);
                        Assert.IsFalse(kit.P.UnlockedArts.Contains(a));
                        Assert.IsTrue(kit.P.UnlockedArts.Contains(def.Unlock.Prereq), a + " needs " + def.Unlock.Prereq);
                        Assert.GreaterOrEqual(turn, def.Unlock.MinWave, a + " at turn " + turn);
                    }
                    string pick = null;
                    foreach (var w in wish) if (c.Contains(w)) { pick = w; break; }
                    Assert.IsTrue(kit.Arts.Unlock(kit.W, kit.P, pick));
                }
                Assert.AreEqual(3 + 6, kit.P.UnlockedArts.Count);
            }
            // 앞에서 창을 골랐다면 4번 차례부터 뇌광창이, 팔방이 있으면 6번 차례에 만개가 적격이다.
            CollectionAssert.Contains(OfferedOverSeeds(Ids.Char.C2, new[] { Ids.Art.Fan, Ids.Art.Spear }, 4), Ids.Art.ThunderSpear);
            CollectionAssert.Contains(OfferedOverSeeds(Ids.Char.C2, new[] { Ids.Art.Fan, Ids.Art.Octastar }, 6), Ids.Art.Bloom);
        }

        [Test]
        public void S03_TV_B24__AllEightUnlockedMeansNoTurns()
        {
            var kit = Kit(Ids.Char.C4, CommonEight);
            for (int turn = 1; turn <= 6; turn++) Assert.AreEqual(0, Offer(kit, turn).Count, "turn " + turn);
        }

        [Test]
        public void S03_TV_B25__SecondRunOffersOnlySecrets()
        {
            var kit = Kit(Ids.Char.C1, new[] { Ids.Art.Fan, Ids.Art.Wings, Ids.Art.Afterimage, Ids.Art.Spear, Ids.Art.Vent, Ids.Art.Octastar });
            Assert.AreEqual(0, Offer(kit, 1).Count);
            Assert.AreEqual(0, Offer(kit, 2).Count);
            Assert.AreEqual(0, Offer(kit, 3).Count);
            CollectionAssert.AreEqual(new[] { Ids.Art.ThunderSpear }, Offer(kit, 4));
            Assert.IsTrue(kit.Arts.Unlock(kit.W, kit.P, Ids.Art.ThunderSpear));
            Assert.AreEqual(0, Offer(kit, 5).Count);
            CollectionAssert.AreEqual(new[] { Ids.Art.Bloom }, Offer(kit, 6));
        }

        [Test]
        public void S03_TV_B26__UnlocksArePerCharacter()
        {
            // 제자 1의 프로필에는 부채 전개가 있고 제자 2는 새 상태다 → 제자 2로 시작한 판에는 부채 전개가 없다.
            var mods = SimModuleSet.Stubs();
            var arts = new ArtSystem();
            mods.Arts = arts;
            var w2 = new SimWorld(TestContent.Db, mods, 1);
            w2.Run.Config = new RunConfig { CharId = Ids.Char.C2, UnlockedArts = new List<string> { Ids.Art.Burst, Ids.Art.Recoil, Ids.Art.BoltDash } };
            w2.P0.Active = true;
            w2.P0.CharId = Ids.Char.C2;
            arts.SetupPlayer(w2, w2.P0);
            CollectionAssert.DoesNotContain(w2.P0.UnlockedArts, Ids.Art.Fan);

            var w1 = new SimWorld(TestContent.Db, mods, 1);
            w1.Run.Config = new RunConfig { CharId = Ids.Char.C1, UnlockedArts = new List<string> { Ids.Art.Burst, Ids.Art.Recoil, Ids.Art.Kiln, Ids.Art.Fan } };
            w1.P0.Active = true;
            w1.P0.CharId = Ids.Char.C1;
            arts.SetupPlayer(w1, w1.P0);
            CollectionAssert.AreEquivalent(new[] { Ids.Art.Burst, Ids.Art.Recoil, Ids.Art.Kiln, Ids.Art.Fan }, w1.P0.UnlockedArts);
        }

        [Test]
        public void S03_U_R7__PrerequisiteTable()
        {
            // 선행 표(11.1절): 잔상 ← 양익, 창·팔방 ← 부채, 방열 ← 반동(항상 보유), 부채·양익 ← 방출(항상 보유).
            var seen = OfferedOverSeeds(Ids.Char.C1, new[] { Ids.Art.Wings }, 3);
            CollectionAssert.AreEquivalent(new[] { Ids.Art.Fan, Ids.Art.Vent, Ids.Art.Afterimage }, seen);
            seen = OfferedOverSeeds(Ids.Char.C1, new[] { Ids.Art.Fan }, 3);
            CollectionAssert.AreEquivalent(new[] { Ids.Art.Wings, Ids.Art.Vent, Ids.Art.Spear, Ids.Art.Octastar }, seen);
        }

        [Test]
        public void S03_U_R3__UnlockAppliesImmediatelyAndOnce()
        {
            var kit = new ArtKit(Ids.Char.C1, 5, 0, None);
            kit.Seq("F, K+2304");
            Assert.AreEqual(Ids.Art.Burst, kit.Last.Art, "fan is locked → burst");
            Assert.IsTrue(kit.Arts.Unlock(kit.W, kit.P, Ids.Art.Fan));
            Assert.AreEqual(1, kit.CountEvents(SimEventType.ArtUnlocked, Ids.Art.Fan));
            Assert.IsFalse(kit.Arts.Unlock(kit.W, kit.P, Ids.Art.Fan), "already unlocked");
            Assert.IsFalse(kit.Arts.Unlock(kit.W, kit.P, Ids.Art.BoltDash), "another character's unique");
            Assert.IsFalse(kit.Arts.Unlock(kit.W, kit.P, "art.unknown"));
            kit.Now += 20000;
            kit.Seq("F, K+2304");
            Assert.AreEqual(Ids.Art.Fan, kit.Last.Art);
        }

        [Test]
        public void S03_U_20__SameSeedGivesSameCandidates()
        {
            for (ulong s = 1; s <= 5; s++)
                CollectionAssert.AreEqual(Offer(Kit(Ids.Char.C3, None, s), 1), Offer(Kit(Ids.Char.C3, None, s), 1));
        }

        [Test]
        public void S13_TV_078__UniqueNodeIsUnlockedOnNewProfile()
        {
            var arts = new ArtSystem();
            var unlocked = new HashSet<string> { Ids.Art.Burst, Ids.Art.Recoil };
            var candidates = new HashSet<string> { Ids.Art.Fan };
            var views = arts.DescribeTrie(TestContent.Db, Ids.Char.C1, unlocked, candidates);
            Assert.AreEqual(13, views.Count);
            foreach (var v in views)
            {
                if (v.ArtId == Ids.Art.Kiln) Assert.AreEqual(ArtNodeState.Unlocked, v.State);
                if (v.ArtId == Ids.Art.Burst || v.ArtId == Ids.Art.Recoil) Assert.AreEqual(ArtNodeState.Unlocked, v.State);
                if (v.ArtId == Ids.Art.Fan) Assert.AreEqual(ArtNodeState.Candidate, v.State);
                if (v.ArtId == Ids.Art.Wings || v.ArtId == Ids.Art.Spear || v.ArtId == Ids.Art.Bloom) Assert.AreEqual(ArtNodeState.Locked, v.State);
                if (v.ArtId == null) Assert.AreEqual(ArtNodeState.Unlocked, v.State, "pass-through node");
                Assert.AreEqual(v.Depth, v.Path.Length);
            }
            Assert.AreEqual(-1, views[0].ParentId);
            Assert.AreEqual(0, views[0].NodeId);
        }

        [Test]
        public void S04_2__JustArtStateIsSeparateFromKArt()
        {
            // FF 마디: K 기술(꿰뚫는 창)과 섬 기술(뇌광창)은 따로 잠긴다(04 요약서 2절·11.2절).
            var arts = new ArtSystem();
            var spearOnly = new HashSet<string> { Ids.Art.Burst, Ids.Art.Recoil, Ids.Art.Spear };
            ArtTrieNodeView ff = null;
            foreach (var v in arts.DescribeTrie(TestContent.Db, Ids.Char.C1, spearOnly, null)) if (v.JustArtId == Ids.Art.ThunderSpear) ff = v;
            Assert.IsNotNull(ff);
            Assert.AreEqual(ArtNodeState.Unlocked, ff.State);
            Assert.AreEqual(ArtNodeState.Locked, ff.JustState);

            foreach (var v in arts.DescribeTrie(TestContent.Db, Ids.Char.C1, spearOnly, new HashSet<string> { Ids.Art.ThunderSpear }))
                if (v.JustArtId == Ids.Art.ThunderSpear) Assert.AreEqual(ArtNodeState.Candidate, v.JustState);

            spearOnly.Add(Ids.Art.ThunderSpear);
            foreach (var v in arts.DescribeTrie(TestContent.Db, Ids.Char.C1, spearOnly, null))
            {
                if (v.JustArtId == Ids.Art.ThunderSpear) Assert.AreEqual(ArtNodeState.Unlocked, v.JustState);
                if (v.JustArtId == null) Assert.AreEqual(ArtNodeState.Unlocked, v.JustState, "섬 기술 없는 마디");
            }
        }

        [Test]
        public void S13_TV_080__OnlyNonDefaultGapsAreMarked()
        {
            var arts = new ArtSystem();
            var all = new HashSet<string>(TestContent.Db.TrieArts(Ids.Char.C1));
            foreach (var v in arts.DescribeTrie(TestContent.Db, Ids.Char.C1, all, null))
            {
                string path = "";
                foreach (var d in v.Path) path += d == CmdDir.Forward ? "F" : d == CmdDir.Back ? "B" : d == CmdDir.Left ? "L" : "R";
                switch (path)
                {
                    case "FF": case "BF":
                        Assert.IsTrue(v.GapIsDefault, path);
                        Assert.AreEqual(0.3f, v.GapSeconds, 1e-6f);
                        break;
                    case "FB": case "BB":
                        Assert.IsFalse(v.GapIsDefault, path);
                        Assert.AreEqual(0.2f, v.GapSeconds, 1e-6f);
                        break;
                    case "LL": case "RR":
                        Assert.IsFalse(v.GapIsDefault, path);
                        Assert.AreEqual(0.25f, v.GapSeconds, 1e-6f);
                        break;
                    case "FBB": case "FBBF":
                        Assert.IsFalse(v.GapIsDefault, path);
                        Assert.AreEqual(0.18f, v.GapSeconds, 1e-6f);
                        break;
                    default:
                        Assert.IsTrue(v.GapIsDefault, path);
                        break;
                }
                if (path == "FF") Assert.AreEqual(Ids.Art.ThunderSpear, v.JustArtId);
            }
            // 얼음 병풍의 간선은 기본 0.3초다(고유 비기 가운데 혼자).
            foreach (var v in arts.DescribeTrie(TestContent.Db, Ids.Char.C4, all, null))
                if (v.ArtId == Ids.Art.IceScreen) Assert.IsTrue(v.GapIsDefault);
        }
    }
}

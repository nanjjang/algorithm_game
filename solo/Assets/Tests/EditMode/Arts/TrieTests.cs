using NUnit.Framework;
using Wizard.Solo.Sim.Arts;

namespace Wizard.Solo.Tests.Arts
{
    /// <summary>트라이 구조: 04 요약서 2.6절 검사표, 14.3절 S군, 원문 S03 5.6절 전이 표.</summary>
    public class TrieTests
    {
        const long G018 = 2765, G020 = 3072, G025 = 3840, G030 = 4608;

        static CommandTrie Build(string charId) => CommandTrie.Build(TestContent.Db, charId);

        static CmdDir[] Path(string name)
        {
            if (name == "root") return new CmdDir[0];
            var p = new CmdDir[name.Length];
            for (int i = 0; i < name.Length; i++)
            {
                CmdDir d;
                Assert.IsTrue(CommandTrie.TryParseDir(name[i].ToString(), out d), name);
                p[i] = d;
            }
            return p;
        }

        static TrieNode N(CommandTrie t, string name)
        {
            var n = t.Find(Path(name));
            Assert.IsNotNull(n, "node " + name + " missing in " + t.CharId);
            return n;
        }

        static void AssertNode(CommandTrie t, string name, string fail, long gap, string kArt)
        {
            var n = N(t, name);
            Assert.AreEqual(N(t, fail).Id, n.Fail, name + ".Fail");
            Assert.AreEqual(gap, n.InEdgeGapU, name + ".InEdgeGap");
            Assert.AreEqual(kArt, n.KArtId, name + ".KArt");
        }

        /// <summary>rows: "노드 F B L R" 한 줄씩.</summary>
        static void AssertNext(string charId, params string[] rows)
        {
            var t = Build(charId);
            Assert.AreEqual(rows.Length, t.Count, charId + " node count");
            foreach (var row in rows)
            {
                var c = row.Split(' ');
                var n = N(t, c[0]);
                for (int d = 0; d < CommandTrie.DirCount; d++)
                    Assert.AreEqual(N(t, c[d + 1]).Id, n.Next[d], charId + " Next[" + c[0] + "][" + (CmdDir)d + "]");
            }
        }

        static void AssertCommonNodes(CommandTrie t)
        {
            AssertNode(t, "root", "root", -1, Ids.Art.Burst);
            AssertNode(t, "B", "root", -1, Ids.Art.Recoil);
            AssertNode(t, "F", "root", -1, Ids.Art.Fan);
            AssertNode(t, "L", "root", -1, Ids.Art.Wings);
            AssertNode(t, "R", "root", -1, Ids.Art.Wings);
            AssertNode(t, "BF", "F", G030, Ids.Art.Vent);
            AssertNode(t, "FB", "B", G020, Ids.Art.Octastar);
            AssertNode(t, "FF", "F", G030, Ids.Art.Spear);
            Assert.AreEqual(Ids.Art.ThunderSpear, N(t, "FF").JustArtId);
            AssertNode(t, "LL", "L", G025, Ids.Art.Afterimage);
            AssertNode(t, "RR", "R", G025, Ids.Art.Afterimage);
            AssertNode(t, "FBB", t.CharId == Ids.Char.C1 ? "BB" : "B", G018, null);
            AssertNode(t, "FBBF", "BF", G018, Ids.Art.Bloom);
        }

        [Test]
        public void S03_2_6__CommonNodeTable_AllCharacters()
        {
            for (int c = 1; c <= Ids.Char.Count; c++) AssertCommonNodes(Build(Ids.Char.Of(c)));
        }

        [Test]
        public void S03_0_3__ParserTimingsInUnits()
        {
            var t = Build(Ids.Char.C1).Timings;
            Assert.AreEqual(1075, t.JustWindowU);
            Assert.AreEqual(2765, t.GapFullBloomU);
            Assert.AreEqual(3072, t.GapShortU);
            Assert.AreEqual(3840, t.GapSameSideU);
            Assert.AreEqual(4608, t.GapDefaultU);
            Assert.AreEqual(6144, t.CooldownU);
            Assert.AreEqual(1536, t.KBufferWindowU);
            Assert.AreEqual(2, t.MaxDebt);
        }

        [Test]
        public void S03_TV_S01__Char1_13Nodes_FbbFailsToBb()
        {
            var t = Build(Ids.Char.C1);
            Assert.AreEqual(13, t.Count);
            Assert.AreEqual(N(t, "BB").Id, N(t, "FBB").Fail);
            AssertNode(t, "BB", "B", G020, Ids.Art.Kiln);
        }

        [Test]
        public void S03_TV_S02__Char2_14Nodes_BoltDashNodes()
        {
            var t = Build(Ids.Char.C2);
            Assert.AreEqual(14, t.Count);
            AssertNode(t, "LF", "F", G020, Ids.Art.BoltDash);
            AssertNode(t, "RF", "F", G020, Ids.Art.BoltDash);
            Assert.AreEqual(N(t, "B").Id, N(t, "FBB").Fail);
        }

        [Test]
        public void S03_TV_S03__Char3_CopyCircleNodes()
        {
            var t = Build(Ids.Char.C3);
            Assert.AreEqual(14, t.Count);
            AssertNode(t, "FL", "L", G020, Ids.Art.CopyCircle);
            AssertNode(t, "FR", "R", G020, Ids.Art.CopyCircle);
        }

        [Test]
        public void S03_TV_S04__Char4_IceScreenNodes_DefaultGap()
        {
            var t = Build(Ids.Char.C4);
            Assert.AreEqual(14, t.Count);
            AssertNode(t, "BL", "L", G030, Ids.Art.IceScreen);
            AssertNode(t, "BR", "R", G030, Ids.Art.IceScreen);
        }

        [Test]
        public void S03_TV_S05__Char5_ShadowHandNodes()
        {
            var t = Build(Ids.Char.C5);
            Assert.AreEqual(14, t.Count);
            AssertNode(t, "LR", "R", G020, Ids.Art.ShadowHand);
            AssertNode(t, "RL", "L", G020, Ids.Art.ShadowHand);
        }

        [Test]
        public void S03_TV_S06__Char1_NextTable()
        {
            AssertNext(Ids.Char.C1,
                "root F B L R", "B BF BB L R", "F FF FB L R", "L F B LL R", "R F B L RR",
                "BB BF BB L R", "BF FF FB L R", "FB BF FBB L R", "FF FF FB L R", "LL F B LL R", "RR F B L RR",
                "FBB FBBF BB L R", "FBBF FF FB L R");
        }

        [Test]
        public void S03_TV_S07__Char2_NextTable()
        {
            AssertNext(Ids.Char.C2,
                "root F B L R", "B BF B L R", "F FF FB L R", "L LF B LL R", "R RF B L RR",
                "BF FF FB L R", "FB BF FBB L R", "FF FF FB L R", "LF FF FB L R", "LL LF B LL R",
                "RF FF FB L R", "RR RF B L RR", "FBB FBBF B L R", "FBBF FF FB L R");
        }

        [Test]
        public void S03_TV_S08__Char3_NextTable()
        {
            AssertNext(Ids.Char.C3,
                "root F B L R", "B BF B L R", "F FF FB FL FR", "L F B LL R", "R F B L RR",
                "BF FF FB FL FR", "FB BF FBB L R", "FF FF FB FL FR", "FL F B LL R", "FR F B L RR",
                "LL F B LL R", "RR F B L RR", "FBB FBBF B L R", "FBBF FF FB FL FR");
        }

        [Test]
        public void S03_TV_S09__Char4_NextTable()
        {
            AssertNext(Ids.Char.C4,
                "root F B L R", "B BF B BL BR", "F FF FB L R", "L F B LL R", "R F B L RR",
                "BF FF FB L R", "BL F B LL R", "BR F B L RR", "FB BF FBB BL BR", "FF FF FB L R",
                "LL F B LL R", "RR F B L RR", "FBB FBBF B BL BR", "FBBF FF FB L R");
        }

        [Test]
        public void S03_TV_S10__Char5_NextTable()
        {
            AssertNext(Ids.Char.C5,
                "root F B L R", "B BF B L R", "F FF FB L R", "L F B LL LR", "R F B RL RR",
                "BF FF FB L R", "FB BF FBB L R", "FF FF FB L R", "LL F B LL LR", "LR F B RL RR",
                "RL F B LL LR", "RR F B RL RR", "FBB FBBF B L R", "FBBF FF FB L R");
        }

        [Test]
        public void S03_TV_S11__NoOtherCharactersUniqueNodes()
        {
            var uniques = new[] { Ids.Art.Kiln, Ids.Art.BoltDash, Ids.Art.CopyCircle, Ids.Art.IceScreen, Ids.Art.ShadowHand };
            for (int c = 1; c <= Ids.Char.Count; c++)
            {
                var t = Build(Ids.Char.Of(c));
                string own = TestContent.Db.Character(Ids.Char.Of(c)).UniqueArt;
                Assert.AreEqual(uniques[c - 1], own);
                foreach (var n in t.Nodes)
                    foreach (var u in uniques)
                        if (u != own) Assert.AreNotEqual(u, n.KArtId, t.CharId + " has " + u);
            }
        }

        [Test]
        public void S03_TV_S12__JustFullBloomAndFallbackFlags()
        {
            for (int c = 1; c <= Ids.Char.Count; c++)
            {
                var t = Build(Ids.Char.Of(c));
                int ff = N(t, "FF").Id, fbbf = N(t, "FBBF").Id;
                foreach (var n in t.Nodes)
                {
                    Assert.AreEqual(n.Id == ff, n.JustArt != null, "JustArt at node " + n.Id);
                    Assert.AreEqual(n.Id == fbbf, n.IsFullBloom, "IsFullBloom at node " + n.Id);
                    Assert.AreEqual(n.KArt != null && n.KArt.IsUnique, n.UniqueFallback, "UniqueFallback at node " + n.Id);
                }
            }
        }

        [Test]
        public void S03_TV_S13__EveryNextIsDepthOneOrMore()
        {
            for (int c = 1; c <= Ids.Char.Count; c++)
            {
                var t = Build(Ids.Char.Of(c));
                foreach (var n in t.Nodes)
                    for (int d = 0; d < CommandTrie.DirCount; d++)
                        Assert.GreaterOrEqual(t.Node(n.Next[d]).Depth, 1);
            }
        }

        [Test]
        public void S03_5_3__NodeIdsAreBfsOrder_MaxDepth4()
        {
            var t = Build(Ids.Char.C1);
            Assert.AreEqual(0, N(t, "root").Id);
            Assert.AreEqual(1, N(t, "F").Id);
            Assert.AreEqual(2, N(t, "B").Id);
            Assert.AreEqual(3, N(t, "L").Id);
            Assert.AreEqual(4, N(t, "R").Id);
            int prevDepth = 0, maxDepth = 0;
            foreach (var n in t.Nodes)
            {
                Assert.GreaterOrEqual(n.Depth, prevDepth);
                prevDepth = n.Depth;
                if (n.Depth > maxDepth) maxDepth = n.Depth;
            }
            Assert.AreEqual(4, maxDepth);
            Assert.AreEqual(N(t, "FBB").Id, N(t, "FBBF").Parent);
        }
    }
}

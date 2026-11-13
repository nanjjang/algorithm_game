using System.Collections.Generic;
using Wizard.Content;
using Wizard.Sim.Arts;
using Xunit;

namespace Wizard.Sim.Tests.Arts.Parser
{
    // spec 03 18.3 S군(트라이 구조). 기대값은 5.5절(실패 링크·InEdgeGap·K 기술)과 5.6절(DFA 표) 그대로.
    // 노드는 루트에서의 경로 글자로 가리킨다(빈 글자 = 루트). gap −1 = "—"(깊이 0~1, 간격 없음)
    public sealed class TrieStructureVectorTests
    {
        private const long None = -1;

        // 5.5 공통 노드(다섯 캐릭터 모두). FBB 의 실패 링크는 캐릭터마다 달라 따로 본다
        private static readonly (string node, string fail, long gap, ArtId k)[] Common =
        {
            ("", "", None, ArtId.Burst),
            ("B", "", None, ArtId.Recoil),
            ("F", "", None, ArtId.Fan),
            ("L", "", None, ArtId.Wings),
            ("R", "", None, ArtId.Wings),
            ("BF", "F", 4608, ArtId.Vent),
            ("FB", "B", 3072, ArtId.Octastar),
            ("FF", "F", 4608, ArtId.Spear),
            ("LL", "L", 3840, ArtId.Afterimage),
            ("RR", "R", 3840, ArtId.Afterimage),
            ("FBB", "?", 2765, ArtId.None),
            ("FBBF", "BF", 2765, ArtId.Bloom),
        };

        private static void CheckNode(CommandTrie t, string node, string fail, long gap, ArtId k)
        {
            int id = t.Find(ParserRig.Path(node));
            Assert.True(id >= 0, "node " + node + " missing");
            if (fail != "?")
            {
                Assert.Equal(fail, ParserRig.Letters(t.Path(t.Fail(id))));
            }
            if (gap == None)
            {
                Assert.False(t.TryGetInEdgeGap(id, out _), "node " + node + " must have no gap");
            }
            else
            {
                Assert.True(t.TryGetInEdgeGap(id, out long g), "node " + node + " needs a gap");
                Assert.Equal(gap, g);
            }
            Assert.Equal(k, t.KArt(id));
        }

        private static void CheckCharacter(CharacterId c, int nodeCount, string fbbFail,
                                           params (string node, string fail, long gap, ArtId k)[] unique)
        {
            CommandTrie t = SpecArts.Trie(c);
            Assert.Equal(nodeCount, t.NodeCount);
            foreach (var row in Common)
            {
                CheckNode(t, row.node, row.fail, row.gap, row.k);
            }
            CheckNode(t, "FBB", fbbFail, 2765, ArtId.None);
            foreach (var row in unique)
            {
                CheckNode(t, row.node, row.fail, row.gap, row.k);
            }
            Assert.Equal(0, t.Depth(CommandTrie.Root));
            Assert.Equal(CommandTrie.Root, t.Fail(CommandTrie.Root));     // 루트의 실패 링크는 자기 자신
            Assert.Equal(4, t.MaxDepth);
        }

        [Fact]
        public void S03_S01__Char1_ThirteenNodes_FbbFailsToBb() =>
            CheckCharacter(CharacterId.Char1, 13, "BB", ("BB", "B", 3072, ArtId.Kiln));

        [Fact]
        public void S03_S02__Char2_FourteenNodes_LfRf() =>
            CheckCharacter(CharacterId.Char2, 14, "B", ("LF", "F", 3072, ArtId.BoltDash), ("RF", "F", 3072, ArtId.BoltDash));

        [Fact]
        public void S03_S03__Char3_FourteenNodes_FlFr() =>
            CheckCharacter(CharacterId.Char3, 14, "B", ("FL", "L", 3072, ArtId.CopyCircle), ("FR", "R", 3072, ArtId.CopyCircle));

        [Fact]
        public void S03_S04__Char4_FourteenNodes_BlBr() =>
            CheckCharacter(CharacterId.Char4, 14, "B", ("BL", "L", 4608, ArtId.IceScreen), ("BR", "R", 4608, ArtId.IceScreen));

        [Fact]
        public void S03_S05__Char5_FourteenNodes_LrRl() =>
            CheckCharacter(CharacterId.Char5, 14, "B", ("LR", "R", 3072, ArtId.ShadowHand), ("RL", "L", 3072, ArtId.ShadowHand));

        // 5.6 표와 칸마다 같고, 표가 트라이의 노드를 빠짐없이 덮는다
        private static void CheckNext(CharacterId c, string[][] table)
        {
            CommandTrie t = SpecArts.Trie(c);
            Assert.Equal(t.NodeCount, table.Length);
            var seen = new HashSet<int>();
            foreach (string[] row in table)
            {
                int id = t.Find(ParserRig.Path(row[0]));
                Assert.True(id >= 0, "node " + row[0] + " missing");
                Assert.True(seen.Add(id), "node " + row[0] + " twice in the table");
                for (int d = 0; d < CommandTrie.DirCount; d++)
                {
                    int next = t.Next(id, (CmdDir)d);
                    Assert.Equal(row[d + 1], ParserRig.Letters(t.Path(next)));
                }
            }
        }

        [Fact]
        public void S03_S06__Char1_NextTable() => CheckNext(CharacterId.Char1, SpecNextTables.Char1);

        [Fact]
        public void S03_S07__Char2_NextTable() => CheckNext(CharacterId.Char2, SpecNextTables.Char2);

        [Fact]
        public void S03_S08__Char3_NextTable() => CheckNext(CharacterId.Char3, SpecNextTables.Char3);

        [Fact]
        public void S03_S09__Char4_NextTable() => CheckNext(CharacterId.Char4, SpecNextTables.Char4);

        [Fact]
        public void S03_S10__Char5_NextTable() => CheckNext(CharacterId.Char5, SpecNextTables.Char5);

        private static readonly CharacterId[] Five =
        {
            CharacterId.Char1, CharacterId.Char2, CharacterId.Char3, CharacterId.Char4, CharacterId.Char5,
        };

        private static string[] UniqueNodes(CharacterId c)
        {
            switch (c)
            {
                case CharacterId.Char1: return new[] { "BB" };
                case CharacterId.Char2: return new[] { "LF", "RF" };
                case CharacterId.Char3: return new[] { "FL", "FR" };
                case CharacterId.Char4: return new[] { "BL", "BR" };
                default: return new[] { "LR", "RL" };
            }
        }

        // 다른 캐릭터의 고유 노드가 없다. 예: char_1 에 LR 이 없어 L, R, K 는 양익 사격(A081)
        [Fact]
        public void S03_S11__NoOtherCharactersUniqueNodes()
        {
            foreach (CharacterId c in Five)
            {
                CommandTrie t = SpecArts.Trie(c);
                foreach (CharacterId other in Five)
                {
                    foreach (string node in UniqueNodes(other))
                    {
                        int id = t.Find(ParserRig.Path(node));
                        if (other == c)
                        {
                            Assert.True(id >= 0, c + " must have " + node);
                            Assert.Equal(SpecArts.UniqueOf(c), t.KArt(id));
                        }
                        else
                        {
                            Assert.True(id < 0, c + " must not have " + other + "'s node " + node);
                        }
                    }
                }
                for (int n = 0; n < t.NodeCount; n++)
                {
                    ArtId k = t.KArt(n);
                    Assert.True(k == ArtId.None || System.Array.IndexOf(SpecArts.CommonTen, k) >= 0 || k == SpecArts.UniqueOf(c));
                }
            }
            ArtDecision v = ParserRig.RunA(CharacterId.Char1, Unlocks.All, 5, 0, new[] { In.L(0), In.R(2304), In.K(2304) },
                                          Want.Fire(ArtId.Wings), 4, 0);
            Assert.Equal(ArtId.Wings, v.SelectedArt);
        }

        // FF 노드만 JustArt = ThunderSpear, FBBF 만 IsFullBloom, 고유 노드만 UniqueFallback
        [Fact]
        public void S03_S12__JustBloomAndFallbackFlagsOnlyWhereSpecSays()
        {
            foreach (CharacterId c in Five)
            {
                CommandTrie t = SpecArts.Trie(c);
                var unique = new HashSet<string>(UniqueNodes(c));
                for (int n = 0; n < t.NodeCount; n++)
                {
                    string path = ParserRig.Letters(t.Path(n));
                    Assert.Equal(path == "FF" ? ArtId.ThunderSpear : ArtId.None, t.JustArt(n));
                    Assert.Equal(path == "FBBF", t.IsFullBloom(n));
                    Assert.Equal(unique.Contains(path), t.UniqueFallback(n));
                }
            }
        }

        // Next[n][d] 는 언제나 깊이 1 이상(루트로 떨어지지 않는다)
        [Fact]
        public void S03_S13__NextNeverFallsToRoot()
        {
            foreach (CharacterId c in Five)
            {
                CommandTrie t = SpecArts.Trie(c);
                for (int n = 0; n < t.NodeCount; n++)
                {
                    for (int d = 0; d < CommandTrie.DirCount; d++)
                    {
                        Assert.True(t.Depth(t.Next(n, (CmdDir)d)) >= 1);
                    }
                }
            }
        }

        // 노드 id 는 BFS 순서(0 = 루트, 자식 F, B, L, R 순, spec 03 5.3 [임시]): 깊이가 줄지 않고 부모가 먼저다
        [Fact]
        public void NodeIds_AreBreadthFirst()
        {
            foreach (CharacterId c in Five)
            {
                CommandTrie t = SpecArts.Trie(c);
                Assert.Equal(CommandTrie.NoNode, t.Parent(CommandTrie.Root));
                for (int n = 1; n < t.NodeCount; n++)
                {
                    Assert.True(t.Depth(n) >= t.Depth(n - 1));
                    Assert.True(t.Parent(n) < n);
                    Assert.Equal(t.Depth(t.Parent(n)) + 1, t.Depth(n));
                }
                Assert.Equal("F", ParserRig.Letters(t.Path(1)));
                Assert.Equal("B", ParserRig.Letters(t.Path(2)));
                Assert.Equal("L", ParserRig.Letters(t.Path(3)));
                Assert.Equal("R", ParserRig.Letters(t.Path(4)));
            }
        }

        // 같은 데이터로 두 번 만들면 같은 트라이(결정론)
        [Fact]
        public void Build_IsDeterministic()
        {
            foreach (CharacterId c in Five)
            {
                CommandTrie a = SpecArts.Trie(c), b = SpecArts.Trie(c);
                Assert.Equal(a.NodeCount, b.NodeCount);
                for (int n = 0; n < a.NodeCount; n++)
                {
                    Assert.Equal(ParserRig.Letters(a.Path(n)), ParserRig.Letters(b.Path(n)));
                    Assert.Equal(a.Fail(n), b.Fail(n));
                    for (int d = 0; d < CommandTrie.DirCount; d++)
                    {
                        Assert.Equal(a.Next(n, (CmdDir)d), b.Next(n, (CmdDir)d));
                    }
                }
            }
        }

        // 계약의 ContentDb 판(CommandTrie.Build(c, ContentDb))은 DataTrieTests 가 본다(M5-DAT-01, T-52 6항): 저장소 팩으로 만든 트라이가
        // 명세 값 트라이와 노드마다 같고, arts·tries 절을 뺀 팩이면 ContentException 이다. 저장소 팩에 절이 없던 때의 시험
        // (저장소 팩으로 부르면 ContentException)은 M5-DAT-01 이 절을 넣으면서 그쪽의 "절을 뺀 팩" 시험으로 옮겼다
    }
}

using System;
using System.Collections.Generic;
using Wizard.Content;
using Wizard.Core;
using Wizard.DataBuild.Core;
using Wizard.Sim.Arts;
using Wizard.Sim.Tests.World;
using Xunit;

namespace Wizard.Sim.Tests.Arts.Parser
{
    // 저장소 데이터(arts·tries·characters 절)로 만든 판정기 데이터와 트라이(M5-DAT-01, CONTRACTS 3.5 T-52 개정문).
    //   1) ParserSourceLoader.FromContent 가 명세 값으로 만든 ParserSource(SpecArts: spec 03 1.2·5.2·9.2·12.1)와 칸마다 같다.
    //   2) CommandTrie.Build(c, ContentDb)가 Build(c, SpecArts.Source())와 노드마다 같다(값 칸, 경로, 자식, 펼친 전이).
    //   3) spec 03 18.3 S군(트라이 구조)의 기대값(5.5·5.6 표)이 데이터로 만든 트라이에서도 맞다. S군 벡터의 정본 시험은
    //      TrieStructureVectorTests(명세 값 트라이)이고 여기서는 같은 기대값을 데이터 트라이에 다시 건다.
    //   4) 데이터의 맞물림(트라이의 비기 = 공용 + 자기 고유 = CharacterDef.UniqueArt, 상수가 같다)이 어긋나면 ContentException.
    public sealed class DataTrieTests
    {
        private static readonly CharacterId[] Five =
        {
            CharacterId.Char1, CharacterId.Char2, CharacterId.Char3, CharacterId.Char4, CharacterId.Char5,
        };

        private static ContentDb Db => WorldData.RepoDb;

        [Fact]
        public void FromContent_TimingsAndMaxDebt_AreSpec03Values()
        {
            ParserTimings t = ParserSourceLoader.FromContent(Db).Timings;
            Assert.Equal(SpecArts.JustWindow, t.JustWindowUnits);          // 0.07초 = 1,075
            Assert.Equal(SpecArts.GapFullBloom, t.GapFullBloomUnits);      // 0.18초 = 2,765
            Assert.Equal(SpecArts.GapShort, t.GapShortUnits);              // 0.2초 = 3,072
            Assert.Equal(SpecArts.GapSameSide, t.GapSameSideUnits);        // 0.25초 = 3,840
            Assert.Equal(SpecArts.GapDefault, t.GapDefaultUnits);          // 0.3초 = 4,608
            Assert.Equal(SpecArts.ArtCooldown, t.ArtCooldownUnits);        // 0.4초 = 6,144
            Assert.Equal(SpecArts.KBufferWindow, t.KBufferWindowUnits);    // 0.1초 = 1,536
            Assert.Equal(SpecArts.MaxDebt, t.MaxDebt);                     // 2(spec 03 9.2)
        }

        // 비기 15종의 경로·종류·주인·섬·비용·외상이 명세 값 판정기 데이터와 같다(ArtId 오름차순)
        [Fact]
        public void FromContent_FifteenArts_MatchTheSpecSource()
        {
            ParserSource data = ParserSourceLoader.FromContent(Db);
            ParserSource spec = SpecArts.Source();
            Assert.Equal(15, data.Count);
            Assert.Equal(spec.Count, data.Count);
            for (int i = 0; i < data.Count; i++)
            {
                ParserArt d = data.At(i);
                Assert.Equal((ArtId)(i + 1), d.Id);
                ParserArt s = spec.Get(d.Id);
                Assert.Equal(s.IsUnique, d.IsUnique);
                Assert.Equal(s.Owner, d.Owner);
                Assert.Equal(s.IsJust, d.IsJust);
                Assert.Equal(s.TakesAll, d.TakesAll);
                Assert.Equal(s.Cost, d.Cost);
                Assert.Equal(s.MinCost, d.MinCost);
                Assert.Equal(s.CreditAllowed, d.CreditAllowed);
                Assert.Equal(s.PathCount, d.PathCount);
                for (int p = 0; p < s.PathCount; p++)
                {
                    Assert.Equal(ParserRig.Letters(s.Path(p)), ParserRig.Letters(d.Path(p)));
                }
            }
        }

        // Build(character, ContentDb) = Build(character, 명세 값 ParserSource), 노드마다
        [Fact]
        public void BuildFromContentDb_EqualsBuildFromSpecSource_NodeByNode()
        {
            foreach (CharacterId c in Five)
            {
                CommandTrie d = CommandTrie.Build(c, Db);
                CommandTrie s = SpecArts.Trie(c);
                Assert.Equal(c, d.Character);
                Assert.Equal(s.NodeCount, d.NodeCount);
                Assert.Equal(s.MaxDepth, d.MaxDepth);
                for (int n = 0; n < s.NodeCount; n++)
                {
                    ref readonly TrieNode a = ref d.Node(n);
                    ref readonly TrieNode b = ref s.Node(n);
                    Assert.Equal(b.Id, a.Id);
                    Assert.Equal(b.Depth, a.Depth);
                    Assert.Equal(b.Parent, a.Parent);
                    Assert.Equal(b.Fail, a.Fail);
                    Assert.Equal(b.KArt, a.KArt);
                    Assert.Equal(b.JustArt, a.JustArt);
                    Assert.Equal(b.InEdgeGapUnits, a.InEdgeGapUnits);
                    Assert.Equal(b.UniqueFallback, a.UniqueFallback);
                    Assert.Equal(b.IsFullBloom, a.IsFullBloom);
                    Assert.Equal(ParserRig.Letters(s.Path(n)), ParserRig.Letters(d.Path(n)));
                    for (int dir = 0; dir < CommandTrie.DirCount; dir++)
                    {
                        Assert.Equal(s.Child(n, (CmdDir)dir), d.Child(n, (CmdDir)dir));
                        Assert.Equal(s.Next(n, (CmdDir)dir), d.Next(n, (CmdDir)dir));
                    }
                }
            }
        }

        // ── spec 03 18.3 S군의 기대값을 데이터 트라이에(5.5 표: 실패 링크·InEdgeGap·K 기술, 5.6 표: Next)

        private const long NoGap = -1;

        private static readonly (string node, string fail, long gap, ArtId k)[] CommonNodes =
        {
            ("", "", NoGap, ArtId.Burst),
            ("B", "", NoGap, ArtId.Recoil),
            ("F", "", NoGap, ArtId.Fan),
            ("L", "", NoGap, ArtId.Wings),
            ("R", "", NoGap, ArtId.Wings),
            ("BF", "F", 4608, ArtId.Vent),
            ("FB", "B", 3072, ArtId.Octastar),
            ("FF", "F", 4608, ArtId.Spear),
            ("LL", "L", 3840, ArtId.Afterimage),
            ("RR", "R", 3840, ArtId.Afterimage),
            ("FBBF", "BF", 2765, ArtId.Bloom),
        };

        private static void CheckNode(CommandTrie t, string node, string fail, long gap, ArtId k)
        {
            int id = t.Find(ParserRig.Path(node));
            Assert.True(id >= 0, "node " + node + " missing");
            Assert.Equal(fail, ParserRig.Letters(t.Path(t.Fail(id))));
            Assert.Equal(gap, t.Node(id).InEdgeGapUnits);
            Assert.Equal(k, t.KArt(id));
        }

        private static void CheckSpecTable(CharacterId c, int nodeCount, string fbbFail, params (string node, string fail, long gap, ArtId k)[] unique)
        {
            CommandTrie t = CommandTrie.Build(c, Db);
            Assert.Equal(nodeCount, t.NodeCount);
            foreach (var row in CommonNodes)
            {
                CheckNode(t, row.node, row.fail, row.gap, row.k);
            }
            CheckNode(t, "FBB", fbbFail, 2765, ArtId.None);
            foreach (var row in unique)
            {
                CheckNode(t, row.node, row.fail, row.gap, row.k);
            }
            Assert.Equal(CommandTrie.Root, t.Fail(CommandTrie.Root));
            Assert.Equal(4, t.MaxDepth);
        }

        [Fact]
        public void Data_S01ToS05__FailLinksGapsAndKArts_Match5_5()
        {
            CheckSpecTable(CharacterId.Char1, 13, "BB", ("BB", "B", 3072, ArtId.Kiln));
            CheckSpecTable(CharacterId.Char2, 14, "B", ("LF", "F", 3072, ArtId.BoltDash), ("RF", "F", 3072, ArtId.BoltDash));
            CheckSpecTable(CharacterId.Char3, 14, "B", ("FL", "L", 3072, ArtId.CopyCircle), ("FR", "R", 3072, ArtId.CopyCircle));
            CheckSpecTable(CharacterId.Char4, 14, "B", ("BL", "L", 4608, ArtId.IceScreen), ("BR", "R", 4608, ArtId.IceScreen));
            CheckSpecTable(CharacterId.Char5, 14, "B", ("LR", "R", 3072, ArtId.ShadowHand), ("RL", "L", 3072, ArtId.ShadowHand));
        }

        private static void CheckNext(CharacterId c, string[][] table)
        {
            CommandTrie t = CommandTrie.Build(c, Db);
            Assert.Equal(t.NodeCount, table.Length);
            var seen = new HashSet<int>();
            foreach (string[] row in table)
            {
                int id = t.Find(ParserRig.Path(row[0]));
                Assert.True(id >= 0, "node " + row[0] + " missing");
                Assert.True(seen.Add(id), "node " + row[0] + " twice in the table");
                for (int d = 0; d < CommandTrie.DirCount; d++)
                {
                    Assert.Equal(row[d + 1], ParserRig.Letters(t.Path(t.Next(id, (CmdDir)d))));
                }
            }
        }

        [Fact]
        public void Data_S06ToS10__NextTables_Match5_6()
        {
            CheckNext(CharacterId.Char1, SpecNextTables.Char1);
            CheckNext(CharacterId.Char2, SpecNextTables.Char2);
            CheckNext(CharacterId.Char3, SpecNextTables.Char3);
            CheckNext(CharacterId.Char4, SpecNextTables.Char4);
            CheckNext(CharacterId.Char5, SpecNextTables.Char5);
        }

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

        // S11 다른 캐릭터의 고유 노드가 없다, S12 FF 만 섬 = 뇌광창·FBBF 만 만개·고유 노드만 부모 대체, S13 Next 는 늘 깊이 1 이상
        [Fact]
        public void Data_S11ToS13__NoForeignUniqueNodes_FlagsOnlyWhereSpecSays_NextNeverRoot()
        {
            foreach (CharacterId c in Five)
            {
                CommandTrie t = CommandTrie.Build(c, Db);
                foreach (CharacterId other in Five)
                {
                    foreach (string node in UniqueNodes(other))
                    {
                        int id = t.Find(ParserRig.Path(node));
                        if (other == c)
                        {
                            Assert.Equal(Db.Character(c).UniqueArt, t.KArt(id));
                        }
                        else
                        {
                            Assert.True(id < 0, c + " must not have " + other + "'s node " + node);
                        }
                    }
                }
                var unique = new HashSet<string>(UniqueNodes(c));
                for (int n = 0; n < t.NodeCount; n++)
                {
                    string path = ParserRig.Letters(t.Path(n));
                    Assert.Equal(path == "FF" ? ArtId.ThunderSpear : ArtId.None, t.JustArt(n));
                    Assert.Equal(path == "FBBF", t.IsFullBloom(n));
                    Assert.Equal(unique.Contains(path), t.UniqueFallback(n));
                    for (int d = 0; d < CommandTrie.DirCount; d++)
                    {
                        Assert.True(t.Depth(t.Next(n, (CmdDir)d)) >= 1);
                    }
                }
            }
        }

        // 데이터 트라이로 만든 판정기가 명세의 손 사례를 그대로 낸다(spec 04 15.3-24·25, spec 03 20.1 D-40의 발표 구도)
        [Fact]
        public void ParserOnDataTrie_FailLinkShowcase()
        {
            ArtDecision spear = Run(CharacterId.Char1, ParserRig.Path("FFF"));
            Assert.Equal(ArtId.Spear, spear.Art);                              // 앞, 앞, 앞, K = 꿰뚫는 창
            ArtDecision copy = Run(CharacterId.Char3, ParserRig.Path("FFL"));
            Assert.Equal(ArtId.CopyCircle, copy.Art);                          // 갈피: 앞, 앞, 옆, K = 베껴 쓰기(실패 링크)
        }

        private static ArtDecision Run(CharacterId c, CmdDir[] dirs)
        {
            var p = new CommandParser(CommandTrie.Build(c, Db));
            long t = ParserRig.T0;
            p.OnStanceChanged(true, t - SpecArts.GapDefault);
            foreach (CmdDir d in dirs)
            {
                t += 2304;                                                    // 0.15초(spec 04 15.3의 기본 간격)
                p.OnDirection(d, t);
            }
            t += 2304;
            var w = new TestWallet(Db.Character(c).CrystalCap, Db.Character(c).CrystalCap, 0, c == CharacterId.Char5);
            Assert.True(p.OnSkillPress(t, w, Unlocks.All.For(c), out ArtDecision v));
            Assert.True(v.IsFire);
            return v;
        }

        // ── 데이터의 맞물림이 어긋나면 ContentException(ParserSourceLoader)

        private static byte[] RepoSection(ContentSection s)
        {
            Assert.True(Db.Pack.TryGetSection((ushort)s, out ReadOnlyMemory<byte> bytes));
            return bytes.ToArray();
        }

        // 저장소의 characters·arts 절 + 손으로 만든 tries 절
        private static ContentDb WithTries(byte[] tries, byte[]? characters = null)
        {
            byte[] pack = PackWriter.Write(PackWriter.SchemaVersion, 1, new byte[PackWriter.CoreCompatLength], new[]
            {
                new PackSection((ushort)ContentSection.Characters, characters ?? RepoSection(ContentSection.Characters)),
                new PackSection((ushort)ContentSection.Arts, RepoSection(ContentSection.Arts)),
                new PackSection((ushort)ContentSection.Tries, tries),
            });
            return ContentDb.Build(ContentPack.Load(pack));
        }

        // tries 절 바이트(ContentDb.Tries.cs 의 꼴). arts(c) = 그 캐릭터의 비기, 오름차순으로 준다
        private static byte[] Tries(Func<CharacterId, ArtId[]> arts, long justWindow = SpecArts.JustWindow, CharacterId[]? who = null)
        {
            var w = new ByteWriter();
            w.I64(justWindow);
            w.I64(SpecArts.GapFullBloom);
            w.I64(SpecArts.GapShort);
            w.I64(SpecArts.GapSameSide);
            w.I64(SpecArts.GapDefault);
            w.I64(SpecArts.ArtCooldown);
            w.I64(SpecArts.KBufferWindow);
            w.I32(SpecArts.MaxDebt);
            CharacterId[] list = who ?? Five;
            w.U16((ushort)list.Length);
            foreach (CharacterId c in list)
            {
                ArtId[] a = arts(c);
                w.U8((byte)c);
                w.U8((byte)a.Length);
                foreach (ArtId x in a)
                {
                    w.U8((byte)x);
                }
            }
            return w.ToArray();
        }

        private static ArtId[] Own(CharacterId c)
        {
            var a = new List<ArtId>(SpecArts.CommonTen) { SpecArts.UniqueOf(c) };
            return a.ToArray();
        }

        [Fact]
        public void HandMadeTriesSection_SameAsRepo_Builds()
        {
            ContentDb db = WithTries(Tries(Own));
            Assert.Equal(RepoSection(ContentSection.Tries), db.Pack.TryGetSection((ushort)ContentSection.Tries, out ReadOnlyMemory<byte> s) ? s.ToArray() : null);
            Assert.Equal(15, ParserSourceLoader.FromContent(db).Count);
        }

        [Fact]
        public void TrieMissingOwnUnique_OrHoldingAnotherUnique_IsContentError()
        {
            // char.1 의 트라이에 불가마가 없다
            ArtId[] NoKiln(CharacterId c) => c == CharacterId.Char1 ? SpecArts.CommonTen : Own(c);
            Assert.Throws<ContentException>(() => ParserSourceLoader.FromContent(WithTries(Tries(NoKiln))));
            // char.1 의 트라이에 벼락 질주(char.2 의 고유)가 더 있다
            ArtId[] Extra(CharacterId c) => c == CharacterId.Char1
                ? new List<ArtId>(Own(c)) { ArtId.BoltDash }.ToArray()
                : Own(c);
            Assert.Throws<ContentException>(() => ParserSourceLoader.FromContent(WithTries(Tries(Extra))));
            // char.1 의 트라이에 공용 하나(부채 전개)가 빠졌다
            ArtId[] NoFan(CharacterId c) => c == CharacterId.Char1 ? Array.FindAll(Own(c), a => a != ArtId.Fan) : Own(c);
            Assert.Throws<ContentException>(() => ParserSourceLoader.FromContent(WithTries(Tries(NoFan))));
            // 어느 트라이에도 부채 전개가 없다: 베껴 쓰기의 부모 대체(art.fan)가 판정기 데이터에 없다
            ArtId[] NoFanAnywhere(CharacterId c) => Array.FindAll(Own(c), a => a != ArtId.Fan);
            Assert.Throws<ContentException>(() => ParserSourceLoader.FromContent(WithTries(Tries(NoFanAnywhere))));
        }

        [Fact]
        public void MissingCharacterRow_OrMissingSections_IsContentError()
        {
            var four = new[] { CharacterId.Char1, CharacterId.Char2, CharacterId.Char3, CharacterId.Char4 };
            Assert.Throws<ContentException>(() => ParserSourceLoader.FromContent(WithTries(Tries(Own, who: four))));
            Assert.Throws<ContentException>(() => CommandTrie.Build(CharacterId.Char1, WithTries(Tries(Own, who: four))));
            // 캐릭터 절에 그 캐릭터가 없으면(기준값만) 고유 비기를 맞춰 볼 수 없다
            byte[] baselineOnly = BaselineOnlyCharacters();
            Assert.Throws<ContentException>(() => ParserSourceLoader.FromContent(WithTries(Tries(Own), baselineOnly)));
        }

        // 팩에 tries 절이나 arts 절이 없으면 계약의 ContentDb 판(CommandTrie.Build(c, ContentDb))은 ContentException(CONTRACTS 3.5, T-52).
        // M1-SIM-08 의 TrieStructureVectorTests.BuildFromContentDb_WithoutTriesSection_IsContentException 은 저장소 팩에 절이 없던 때의
        // 시험이라 이 작업 뒤로는 절을 뺀 팩으로 여기서 본다
        [Fact]
        public void PackWithoutTriesOrArtsSection_IsContentError()
        {
            byte[] noTries = PackWriter.Write(PackWriter.SchemaVersion, 1, new byte[PackWriter.CoreCompatLength], new[]
            {
                new PackSection((ushort)ContentSection.Characters, RepoSection(ContentSection.Characters)),
                new PackSection((ushort)ContentSection.Arts, RepoSection(ContentSection.Arts)),
            });
            ContentDb withoutTries = ContentDb.Build(ContentPack.Load(noTries));
            Assert.Throws<ContentException>(() => CommandTrie.Build(CharacterId.Char1, withoutTries));
            Assert.Throws<ContentException>(() => ParserSourceLoader.FromContent(withoutTries));

            byte[] noArts = PackWriter.Write(PackWriter.SchemaVersion, 1, new byte[PackWriter.CoreCompatLength], new[]
            {
                new PackSection((ushort)ContentSection.Characters, RepoSection(ContentSection.Characters)),
                new PackSection((ushort)ContentSection.Tries, RepoSection(ContentSection.Tries)),
            });
            Assert.Throws<ContentException>(() => CommandTrie.Build(CharacterId.Char1, ContentDb.Build(ContentPack.Load(noArts))));
        }

        // 캐릭터 절의 첫 항목(기준값)만 남긴다. 항목 길이 = u8 5 + i32 3 + i64 3 + i32 12 = 5 + 12 + 24 + 48 = 89(ContentDb.Characters.cs)
        private static byte[] BaselineOnlyCharacters()
        {
            byte[] all = RepoSection(ContentSection.Characters);
            const int entry = 5 + 3 * 4 + 3 * 8 + 12 * 4;
            Assert.Equal(2 + entry * 6, all.Length);
            var one = new byte[2 + entry];
            one[0] = 1;
            Array.Copy(all, 2, one, 2, entry);
            return one;
        }

        [Fact]
        public void CharacterUniqueArtDisagreesWithTheArtsSection_IsContentError()
        {
            // char.1 의 unique_art 칸(u16 개수 뒤 둘째 항목의 다섯째 바이트)을 벼락 질주로
            byte[] chars = RepoSection(ContentSection.Characters);
            const int entry = 5 + 3 * 4 + 3 * 8 + 12 * 4;
            int at = 2 + entry + 4;
            Assert.Equal((byte)ArtId.Kiln, chars[at]);
            chars[at] = (byte)ArtId.BoltDash;
            Assert.Throws<ContentException>(() => ParserSourceLoader.FromContent(WithTries(Tries(Own), chars)));
        }

        [Fact]
        public void TriesFromRepo_SameContentDb_BuildsTheSameTrieTwice()
        {
            foreach (CharacterId c in Five)
            {
                CommandTrie a = CommandTrie.Build(c, Db), b = CommandTrie.Build(c, Db);
                Assert.Equal(a.NodeCount, b.NodeCount);
                for (int n = 0; n < a.NodeCount; n++)
                {
                    Assert.Equal(a.Fail(n), b.Fail(n));
                    for (int d = 0; d < CommandTrie.DirCount; d++)
                    {
                        Assert.Equal(a.Next(n, (CmdDir)d), b.Next(n, (CmdDir)d));
                    }
                }
            }
        }

        // 판정기 상수는 tries 절에서 온다(코드에 수가 없다). 섬 창을 1,229(0.08초)로 바꾼 절이면 그 값을 쓴다.
        // 절에 상수가 한 벌뿐이라 캐릭터마다 다른 상수는 절의 꼴로 생길 수 없다(SameConstants 는 그 불변을 지킨다)
        [Fact]
        public void ParserConstants_ComeFromTheTriesSection()
        {
            ParserSource s = ParserSourceLoader.FromContent(WithTries(Tries(Own, justWindow: 1229)));
            Assert.Equal(1229, s.Timings.JustWindowUnits);
        }
    }
}

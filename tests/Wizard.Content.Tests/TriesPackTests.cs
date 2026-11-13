using System;
using System.Collections.Generic;
using Wizard.Content.Tests.Pack;
using Wizard.DataBuild.Core;
using Wizard.TestKit;
using Xunit;

namespace Wizard.Content.Tests
{
    // data/sim/tries.json → 팩 절 14 → ContentDb.Tries 의 왕복(M5-DAT-01).
    // 기대값의 출처: spec 03 1.2(판정기 상수의 서브틱), 9.2(빚 상한 2), 5.1(공용 10 + 고유 1), 2.2·14절(캐릭터의 고유 비기),
    // spec 04 15.1-6(초 → 서브틱 환산 벡터).
    public sealed class TriesPackTests
    {
        private static readonly Lazy<byte[]> RepoPack = new Lazy<byte[]>(() => BuildPack(TestRepo.Root));

        private static byte[] BuildPack(string root)
        {
            var d = new Diagnostics();
            Pipeline.Output o = Pipeline.Run(root, ModuleRegistry.Discover(), PackOptions.Default(PackOptions.DefaultDataVersion), d);
            Assert.False(d.HasErrors, string.Join("\n", d.Sorted()));
            return o.Pack!;
        }

        private static List<string> Problems(string root)
        {
            var d = new Diagnostics();
            Pipeline.Run(root, ModuleRegistry.Discover(), PackOptions.Default(PackOptions.DefaultDataVersion), d);
            return new List<string>(d.Sorted());
        }

        private static ContentDb RepoDb() => ContentDb.Build(ContentPack.Load(RepoPack.Value));

        private static readonly CharacterId[] Five =
        {
            CharacterId.Char1, CharacterId.Char2, CharacterId.Char3, CharacterId.Char4, CharacterId.Char5,
        };

        // spec 03 5.1: 공용 10종(①~⑩)
        private static readonly ArtId[] CommonTen =
        {
            ArtId.Burst, ArtId.Recoil, ArtId.Fan, ArtId.Wings, ArtId.Afterimage,
            ArtId.Spear, ArtId.Vent, ArtId.Octastar, ArtId.ThunderSpear, ArtId.Bloom,
        };

        // spec 03 2.1·14절: 캐릭터의 고유 비기
        private static ArtId UniqueOf(CharacterId c)
        {
            switch (c)
            {
                case CharacterId.Char1: return ArtId.Kiln;
                case CharacterId.Char2: return ArtId.BoltDash;
                case CharacterId.Char3: return ArtId.CopyCircle;
                case CharacterId.Char4: return ArtId.IceScreen;
                default: return ArtId.ShadowHand;
            }
        }

        // spec 04 15.1-6: 0.07초 → 1,075. 0.18 → 2,765, 0.2 → 3,072, 0.25 → 3,840, 0.3 → 4,608(spec 03 1.2 표)
        [Fact]
        public void S04_N6__SecondsToSubticks_1075_2765_3072_3840_4608()
        {
            ContentDb db = RepoDb();
            foreach (CharacterId c in Five)
            {
                TrieDef t = db.Tries(c);
                Assert.Equal(1_075, t.JustWindowUnits);          // 0.07초 = 1,075.2
                Assert.Equal(2_765, t.GapFullBloomUnits);        // 0.18초 = 2,764.8
                Assert.Equal(3_072, t.GapShortUnits);            // 0.2초
                Assert.Equal(3_840, t.GapSameSideUnits);         // 0.25초
                Assert.Equal(4_608, t.GapDefaultUnits);          // 0.3초
            }
        }

        // spec 03 1.2의 나머지 상수(재사용 대기 0.4초 = 6,144, K 보관 창 0.1초 = 1,536)와 9.2의 빚 상한 2
        [Fact]
        public void CooldownBufferAndMaxDebt_MatchSpec03()
        {
            ContentDb db = RepoDb();
            foreach (CharacterId c in Five)
            {
                TrieDef t = db.Tries(c);
                Assert.Equal(6_144, t.ArtCooldownUnits);
                Assert.Equal(1_536, t.KBufferWindowUnits);
                Assert.Equal(2, t.MaxDebt);
            }
        }

        // spec 03 5.1: 각 캐릭터의 트라이 = 공용 10 + 그 캐릭터의 고유 1 = 11종(ArtId 오름차순). 다른 캐릭터의 고유는 없다
        [Fact]
        public void EachCharacter_CommonTenPlusOwnUnique()
        {
            ContentDb db = RepoDb();
            foreach (CharacterId c in Five)
            {
                TrieDef t = db.Tries(c);
                Assert.Equal(c, t.Character);
                var expected = new List<ArtId>(CommonTen) { UniqueOf(c) };
                Assert.Equal(expected.ToArray(), t.Arts);
                Assert.Equal(11, t.Arts.Length);
            }
        }

        [Fact]
        public void MissingCharacters_AndPackWithoutTheSection_Throw()
        {
            Assert.Throws<ContentException>(() => RepoDb().Tries(CharacterId.Baseline));   // 기준값 캐릭터의 줄은 없다
            Assert.Throws<ContentException>(() => RepoDb().Tries((CharacterId)6));
            byte[] empty = PackWriter.Write(PackWriter.SchemaVersion, 1, new byte[PackWriter.CoreCompatLength], new PackSection[0]);
            Assert.Throws<ContentException>(() => ContentDb.Build(ContentPack.Load(empty)).Tries(CharacterId.Char1));
        }

        [Fact]
        public void SameData_SameSectionBytes()
        {
            Assert.Equal(Section(RepoPack.Value), Section(BuildPack(TestRepo.Root)));
        }

        // 원본의 캐릭터 순서와 비기 순서를 바꿔도 절 바이트가 같다
        [Fact]
        public void ReversedSourceOrder_GivesTheSameBytes()
        {
            var rows = new List<string>();
            for (int i = Five.Length - 1; i >= 0; i--)
            {
                var arts = new List<ArtId>(CommonTen) { UniqueOf(Five[i]) };
                arts.Reverse();
                rows.Add(Row(Five[i], arts));
            }
            using TempDataRepo repo = Repo(ParserJson, rows);
            Assert.Equal(Section(RepoPack.Value), Section(BuildPack(repo.Root)));
        }

        [Fact]
        public void DuplicatesAndUnknownIds_AreProblems()
        {
            var twice = new List<string> { Row(CharacterId.Char1, CommonTen), Row(CharacterId.Char1, CommonTen) };
            using (TempDataRepo repo = Repo(ParserJson, twice))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/tries.json:/characters/1/character") && p.Contains("두 번"));
            }
            var dupArt = new List<string> { Row(CharacterId.Char1, new[] { ArtId.Burst, ArtId.Burst }) };
            using (TempDataRepo repo = Repo(ParserJson, dupArt))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("/characters/0/arts/1") && p.Contains("두 번"));
            }
            var unknown = new List<string> { "{ \"character\": \"char.9\", \"arts\": [\"art.burst\", \"art.nope\"] }" };
            using (TempDataRepo repo = Repo(ParserJson, unknown))
            {
                List<string> p = Problems(repo.Root);
                Assert.Contains(p, x => x.Contains("/characters/0/character") && x.Contains("char.9"));
                Assert.Contains(p, x => x.Contains("/characters/0/arts/1") && x.Contains("art.nope"));
            }
        }

        [Fact]
        public void BufferLongerThanCooldown_AndSchemaViolations_AreProblems()
        {
            var rows = new List<string> { Row(CharacterId.Char1, CommonTen) };
            using (TempDataRepo repo = Repo(ParserJson.Replace("\"k_buffer_window_seconds\": 0.1", "\"k_buffer_window_seconds\": 0.5"), rows))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("/parser/k_buffer_window_seconds"));
            }
            using (TempDataRepo repo = Repo(ParserJson.Replace("\"max_debt\": 2", "\"max_debt\": 2, \"dir_history\": 8"), rows))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/tries.json") && p.Contains("스키마"));
            }
            using (TempDataRepo repo = Repo(ParserJson.Replace("\"gap_default_seconds\": 0.3,", ""), rows))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/tries.json") && p.Contains("스키마"));
            }
        }

        [Fact]
        public void BrokenSectionBytes_ThrowContentException()
        {
            byte[] good = Section(RepoPack.Value);
            const int head = 7 * 8 + 4;                       // 상수 일곱(i64) + 빚 상한(i32)
            Assert.Throws<ContentException>(() => Load(good.AsSpan(0, good.Length - 1).ToArray()));   // 모자람
            Assert.Throws<ContentException>(() => Load(new byte[3]));
            var longer = new byte[good.Length + 1];
            good.CopyTo(longer, 0);
            Assert.Throws<ContentException>(() => Load(longer));                                      // 남음
            byte[] negative = (byte[])good.Clone();
            negative[7] = 0x80;                                                                        // 섬 창의 부호 비트
            Assert.Throws<ContentException>(() => Load(negative));
            byte[] badChar = (byte[])good.Clone();
            badChar[head + 2] = 99;                                                                    // 첫 항목의 character
            Assert.Throws<ContentException>(() => Load(badChar));
            byte[] artOrder = (byte[])good.Clone();
            artOrder[head + 2 + 2] = (byte)ArtId.Recoil;                                              // 첫 비기를 둘째와 같게(오름차순 위반)
            Assert.Throws<ContentException>(() => Load(artOrder));
            byte[] charOrder = (byte[])good.Clone();
            charOrder[head + 2 + 13] = (byte)CharacterId.Char1;                                       // 둘째 항목(1 + 1 + 11 바이트 뒤)을 char.1 로
            Assert.Throws<ContentException>(() => Load(charOrder));
            Assert.Equal(CharacterId.Char2, (CharacterId)good[head + 2 + 13]);
            Load(good);
        }

        private static ContentDb Load(byte[] section) =>
            ContentDb.Build(ContentPack.Load(PackWriter.Write(PackWriter.SchemaVersion, 1, new byte[PackWriter.CoreCompatLength],
                new[] { new PackSection((ushort)ContentSection.Tries, section) })));

        private static byte[] Section(byte[] pack)
        {
            Assert.True(ContentPack.Load(pack).TryGetSection((ushort)ContentSection.Tries, out ReadOnlyMemory<byte> s));
            return s.ToArray();
        }

        // spec 03 1.2·9.2
        private const string ParserJson =
            "{ \"just_window_seconds\": 0.07, \"gap_full_bloom_seconds\": 0.18, \"gap_short_seconds\": 0.2, \"gap_same_side_seconds\": 0.25,\n" +
            "  \"gap_default_seconds\": 0.3, \"art_cooldown_seconds\": 0.4, \"k_buffer_window_seconds\": 0.1, \"max_debt\": 2 }";

        private static string Row(CharacterId c, IEnumerable<ArtId> arts)
        {
            var ids = new List<string>();
            foreach (ArtId a in arts)
            {
                ids.Add("\"" + ContentIds.ToId(a) + "\"");
            }
            return "{ \"character\": \"" + ContentIds.ToId(c) + "\", \"arts\": [" + string.Join(", ", ids) + "] }";
        }

        private static TempDataRepo Repo(string parser, List<string> rows)
        {
            var repo = new TempDataRepo();
            repo.Write("data/schema/tries.schema.json", System.IO.File.ReadAllText(TestRepo.Path("data/schema/tries.schema.json")));
            repo.Write("data/sim/tries.json", "{ \"parser\": " + parser + ", \"characters\": [" + string.Join(",\n", rows) + "] }\n");
            return repo;
        }
    }
}

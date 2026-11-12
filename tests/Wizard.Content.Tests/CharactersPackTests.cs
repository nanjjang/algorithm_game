using System;
using System.Collections.Generic;
using Wizard.Content.Tests.Pack;
using Wizard.Core;
using Wizard.DataBuild.Core;
using Wizard.TestKit;
using Xunit;

namespace Wizard.Content.Tests
{
    // data/sim/characters.json → 팩 절 1 → ContentDb.Character 의 왕복(M1-CONT-03).
    // 기대값은 명세의 표에서 온다: spec 01 5.1(기준값 열), 2.2(Raw 상수), 2.4(HU), 2.5(틱), 5.6(몸 반지름·스침 가속), spec 04 2.2(char.3).
    public sealed class CharactersPackTests
    {
        // 칸 → Raw(spec 01 2.2 표의 값)
        private const long Raw015 = 644_245_094;
        private const long Raw030 = 1_288_490_189;
        private const long Raw060 = 2_576_980_378;

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

        [Fact]
        public void Baseline_RoundTrip_EveryField_IsTheSpecInteger()
        {
            CharacterDef c = RepoDb().Character(CharacterId.Baseline);
            Assert.Equal(CharacterId.Baseline, c.Id);
            Assert.Equal(Element.None, c.Element);               // spec 01 5.1 "—"
            Assert.Equal(PassiveId.None, c.Passive);
            Assert.Equal(WandTraitId.None, c.WandTrait);
            Assert.Equal(ArtId.None, c.UniqueArt);
            Assert.Equal(6, c.MaxHpCells);
            Assert.Equal(500, c.WalkSpeedCenti);                 // 5.0 칸/초
            Assert.Equal(425, c.StanceSpeedCenti);               // 4.25 칸/초
            Assert.Equal(Raw015, c.HitRadius.Raw);               // 0.15
            Assert.Equal(Raw030, c.BodyRadius.Raw);              // 0.30 (spec 01 5.6)
            Assert.Equal(Raw060, c.GrazeRadius.Raw);             // 0.6
            Assert.Equal(60, c.InvulnTicks);                     // 1초
            Assert.Equal(5, c.CrystalCap);
            Assert.Equal(15, c.ComboPerCrystal);
            Assert.Equal(1, c.CrystalLossOnHit);
            Assert.Equal(4, c.BaseLinkSlots);
            Assert.Equal(2, c.MaxMutations);
            Assert.Equal(90, c.CoolHuPerTick);                   // 초당 45 → 틱당 90 HU (spec 01 2.4)
            Assert.Equal(12, c.CoolDelayTicks);                  // 0.2초
            Assert.Equal(10_200, c.CritStartHu);                 // 85 (spec 01 2.4)
            Assert.Equal(180, c.LockTicks);                      // 3.0초
            Assert.Equal(90, c.GrazeBuffTicks);                  // 1.5초
            Assert.Equal(1200, c.GrazeBuffPermille);             // +20% (spec 01 5.6)
            Assert.NotNull(c.Duel);
        }

        [Fact]
        public void RepoData_HasOnlyTheBaseline_OtherIdsThrow()
        {
            ContentDb db = RepoDb();
            Assert.Throws<ContentException>(() => db.Character(CharacterId.Char1));
            Assert.Throws<ContentException>(() => db.Character(CharacterId.Char5));
            Assert.Throws<ContentException>(() => db.Character((CharacterId)200));
        }

        [Fact]
        public void PackWithoutTheSection_Throws()
        {
            byte[] empty = PackWriter.Write(PackWriter.SchemaVersion, 1, new byte[PackWriter.CoreCompatLength], new PackSection[0]);
            ContentDb db = ContentDb.Build(ContentPack.Load(empty));
            Assert.Throws<ContentException>(() => db.Character(CharacterId.Baseline));
        }

        [Fact]
        public void SameData_SameSectionBytes()
        {
            Assert.Equal(Section(RepoPack.Value), Section(BuildPack(TestRepo.Root)));
        }

        // characters[] 의 줄(M5-DAT-01 이 채운다)도 같은 절에 같은 꼴로 들어간다. 값은 spec 04 2.2 의 char.3 열과 spec 01 5.1 갈피 열
        [Fact]
        public void CharacterRow_RoundTrip_UsesContentIds()
        {
            using TempDataRepo repo = Repo(BaselineJson, Char3Json);
            ContentDb db = ContentDb.Build(ContentPack.Load(BuildPack(repo.Root)));
            CharacterDef c = db.Character(CharacterId.Char3);
            Assert.Equal(CharacterId.Char3, c.Id);
            Assert.Equal(Element.Arcane, c.Element);
            Assert.Equal(PassiveId.Underline, c.Passive);
            Assert.Equal(WandTraitId.DepthDecay, c.WandTrait);
            Assert.Equal(ArtId.CopyCircle, c.UniqueArt);
            Assert.Equal(6, c.MaxHpCells);
            Assert.Equal(500, c.WalkSpeedCenti);
            Assert.Equal(425, c.StanceSpeedCenti);
            Assert.Equal(Raw015, c.HitRadius.Raw);
            Assert.Equal(Raw030, c.BodyRadius.Raw);
            Assert.Equal(Raw060, c.GrazeRadius.Raw);
            Assert.Equal(60, c.InvulnTicks);
            Assert.Equal(4, c.CrystalCap);
            Assert.Equal(15, c.ComboPerCrystal);
            Assert.Equal(1, c.CrystalLossOnHit);
            Assert.Equal(5, c.BaseLinkSlots);
            Assert.Equal(2, c.MaxMutations);
            Assert.Equal(90, c.CoolHuPerTick);
            Assert.Equal(12, c.CoolDelayTicks);
            Assert.Equal(10_200, c.CritStartHu);
            Assert.Equal(210, c.LockTicks);                      // 3.5초
            Assert.Equal(90, c.GrazeBuffTicks);
            Assert.Equal(1200, c.GrazeBuffPermille);
            Assert.Equal(180, db.Character(CharacterId.Baseline).LockTicks);
        }

        [Fact]
        public void CoolPerSecond_50_Is100HuPerTick()
        {
            // spec 01 2.4: 냉각 초당 50(초롱) → 틱당 100 HU
            using TempDataRepo repo = Repo(BaselineJson.Replace("\"cool_per_second\": 45", "\"cool_per_second\": 50"), null);
            Assert.Equal(100, ContentDb.Build(ContentPack.Load(BuildPack(repo.Root))).Character(CharacterId.Baseline).CoolHuPerTick);
        }

        [Fact]
        public void DuplicateCharacter_IsAProblem()
        {
            using TempDataRepo repo = Repo(BaselineJson, Char3Json + ", " + Char3Json);
            Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/characters.json:/characters/1/id") && p.Contains("두 번"));
        }

        [Fact]
        public void UnknownReferenceIds_AreProblems()
        {
            using TempDataRepo repo = Repo(BaselineJson, Char3Json.Replace("passive.underline", "passive.nope").Replace("char.3", "char.9"));
            List<string> p = Problems(repo.Root);
            Assert.Contains(p, x => x.Contains("/characters/0/id") && x.Contains("char.9"));
            using TempDataRepo repo2 = Repo(BaselineJson, Char3Json.Replace("passive.underline", "passive.nope"));
            Assert.Contains(Problems(repo2.Root), x => x.Contains("/characters/0/passive") && x.Contains("passive.nope"));
        }

        [Fact]
        public void Schema_RejectsUnknownField_MissingField_AndTooFewCrystals()
        {
            using (TempDataRepo repo = Repo(BaselineJson.Replace("\"max_hp_cells\": 6,", "\"max_hp_cells\": 6, \"depth_decay\": 0.85,"), null))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/characters.json") && p.Contains("스키마"));
            }
            using (TempDataRepo repo = Repo(BaselineJson.Replace("\"lock_seconds\": 3.0,", ""), null))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/characters.json") && p.Contains("스키마"));
            }
            using (TempDataRepo repo = Repo(BaselineJson.Replace("\"crystal_cap\": 5", "\"crystal_cap\": 2"), null))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/characters.json:/baseline/crystal_cap") && p.Contains("스키마"));
            }
        }

        [Fact]
        public void BrokenSectionBytes_ThrowContentException()
        {
            byte[] good = Section(RepoPack.Value);
            // 모자람
            Assert.Throws<ContentException>(() => Load(good.AsSpan(0, good.Length - 1).ToArray()));
            Assert.Throws<ContentException>(() => Load(new byte[1]));
            // 남음
            var longer = new byte[good.Length + 1];
            good.CopyTo(longer, 0);
            Assert.Throws<ContentException>(() => Load(longer));
            // 모르는 열거형 값(첫 항목의 element 자리: u16 개수 + u8 id 뒤)
            byte[] badEnum = (byte[])good.Clone();
            badEnum[3] = 250;
            Assert.Throws<ContentException>(() => Load(badEnum));
            // 같은 id 두 번(순서 위반)
            byte[] entry = good.AsSpan(2).ToArray();
            var twice = new byte[2 + entry.Length * 2];
            twice[0] = 2;
            entry.CopyTo(twice, 2);
            entry.CopyTo(twice, 2 + entry.Length);
            Assert.Throws<ContentException>(() => Load(twice));
            // 그대로는 읽힌다
            Load(good);
        }

        private static ContentDb Load(byte[] section) =>
            ContentDb.Build(ContentPack.Load(PackWriter.Write(PackWriter.SchemaVersion, 1, new byte[PackWriter.CoreCompatLength],
                new[] { new PackSection((ushort)ContentSection.Characters, section) })));

        private static byte[] Section(byte[] pack)
        {
            Assert.True(ContentPack.Load(pack).TryGetSection((ushort)ContentSection.Characters, out ReadOnlyMemory<byte> s));
            return s.ToArray();
        }

        // 기준값 열(spec 01 5.1)
        private const string BaselineJson =
            "{ \"max_hp_cells\": 6, \"walk_speed\": 5.0, \"stance_speed\": 4.25, \"hit_radius\": 0.15, \"body_radius\": 0.30, \"graze_radius\": 0.6,\n" +
            "  \"invuln_seconds\": 1.0, \"crystal_cap\": 5, \"combo_per_crystal\": 15, \"crystal_loss_on_hit\": 1, \"base_link_slots\": 4, \"max_mutations\": 2,\n" +
            "  \"cool_per_second\": 45, \"cool_delay_seconds\": 0.2, \"crit_start\": 85, \"lock_seconds\": 3.0, \"graze_buff_seconds\": 1.5, \"graze_buff_speed\": 1.2 }";

        // char.3 (spec 04 2.2, spec 01 5.1 갈피 열, spec 14 3.2)
        private const string Char3Json =
            "{ \"id\": \"char.3\", \"element\": \"element.arcane\", \"passive\": \"passive.underline\", \"wand_trait\": \"wand_trait.depth_decay\", \"unique_art\": \"art.copy_circle\",\n" +
            "  \"max_hp_cells\": 6, \"walk_speed\": 5.0, \"stance_speed\": 4.25, \"hit_radius\": 0.15, \"body_radius\": 0.30, \"graze_radius\": 0.6,\n" +
            "  \"invuln_seconds\": 1.0, \"crystal_cap\": 4, \"combo_per_crystal\": 15, \"crystal_loss_on_hit\": 1, \"base_link_slots\": 5, \"max_mutations\": 2,\n" +
            "  \"cool_per_second\": 45, \"cool_delay_seconds\": 0.2, \"crit_start\": 85, \"lock_seconds\": 3.5, \"graze_buff_seconds\": 1.5, \"graze_buff_speed\": 1.2 }";

        private static TempDataRepo Repo(string baseline, string? rows)
        {
            var repo = new TempDataRepo();
            repo.Write("data/schema/characters.schema.json", System.IO.File.ReadAllText(TestRepo.Path("data/schema/characters.schema.json")));
            repo.Write("data/sim/characters.json", "{ \"baseline\": " + baseline + (rows == null ? "" : ", \"characters\": [" + rows + "]") + " }\n");
            repo.Write("data/names/names.ko.json", "{\n  \"char.3\": \"제자 3\",\n  \"~end\": \"\"\n}\n");
            return repo;
        }
    }
}

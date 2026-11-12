using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Wizard.Content.Tests.Pack;
using Wizard.DataBuild.Core;
using Wizard.TestKit;
using Xunit;

namespace Wizard.Content.Tests
{
    // data/sim/tod.json → 팩 절 10 → ContentDb.TimeOfDay(tod) 의 왕복(M3-DAT-02).
    // 기대값은 spec 07 2.3 표(가중치 배율, 백분율 정수), spec 14 3.9 표와 7.3 TV-051(밝은 림 세기)에서 온다.
    public sealed class TodPackTests
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

        // spec 14 7.3 TV-051: 시간대 → 밝은 림 세기 day 20%, dusk 40%, night 60%, boss 60% (천분율로 든다)
        [Fact]
        public void S14_TV_051__RimStrengthPerTimeOfDay()
        {
            ContentDb db = RepoDb();
            Assert.Equal(200, db.TimeOfDay(TimeOfDay.Day).RimStrengthPermille);
            Assert.Equal(400, db.TimeOfDay(TimeOfDay.Dusk).RimStrengthPermille);
            Assert.Equal(600, db.TimeOfDay(TimeOfDay.Night).RimStrengthPermille);
            Assert.Equal(600, db.TimeOfDay(TimeOfDay.BossWave).RimStrengthPermille);
        }

        [Fact]
        public void Spec07_2_3_Day()
        {
            // day: hog_needle 150, vine_thorn 150, boar_tusk 150, bat_dusk 0, wisp_stray 0
            TimeOfDayDef d = RepoDb().TimeOfDay(TimeOfDay.Day);
            Assert.Equal(TimeOfDay.Day, d.Id);
            Assert.True(d.NormalEnemies);
            AssertWeights(d, new[] { (EnemyTypeId.HogNeedle, 150), (EnemyTypeId.BatDusk, 0), (EnemyTypeId.WispStray, 0), (EnemyTypeId.VineThorn, 150), (EnemyTypeId.BoarTusk, 150) });
        }

        [Fact]
        public void Spec07_2_3_Dusk()
        {
            // dusk: wolf_bramble 200, bat_dusk 50, wisp_stray 0
            TimeOfDayDef d = RepoDb().TimeOfDay(TimeOfDay.Dusk);
            Assert.Equal(TimeOfDay.Dusk, d.Id);
            Assert.True(d.NormalEnemies);
            AssertWeights(d, new[] { (EnemyTypeId.WolfBramble, 200), (EnemyTypeId.BatDusk, 50), (EnemyTypeId.WispStray, 0) });
        }

        [Fact]
        public void Spec07_2_3_Night()
        {
            // night: bat_dusk 200, wisp_stray 200, slime_moss 50, hog_needle 0, vine_thorn 0, boar_tusk 0
            TimeOfDayDef d = RepoDb().TimeOfDay(TimeOfDay.Night);
            Assert.Equal(TimeOfDay.Night, d.Id);
            Assert.True(d.NormalEnemies);
            AssertWeights(d, new[] { (EnemyTypeId.SlimeMoss, 50), (EnemyTypeId.HogNeedle, 0), (EnemyTypeId.BatDusk, 200), (EnemyTypeId.WispStray, 200), (EnemyTypeId.VineThorn, 0), (EnemyTypeId.BoarTusk, 0) });
        }

        [Fact]
        public void Spec07_2_3_Boss_NoNormalEnemies()
        {
            // boss: 일반 적 없음. 보스와 졸개만
            TimeOfDayDef d = RepoDb().TimeOfDay(TimeOfDay.BossWave);
            Assert.Equal(TimeOfDay.BossWave, d.Id);
            Assert.False(d.NormalEnemies);
            Assert.Empty(d.WeightSpecies);
            Assert.Empty(d.WeightPct);
        }

        [Fact]
        public void MissingTodOrSection_Throws()
        {
            Assert.Throws<ContentException>(() => RepoDb().TimeOfDay(TimeOfDay.None));
            Assert.Throws<ContentException>(() => RepoDb().TimeOfDay((TimeOfDay)9));
            Assert.Throws<ContentException>(() => Load(null).TimeOfDay(TimeOfDay.Day));
        }

        [Fact]
        public void SameData_SameSectionBytes()
        {
            Assert.Equal(Section(RepoPack.Value), Section(BuildPack(TestRepo.Root)));
        }

        [Fact]
        public void ReversedSourceOrder_GivesTheSameBytes()
        {
            JsonObject o = RepoJson();
            var tods = (JsonArray)o["tods"]!;
            Reverse(tods);
            foreach (JsonNode? row in tods)
            {
                Reverse((JsonArray)row!["weights"]!);
            }
            using TempDataRepo repo = Repo(o.ToJsonString());
            Assert.Equal(Section(RepoPack.Value), Section(BuildPack(repo.Root)));
        }

        [Fact]
        public void Problems_UnknownId_Duplicate_Missing_Weights()
        {
            JsonObject o = RepoJson();
            o["tods"]![0]!["id"] = "tod.noon";
            using (TempDataRepo repo = Repo(o.ToJsonString()))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/tod.json:/tods/0/id") && p.Contains("tod.noon"));
            }
            o = RepoJson();
            o["tods"]![3]!["id"] = "tod.night";
            using (TempDataRepo repo = Repo(o.ToJsonString()))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/tod.json:/tods/3/id") && p.Contains("두 번"));
            }
            o = RepoJson();
            ((JsonArray)o["tods"]!).RemoveAt(3);
            using (TempDataRepo repo = Repo(o.ToJsonString()))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/tod.json:/tods") && p.Contains("tod.boss") && p.Contains("없다"));
            }
            o = RepoJson();
            o["tods"]![1]!["weights"]![1]!["species"] = "enemy.wolf_bramble";
            using (TempDataRepo repo = Repo(o.ToJsonString()))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/tod.json:/tods/1/weights/1/species") && p.Contains("두 번"));
            }
            o = RepoJson();
            o["tods"]![0]!["weights"]![0]!["weight"] = 1.555m;   // × 100 이 정수가 아니다
            using (TempDataRepo repo = Repo(o.ToJsonString()))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/tod.json:/tods/0/weights/0/weight") && p.Contains("정수"));
            }
            o = RepoJson();
            ((JsonArray)o["tods"]![3]!["weights"]!).Add(JsonNode.Parse("{\"species\": \"enemy.slime_moss\", \"weight\": 1}"));
            using (TempDataRepo repo = Repo(o.ToJsonString()))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/tod.json:/tods/3/weights") && p.Contains("normal_enemies"));
            }
            o = RepoJson();
            o["tods"]![0]!["rim_strength_percent"] = 120;
            using (TempDataRepo repo = Repo(o.ToJsonString()))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/tod.json") && p.Contains("스키마"));
            }
        }

        [Fact]
        public void BrokenSectionBytes_ThrowContentException()
        {
            byte[] good = Section(RepoPack.Value);
            Assert.Throws<ContentException>(() => Load(good.AsSpan(0, good.Length - 1).ToArray()));
            var longer = new byte[good.Length + 1];
            good.CopyTo(longer, 0);
            Assert.Throws<ContentException>(() => Load(longer));
            // 첫 항목의 id(u16 개수 뒤): 모르는 값, None, 순서
            byte[] badId = (byte[])good.Clone();
            badId[2] = 77;
            Assert.Throws<ContentException>(() => Load(badId));
            badId[2] = (byte)TimeOfDay.None;
            Assert.Throws<ContentException>(() => Load(badId));
            badId[2] = (byte)TimeOfDay.BossWave;
            Assert.Throws<ContentException>(() => Load(badId));
            // 첫 항목의 normal_enemies: 0/1 이 아니다
            byte[] badBool = (byte[])good.Clone();
            badBool[3] = 2;
            Assert.Throws<ContentException>(() => Load(badBool));
            Load(good);
        }

        private static void AssertWeights(TimeOfDayDef d, (EnemyTypeId Species, int Pct)[] expected)
        {
            var species = new EnemyTypeId[expected.Length];
            var pct = new int[expected.Length];
            for (int i = 0; i < expected.Length; i++)
            {
                species[i] = expected[i].Species;
                pct[i] = expected[i].Pct;
            }
            Assert.Equal(species, d.WeightSpecies);           // EnemyTypeId 오름차순으로 든다
            Assert.Equal(pct, d.WeightPct);
        }

        private static void Reverse(JsonArray a)
        {
            var items = new List<JsonNode?>();
            while (a.Count > 0)
            {
                JsonNode? n = a[a.Count - 1];
                a.RemoveAt(a.Count - 1);
                items.Add(n);
            }
            foreach (JsonNode? n in items)
            {
                a.Add(n);
            }
        }

        private static JsonObject RepoJson() => (JsonObject)JsonNode.Parse(System.IO.File.ReadAllText(TestRepo.Path("data/sim/tod.json")))!;

        private static ContentDb Load(byte[]? section)
        {
            var sections = section == null ? new PackSection[0] : new[] { new PackSection((ushort)ContentSection.Tod, section) };
            return ContentDb.Build(ContentPack.Load(PackWriter.Write(PackWriter.SchemaVersion, 1, new byte[PackWriter.CoreCompatLength], sections)));
        }

        private static byte[] Section(byte[] pack)
        {
            Assert.True(ContentPack.Load(pack).TryGetSection((ushort)ContentSection.Tod, out ReadOnlyMemory<byte> s));
            return s.ToArray();
        }

        private static TempDataRepo Repo(string tods)
        {
            var repo = new TempDataRepo();
            repo.Write("data/schema/tod.schema.json", System.IO.File.ReadAllText(TestRepo.Path("data/schema/tod.schema.json")));
            repo.Write("data/sim/tod.json", tods + "\n");
            repo.Write("data/names/names.ko.json", "{\n  \"tod.boss\": \"보스\",\n  \"tod.day\": \"낮\",\n  \"tod.dusk\": \"해질녘\",\n  \"tod.night\": \"밤\",\n  \"tod.noon\": \"한낮\",\n  \"~end\": \"\"\n}\n");
            return repo;
        }
    }
}

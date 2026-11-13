using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Wizard.Content.Tests.Pack;
using Wizard.DataBuild.Core;
using Wizard.TestKit;
using Xunit;

namespace Wizard.Content.Tests
{
    // data/sim/starfall.json → 팩 절 15 → ContentDb.Starfall 의 왕복(M8-DAT-03).
    // 기대값은 world-characters 2.3(밤 8~9, 1.5초 예고, 8초, 결정 1개), effects 11.3(마지막 2초 깜빡임), spec 01 2.3 시간 표(90, 480, 120틱),
    // spec 01 10.5 의 [임시] 표(확률 50%, 10~40초, 가운데 20×10칸의 0.5칸 격자, 위험에서 2칸·플레이어에서 3칸, 1초 뒤 다시, 줍는 거리 0.5칸)에서 온다.
    public sealed class StarfallPackTests
    {
        private const long RawPerCell = 4294967296L;                // 2^32

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

        private static StarfallDef Starfall() => ContentDb.Build(ContentPack.Load(RepoPack.Value)).Starfall;

        [Fact]
        public void Spec01_10_5_WhenAndHowOften()
        {
            StarfallDef d = Starfall();
            Assert.Equal(TimeOfDay.Night, d.OnlyTod);          // 밤 = 하루 속 순번 8~9(spec 01 18절 C12)
            Assert.Equal(5000, d.ChanceBp);                     // 50% → 만분율 5,000 [임시]
            Assert.Equal(600, d.DropFromTicks);                 // 10초 [임시]
            Assert.Equal(2400, d.DropToTicks);                  // 40초 [임시]
        }

        [Fact]
        public void Spec01_2_3_Timings()
        {
            // spec 01 2.3: 별똥 예고 1.5초 = 90, 남는 시간 8초 = 480, 깜빡임(마지막 2초) = 120. T-X19: 착지 뒤 360틱부터 깜빡이고 480에 사라진다
            StarfallDef d = Starfall();
            Assert.Equal(90, d.TelegraphTicks);
            Assert.Equal(480, d.LandedTicks);
            Assert.Equal(120, d.BlinkTicks);
            Assert.Equal(360, d.LandedTicks - d.BlinkTicks);
        }

        [Fact]
        public void Spec01_10_5_Placement()
        {
            // "빈 바닥": 가운데 20×10칸 안의 0.5칸 격자점, 위험 경계에서 2칸·플레이어에서 3칸 이상, 없으면 1초 뒤 다시 [임시]
            StarfallDef d = Starfall();
            Assert.Equal(20 * RawPerCell, d.AreaWidth.Raw);     // 85,899,345,920
            Assert.Equal(10 * RawPerCell, d.AreaHeight.Raw);    // 42,949,672,960
            Assert.Equal(2147483648L, d.GridStep.Raw);          // 0.5칸 (spec 07 0.2)
            Assert.Equal(8589934592L, d.HazardClearance.Raw);   // 2칸
            Assert.Equal(3 * RawPerCell, d.PlayerClearance.Raw);
            Assert.Equal(60, d.RetryTicks);
        }

        [Fact]
        public void Spec01_10_5_Pickup()
        {
            // 판정 중심 거리 ≤ 0.5칸 [임시], 마력 결정 1개(world-characters 2.3)
            StarfallDef d = Starfall();
            Assert.Equal(2147483648L, d.PickRadius.Raw);
            Assert.Equal(1, d.Crystals);
        }

        [Fact]
        public void PackWithoutTheSection_Throws()
        {
            ContentDb db = Load(null);
            Assert.Throws<ContentException>(() => db.Starfall);
        }

        [Fact]
        public void SameData_SameSectionBytes()
        {
            Assert.Equal(Section(RepoPack.Value), Section(BuildPack(TestRepo.Root)));
        }

        [Fact]
        public void Problems_Tod_Windows_Schema()
        {
            JsonObject o = RepoJson();
            o["only_tod"] = "tod.boss";
            using (TempDataRepo repo = Repo(o.ToJsonString()))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/starfall.json:/only_tod") && p.Contains("보스"));
            }
            o = RepoJson();
            o["only_tod"] = "tod.noon";
            using (TempDataRepo repo = Repo(o.ToJsonString()))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/starfall.json:/only_tod") && p.Contains("tod.noon"));
            }
            o = RepoJson();
            o["drop_window_seconds"]!["from"] = 50;
            using (TempDataRepo repo = Repo(o.ToJsonString()))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/starfall.json:/drop_window_seconds/from"));
            }
            o = RepoJson();
            o["blink_seconds"] = 9;
            using (TempDataRepo repo = Repo(o.ToJsonString()))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/starfall.json:/blink_seconds"));
            }
            o = RepoJson();
            o["chance_percent"] = 101;
            using (TempDataRepo repo = Repo(o.ToJsonString()))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/starfall.json") && p.Contains("스키마"));
            }
            o = RepoJson();
            o["telegraph_seconds"] = 0;                           // 예고 없이 떨어지지 않는다
            using (TempDataRepo repo = Repo(o.ToJsonString()))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/starfall.json") && p.Contains("스키마"));
            }
            o = RepoJson();
            o.Remove("pick_radius");
            using (TempDataRepo repo = Repo(o.ToJsonString()))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/starfall.json") && p.Contains("스키마"));
            }
            o = RepoJson();
            o["max_per_wave"] = 2;                                // 모르는 필드
            using (TempDataRepo repo = Repo(o.ToJsonString()))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/starfall.json") && p.Contains("스키마"));
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
            Assert.Throws<ContentException>(() => Load(new byte[0]));

            // only_tod(첫 바이트): None, 보스, 모르는 값
            byte[] bad = (byte[])good.Clone();
            foreach (byte v in new byte[] { (byte)TimeOfDay.None, (byte)TimeOfDay.BossWave, 9 })
            {
                bad[0] = v;
                Assert.Throws<ContentException>(() => Load(bad));
            }

            // 떨어지는 때가 뒤집힘: drop_from(u8, i32 chance 뒤)을 drop_to 보다 크게
            bad = (byte[])good.Clone();
            WriteLe(bad, 1 + 4, 2401);
            Assert.Throws<ContentException>(() => Load(bad));

            // 깜빡임 > 놓여 있는 시간: blink(u8, i32 × 5 뒤)
            bad = (byte[])good.Clone();
            WriteLe(bad, 1 + 4 * 5, 481);
            Assert.Throws<ContentException>(() => Load(bad));

            Load(good);
        }

        private static void WriteLe(byte[] b, int at, int v)
        {
            for (int i = 0; i < 4; i++)
            {
                b[at + i] = (byte)(v >> (8 * i));
            }
        }

        private static JsonObject RepoJson() => (JsonObject)JsonNode.Parse(System.IO.File.ReadAllText(TestRepo.Path("data/sim/starfall.json")))!;

        private static ContentDb Load(byte[]? section)
        {
            var sections = section == null ? new PackSection[0] : new[] { new PackSection((ushort)ContentSection.Starfall, section) };
            return ContentDb.Build(ContentPack.Load(PackWriter.Write(PackWriter.SchemaVersion, 1, new byte[PackWriter.CoreCompatLength], sections)));
        }

        private static byte[] Section(byte[] pack)
        {
            Assert.True(ContentPack.Load(pack).TryGetSection((ushort)ContentSection.Starfall, out ReadOnlyMemory<byte> s));
            return s.ToArray();
        }

        private static TempDataRepo Repo(string starfall)
        {
            var repo = new TempDataRepo();
            repo.Write("data/schema/starfall.schema.json", System.IO.File.ReadAllText(TestRepo.Path("data/schema/starfall.schema.json")));
            repo.Write("data/sim/starfall.json", starfall + "\n");
            return repo;
        }
    }
}

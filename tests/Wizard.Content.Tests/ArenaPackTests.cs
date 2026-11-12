using System;
using System.Collections.Generic;
using Wizard.Content.Tests.Pack;
using Wizard.Core;
using Wizard.DataBuild.Core;
using Wizard.TestKit;
using Xunit;

namespace Wizard.Content.Tests
{
    // data/sim/arena.json → 팩 절 12 → ContentDb.Arena(SimMode) 의 왕복(M1-CONT-03).
    // 기대값: DECISIONS 2.3(24×14칸), world-characters 2.6·spec 10 4절(돌마당 기둥·시작 위치), spec 01 3.7(무한 웨이브 시작 위치 [임시]).
    public sealed class ArenaPackTests
    {
        private const long OneRaw = 4_294_967_296L;            // 2^32 (spec 14 0.3)

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

        private static FixVec2 Cell(long x, long y) => new FixVec2(Fix64.FromRaw(x * OneRaw), Fix64.FromRaw(y * OneRaw));

        [Theory]
        [InlineData(SimMode.Endless)]
        [InlineData(SimMode.Practice)]
        public void Field_24x14_NoPillars_StartAtCentre(SimMode mode)
        {
            ArenaDef a = RepoDb().Arena(mode);
            Assert.Equal(24 * OneRaw, a.Width.Raw);
            Assert.Equal(14 * OneRaw, a.Height.Raw);
            Assert.Empty(a.Pillars);
            Assert.Equal(new[] { Cell(12, 7) }, a.SpawnPoints);  // spec 01 3.7 [임시]
        }

        [Fact]
        public void Duel_24x14_FourPillars_TwoStarts()
        {
            ArenaDef a = RepoDb().Arena(SimMode.Duel);
            Assert.Equal(24 * OneRaw, a.Width.Raw);
            Assert.Equal(14 * OneRaw, a.Height.Raw);
            // world-characters 2.6: 칸 (9,3), (14,3), (9,10), (14,10). 원본의 순서 그대로
            Assert.Equal(new[] { Cell(9, 3), Cell(14, 3), Cell(9, 10), Cell(14, 10) }, a.Pillars);
            // 왼쪽 (6.0, 7.0), 오른쪽 (18.0, 7.0). 슬롯 순
            Assert.Equal(new[] { Cell(6, 7), Cell(18, 7) }, a.SpawnPoints);
        }

        [Fact]
        public void Duel_PillarsAreSymmetricAboutTheCentre()
        {
            // world-characters 2.6: 범위 x 9~10·14~15, y 3~4·10~11 이 가운데 (12.0, 7.0) 기준으로 좌우·상하 대칭이다
            ArenaDef a = RepoDb().Arena(SimMode.Duel);
            foreach (FixVec2 p in a.Pillars)
            {
                long mirrorX = a.Width.Raw - (p.X.Raw + OneRaw);
                long mirrorY = a.Height.Raw - (p.Y.Raw + OneRaw);
                Assert.Contains(a.Pillars, q => q.X.Raw == mirrorX && q.Y.Raw == p.Y.Raw);
                Assert.Contains(a.Pillars, q => q.X.Raw == p.X.Raw && q.Y.Raw == mirrorY);
            }
            Assert.Equal(a.Width.Raw, a.SpawnPoints[0].X.Raw + a.SpawnPoints[1].X.Raw);
        }

        [Fact]
        public void ModesWithoutAnArena_Throw()
        {
            ContentDb db = RepoDb();
            Assert.Throws<ContentException>(() => db.Arena(SimMode.Plaza));   // 광장 지도는 이 절이 아니다(spec 11)
            Assert.Throws<ContentException>(() => db.Arena((SimMode)9));
        }

        [Fact]
        public void SameData_SameSectionBytes()
        {
            Assert.Equal(Section(RepoPack.Value), Section(BuildPack(TestRepo.Root)));
        }

        [Fact]
        public void Problems_DuplicateMode_PillarOutside_SpawnOnPillarOrWall()
        {
            using (TempDataRepo repo = Repo(Field("[\"endless\"]", "[]", "[{\"x\": 12, \"y\": 7}]") + ", " + Field("[\"duel\", \"endless\"]", "[]", "[{\"x\": 6, \"y\": 7}]")))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/arena.json:/arenas/1/modes/1") && p.Contains("endless"));
            }
            using (TempDataRepo repo = Repo(Field("[\"duel\"]", "[{\"x\": 23.5, \"y\": 3}]", "[{\"x\": 6, \"y\": 7}]")))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/arena.json:/arenas/0/pillars/0"));
            }
            using (TempDataRepo repo = Repo(Field("[\"duel\"]", "[{\"x\": 9, \"y\": 3}]", "[{\"x\": 9.5, \"y\": 3.5}]")))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/arena.json:/arenas/0/spawn_points/0") && p.Contains("기둥"));
            }
            using (TempDataRepo repo = Repo(Field("[\"duel\"]", "[]", "[{\"x\": 0, \"y\": 7}]")))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/arena.json:/arenas/0/spawn_points/0"));
            }
            using (TempDataRepo repo = Repo(Field("[\"arcade\"]", "[]", "[{\"x\": 6, \"y\": 7}]")))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/arena.json:/arenas/0/modes/0") && p.Contains("스키마"));
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
            // 첫 항목의 모드 바이트(u16 개수 뒤): 모르는 값
            byte[] badMode = (byte[])good.Clone();
            badMode[2] = 77;
            Assert.Throws<ContentException>(() => Load(badMode));
            // 모드 순서: 첫 항목을 둘째(duel = 1) 이상으로 바꾸면 오름차순이 깨진다
            byte[] order = (byte[])good.Clone();
            order[2] = (byte)SimMode.Practice;
            Assert.Throws<ContentException>(() => Load(order));
            Load(good);
        }

        private static string Field(string modes, string pillars, string spawns) =>
            "{ \"id\": \"place.field\", \"modes\": " + modes + ", \"width\": 24, \"height\": 14, \"pillars\": " + pillars + ", \"spawn_points\": " + spawns + " }";

        private static ContentDb Load(byte[] section) =>
            ContentDb.Build(ContentPack.Load(PackWriter.Write(PackWriter.SchemaVersion, 1, new byte[PackWriter.CoreCompatLength],
                new[] { new PackSection((ushort)ContentSection.Arena, section) })));

        private static byte[] Section(byte[] pack)
        {
            Assert.True(ContentPack.Load(pack).TryGetSection((ushort)ContentSection.Arena, out ReadOnlyMemory<byte> s));
            return s.ToArray();
        }

        private static TempDataRepo Repo(string arenas)
        {
            var repo = new TempDataRepo();
            repo.Write("data/schema/arena.schema.json", System.IO.File.ReadAllText(TestRepo.Path("data/schema/arena.schema.json")));
            repo.Write("data/sim/arena.json", "{ \"arenas\": [" + arenas + "] }\n");
            repo.Write("data/names/names.ko.json", "{\n  \"place.field\": \"한뎃벌\",\n  \"~end\": \"\"\n}\n");
            return repo;
        }
    }
}

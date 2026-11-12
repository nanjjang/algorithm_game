using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Wizard.Content.Tests.Pack;
using Wizard.DataBuild.Core;
using Wizard.TestKit;
using Xunit;

namespace Wizard.Content.Tests
{
    // data/sim/frames.json → 팩 절 8 → ContentDb.Frames 의 왕복(M3-DAT-02).
    // 기대값은 spec 07 2.4 표(틀 5행: 주 묶음, 그룹 몫, 고정 개수, 나머지)에서 온다. 틀 id 는 spec 14 3.9(frame.hunting_party = FrameId.Hunt).
    public sealed class FramesPackTests
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

        private static IReadOnlyList<FrameDef> Frames() => ContentDb.Build(ContentPack.Load(RepoPack.Value)).Frames;

        [Fact]
        public void Spec07_2_4_FiveFrames_InFrameIdOrder()
        {
            IReadOnlyList<FrameDef> f = Frames();
            Assert.Equal(5, f.Count);
            Assert.Equal(new[] { FrameId.Swarm, FrameId.Hunt, FrameId.Battery, FrameId.Ambush, FrameId.Mixed }, new[] { f[0].Id, f[1].Id, f[2].Id, f[3].Id, f[4].Id });
            foreach (FrameDef d in f)
            {
                Assert.True(d.RestMixed);                         // 나머지는 늘 혼합
            }
        }

        [Fact]
        public void Spec07_2_4_Swarm()
        {
            // 떼: cost ∈ {1,2} (slime_moss, bat_dusk, bug_lantern) 70%, 고정 없음, 나머지 30%
            FrameDef d = Frames()[0];
            Assert.Equal(new[] { EnemyTypeId.SlimeMoss, EnemyTypeId.BugLantern, EnemyTypeId.BatDusk }, d.GroupSpecies);
            Assert.Equal(70, d.GroupSharePct);
            Assert.Empty(d.FixedSpecies);
            Assert.Empty(d.FixedMin);
            Assert.Empty(d.FixedMax);
        }

        [Fact]
        public void Spec07_2_4_HuntingParty()
        {
            // 사냥대: wolf_bramble, boar_tusk 60%, 고정 toad_drum 1
            FrameDef d = Frames()[1];
            Assert.Equal(new[] { EnemyTypeId.WolfBramble, EnemyTypeId.BoarTusk }, d.GroupSpecies);
            Assert.Equal(60, d.GroupSharePct);
            Assert.Equal(new[] { EnemyTypeId.ToadDrum }, d.FixedSpecies);
            Assert.Equal(new[] { 1 }, d.FixedMin);
            Assert.Equal(new[] { 1 }, d.FixedMax);
        }

        [Fact]
        public void Spec07_2_4_Battery()
        {
            // 포대: shaman_cap, hog_needle, wisp_stray, vine_thorn 60%, 고정 golem_menhir 1~2
            FrameDef d = Frames()[2];
            Assert.Equal(new[] { EnemyTypeId.ShamanCap, EnemyTypeId.HogNeedle, EnemyTypeId.WispStray, EnemyTypeId.VineThorn }, d.GroupSpecies);
            Assert.Equal(60, d.GroupSharePct);
            Assert.Equal(new[] { EnemyTypeId.GolemMenhir }, d.FixedSpecies);
            Assert.Equal(new[] { 1 }, d.FixedMin);
            Assert.Equal(new[] { 2 }, d.FixedMax);
        }

        [Fact]
        public void Spec07_2_4_Ambush()
        {
            // 매복: weasel_pot, bug_lantern 50%, 고정 없음
            FrameDef d = Frames()[3];
            Assert.Equal(new[] { EnemyTypeId.BugLantern, EnemyTypeId.WeaselPot }, d.GroupSpecies);
            Assert.Equal(50, d.GroupSharePct);
            Assert.Empty(d.FixedSpecies);
        }

        [Fact]
        public void Spec07_2_4_Mixed()
        {
            // 혼합: 주 묶음 없음, 나머지 100%
            FrameDef d = Frames()[4];
            Assert.Empty(d.GroupSpecies);
            Assert.Equal(0, d.GroupSharePct);
            Assert.Empty(d.FixedSpecies);
        }

        [Fact]
        public void FramesList_IsReadOnly()
        {
            IReadOnlyList<FrameDef> f = Frames();
            Assert.False(f is FrameDef[]);
            Assert.True(((ICollection<FrameDef>)f).IsReadOnly);
        }

        [Fact]
        public void PackWithoutTheSection_Throws()
        {
            ContentDb db = Load(null);
            Assert.Throws<ContentException>(() => db.Frames);
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
            var frames = (JsonArray)o["frames"]!;
            Reverse(frames);
            foreach (JsonNode? row in frames)
            {
                Reverse((JsonArray)row!["group"]!);
            }
            using TempDataRepo repo = Repo(o.ToJsonString());
            Assert.Equal(Section(RepoPack.Value), Section(BuildPack(repo.Root)));
        }

        [Fact]
        public void Problems_UnknownId_Duplicate_Missing_Share_Schema()
        {
            JsonObject o = RepoJson();
            o["frames"]![0]!["id"] = "frame.parade";
            using (TempDataRepo repo = Repo(o.ToJsonString()))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/frames.json:/frames/0/id") && p.Contains("frame.parade"));
            }
            o = RepoJson();
            o["frames"]![4]!["id"] = "frame.swarm";             // 떼가 둘, 혼합이 없다
            using (TempDataRepo repo = Repo(o.ToJsonString()))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/frames.json:/frames/4/id") && p.Contains("두 번"));
            }
            o = RepoJson();
            ((JsonArray)o["frames"]!).RemoveAt(4);
            using (TempDataRepo repo = Repo(o.ToJsonString()))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/frames.json:/frames") && p.Contains("frame.mixed") && p.Contains("없다"));
            }
            o = RepoJson();
            o["frames"]![4]!["group_share"] = 10;               // 주 묶음이 없는데 몫
            using (TempDataRepo repo = Repo(o.ToJsonString()))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/frames.json:/frames/4/group_share"));
            }
            o = RepoJson();
            o["frames"]![1]!["group"]![0] = "enemy.wyvern";
            using (TempDataRepo repo = Repo(o.ToJsonString()))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/frames.json:/frames/1/group/0") && p.Contains("enemy.wyvern"));
            }
            o = RepoJson();
            o["frames"]![2]!["fixed"]![0]!["min"] = 3;          // 1~2 → 3~2
            using (TempDataRepo repo = Repo(o.ToJsonString()))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/frames.json:/frames/2/fixed/0/min"));
            }
            o = RepoJson();
            o["frames"]![0]!["group_share"] = 101;
            using (TempDataRepo repo = Repo(o.ToJsonString()))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/frames.json") && p.Contains("스키마"));
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
            // 첫 틀의 frame 바이트(u16 개수 뒤): 모르는 값, 그리고 순서(혼합 = 5 를 맨 앞에)
            byte[] badFrame = (byte[])good.Clone();
            badFrame[2] = 77;
            Assert.Throws<ContentException>(() => Load(badFrame));
            badFrame[2] = (byte)FrameId.Mixed;
            Assert.Throws<ContentException>(() => Load(badFrame));
            // 첫 틀의 첫 주 묶음 종(u16 개수, u8 frame, u16 종 수 뒤): 모르는 종, 순서(둘째 종 이상)
            byte[] badSpecies = (byte[])good.Clone();
            badSpecies[2 + 1 + 2] = 200;
            Assert.Throws<ContentException>(() => Load(badSpecies));
            badSpecies[2 + 1 + 2] = (byte)EnemyTypeId.ToadDrum;
            Assert.Throws<ContentException>(() => Load(badSpecies));
            Load(good);
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

        private static JsonObject RepoJson() => (JsonObject)JsonNode.Parse(System.IO.File.ReadAllText(TestRepo.Path("data/sim/frames.json")))!;

        private static ContentDb Load(byte[]? section)
        {
            var sections = section == null ? new PackSection[0] : new[] { new PackSection((ushort)ContentSection.Frames, section) };
            return ContentDb.Build(ContentPack.Load(PackWriter.Write(PackWriter.SchemaVersion, 1, new byte[PackWriter.CoreCompatLength], sections)));
        }

        private static byte[] Section(byte[] pack)
        {
            Assert.True(ContentPack.Load(pack).TryGetSection((ushort)ContentSection.Frames, out ReadOnlyMemory<byte> s));
            return s.ToArray();
        }

        private static TempDataRepo Repo(string frames)
        {
            var repo = new TempDataRepo();
            repo.Write("data/schema/frames.schema.json", System.IO.File.ReadAllText(TestRepo.Path("data/schema/frames.schema.json")));
            repo.Write("data/sim/frames.json", frames + "\n");
            repo.Write("data/names/names.ko.json", "{\n  \"frame.ambush\": \"매복\",\n  \"frame.battery\": \"포대\",\n  \"frame.hunting_party\": \"사냥대\",\n  \"frame.mixed\": \"혼합\",\n  \"frame.parade\": \"행진\",\n  \"frame.swarm\": \"떼\",\n  \"~end\": \"\"\n}\n");
            return repo;
        }
    }
}

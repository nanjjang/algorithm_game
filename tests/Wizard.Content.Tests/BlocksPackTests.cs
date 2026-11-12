using System;
using System.Collections.Generic;
using Wizard.Content.Tests.Pack;
using Wizard.Core;
using Wizard.DataBuild.Core;
using Wizard.TestKit;
using Xunit;

namespace Wizard.Content.Tests
{
    // data/sim/blocks.json → 팩 절 2 → ContentDb.Block 의 왕복(M1-CONT-03: 마력탄, 거대 화염구).
    // 기대값은 spec 02 4.1·4.2 표(속도 Raw 는 표에 적힌 값), spec 01 2.4(열 HU)·2.5(간격 틱), spec 14 3.3(종류·등급·속성)에서 온다.
    public sealed class BlocksPackTests
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

        [Fact]
        public void MagicBolt_RoundTrip_EveryField_IsTheSpecInteger()
        {
            BlockDef b = RepoDb().Block(BlockId.MagicBolt);
            Assert.Equal(BlockId.MagicBolt, b.Id);
            Assert.Equal(BlockKind.Projectile, b.Kind);
            Assert.Equal(Rarity.Common, b.Rarity);
            Assert.Equal(Element.Arcane, b.Element);
            Assert.Equal(480, b.HeatHu);                          // 열 4 (spec 01 2.4)
            Assert.Equal(1000, b.HeatMulPermille);
            Assert.Equal(1, b.SplitCount);
            Assert.Equal(10 * OneRaw, b.BaseDamage.Raw);          // 피해 10
            Assert.Equal(9, b.IntervalTicks);                     // 0.15초
            Assert.Equal(1_002_159_036L, b.SpeedPerTick.Raw);     // 14 칸/초 (spec 02 4.2)
            Assert.False(b.IsInstant);
            Assert.Equal(10 * OneRaw, b.Range.Raw);               // 사거리 10
            Assert.Equal(515_396_076L, b.HitRadius.Raw);          // 0.12 × 2^32 = 515,396,075.52 → 반올림 (spec 02 4.2 [임시])
            Assert.Equal(0, b.DamageBonusPermille);
            Assert.Equal(ProjectileFlags.None, b.Flags);
            Assert.Equal(ModifierEffect.None, b.Effect);
            Assert.NotNull(b.Params);
        }

        [Fact]
        public void Fireball_RoundTrip_EveryField_IsTheSpecInteger()
        {
            BlockDef b = RepoDb().Block(BlockId.Fireball);
            Assert.Equal(BlockId.Fireball, b.Id);
            Assert.Equal(BlockKind.Projectile, b.Kind);
            Assert.Equal(Rarity.Rare, b.Rarity);
            Assert.Equal(Element.Fire, b.Element);
            Assert.Equal(2160, b.HeatHu);                         // 열 18 (spec 01 2.5)
            Assert.Equal(1000, b.HeatMulPermille);
            Assert.Equal(1, b.SplitCount);
            Assert.Equal(40 * OneRaw, b.BaseDamage.Raw);          // 피해 40
            Assert.Equal(36, b.IntervalTicks);                    // 0.6초
            Assert.Equal(644_245_094L, b.SpeedPerTick.Raw);       // 9 칸/초 → 0.15 (spec 02 4.2)
            Assert.False(b.IsInstant);
            Assert.Equal(8 * OneRaw, b.Range.Raw);                // 사거리 8
            Assert.Equal(773_094_113L, b.HitRadius.Raw);          // 0.18 × 2^32 = 773,094,113.28 → 반올림 (spec 02 4.2 [임시])
            Assert.Equal(0, b.DamageBonusPermille);
            Assert.Equal(ProjectileFlags.None, b.Flags);          // Explodes 비트는 M2-SPL-01
            Assert.Equal(ModifierEffect.None, b.Effect);
        }

        [Fact]
        public void BlocksNotInPack_Throw()
        {
            ContentDb db = RepoDb();
            Assert.Throws<ContentException>(() => db.Block(BlockId.None));
            Assert.Throws<ContentException>(() => db.Block(BlockId.ChainLightning));
            Assert.Throws<ContentException>(() => db.Block(BlockId.Corrosion));
        }

        [Fact]
        public void SameData_SameSectionBytes_InIdOrder()
        {
            byte[] s = Section(RepoPack.Value);
            Assert.Equal(s, Section(BuildPack(TestRepo.Root)));
            // 원본은 마력탄, 거대 화염구 순이 아니어도 절은 BlockId 오름차순이다(u16 개수 뒤 첫 바이트가 id)
            Assert.Equal(2, s[0] | (s[1] << 8));
            Assert.Equal((byte)BlockId.MagicBolt, s[2]);
        }

        [Fact]
        public void ReversedSourceOrder_GivesTheSameBytes()
        {
            using TempDataRepo repo = Repo("[" + FireballJson + ", " + MagicBoltJson + "]");
            Assert.Equal(Section(RepoPack.Value), Section(BuildPack(repo.Root)));
        }

        [Fact]
        public void Problems_UnknownId_Duplicate_InstantWithSpeed()
        {
            using (TempDataRepo repo = Repo("[" + MagicBoltJson.Replace("block.magic_bolt", "block.nope") + "]"))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/blocks.json:/blocks/0/id") && p.Contains("block.nope"));
            }
            using (TempDataRepo repo = Repo("[" + MagicBoltJson + ", " + MagicBoltJson + "]"))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/blocks.json:/blocks/1/id") && p.Contains("두 번"));
            }
            using (TempDataRepo repo = Repo("[" + MagicBoltJson.Replace("\"instant\": false", "\"instant\": true") + "]"))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/blocks.json:/blocks/0/speed"));
            }
            using (TempDataRepo repo = Repo("[" + MagicBoltJson.Replace("\"kind\": \"projectile\"", "\"kind\": \"beam\"") + "]"))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/blocks.json:/blocks/0/kind") && p.Contains("스키마"));
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
            int entry = (good.Length - 2) / 2;
            // 첫 항목: u8 id(2) kind(3) rarity(4) element(5), i32×3(6..17), i64(18..25), i32(26..29), i64(30..37), u8 is_instant(38),
            //          i64(39..46), i64(47..54), i32(55..58), u16 flags(59..60), u8 effect(61)
            Assert.Equal(60, entry);
            byte[] badKind = (byte[])good.Clone();
            badKind[3] = 0;
            Assert.Throws<ContentException>(() => Load(badKind));
            byte[] badInstant = (byte[])good.Clone();
            badInstant[38] = 2;
            Assert.Throws<ContentException>(() => Load(badInstant));
            byte[] badFlags = (byte[])good.Clone();
            badFlags[59] = 0x80;
            Assert.Throws<ContentException>(() => Load(badFlags));
            byte[] noneId = (byte[])good.Clone();
            noneId[2] = 0;
            Assert.Throws<ContentException>(() => Load(noneId));
            // 순서 위반: 두 항목을 바꾼다
            var swapped = new byte[good.Length];
            Array.Copy(good, 0, swapped, 0, 2);
            Array.Copy(good, 2 + entry, swapped, 2, entry);
            Array.Copy(good, 2, swapped, 2 + entry, entry);
            Assert.Throws<ContentException>(() => Load(swapped));
            Load(good);
        }

        private static ContentDb Load(byte[] section) =>
            ContentDb.Build(ContentPack.Load(PackWriter.Write(PackWriter.SchemaVersion, 1, new byte[PackWriter.CoreCompatLength],
                new[] { new PackSection((ushort)ContentSection.Blocks, section) })));

        private static byte[] Section(byte[] pack)
        {
            Assert.True(ContentPack.Load(pack).TryGetSection((ushort)ContentSection.Blocks, out ReadOnlyMemory<byte> s));
            return s.ToArray();
        }

        // spec 02 4.1·4.2 의 두 줄
        private const string MagicBoltJson =
            "{ \"id\": \"block.magic_bolt\", \"kind\": \"projectile\", \"rarity\": \"common\", \"element\": \"element.arcane\", \"heat\": 4, \"heat_multiplier\": 1,\n" +
            "  \"split_count\": 1, \"base_damage\": 10, \"interval_seconds\": 0.15, \"speed\": 14, \"instant\": false, \"range\": 10, \"hit_radius\": 0.12, \"damage_bonus\": 0 }";

        private const string FireballJson =
            "{ \"id\": \"block.fireball\", \"kind\": \"projectile\", \"rarity\": \"rare\", \"element\": \"element.fire\", \"heat\": 18, \"heat_multiplier\": 1,\n" +
            "  \"split_count\": 1, \"base_damage\": 40, \"interval_seconds\": 0.6, \"speed\": 9, \"instant\": false, \"range\": 8, \"hit_radius\": 0.18, \"damage_bonus\": 0 }";

        private static TempDataRepo Repo(string blocksArray)
        {
            var repo = new TempDataRepo();
            repo.Write("data/schema/blocks.schema.json", System.IO.File.ReadAllText(TestRepo.Path("data/schema/blocks.schema.json")));
            repo.Write("data/sim/blocks.json", "{ \"blocks\": " + blocksArray + " }\n");
            repo.Write("data/names/names.ko.json", "{\n  \"block.fireball\": \"거대 화염구\",\n  \"block.magic_bolt\": \"마력탄\",\n  \"block.nope\": \"없음\",\n  \"~end\": \"\"\n}\n");
            return repo;
        }
    }
}

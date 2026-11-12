using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Wizard.Content;
using Wizard.Core;
using Wizard.DataBuild.Core;
using Wizard.DataBuild.Names;
using Xunit;

namespace Wizard.Content.Tests.Pack
{
    // databuild 의 흐름(ARCHITECTURE 8.2): 꼴 → 스키마 → 변환 → 교차 검사 → 결정성 → 팩.
    // 시험용 변환기(blocks 절)로 돌린다. 저장소의 변환기는 ModuleRegistry.Discover 가 찾는다(이 시험의 변환기는 거기에 들지 않는다).
    public sealed class PackBuildTests
    {
        // 시험용 blocks 절: { "blocks": [ { "id": <contentId>, "speed": <칸/초> } … ] } → u16 개수, (id 길이·바이트, 틱당 Raw i64) × 개수(id 서수 오름차순)
        private sealed class TestBlocksConverter : ISectionConverter
        {
            public ContentSection Section => ContentSection.Blocks;

            public void Convert(DataFile file, ByteWriter output, Diagnostics diagnostics)
            {
                var items = new List<(string Id, decimal Speed)>();
                foreach (JsonElement b in file.Root.GetProperty("blocks").EnumerateArray())
                {
                    items.Add((b.GetProperty("id").GetString()!, b.GetProperty("speed").GetDecimal()));
                }
                items.Sort((x, y) => string.CompareOrdinal(x.Id, y.Id));
                output.U16((ushort)items.Count);
                foreach (var (id, speed) in items)
                {
                    byte[] utf8 = System.Text.Encoding.UTF8.GetBytes(id);
                    output.U8((byte)utf8.Length);
                    output.Bytes(utf8);
                    output.I64(Units.CellsPerSecondToRawPerTick(speed));
                }
            }
        }

        // 부를 때마다 다른 바이트를 내는 변환기(결정성 검사가 잡아야 한다)
        private sealed class DriftingConverter : ISectionConverter
        {
            private byte _n;

            public ContentSection Section => ContentSection.Arena;

            public void Convert(DataFile file, ByteWriter output, Diagnostics diagnostics) => output.U8(_n++);
        }

        private const string BlocksSchemaBody =
            "  \"type\": \"object\",\n" +
            "  \"required\": [\"blocks\"],\n" +
            "  \"additionalProperties\": false,\n" +
            "  \"properties\": {\n" +
            "    \"blocks\": { \"type\": \"array\", \"items\": {\n" +
            "      \"type\": \"object\", \"required\": [\"id\", \"speed\"], \"additionalProperties\": false,\n" +
            "      \"properties\": { \"id\": { \"$ref\": \"_common.schema.json#/$defs/contentId\" }, \"speed\": { \"$ref\": \"_common.schema.json#/$defs/cellsPerSecond\" } }\n" +
            "    } }\n" +
            "  }";

        private static TempDataRepo BlocksRepo(string blocksJson)
        {
            var repo = new TempDataRepo();
            repo.Write("data/schema/blocks.schema.json", TempDataRepo.Schema("blocks.schema.json", BlocksSchemaBody));
            repo.Write("data/sim/blocks.json", blocksJson);
            repo.Write("data/names/names.ko.json", "{\n  \"block.fireball\": \"거대 화염구\",\n  \"block.magic_bolt\": \"마력탄\",\n  \"~end\": \"\"\n}\n");
            return repo;
        }

        private const string TwoBlocks = "{ \"blocks\": [ { \"id\": \"block.magic_bolt\", \"speed\": 9 }, { \"id\": \"block.fireball\", \"speed\": 4.7 } ] }\n";

        private static ModuleRegistry Modules(params ISectionConverter[] converters) =>
            new ModuleRegistry(converters, new IDataValidator[] { new NameKeyCoverage() });

        private static (Pipeline.Output Out, Diagnostics D) Run(TempDataRepo repo, ModuleRegistry modules, uint dataVersion = 5)
        {
            var d = new Diagnostics();
            Pipeline.Output o = Pipeline.Run(repo.Root, modules, PackOptions.Default(dataVersion), d);
            return (o, d);
        }

        [Fact]
        public void Build_ConvertsAndPacks_ContentPackReadsIt()
        {
            using TempDataRepo repo = BlocksRepo(TwoBlocks);
            var (o, d) = Run(repo, Modules(new TestBlocksConverter()));
            Assert.False(d.HasErrors, string.Join("\n", d.Sorted()));
            ContentPack pack = ContentPack.Load(o.Pack!);
            Assert.Equal(5, pack.DataVersion);
            Assert.Equal(Wizard.Sim.SimVersion.CoreVersion, pack.CoreCompat);
            Assert.Equal(1, pack.SectionCount);
            Assert.True(pack.TryGetSection((ushort)ContentSection.Blocks, out ReadOnlyMemory<byte> s));
            var r = new ByteReader(s.Span);
            Assert.True(r.TryU16(out ushort count));
            Assert.Equal(2, count);
            Assert.True(r.TryU8(out byte len0));
            Assert.True(r.TryBytes(len0, out ReadOnlySpan<byte> id0));
            Assert.Equal("block.fireball", System.Text.Encoding.UTF8.GetString(id0));   // id 오름차순(파일 순서가 아니라)
            Assert.True(r.TryI64(out long speed0));
            Assert.Equal(336_439_105L, speed0);                                            // 4.7칸/초
            Assert.True(r.TryU8(out byte len1));
            Assert.True(r.TryBytes(len1, out ReadOnlySpan<byte> id1));
            Assert.Equal("block.magic_bolt", System.Text.Encoding.UTF8.GetString(id1));
            Assert.True(r.TryI64(out long speed1));
            Assert.Equal(644_245_094L, speed1);                                            // 9칸/초 = 9 × 2^32 ÷ 60 = 644,245,094.4
            Assert.Equal(0, r.Remaining);
            Assert.NotNull(o.Names);
        }

        [Fact]
        public void SameData_SameBytes_RegardlessOfFileOrderAndWriteTime()
        {
            using TempDataRepo a = BlocksRepo(TwoBlocks);
            var (oa, da) = Run(a, Modules(new TestBlocksConverter()));
            var (oa2, da2) = Run(a, Modules(new TestBlocksConverter()));
            Assert.False(da.HasErrors || da2.HasErrors);
            Assert.Equal(oa.Pack, oa2.Pack);
            Assert.Equal(oa.Names, oa2.Names);

            // 같은 내용을 다른 순서로 적은 파일(항목 순서, 띄어쓰기). 변환기가 정규 순서로 쓰므로 바이트가 같다
            using TempDataRepo b = BlocksRepo("{\"blocks\":[{\"speed\":4.70,\"id\":\"block.fireball\"},{\"id\":\"block.magic_bolt\",\"speed\":9.0}]}");
            var (ob, db) = Run(b, Modules(new TestBlocksConverter()));
            Assert.False(db.HasErrors, string.Join("\n", db.Sorted()));
            Assert.Equal(oa.Pack, ob.Pack);
        }

        [Fact]
        public void NondeterministicConverter_IsCaught()
        {
            using TempDataRepo repo = BlocksRepo(TwoBlocks);
            repo.Write("data/schema/arena.schema.json", TempDataRepo.Schema("arena.schema.json", "  \"type\": \"object\""));
            repo.Write("data/sim/arena.json", "{}\n");
            var (o, d) = Run(repo, Modules(new TestBlocksConverter(), new DriftingConverter()));
            Assert.Null(o.Pack);
            Assert.Contains(d.Sorted(), e => e.Contains("arena", StringComparison.Ordinal) && e.Contains("비결정적", StringComparison.Ordinal));
        }

        [Fact]
        public void MissingConverter_IsAnError()
        {
            using TempDataRepo repo = BlocksRepo(TwoBlocks);
            var (o, d) = Run(repo, Modules());
            Assert.Null(o.Pack);
            Assert.Contains(d.Sorted(), e => e.StartsWith("data/sim/blocks.json: ", StringComparison.Ordinal) && e.Contains("변환기", StringComparison.Ordinal));
        }

        [Fact]
        public void SchemaViolation_IsReportedWithPointer_AndStopsConversion()
        {
            using TempDataRepo repo = BlocksRepo("{ \"blocks\": [ { \"id\": \"Block.Fireball\", \"speed\": 4.7 } ] }\n");
            var (o, d) = Run(repo, Modules(new TestBlocksConverter()));
            Assert.Null(o.Pack);
            Assert.Contains(d.Sorted(), e => e.StartsWith("data/sim/blocks.json:/blocks/0/id: ", StringComparison.Ordinal));
        }

        [Fact]
        public void MissingSchema_UnknownFile_DuplicateKey_BadJson_AreErrors()
        {
            using TempDataRepo repo = BlocksRepo(TwoBlocks);
            repo.Write("data/sim/arena.json", "{}\n");                                   // 스키마가 없다
            repo.Write("data/sim/whatever.json", "{}\n");                                // 절 목차에 없다
            repo.Write("data/rules/net.json", "{ \"a\": 1, \"a\": 2 }\n");               // 같은 열쇠
            repo.Write("data/rules/fees.json", "{ \"a\": 1, }\n");                       // 끝 쉼표
            repo.Write("data/stray.json", "{}\n");                                       // data/ 바로 아래 파일
            var (o, d) = Run(repo, Modules(new TestBlocksConverter()));
            Assert.Null(o.Pack);
            IReadOnlyList<string> e = d.Sorted();
            Assert.Contains(e, x => x.StartsWith("data/sim/arena.json: ", StringComparison.Ordinal) && x.Contains("arena.schema.json", StringComparison.Ordinal));
            Assert.Contains(e, x => x.StartsWith("data/sim/whatever.json: ", StringComparison.Ordinal));
            Assert.Contains(e, x => x.StartsWith("data/rules/net.json:/a: ", StringComparison.Ordinal));
            Assert.Contains(e, x => x.StartsWith("data/rules/fees.json: ", StringComparison.Ordinal));
            Assert.Contains(e, x => x.StartsWith("data/stray.json: ", StringComparison.Ordinal));
        }

        [Fact]
        public void SchemaFile_MustDeclareDraftAndId()
        {
            using TempDataRepo repo = BlocksRepo(TwoBlocks);
            repo.Write("data/schema/blocks.schema.json", "{ \"$schema\": \"https://json-schema.org/draft/2020-12/schema\", \"type\": \"object\" }\n");
            var (o, d) = Run(repo, Modules(new TestBlocksConverter()));
            Assert.Null(o.Pack);
            Assert.Contains(d.Sorted(), x => x.StartsWith("data/schema/blocks.schema.json: ", StringComparison.Ordinal) && x.Contains("$id", StringComparison.Ordinal));
        }

        [Fact]
        public void ConverterDataBuildException_BecomesOneLine()
        {
            using TempDataRepo repo = BlocksRepo("{ \"blocks\": [ { \"id\": \"block.fireball\", \"speed\": 1000000000000 } ] }\n");
            var (o, d) = Run(repo, Modules(new TestBlocksConverter()));
            Assert.Null(o.Pack);
            Assert.Contains(d.Sorted(), x => x.StartsWith("data/sim/blocks.json: ", StringComparison.Ordinal) && x.Contains("64비트", StringComparison.Ordinal));
        }

        [Fact]
        public void Registry_RejectsTwoConvertersForOneSection_AndOrdersById()
        {
            Assert.Throws<DataBuildException>(() => Modules(new TestBlocksConverter(), new TestBlocksConverter()));
            ModuleRegistry m = Modules(new DriftingConverter(), new TestBlocksConverter());
            Assert.Equal(new[] { ContentSection.Blocks, ContentSection.Arena }, m.Converters.Select(c => c.Section).ToArray());
        }

        [Fact]
        public void Registry_DiscoversTheNameKeyValidator()
        {
            ModuleRegistry m = ModuleRegistry.Discover();
            Assert.Contains(m.Validators, v => v is NameKeyCoverage);
        }

        [Fact]
        public void RepoData_PassesCheck()
        {
            var d = new Diagnostics();
            Pipeline.Output o = Pipeline.Run(Wizard.TestKit.TestRepo.Root, ModuleRegistry.Discover(), PackOptions.Default(PackOptions.DefaultDataVersion), d);
            Assert.False(d.HasErrors, string.Join("\n", d.Sorted()));
            Assert.NotNull(o.Pack);
            ContentPack.Load(o.Pack!);
        }
    }
}

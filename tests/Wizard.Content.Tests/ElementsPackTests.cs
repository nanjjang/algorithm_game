using System;
using System.Collections.Generic;
using Wizard.Content.Tests.Pack;
using Wizard.Core;
using Wizard.DataBuild.Core;
using Wizard.TestKit;
using Xunit;

namespace Wizard.Content.Tests
{
    // data/sim/elements.json → 팩 절 19 → ContentDb.ElementRules 의 왕복. 기대값은 spec 02 2.2 표(1.25 → 5,368,709,120, 0.8 → 3,435,973,837)와
    // 3.3(AF-1, AF-8), DECISIONS 2.5 에서 온다. 적마다의 약점·저항 배치(상성 표)는 적 데이터의 몫이라 여기에 없다.
    // 아래 벡터는 상성 표에서 slime_moss 한 줄(약점 Fire, 저항 Frost = Element.Ice)을 시험 입력으로 쓴다.
    public sealed class ElementsPackTests
    {
        private const long WeakRaw = 5_368_709_120L;           // 1.25 × 2^32 (spec 02 2.2)
        private const long ResistRaw = 3_435_973_837L;         // 0.8 × 2^32 = 3,435,973,836.8 → 반올림 (spec 02 2.2)

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
        public void RoundTrip_EveryField_IsTheSpecInteger()
        {
            ElementDef e = RepoDb().ElementRules;
            Assert.Equal(WeakRaw, e.WeakMultiplier.Raw);
            Assert.Equal(ResistRaw, e.ResistMultiplier.Raw);
        }

        [Fact]
        public void SectionMissing_Throws()
        {
            ContentDb db = ContentDb.Build(ContentPack.Load(PackWriter.Write(PackWriter.SchemaVersion, 1, new byte[PackWriter.CoreCompatLength], new PackSection[0])));
            Assert.Throws<ContentException>(() => db.ElementRules);
        }

        [Fact]
        public void SameData_SameSectionBytes()
        {
            byte[] s = Section(RepoPack.Value);
            Assert.Equal(16, s.Length);                           // i64 × 2
            Assert.Equal(s, Section(BuildPack(TestRepo.Root)));
        }

        [Fact]
        public void Problems_Schema_And_CrossChecks()
        {
            using (TempDataRepo repo = Repo("{ \"weak_multiplier\": 1.25 }"))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/elements.json") && p.Contains("resist_multiplier"));
            }
            using (TempDataRepo repo = Repo("{ \"weak_multiplier\": 1.25, \"resist_multiplier\": 0.8, \"extra\": 1 }"))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/elements.json") && p.Contains("extra"));
            }
            using (TempDataRepo repo = Repo("{ \"weak_multiplier\": 0.9, \"resist_multiplier\": 0.8 }"))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/elements.json:/weak_multiplier"));
            }
            using (TempDataRepo repo = Repo("{ \"weak_multiplier\": 1.25, \"resist_multiplier\": 1.1 }"))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/elements.json:/resist_multiplier"));
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
            // 상성이 뒤집힌 값(약점 ≤ 1, 저항 ≥ 1 또는 ≤ 0)
            Assert.Throws<ContentException>(() => Load(Bytes(Fix64.OneRaw, ResistRaw)));
            Assert.Throws<ContentException>(() => Load(Bytes(WeakRaw, Fix64.OneRaw)));
            Assert.Throws<ContentException>(() => Load(Bytes(WeakRaw, 0)));
            Load(good);
        }

        // ── 명세 벡터: spec 02 16.2 (데이터로 시험할 수 있는 앞의 넷) ──

        [Fact]
        public void S02_TV_B01__FireOnFireWeak_Is1Point25()
        {
            ElementDef e = RepoDb().ElementRules;
            Assert.Equal(WeakRaw, e.Multiplier(Element.Fire, Element.Fire, Element.Ice).Raw);        // slime_moss: 약점 Fire, 저항 Frost
        }

        [Fact]
        public void S02_TV_B02__FrostOnFrostResist_Is0Point8()
        {
            ElementDef e = RepoDb().ElementRules;
            Assert.Equal(ResistRaw, e.Multiplier(Element.Ice, Element.Fire, Element.Ice).Raw);
        }

        [Fact]
        public void S02_TV_B03__ArcaneOnSlimeMoss_Is1()
        {
            ElementDef e = RepoDb().ElementRules;
            Assert.Equal(Fix64.OneRaw, e.Multiplier(Element.Arcane, Element.Fire, Element.Ice).Raw);
            Assert.Equal(Fix64.OneRaw, e.Multiplier(Element.Lightning, Element.Fire, Element.Ice).Raw);
            Assert.Equal(Fix64.OneRaw, e.Multiplier(Element.Dark, Element.Fire, Element.Ice).Raw);
        }

        [Fact]
        public void S02_TV_B04__NeutralDamage_Is1_EvenAgainstAnyWeakResist()
        {
            ElementDef e = RepoDb().ElementRules;
            Assert.Equal(Fix64.OneRaw, e.Multiplier(Element.None, Element.Fire, Element.Ice).Raw);
            // 약점이 무속성인 대상도 없지만(AF-8) 무속성 피해는 어떤 값으로도 배율을 받지 않는다
            Assert.Equal(Fix64.OneRaw, e.Multiplier(Element.None, Element.None, Element.None).Raw);
        }

        private static byte[] Bytes(long weak, long resist)
        {
            var w = new ByteWriter();
            w.I64(weak);
            w.I64(resist);
            return w.ToArray();
        }

        private static ContentDb Load(byte[] section) =>
            ContentDb.Build(ContentPack.Load(PackWriter.Write(PackWriter.SchemaVersion, 1, new byte[PackWriter.CoreCompatLength],
                new[] { new PackSection((ushort)ContentSection.Elements, section) })));

        private static byte[] Section(byte[] pack)
        {
            Assert.True(ContentPack.Load(pack).TryGetSection((ushort)ContentSection.Elements, out ReadOnlyMemory<byte> s));
            return s.ToArray();
        }

        private static TempDataRepo Repo(string elementsJson)
        {
            var repo = new TempDataRepo();
            repo.Write("data/schema/elements.schema.json", System.IO.File.ReadAllText(TestRepo.Path("data/schema/elements.schema.json")));
            repo.Write("data/sim/elements.json", elementsJson + "\n");
            return repo;
        }
    }
}

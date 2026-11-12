using System;
using System.Collections.Generic;
using Wizard.DataBuild.Core;
using Wizard.DataBuild.Names;
using Xunit;

namespace Wizard.DataBuild.Tests.Names
{
    // databuild check 의 "데이터의 모든 id 에 이름 열쇠"(G5, ARCHITECTURE 8.3). M1 부터 돈다.
    public sealed class NameKeyCoverageTests
    {
        private static IReadOnlyList<string> Validate(TempRepo repo)
        {
            var d = new Diagnostics();
            using (DataSet data = DataLoader.Load(repo.Root, d))
            {
                Assert.False(d.HasErrors, string.Join("\n", d.Sorted()));
                new NameKeyCoverage().Validate(data, d);
            }
            return d.Sorted();
        }

        private const string Blocks =
            "{ \"blocks\": [ { \"id\": \"block.fireball\", \"core\": \"block.magic_bolt\" }, { \"id\": \"block.magic_bolt\", \"nested\": { \"id\": \"block.pierce\" } } ] }\n";

        [Fact]
        public void MissingNameKey_IsReportedWithFileAndPointer()
        {
            using var repo = new TempRepo();
            repo.Write("data/sim/blocks.json", Blocks);
            repo.Write("data/names/names.ko.json", "{\n  \"block.fireball\": \"거대 화염구\",\n  \"~end\": \"\"\n}\n");
            IReadOnlyList<string> e = Validate(repo);
            Assert.Equal(2, e.Count);
            Assert.StartsWith("data/sim/blocks.json:/blocks/1/id: id 'block.magic_bolt' 의 이름 열쇠가", e[0]);
            Assert.StartsWith("data/sim/blocks.json:/blocks/1/nested/id: id 'block.pierce' 의 이름 열쇠가", e[1]);
        }

        [Fact]
        public void AllKeysPresent_Passes_ReferencesAreNotDefinitions()
        {
            using var repo = new TempRepo();
            repo.Write("data/sim/blocks.json", Blocks);
            repo.Write("data/names/names.ko.json",
                "{\n  \"block.fireball\": \"거대 화염구\",\n  \"block.magic_bolt\": \"마력탄\",\n  \"block.pierce\": \"관통\",\n  \"~end\": \"\"\n}\n");
            Assert.Empty(Validate(repo));
        }

        [Fact]
        public void IdGrammar_IsChecked()
        {
            using var repo = new TempRepo();
            repo.Write("data/rules/items.json", "{ \"items\": [ { \"id\": \"Mat.Stone\" }, { \"id\": \"" + new string('a', 30) + "." + new string('b', 40) + "\" } ] }\n");
            IReadOnlyList<string> e = Validate(repo);
            Assert.Equal(2, e.Count);
            Assert.Contains("문법", e[0]);
            Assert.Contains("문법", e[1]);
        }

        [Fact]
        public void PresentationFiles_AreNotCheckedForNameKeys()
        {
            using var repo = new TempRepo();
            repo.Write("data/presentation/event_bindings.json", "{ \"bindings\": [ { \"id\": \"binding.hit\" } ] }\n");
            Assert.Empty(Validate(repo));
        }

        [Fact]
        public void Runs_InDatabuildCheck_FromM1()
        {
            // 저장소의 data/ 에는 아직 데이터 파일이 없어도 이 검사는 등록되어 돈다(ModuleRegistry 가 찾는다)
            ModuleRegistry m = ModuleRegistry.Discover();
            Assert.Contains(m.Validators, v => v.Name == "name-keys" && v is NameKeyCoverage);
        }

        [Fact]
        public void CommandLine_CheckFailsOnMissingNameKey()
        {
            using var repo = new TempRepo();
            repo.Write("data/schema/items.schema.json", TempRepo.Schema("items.schema.json", "  \"type\": \"object\""));
            repo.Write("data/rules/items.json", "{ \"items\": [ { \"id\": \"mat.stone_low\" } ] }\n");
            var stdout = new System.IO.StringWriter();
            var stderr = new System.IO.StringWriter();
            int code = Program.Run(new[] { "check" }, stdout, stderr, repo.Root);
            Assert.Equal(Program.ExitProblems, code);
            Assert.Contains("data/rules/items.json:/items/0/id: id 'mat.stone_low' 의 이름 열쇠가", stderr.ToString());
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Wizard.TestKit;
using Xunit;

namespace Wizard.Sim.Tests.Runtime
{
    // CONTRACTS 3.2 "부분 클래스와 훅": 훅이 (단계, 모듈)마다 따로 선언되어 있고 표와 같다. 상태 훅 넷이 모듈마다 있다.
    // 한 단계 안의 호출 순서가 표의 위에서 아래 순이고, 단계는 0~12 순이다. 상태 절의 순서는 절 id 오름차순이다.
    // 구현이 없는 부분 메서드는 IL 에 남지 않으므로 선언과 호출은 소스에서 읽는다. 기대값은 CONTRACTS.md 의 표에서 읽는다.
    public sealed class HookTableTests
    {
        private const string CoreDir = "src/Wizard.Sim/Runtime/Core/";

        private sealed class Row
        {
            public readonly List<(string Name, int Id)> Sections = new List<(string, int)>();
            public readonly List<string> StageHooks = new List<string>();
        }

        // CONTRACTS 3.2 의 "| 모듈(절 id) | 폴더 | 훅 |" 표
        private static List<Row> ContractTable()
        {
            string[] lines = File.ReadAllLines(TestRepo.Path("docs/dev/CONTRACTS.md"));
            int start = Array.FindIndex(lines, l => l.StartsWith("| 모듈(절 id) |", StringComparison.Ordinal));
            Assert.True(start >= 0, "hook table header not found in CONTRACTS.md");
            var rows = new List<Row>();
            for (int i = start + 2; i < lines.Length && lines[i].StartsWith("|", StringComparison.Ordinal); i++)
            {
                string[] cells = lines[i].Split('|');
                var row = new Row();
                foreach (Match m in Regex.Matches(cells[1], @"(\w+)\((\d+)\)"))
                {
                    row.Sections.Add((m.Groups[1].Value, int.Parse(m.Groups[2].Value)));
                }
                foreach (Match m in Regex.Matches(cells[3], @"`(Stage\d\d_\w+)`"))
                {
                    row.StageHooks.Add(m.Groups[1].Value);
                }
                Assert.NotEmpty(row.Sections);
                rows.Add(row);
            }
            Assert.Equal(13, rows.Count);
            return rows;
        }

        private static string Source(string file) => File.ReadAllText(TestRepo.Path(CoreDir + file));

        private static int StageOf(string hook) => int.Parse(hook.Substring(5, 2));

        // 단계 오름차순, 같은 단계 안에서는 표의 위에서 아래
        private static List<string> ExpectedStageOrder(List<Row> rows)
        {
            var order = new List<string>();
            for (int stage = 0; stage <= 11; stage++)
            {
                foreach (Row row in rows)
                {
                    order.AddRange(row.StageHooks.Where(h => StageOf(h) == stage));
                }
            }
            return order;
        }

        private static List<(string Name, int Id)> SectionsById(List<Row> rows) =>
            rows.SelectMany(r => r.Sections).OrderBy(s => s.Id).ToList();

        [Fact]
        public void ContractTable_HasSeventeenSectionsInIdOrder()
        {
            List<(string Name, int Id)> sections = SectionsById(ContractTable());
            Assert.Equal(Enumerable.Range(1, 17), sections.Select(s => s.Id));
        }

        [Fact]
        public void StateSectionEnum_MatchesContractTable()
        {
            List<(string Name, int Id)> sections = SectionsById(ContractTable());
            Assert.Equal(sections.Count, Enum.GetValues(typeof(StateSection)).Length);
            foreach ((string name, int id) in sections)
            {
                Assert.True(Enum.TryParse(name, out StateSection value), "StateSection." + name + " missing");
                Assert.Equal(id, (int)value);
            }
        }

        [Fact]
        public void Declarations_AreExactlyTheContractHooks()
        {
            List<Row> rows = ContractTable();
            var expected = new SortedSet<string>(StringComparer.Ordinal);
            foreach (Row row in rows)
            {
                expected.UnionWith(row.StageHooks);
            }
            foreach ((string name, int _) in SectionsById(rows))
            {
                if (name == "Rng")
                {
                    continue;   // Runtime/Core 의 절: 훅 대신 SimWorld.Rng.cs 의 구현
                }
                expected.Add("Snapshot_Save_" + name);
                expected.Add("Snapshot_Load_" + name);
                expected.Add("State_Write_" + name);
                expected.Add("State_Read_" + name);
            }

            string hooks = Source("SimWorld.Hooks.cs");
            List<string> declared = Regex.Matches(hooks, @"partial\s+void\s+(\w+)\s*\(").Select(m => m.Groups[1].Value).ToList();
            Assert.Equal(declared.Count, declared.Distinct().Count());
            Assert.Equal(expected, new SortedSet<string>(declared, StringComparer.Ordinal));

            // 선언은 이 파일에만 있다(뼈대의 다른 파일이 훅을 더하지 않는다)
            foreach (string file in Directory.GetFiles(TestRepo.Path(CoreDir), "*.cs"))
            {
                if (Path.GetFileName(file) != "SimWorld.Hooks.cs")
                {
                    Assert.DoesNotMatch(@"partial\s+void\s", File.ReadAllText(file));
                }
            }
        }

        [Fact]
        public void StateHooks_HaveContractSignatures()
        {
            string hooks = Source("SimWorld.Hooks.cs");
            foreach (Match m in Regex.Matches(hooks, @"partial\s+void\s+(State_Write_\w+|State_Read_\w+|Snapshot_Save_\w+|Snapshot_Load_\w+)\s*\(([^)]*)\)"))
            {
                string name = m.Groups[1].Value;
                string args = Regex.Replace(m.Groups[2].Value.Trim(), @"\s+", " ");
                if (name.StartsWith("State_Write_", StringComparison.Ordinal))
                {
                    Assert.Equal("ref StateWriter w", args);
                }
                else if (name.StartsWith("State_Read_", StringComparison.Ordinal))
                {
                    Assert.Equal("ref StateReader r", args);
                }
                else
                {
                    Assert.Matches(@"^SimWorld \w+$", args);
                }
            }
        }

        [Fact]
        public void Step_CallsStageHooksInContractOrder()
        {
            List<Row> rows = ContractTable();
            string step = Source("SimWorld.Step.cs");
            List<string> calls = Regex.Matches(step, @"\b(Stage\d\d_\w+)\s*\(").Select(m => m.Groups[1].Value).ToList();

            // 1~11단계: Step 본문에 한 번씩, 표의 순서대로
            List<string> expected = ExpectedStageOrder(rows);
            Assert.Equal(expected.Where(h => StageOf(h) != 0), calls.Where(h => StageOf(h) != 0));

            // 0단계: 확인과 적용 두 자리에서 각각 표의 순서대로
            List<string> stage0 = expected.Where(h => StageOf(h) == 0).ToList();
            List<string> calls0 = calls.Where(h => StageOf(h) == 0).ToList();
            Assert.Equal(stage0.Concat(stage0), calls0);

            // 0단계(명령)는 1단계보다 먼저, 2단계는 입력 이벤트마다, 12단계(Tick += 1)는 마지막
            int body = step.IndexOf("public void Step(", StringComparison.Ordinal);
            int cmd = step.IndexOf("ApplyCommand(in commands[i], sink)", body, StringComparison.Ordinal);
            int s1 = step.IndexOf("Stage01_", body, StringComparison.Ordinal);
            int inputLoop = step.IndexOf("for (int i = 0; i < inputs.Length; i++)", body, StringComparison.Ordinal);
            int s2 = step.IndexOf("Stage02_Player(in inputs[i], sink)", body, StringComparison.Ordinal);
            int s3 = step.IndexOf("Stage03_", body, StringComparison.Ordinal);
            int s11 = step.LastIndexOf("Stage11_", StringComparison.Ordinal);
            int tick = step.IndexOf("_tick = unchecked(_tick + 1U);", body, StringComparison.Ordinal);
            Assert.True(body >= 0 && cmd > body && s1 > cmd && inputLoop > s1 && s2 > inputLoop && s3 > s2 && tick > s11,
                "Step stage layout differs from CONTRACTS 3.2");
        }

        [Fact]
        public void StateHooks_AreCalledInSectionIdOrder()
        {
            List<(string Name, int Id)> sections = SectionsById(ContractTable());
            string state = Source("SimWorld.State.cs");

            List<string> Calls(string prefix) =>
                Regex.Matches(state, @"\b" + prefix + @"(\w+)\s*\(").Select(m => m.Groups[1].Value).ToList();

            List<string> all = sections.Select(s => s.Name).ToList();
            List<string> withoutRng = all.Where(n => n != "Rng").ToList();
            Assert.Equal(all, Calls("State_Write_"));
            Assert.Equal(all, Calls("State_Read_"));
            Assert.Equal(withoutRng, Calls("Snapshot_Save_"));
            Assert.Equal(withoutRng, Calls("Snapshot_Load_"));
        }

        // 스칼라 절은 세계가 틀을 쓰고, 풀 절(Spell, Enemies, Telegraph, Bosses)은 모듈이 TryBeginPoolSection 으로 쓴다
        [Fact]
        public void PoolSections_AreFramedByModulesAndScalarSectionsByTheWorld()
        {
            string state = Source("SimWorld.State.cs");
            string[] pools = { "Spell", "Enemies", "Telegraph", "Bosses" };
            foreach ((string name, int _) in SectionsById(ContractTable()))
            {
                bool framed = Regex.IsMatch(state, @"BeginScalarSection\(ref w, StateSection\." + name + @"\);\s*State_Write_" + name + @"\(ref w\);")
                              && Regex.IsMatch(state, @"ExpectScalarSection\(ref r, StateSection\." + name + @"\);\s*State_Read_" + name + @"\(ref r\);");
                bool anyFrame = state.Contains("BeginScalarSection(ref w, StateSection." + name + ")")
                                || state.Contains("ExpectScalarSection(ref r, StateSection." + name + ")");
                if (pools.Contains(name))
                {
                    Assert.False(anyFrame, name + " is a pool section: the world must not frame it");
                }
                else
                {
                    Assert.True(framed, name + " is a scalar section: the world must frame it right before its hook");
                }
            }
        }
    }
}

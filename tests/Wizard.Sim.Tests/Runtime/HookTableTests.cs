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
    // 한 단계 안의 호출 순서가 표의 위에서 아래 순이고("단계 안 순서의 예외"에 적힌 훅만 그 단계의 맨 뒤), 단계는 0~12 순이다.
    // 상태 절의 순서는 절 id 오름차순이다. 이음 훅(T-50)은 "모듈 사이의 이음"의 표와 같고 Raise… 가 표의 순서로 부른다.
    // 뷰 훅(T-54)은 CONTRACTS 3.3 "뷰 훅"의 표와 같고 SimWorld.View.cs 의 Fill… 이 표의 순서로 부른다.
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

        // CONTRACTS 3.2 "단계 안 순서의 예외": "<N>단계에서는 `StageNN_X`를 그 단계의 맨 뒤에 부른다"
        private static List<string> LastInStageHooks()
        {
            string text = File.ReadAllText(TestRepo.Path("docs/dev/CONTRACTS.md"));
            int at = text.IndexOf("**단계 안 순서의 예외**", StringComparison.Ordinal);
            Assert.True(at >= 0, "the in-stage order exception paragraph was not found in CONTRACTS.md");
            int end = text.IndexOf('\n', at);
            string paragraph = text.Substring(at, end - at);
            List<string> hooks = Regex.Matches(paragraph, @"(\d+)단계에서는 `(Stage(\d\d)_\w+)`를 그 단계의 맨 뒤에 부른다")
                .Select(m =>
                {
                    Assert.Equal(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[3].Value));
                    return m.Groups[2].Value;
                })
                .ToList();
            Assert.Single(hooks);       // 계약은 예외가 하나뿐이라고 적었다
            return hooks;
        }

        private sealed class LinkRow
        {
            public string Raise = "";
            public readonly List<string> Hooks = new List<string>();
        }

        // CONTRACTS 3.2 의 "| 사건 | 내는 곳 | 뼈대의 문 | 이음 훅(부르는 순서) |" 표
        private static List<LinkRow> LinkTable()
        {
            string[] lines = File.ReadAllLines(TestRepo.Path("docs/dev/CONTRACTS.md"));
            int start = Array.FindIndex(lines, l => l.StartsWith("| 사건 | 내는 곳 | 뼈대의 문 |", StringComparison.Ordinal));
            Assert.True(start >= 0, "link hook table header not found in CONTRACTS.md");
            var rows = new List<LinkRow>();
            for (int i = start + 2; i < lines.Length && lines[i].StartsWith("|", StringComparison.Ordinal); i++)
            {
                string[] cells = lines[i].Split('|');
                var row = new LinkRow();
                Match raise = Regex.Match(cells[3], @"`(Raise\w+)\(PlayerSlot slot, ISimEventSink sink\)`");
                Assert.True(raise.Success, "link hook row without a Raise… door: " + lines[i]);
                row.Raise = raise.Groups[1].Value;
                foreach (Match m in Regex.Matches(cells[4], @"`(On\w+_\w+)`"))
                {
                    row.Hooks.Add(m.Groups[1].Value);
                }
                Assert.NotEmpty(row.Hooks);
                rows.Add(row);
            }
            Assert.NotEmpty(rows);
            return rows;
        }

        private sealed class ViewRow
        {
            public string Door = "";
            public string Parameters = "";
            public readonly List<string> Hooks = new List<string>();
        }

        // CONTRACTS 3.3 의 "| 뷰 | 뼈대의 문 | 뷰 훅(부르는 순서) |" 표
        private static List<ViewRow> ViewTable()
        {
            string[] lines = File.ReadAllLines(TestRepo.Path("docs/dev/CONTRACTS.md"));
            int start = Array.FindIndex(lines, l => l.StartsWith("| 뷰 | 뼈대의 문 | 뷰 훅(부르는 순서) |", StringComparison.Ordinal));
            Assert.True(start >= 0, "view hook table header not found in CONTRACTS.md");
            var rows = new List<ViewRow>();
            for (int i = start + 2; i < lines.Length && lines[i].StartsWith("|", StringComparison.Ordinal); i++)
            {
                string[] cells = lines[i].Split('|');
                var row = new ViewRow();
                Match door = Regex.Match(cells[2], @"`(Fill\w+)\(([^)]*)\)`");
                Assert.True(door.Success, "view hook row without a Fill… door: " + lines[i]);
                row.Door = door.Groups[1].Value;
                row.Parameters = door.Groups[2].Value;
                foreach (Match m in Regex.Matches(cells[3], @"`(View_\w+)`"))
                {
                    row.Hooks.Add(m.Groups[1].Value);
                }
                Assert.NotEmpty(row.Hooks);
                rows.Add(row);
            }
            Assert.NotEmpty(rows);
            return rows;
        }

        // "int index, ref PlayerViewDraft draft" → "index, ref draft"
        private static string CallArgs(string parameters) =>
            string.Join(", ", parameters.Split(',').Select(p =>
            {
                string[] words = p.Trim().Split(' ');
                string name = words[words.Length - 1];
                return words[0] == "ref" ? "ref " + name : name;
            }));

        private static string Source(string file) => File.ReadAllText(TestRepo.Path(CoreDir + file));

        private static int StageOf(string hook) => int.Parse(hook.Substring(5, 2));

        // 단계 오름차순, 같은 단계 안에서는 표의 위에서 아래. 예외로 적힌 훅은 그 단계의 맨 뒤
        private static List<string> ExpectedStageOrder(List<Row> rows)
        {
            List<string> last = LastInStageHooks();
            var order = new List<string>();
            for (int stage = 0; stage <= 11; stage++)
            {
                var inStage = new List<string>();
                foreach (Row row in rows)
                {
                    inStage.AddRange(row.StageHooks.Where(h => StageOf(h) == stage));
                }
                foreach (string hook in last.Where(h => StageOf(h) == stage))
                {
                    Assert.True(inStage.Remove(hook), hook + " is named by the exception but is not in the hook table");
                    inStage.Add(hook);
                }
                order.AddRange(inStage);
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

            foreach (LinkRow link in LinkTable())
            {
                expected.UnionWith(link.Hooks);
            }

            foreach (ViewRow view in ViewTable())
            {
                expected.UnionWith(view.Hooks);
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

        // T-50: 9단계에서 Graze 는 피격 후보를 넣는 모듈(Spell, Enemies, Duel)의 뒤다. 수락 조건 "피격 판정이 스침 판정보다 먼저다"의 순서 쪽
        [Fact]
        public void Step_JudgesGrazeAfterEveryOtherStage09Hook()
        {
            Assert.Equal(new[] { "Stage09_Graze" }, LastInStageHooks());
            string step = Source("SimWorld.Step.cs");
            List<string> stage9 = Regex.Matches(step, @"\b(Stage09_\w+)\s*\(").Select(m => m.Groups[1].Value).ToList();
            Assert.Equal(new[] { "Stage09_Spell", "Stage09_Enemies", "Stage09_Duel", "Stage09_Graze" }, stage9);
        }

        // 이음 훅: 선언의 시그니처가 계약과 같고, Raise… 가 표의 순서로 한 번씩 부른다. 다른 자리에서는 부르지 않는다
        [Fact]
        public void LinkHooks_AreRaisedInContractOrderFromOneDoor()
        {
            string hooks = Source("SimWorld.Hooks.cs");
            string step = Source("SimWorld.Step.cs");
            foreach (LinkRow link in LinkTable())
            {
                foreach (string hook in link.Hooks)
                {
                    Assert.Matches(@"partial\s+void\s+" + hook + @"\(PlayerSlot slot, ISimEventSink sink\);", hooks);
                }

                Match door = Regex.Match(step, @"internal void " + link.Raise + @"\(PlayerSlot slot, ISimEventSink sink\)\s*\{([^}]*)\}");
                Assert.True(door.Success, link.Raise + " is not in SimWorld.Step.cs with the contract signature");
                List<string> calls = Regex.Matches(door.Groups[1].Value, @"\b(On\w+_\w+)\(slot, sink\);").Select(m => m.Groups[1].Value).ToList();
                Assert.Equal(link.Hooks, calls);

                // 훅을 부르는 곳은 그 문 하나다(src/Wizard.Sim 전체에서 호출은 한 번, 나머지는 선언과 구현)
                foreach (string hook in link.Hooks)
                {
                    int callSites = 0;
                    foreach (string file in Directory.GetFiles(TestRepo.Path("src/Wizard.Sim"), "*.cs", SearchOption.AllDirectories))
                    {
                        string code = Regex.Replace(File.ReadAllText(file), @"//[^\n]*", "");   // 주석의 언급은 세지 않는다
                        callSites += Regex.Matches(code, @"(?<!partial void )\b" + hook + @"\(").Count;
                    }
                    Assert.Equal(1, callSites);
                }
            }
        }

        // 뷰 훅(T-54): 선언의 매개변수가 문의 것과 같고, Fill… 이 표의 순서로 한 번씩 부른다. 다른 자리에서는 부르지 않는다.
        // 한 뷰 안의 순서는 훅 표의 위에서 아래 순이고, 훅 이름의 모듈은 훅 표에 있는 모듈이다
        [Fact]
        public void ViewHooks_AreFilledInContractOrderFromOneDoor()
        {
            string hooks = Source("SimWorld.Hooks.cs");
            string doors = Source("SimWorld.View.cs");
            List<string> modules = ContractTable().SelectMany(r => r.Sections).Select(s => s.Name).ToList();
            var seenDoors = new HashSet<string>(StringComparer.Ordinal);
            foreach (ViewRow view in ViewTable())
            {
                Assert.True(seenDoors.Add(view.Door), view.Door + " is listed twice");
                string args = CallArgs(view.Parameters);
                foreach (string hook in view.Hooks)
                {
                    Assert.Matches(@"partial\s+void\s+" + hook + @"\(" + Regex.Escape(view.Parameters) + @"\);", hooks);
                    string module = hook.Substring(hook.LastIndexOf('_') + 1);
                    Assert.Contains(module, modules);
                }
                List<int> order = view.Hooks.Select(h => modules.IndexOf(h.Substring(h.LastIndexOf('_') + 1))).ToList();
                Assert.Equal(order.OrderBy(x => x), order);

                Match door = Regex.Match(doors, @"internal void " + view.Door + @"\(" + Regex.Escape(view.Parameters) + @"\)\s*\{([^}]*)\}");
                Assert.True(door.Success, view.Door + " is not in SimWorld.View.cs with the contract signature");
                List<string> calls = Regex.Matches(door.Groups[1].Value, @"\b(View_\w+)\(" + Regex.Escape(args) + @"\);")
                    .Select(m => m.Groups[1].Value).ToList();
                Assert.Equal(view.Hooks, calls);

                foreach (string hook in view.Hooks)
                {
                    int callSites = 0;
                    foreach (string file in Directory.GetFiles(TestRepo.Path("src/Wizard.Sim"), "*.cs", SearchOption.AllDirectories))
                    {
                        string code = Regex.Replace(File.ReadAllText(file), @"//[^\n]*", "");   // 주석의 언급은 세지 않는다
                        callSites += Regex.Matches(code, @"(?<!partial void )\b" + hook + @"\(").Count;
                    }
                    Assert.Equal(1, callSites);
                }
            }

            // 뼈대의 문은 표의 것뿐이다
            List<string> declaredDoors = Regex.Matches(doors, @"internal void (Fill\w+)\(").Select(m => m.Groups[1].Value).ToList();
            Assert.Equal(seenDoors.OrderBy(x => x, StringComparer.Ordinal), declaredDoors.OrderBy(x => x, StringComparer.Ordinal));
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

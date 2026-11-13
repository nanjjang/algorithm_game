using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using Xunit;

namespace Wizard.TaskGraph.Tests
{
    // FIX-7, DECISIONS-TECH T-65 7번(ledger)과 10번(recount)
    public sealed class LedgerCheckAndRecountTests
    {
        private static string Real(string rel) => File.ReadAllText(Path.Combine(Fx.RepoRoot, rel), Encoding.UTF8);

        // ---------- ledger ----------

        [Fact]
        public void Ledger_CurrentRepoDocuments_Pass()
        {
            LedgerCheck.Result r = LedgerCheck.Check(Real(Ledger.DecisionsPath), Real(Ledger.QuestionsPath), Real(Ledger.TasksPath));
            Assert.True(r.Ok, string.Join("\n", r.Errors));
            Assert.True(r.Sections > 0 && r.SummaryRows > 0 && r.Questions > 0);
            CliRun cli = CliRun.Run("--repo", Fx.RepoRoot, "ledger");
            Assert.True(cli.Code == 0, cli.ToString());
        }

        // 줄 하나를 lineNo(1부터) 앞에 넣는다
        private static (string Text, int Line) InsertBefore(string text, Func<string, bool> where, string line)
        {
            var lines = text.Split('\n').ToList();
            int at = lines.FindIndex(l => where(l));
            Assert.True(at >= 0);
            lines.Insert(at, line);
            return (string.Join("\n", lines), at + 1);
        }

        private static string FirstSectionLine(string decisions) => decisions.Split('\n').First(l => l.StartsWith("## T-", StringComparison.Ordinal));

        [Fact]
        public void Ledger_DuplicateSectionHeading_FailsAtThatLine()
        {
            string d = Real(Ledger.DecisionsPath);
            string first = FirstSectionLine(d);
            var (text, line) = InsertBefore(d, l => l.StartsWith("## 아직 정하지 않은 기술 항목", StringComparison.Ordinal), first);
            string number = first.Substring(3).Split(' ')[0];
            LedgerCheck.Result r = LedgerCheck.Check(text, Real(Ledger.QuestionsPath), Real(Ledger.TasksPath));
            Assert.Single(r.Errors);
            Assert.StartsWith($"{Ledger.DecisionsPath}:{line}: {number} 절 제목이 두 번 나온다", r.Errors[0]);
        }

        [Fact]
        public void Ledger_DuplicateSummaryRow_FailsAtThatLine()
        {
            string d = Real(Ledger.DecisionsPath);
            string row = d.Split('\n').First(l => l.StartsWith("| T-", StringComparison.Ordinal));
            var (text, line) = InsertBefore(d, l => l.StartsWith("| T-", StringComparison.Ordinal) && l != row, row);
            LedgerCheck.Result r = LedgerCheck.Check(text, Real(Ledger.QuestionsPath), Real(Ledger.TasksPath));
            Assert.Single(r.Errors);
            string number = row.Split('|')[1].Trim();
            Assert.StartsWith($"{Ledger.DecisionsPath}:{line}: 요약 표에 {number} 줄이 두 번 나온다", r.Errors[0]);
        }

        [Fact]
        public void Ledger_SummaryRowWithoutSection_Fails_SectionWithoutRowDoesNot()
        {
            string d = Real(Ledger.DecisionsPath);
            var (text, line) = InsertBefore(d, l => l.StartsWith("| T-01 |", StringComparison.Ordinal), "| T-998 | 절이 없는 요약 줄 | [신규] |");
            LedgerCheck.Result r = LedgerCheck.Check(text, Real(Ledger.QuestionsPath), Real(Ledger.TasksPath));
            Assert.Equal(new[] { $"{Ledger.DecisionsPath}:{line}: 요약 표의 T-998 줄에 맞는 '## T-998' 절이 없다" }, r.Errors.ToArray());

            var (text2, _) = InsertBefore(d, l => l.StartsWith("## 아직 정하지 않은 기술 항목", StringComparison.Ordinal), "## T-999 요약 줄이 없는 절\n");
            Assert.True(LedgerCheck.Check(text2, Real(Ledger.QuestionsPath), Real(Ledger.TasksPath)).Ok);
        }

        [Fact]
        public void Ledger_DuplicateQuestion_FailsAtThatLine()
        {
            string q = Real(Ledger.QuestionsPath);
            string row = q.Split('\n').First(l => l.StartsWith("| Q-", StringComparison.Ordinal));
            string number = row.Split('|')[1].Trim();
            string text = q.TrimEnd('\n') + "\n" + row + "\n";
            int line = text.Split('\n').Length - 1;
            LedgerCheck.Result r = LedgerCheck.Check(Real(Ledger.DecisionsPath), text, Real(Ledger.TasksPath));
            Assert.Single(r.Errors);
            Assert.StartsWith($"{Ledger.QuestionsPath}:{line}: {number} 줄이 두 번 나온다", r.Errors[0]);
        }

        [Theory]
        [InlineData("<<<<<<< HEAD")]
        [InlineData("||||||| base")]
        [InlineData(">>>>>>> 1234abc (feat(X): y)")]
        public void Ledger_ConflictMarker_FailsAtThatLine_InEachLedger(string marker)
        {
            string d = Real(Ledger.DecisionsPath), q = Real(Ledger.QuestionsPath), t = Real(Ledger.TasksPath);
            var (dm, dl) = InsertBefore(d, l => l == "## 요약", marker);
            Assert.Equal(new[] { $"{Ledger.DecisionsPath}:{dl}: 충돌 표식이 남아 있다('{marker.Split(' ')[0]}')" }, LedgerCheck.Check(dm, q, t).Errors.ToArray());
            var (qm, ql) = InsertBefore(q, l => l.StartsWith("| Q-", StringComparison.Ordinal), marker);
            Assert.Equal(new[] { $"{Ledger.QuestionsPath}:{ql}: 충돌 표식이 남아 있다('{marker.Split(' ')[0]}')" }, LedgerCheck.Check(d, qm, t).Errors.ToArray());
            var (tm, tl) = InsertBefore(t, l => l.Contains("\"tasks\"", StringComparison.Ordinal), marker);
            Assert.Equal(new[] { $"{Ledger.TasksPath}:{tl}: 충돌 표식이 남아 있다('{marker.Split(' ')[0]}')" }, LedgerCheck.Check(d, q, tm).Errors.ToArray());
        }

        [Fact]
        public void Ledger_Cli_FailsWithExitOne()
        {
            using TempRepo repo = TempRepo.Create();
            repo.Write(Ledger.DecisionsPath, "## T-01 하나\n\n## T-01 또 하나\n");
            repo.Write(Ledger.QuestionsPath, "| Q-01 | a |\n");
            repo.Write(Ledger.TasksPath, "{}\n");
            CliRun r = CliRun.In(repo, repo.PathOf(Ledger.TasksPath), "ledger");
            Assert.Equal(1, r.Code);
            Assert.Contains($"FAIL  {Ledger.DecisionsPath}:3: T-01 절 제목이 두 번 나온다(앞: 1줄)", r.Stdout);
        }

        // ---------- recount ----------

        private static JsonObject M2Graph(int cards)
        {
            var tasks = Enumerable.Range(1, cards)
                .Select(i => Fx.Task($"M2-X-{i:00}", milestone: "M2", group: i % 2 == 0 ? "M2-W02" : "M2-W01", paths: new[] { $"src/m2/{i}/" }))
                .ToArray();
            return Fx.Doc(tasks);
        }

        // 두 브랜치가 같은 마일스톤에 카드를 하나씩 더해 git 이 task_count 를 충돌 없이 틀리게 합친 꼴(14 → 둘 다 15, 맞는 값 16)
        [Fact]
        public void Recount_CatchesAndFixesSilentlyWrongTaskCount()
        {
            JsonObject doc = M2Graph(16);
            ((JsonObject)((JsonArray)doc["milestones"]!)[0]!)["task_count"] = 15;
            using TempRepo repo = TempRepo.Create(git: false);
            string tasks = repo.PathOf(Ledger.TasksPath);
            repo.Write(Ledger.TasksPath, Fx.Text(doc));
            byte[] before = File.ReadAllBytes(tasks);

            CliRun check = CliRun.Run("--repo", Fx.RepoRoot, "--tasks", tasks, "recount", "--check");
            Assert.Equal(1, check.Code);
            Assert.Contains("FAIL  milestone M2: task_count 15 → 16", check.Stdout);
            Assert.Equal(before, File.ReadAllBytes(tasks));

            CliRun fix = CliRun.Run("--repo", Fx.RepoRoot, "--tasks", tasks, "recount");
            Assert.True(fix.Code == 0, fix.ToString());
            string after = File.ReadAllText(tasks);
            Assert.Equal(Fx.Text(M2Graph(16)), after);
            // 바뀐 줄은 task_count 하나뿐이다
            var changed = after.Split('\n').Zip(Encoding.UTF8.GetString(before).Split('\n')).Where(p => p.First != p.Second).ToList();
            Assert.Single(changed);
            Assert.Equal("   \"task_count\": 16,", changed[0].First);

            Assert.Equal(0, CliRun.Run("--repo", Fx.RepoRoot, "--tasks", tasks, "recount", "--check").Code);
            DateTime stamp = File.GetLastWriteTimeUtc(tasks);
            File.SetLastWriteTimeUtc(tasks, stamp.AddMinutes(-5));
            CliRun same = CliRun.Run("--repo", Fx.RepoRoot, "--tasks", tasks, "recount");
            Assert.Contains("마일스톤 집계가 카드와 같다", same.Stdout);
            Assert.Equal(stamp.AddMinutes(-5), File.GetLastWriteTimeUtc(tasks)); // 바뀐 것이 없으면 쓰지 않는다
        }

        [Fact]
        public void Recount_ParallelGroups_KeepsOrder_DropsEmpty_InsertsMissingByAddRule()
        {
            JsonObject doc = M2Graph(4);
            ((JsonArray)doc["tasks"]!).Add(Fx.Task("M2-X-09", milestone: "M2", group: "M2-W00", paths: new[] { "src/m2/9/" }));
            ((JsonArray)doc["tasks"]!).Add(Fx.Task("M2-X-10", milestone: "M2", group: "M2-W09", status: "blocked-on-assets", paths: new[] { "src/m2/10/" }));
            var m = (JsonObject)((JsonArray)doc["milestones"]!)[0]!;
            m["parallel_groups"] = Fx.Arr(new[] { "M2-W02", "M2-W05", "M2-W01" });   // 이름순이 아니고, W05 는 비었다
            var changes = Recount.Apply(doc);
            Assert.Equal(new[] { "M2-W02", "M2-W01", "M2-W00", "M2-W09" }, ((JsonArray)m["parallel_groups"]!).Select(x => (string)x!).ToArray());
            Assert.Equal(6, (int)m["task_count"]!);
            Assert.Equal(1, (int)m["blocked_on_assets"]!);
            Assert.Equal(3, changes.Count);

            // 이름순 목록이면 이름순 자리에 넣는다(taskgraph add 와 같다)
            m["parallel_groups"] = Fx.Arr(new[] { "M2-W01", "M2-W02" });
            Recount.Apply(doc);
            Assert.Equal(new[] { "M2-W00", "M2-W01", "M2-W02", "M2-W09" }, ((JsonArray)m["parallel_groups"]!).Select(x => (string)x!).ToArray());
            Assert.True(GraphValidator.Validate(Fx.Parse(doc), Fx.SchemaPath, null).Ok);
        }
    }
}

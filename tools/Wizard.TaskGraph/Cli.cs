using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;

namespace Wizard.TaskGraph
{
    // taskgraph <명령> [인자] [옵션]   (PIPELINE 3·4절, DECISIONS-TECH T-40·T-41·T-65)
    //
    //   validate                 tasks.json 의 스키마와 그래프 규칙(check-tasks.mjs 와 같은 것)을 본다.
    //                            경로 겹침은 개정 장부를 세지 않고, 둘 다 <base>에 병합되지 않은 쌍에만 건다(T-65 2·4번)
    //   ready [--json]           deps 가 전부 done 인 todo 작업을 parallel_group 별로 낸다
    //   status [<id>]            진행 상태(상태 파일이 tasks.json 의 처음 분류를 덮은 값)
    //   start <id>               .claude/worktrees/<id> 에 task/<id> 작업 트리를 만들고 todo → in_progress
    //   review <id>              in_progress → review
    //   done <id>                review → done. main 에 그 작업의 커밋이 있어야 하고, 작업 트리와 브랜치를 지운다
    //   reopen <id>              review → in_progress (반려, 병합 게이트 실패)
    //   unblock <id>             blocked-on-assets → todo (에셋 반입)
    //   reconcile                상태 파일을 main 의 커밋 기록(<type>(<id>): …)과 남은 작업 트리에서 다시 만든다
    //   card <id>                PIPELINE 3.4절의 작업 카드(WT 는 절대 경로)
    //   add <파일.json | ->      CCR·FIX 작업을 검사를 거쳐 tasks.json 에 더한다. 자동 번호("FIX", "CCR")는 next-ids 의 최댓값 + 1
    //   check-diff <id>          task/<id> 의 변경이 owned_paths 와 공유 파일 규칙 안인가
    //   next-ids [--json]        <base>, 모든 task/* 브랜치의 끝, 모든 작업 트리의 작업 파일에서 쓰인 T·Q·FIX·CCR 번호의 최댓값과 다음 번호
    //   ledger                   개정 장부의 번호 겹침(T 절 제목, 요약 줄, Q 줄)과 충돌 표식을 본다
    //   recount [--check]        마일스톤의 task_count·blocked_on_assets·parallel_groups 를 카드에서 다시 센다(--check 는 쓰지 않고 다르면 실패)
    //   resolve                  --repo(기본: 이 도구가 놓인 작업 트리)의 색인에서 충돌한 개정 장부를 푼다(T-65 9번). 장부가 아닌 충돌은
    //                            건드리지 않고 "남은 충돌"로 적는다. 하나라도 풀지 못하면 파일도 색인도 그대로 두고 1. rebase --continue 는 부르지 않는다
    //
    // 병합 기록: validate·add·resolve 는 --base <ref>(기본 main)의 커밋 제목 <type>(<id>): … 에서 병합된 작업을 읽는다.
    //   --no-history 는 기록을 보지 않는다(전부 병합 전, 엄격). 기록을 읽지 못하면 실패하지 않고 엄격하게 보며 요약에 적는다.
    //   읽는 곳은 이 명령 줄 층뿐이다(검사 함수는 집합을 인자로 받는다). 환경 변수는 보지 않는다.
    //
    // 옵션: --repo <저장소>  --tasks <tasks.json>  --specs <명세 폴더>  --schema <스키마>  --state <상태 파일>
    //       --no-vectors(validate·add·resolve 에서 벡터 배정을 보지 않는다)  --base <ref>(기본 main)  --head <ref>(check-diff, 기본 task/<id>)
    //       --no-history(validate·add·resolve)  --check(recount)  --json(ready, next-ids)
    // 종료 코드: 0 통과, 1 검사 실패·허용되지 않는 일, 2 사용법·입력 오류
    public static class Cli
    {
        public static int Run(string[] args, TextWriter o, TextWriter e)
        {
            try
            {
                CliOptions opt = CliOptions.Parse(args);
                if (opt.Positional.Count == 0 || opt.Positional[0] == "help" || opt.Positional[0] == "-h")
                {
                    Usage(opt.Positional.Count == 0 ? e : o);
                    return opt.Positional.Count == 0 ? TaskGraphException.UsageExitCode : 0;
                }
                string cmd = opt.Positional[0];
                RepoLayout layout = RepoLayout.Resolve(opt);
                switch (cmd)
                {
                    case "validate": return Validate(layout, opt, o);
                    case "ready": return Ready(layout, opt, o);
                    case "status": return Status(layout, opt, o);
                    case "start": return Print(o, new Lifecycle(layout, TaskDoc.Load(layout.TasksPath)).Start(Id(opt), opt.Base));
                    case "review": return Print(o, new Lifecycle(layout, TaskDoc.Load(layout.TasksPath)).Transition(Id(opt), "in_progress", "review"));
                    case "reopen": return Print(o, new Lifecycle(layout, TaskDoc.Load(layout.TasksPath)).Transition(Id(opt), "review", "in_progress"));
                    case "unblock": return Print(o, new Lifecycle(layout, TaskDoc.Load(layout.TasksPath)).Transition(Id(opt), "blocked-on-assets", "todo"));
                    case "done": return Print(o, new Lifecycle(layout, TaskDoc.Load(layout.TasksPath)).Done(Id(opt), opt.Base));
                    case "reconcile": return Reconcile(layout, opt, o);
                    case "card": return Card(layout, opt, o);
                    case "add": return Add(layout, opt, o);
                    case "check-diff": return CheckDiff(layout, opt, o);
                    case "next-ids": return NextIdsCmd(layout, opt, o);
                    case "ledger": return LedgerCmd(layout, o);
                    case "recount": return RecountCmd(layout, opt, o);
                    case "resolve": return ResolveCmd(layout, opt, o);
                    default:
                        throw new TaskGraphException($"모르는 명령 '{cmd}'", TaskGraphException.UsageExitCode);
                }
            }
            catch (TaskGraphException ex)
            {
                e.WriteLine("taskgraph: " + ex.Message);
                return ex.ExitCode;
            }
        }

        private static void Usage(TextWriter w)
        {
            w.WriteLine("usage: taskgraph validate | ready [--json] | status [<id>] | start <id> | review <id> | done <id> | reopen <id> | unblock <id>");
            w.WriteLine("                 | reconcile | card <id> | add <file.json|-> | check-diff <id> [--base <ref>] [--head <ref>]");
            w.WriteLine("                 | next-ids [--json] | ledger | recount [--check] | resolve");
            w.WriteLine("       options: --repo <dir> --tasks <file> --specs <dir> --schema <file> --state <file> --no-vectors");
            w.WriteLine("                --base <ref> (merge history, default main) --no-history (treat every task as unmerged)");
        }

        private static string Id(CliOptions opt)
        {
            if (opt.Positional.Count < 2)
            {
                throw new TaskGraphException($"{opt.Positional[0]} 에는 작업 id 가 있어야 한다", TaskGraphException.UsageExitCode);
            }
            if (opt.Positional.Count > 2)
            {
                throw new TaskGraphException($"인자가 너무 많다: {string.Join(" ", opt.Positional.Skip(2))}", TaskGraphException.UsageExitCode);
            }
            return opt.Positional[1];
        }

        private static int Print(TextWriter o, string text)
        {
            o.WriteLine(text);
            return 0;
        }

        private static int Validate(RepoLayout layout, CliOptions opt, TextWriter o)
        {
            TaskDoc doc = TaskDoc.Load(layout.TasksPath);
            MergeHistory history = MergeHistoryReader.Read(opt, new Git(layout.MainRepo), doc);
            ValidationReport r = GraphValidator.Validate(doc, layout.SchemaPath, opt.NoVectors ? null : layout.SpecsDir, history);
            r.Print(o);
            return r.Ok ? 0 : TaskGraphException.FailureExitCode;
        }

        private static int Ready(RepoLayout layout, CliOptions opt, TextWriter o)
        {
            var groups = new Lifecycle(layout, TaskDoc.Load(layout.TasksPath)).Ready();
            if (opt.Json)
            {
                var arr = new JsonArray();
                foreach ((string g, IReadOnlyList<TaskDef> ts) in groups)
                {
                    var items = new JsonArray();
                    foreach (TaskDef t in ts)
                    {
                        items.Add(new JsonObject
                        {
                            ["id"] = t.Id,
                            ["owner_agent"] = t.OwnerAgent,
                            ["owner_model"] = t.OwnerModel,
                            ["reviewer_model"] = t.ReviewerModel,
                            ["title"] = t.Title,
                        });
                    }
                    arr.Add(new JsonObject { ["parallel_group"] = g, ["tasks"] = items });
                }
                o.Write(JsStyleJson.Serialize(arr, 1));
                return 0;
            }
            if (groups.Count == 0)
            {
                o.WriteLine("시작할 수 있는 작업이 없다");
                return 0;
            }
            foreach ((string g, IReadOnlyList<TaskDef> ts) in groups)
            {
                o.WriteLine(g);
                foreach (TaskDef t in ts)
                {
                    o.WriteLine($"  {t.Id}  {t.OwnerAgent} ({t.OwnerModel}, 검토 {t.ReviewerModel})  {t.Title}");
                }
            }
            return 0;
        }

        private static int Status(RepoLayout layout, CliOptions opt, TextWriter o)
        {
            TaskDoc doc = TaskDoc.Load(layout.TasksPath);
            var st = new StateStore(layout.StatePath).Read();
            IEnumerable<TaskDef> list = opt.Positional.Count >= 2 ? new[] { doc.Get(opt.Positional[1]) } : doc.Tasks;
            foreach (TaskDef t in list)
            {
                o.WriteLine($"{t.Id}\t{Lifecycle.Effective(t, st)}");
            }
            return 0;
        }

        private static int Reconcile(RepoLayout layout, CliOptions opt, TextWriter o)
        {
            IReadOnlyList<string> log = new Lifecycle(layout, TaskDoc.Load(layout.TasksPath)).Reconcile(opt.Base);
            foreach (string l in log)
            {
                o.WriteLine(l);
            }
            o.WriteLine($"reconcile: 바뀐 것 {log.Count(l => !l.StartsWith("경고", StringComparison.Ordinal))}개, 상태 파일 {layout.StatePath}");
            return 0;
        }

        private static int Card(RepoLayout layout, CliOptions opt, TextWriter o)
        {
            TaskDoc doc = TaskDoc.Load(layout.TasksPath);
            TaskDef t = doc.Get(Id(opt));
            o.Write(CardWriter.Write(doc, t, layout, new StateStore(layout.StatePath).Read()));
            return 0;
        }

        private static int Add(RepoLayout layout, CliOptions opt, TextWriter o)
        {
            string src = Id(opt);
            string input = src == "-" ? Console.In.ReadToEnd() : File.Exists(src)
                ? File.ReadAllText(src, Encoding.UTF8)
                : throw new TaskGraphException($"더할 작업 파일이 없다: {src}", TaskGraphException.UsageExitCode);
            if (!File.Exists(layout.TasksPath))
            {
                throw new TaskGraphException($"작업 그래프가 없다: {layout.TasksPath}", TaskGraphException.UsageExitCode);
            }
            // 다른 브랜치·작업 트리에서 쓰인 번호(T-65 6번). 잠그기 전에 읽는다(이 작업 트리의 tasks.json 도 그 가운데 하나다).
            // 잠근 뒤 읽는 자기 문서의 번호도 같이 보므로 그사이에 더해진 카드의 번호도 빠지지 않는다
            var git = new Git(layout.MainRepo);
            IReadOnlyDictionary<string, int>? usedMax = null;
            try
            {
                usedMax = NextIds.MaxByKind(NextIds.Scan(NextIdsReader.Read(git, opt.Base)));
            }
            catch (TaskGraphException ex)
            {
                o.WriteLine($"add: 다른 브랜치·작업 트리의 번호를 읽지 못했다({ex.Message}). 이 문서의 번호만 본다");
            }
            // tasks.json 을 배타로 잠그고 읽고·검사하고·제자리에 쓴다(동시에 둘이 더해도 하나가 사라지지 않는다)
            using FileStream fs = OpenExclusive(layout.TasksPath);
            var ms = new MemoryStream();
            fs.CopyTo(ms);
            string current = new UTF8Encoding(false).GetString(ms.ToArray());
            MergeHistory history = MergeHistoryReader.Read(opt, git, TaskDoc.Parse(current, layout.TasksPath));
            AddResult r = TaskAdder.Prepare(current, layout.TasksPath, input, layout.SchemaPath, opt.NoVectors ? null : layout.SpecsDir, history, usedMax);
            if (!r.Report.Ok)
            {
                r.Report.Print(o);
                o.WriteLine($"add: {r.Id} 을 더하면 검사에 실패한다. tasks.json 은 그대로다");
                return TaskGraphException.FailureExitCode;
            }
            byte[] bytes = new UTF8Encoding(false).GetBytes(r.NewText);
            fs.Position = 0;
            fs.SetLength(0);
            fs.Write(bytes, 0, bytes.Length);
            fs.Flush(true);
            o.WriteLine($"add: {r.Id} 을 더했다({layout.TasksPath})");
            return 0;
        }

        private static FileStream OpenExclusive(string path)
        {
            StateStore.EnsureLockingEnabled();
            DateTime deadline = DateTime.UtcNow + StateStore.LockTimeout;
            while (true)
            {
                try
                {
                    return new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                }
                catch (IOException) when (DateTime.UtcNow < deadline)
                {
                    Thread.Sleep(15);
                }
                catch (IOException ex)
                {
                    throw new TaskGraphException($"{path} 의 잠금을 잡지 못했다 — {ex.Message}");
                }
            }
        }

        private static int NextIdsCmd(RepoLayout layout, CliOptions opt, TextWriter o)
        {
            IReadOnlyList<NextIds.Entry> entries = NextIds.Scan(NextIdsReader.Read(new Git(layout.MainRepo), opt.Base));
            o.Write(opt.Json ? NextIds.Json(entries) : NextIds.Text(entries));
            return 0;
        }

        private static int LedgerCmd(RepoLayout layout, TextWriter o)
        {
            string? Read(string rel)
            {
                string p = Path.Combine(layout.ToolRoot, rel);
                return File.Exists(p) ? File.ReadAllText(p, Encoding.UTF8) : null;
            }
            LedgerCheck.Result r = LedgerCheck.Check(Read(Ledger.DecisionsPath), Read(Ledger.QuestionsPath), Read(Ledger.TasksPath));
            foreach (string e in r.Errors)
            {
                o.WriteLine("FAIL  " + e);
            }
            o.WriteLine(r.SummaryLine);
            o.WriteLine(r.Ok ? "통과" : $"실패 {r.Errors.Count}건");
            return r.Ok ? 0 : TaskGraphException.FailureExitCode;
        }

        private static int RecountCmd(RepoLayout layout, CliOptions opt, TextWriter o)
        {
            TaskDoc doc = TaskDoc.Load(layout.TasksPath);
            IReadOnlyList<string> changes = Recount.Apply(doc.Root);
            if (changes.Count == 0)
            {
                o.WriteLine("recount: 마일스톤 집계가 카드와 같다");
                return 0;
            }
            if (opt.Check)
            {
                foreach (string c in changes)
                {
                    o.WriteLine("FAIL  " + c);
                }
                o.WriteLine($"recount --check: 마일스톤 집계가 카드와 {changes.Count}군데 다르다(taskgraph recount 로 고친다)");
                return TaskGraphException.FailureExitCode;
            }
            File.WriteAllText(layout.TasksPath, doc.Serialize(), new UTF8Encoding(false));
            foreach (string c in changes)
            {
                o.WriteLine(c);
            }
            o.WriteLine($"recount: {changes.Count}군데를 고쳐 썼다({layout.TasksPath})");
            return 0;
        }

        // 색인의 충돌(git ls-files -u)을 읽어 장부만 푼다(T-65 9번)
        private static int ResolveCmd(RepoLayout layout, CliOptions opt, TextWriter o)
        {
            var git = new Git(layout.ToolRoot);
            string listing = git.RunChecked("ls-files", "-u", "-z");
            var stages = new SortedDictionary<string, Dictionary<int, string>>(StringComparer.Ordinal);
            foreach (string entry in listing.Split('\0'))
            {
                int tab = entry.IndexOf('\t');
                if (tab < 0)
                {
                    continue;
                }
                string[] meta = entry.Substring(0, tab).Split(' ');
                string path = entry.Substring(tab + 1);
                if (meta.Length < 3 || !int.TryParse(meta[2], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int stage))
                {
                    continue;
                }
                if (!stages.TryGetValue(path, out Dictionary<int, string>? s))
                {
                    s = new Dictionary<int, string>();
                    stages[path] = s;
                }
                s[stage] = meta[1];
            }
            List<string> remaining = stages.Keys.Where(p => !Ledger.IsLedger(p)).ToList();
            void PrintRemaining()
            {
                foreach (string p in remaining)
                {
                    o.WriteLine("남은 충돌(장부가 아니다. 건드리지 않았다): " + p);
                }
            }
            var conflicted = new Dictionary<string, LedgerResolver.Stages>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, Dictionary<int, string>> kv in stages.Where(kv => Ledger.IsLedger(kv.Key)))
            {
                string? Blob(int n) => kv.Value.TryGetValue(n, out string? sha) ? git.RunChecked("cat-file", "blob", sha) : null;
                conflicted[kv.Key] = new LedgerResolver.Stages(Blob(1), Blob(2), Blob(3));
            }
            if (conflicted.Count == 0)
            {
                o.WriteLine("resolve: 충돌한 장부가 없다");
                PrintRemaining();
                return 0;
            }
            string? ReadWorking(string rel)
            {
                string p = Path.Combine(layout.ToolRoot, rel);
                return File.Exists(p) ? File.ReadAllText(p, Encoding.UTF8) : null;
            }
            var historyGit = new Git(layout.MainRepo);
            LedgerResolver.Result r = LedgerResolver.Resolve(conflicted, ReadWorking, layout.SchemaPath, opt.NoVectors ? null : layout.SpecsDir,
                doc => MergeHistoryReader.Read(opt, historyGit, doc), Path.Combine(layout.ToolRoot, "artifacts", "taskgraph", "resolve"));
            if (!r.Ok)
            {
                foreach (string c in r.Conflicts)
                {
                    o.WriteLine("FAIL  " + c);
                }
                o.WriteLine($"resolve: 장부의 충돌 {r.Conflicts.Count}건을 풀지 못했다(진짜 충돌). 작업 파일과 색인은 그대로다. 이 브랜치를 tech-lead 에게 돌려보낸다");
                PrintRemaining();
                return TaskGraphException.FailureExitCode;
            }
            foreach (KeyValuePair<string, string> kv in r.Resolved.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            {
                File.WriteAllText(Path.Combine(layout.ToolRoot, kv.Key), kv.Value, new UTF8Encoding(false));
            }
            var add = new List<string> { "add", "--" };
            add.AddRange(r.Resolved.Keys.OrderBy(k => k, StringComparer.Ordinal));
            git.RunChecked(add.ToArray());
            foreach (string p in r.Resolved.Keys.OrderBy(k => k, StringComparer.Ordinal))
            {
                o.WriteLine("resolve: 풀었다 — " + p);
            }
            PrintRemaining();
            o.WriteLine(remaining.Count == 0
                ? "resolve: 장부의 충돌을 다 풀고 git add 했다. 다음: command git -C <작업 트리> rebase --continue"
                : $"resolve: 장부의 충돌을 다 풀고 git add 했다. 장부가 아닌 충돌 {remaining.Count}개가 남았다(PIPELINE 3.8절의 표대로 다룬다)");
            return 0;
        }

        private static int CheckDiff(RepoLayout layout, CliOptions opt, TextWriter o)
        {
            TaskDoc doc = TaskDoc.Load(layout.TasksPath);
            TaskDef t = doc.Get(Id(opt));
            string head = opt.Head ?? RepoLayout.BranchOf(t.Id);
            var checker = new DiffChecker(new Git(layout.MainRepo), Path.Combine(layout.ToolRoot, "artifacts", "taskgraph"), Regenerator.Defaults());
            DiffReport r = checker.Check(t, opt.Base, head);
            r.Print(o);
            return r.Passed ? 0 : TaskGraphException.FailureExitCode;
        }
    }
}

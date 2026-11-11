using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;

namespace Wizard.TaskGraph
{
    // taskgraph <명령> [인자] [옵션]   (PIPELINE 3·4절, DECISIONS-TECH T-40·T-41)
    //
    //   validate                 tasks.json 의 스키마와 그래프 규칙(check-tasks.mjs 와 같은 것)을 본다
    //   ready [--json]           deps 가 전부 done 인 todo 작업을 parallel_group 별로 낸다
    //   status [<id>]            진행 상태(상태 파일이 tasks.json 의 처음 분류를 덮은 값)
    //   start <id>               .claude/worktrees/<id> 에 task/<id> 작업 트리를 만들고 todo → in_progress
    //   review <id>              in_progress → review
    //   done <id>                review → done. main 에 그 작업의 커밋이 있어야 하고, 작업 트리와 브랜치를 지운다
    //   reopen <id>              review → in_progress (반려, 병합 게이트 실패)
    //   unblock <id>             blocked-on-assets → todo (에셋 반입)
    //   reconcile                상태 파일을 main 의 커밋 기록(<type>(<id>): …)과 남은 작업 트리에서 다시 만든다
    //   card <id>                PIPELINE 3.4절의 작업 카드(WT 는 절대 경로)
    //   add <파일.json | ->      CCR·FIX 작업을 검사를 거쳐 tasks.json 에 더한다
    //   check-diff <id>          task/<id> 의 변경이 owned_paths 와 공유 파일 규칙 안인가
    //
    // 옵션: --repo <저장소>  --tasks <tasks.json>  --specs <명세 폴더>  --schema <스키마>  --state <상태 파일>
    //       --no-vectors(validate·add 에서 벡터 배정을 보지 않는다)  --base <ref>(기본 main)  --head <ref>(check-diff, 기본 task/<id>)
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
            w.WriteLine("       options: --repo <dir> --tasks <file> --specs <dir> --schema <file> --state <file> --no-vectors");
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
            ValidationReport r = GraphValidator.Validate(doc, layout.SchemaPath, opt.NoVectors ? null : layout.SpecsDir);
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
            // tasks.json 을 배타로 잠그고 읽고·검사하고·제자리에 쓴다(동시에 둘이 더해도 하나가 사라지지 않는다)
            using FileStream fs = OpenExclusive(layout.TasksPath);
            var ms = new MemoryStream();
            fs.CopyTo(ms);
            string current = new UTF8Encoding(false).GetString(ms.ToArray());
            AddResult r = TaskAdder.Prepare(current, layout.TasksPath, input, layout.SchemaPath, opt.NoVectors ? null : layout.SpecsDir);
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

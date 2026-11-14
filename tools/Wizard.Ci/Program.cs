using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Wizard.Ci
{
    // CI 의 로직(DECISIONS-TECH T-40, PIPELINE 4.6). tools/ci/*.sh 는 bash 3.2 껍데기다.
    //
    //   ci stage fast|full|integration [--all]   그 단계까지의 검사를 돈다(ci.sh 가 부른다). 결과 artifacts/ci/<단계>.json
    //   ci gate M<N>                             fast·full·integration 전부(바뀐 경로로 줄이지 않는다) + docs/dev/gates/M<N>.json
    //   ci run <스크립트>                         검사 하나(tools/ci/ 또는 tools/ci/lib/). 결과 artifacts/ci/run.json
    //   ci changed [--base <rev>]                바뀐 경로(비교 기준과 이유를 같이)
    //   ci affected-tests [--base <rev>]         돌릴 테스트 프로젝트
    //   ci test-affected [--base <rev>]          그 테스트 프로젝트를 돌린다(TRX 는 WIZARD_CI_TRX_DIR 또는 artifacts/ci/tests)
    //   ci trx-check <폴더> [--expect <수>|solution]  dotnet test 의 TRX 판정: 없음, 파일 수가 다름, 전체 0, 실패, 건너뜀이면 1(TrxCheck.cs)
    //   ci unity-scope [--base <rev>]            Unity 단계가 필요하면 0, 건너뛰어도 되면 3(이유를 출력)
    //   ci require unity|il2cpp|x64-dotnet|docker …   없으면 무엇이 없는지와 사용자가 할 일을 적고 1
    //   ci precommit                             pre-commit 훅의 검사(올린 파일)
    //   ci commit-msg <파일>                      commit-msg 훅의 검사
    //   ci commit-msgs [--task <id>] [--base <rev>]   갈림점..HEAD 의 커밋 메시지 형식(병합 제외. --task 면 첫 줄의 id 도 그 작업이어야 한다)
    //   ci exec --name <n> --timeout <초> --out <json> -- <명령> [인자…]   명령 하나를 시간 제한 안에서
    //   공통: --root <저장소>(없으면 이 도구가 놓인 곳에서 위로 찾는다)
    //   --all 또는 WIZARD_CI_FORCE_ALL=1: 바뀐 경로로 줄이지 않는다. WIZARD_CI_BASE: 비교 기준(기본 merge-base HEAD main)
    // 환경 변수(WIZARD_CI_BASE, WIZARD_CI_FORCE_ALL, WIZARD_CI_NOTE, WIZARD_CI_TRX_DIR, WIZARD_CI_LOCK_WAIT, CI_STEP_TIMEOUT, 기기 요구의 변수)는
    // 이 명령 줄 층(Run 의 env)에서만 읽어 인자로 넘긴다. 판정 함수(ChangeScope, BranchCommits, StageRunner, Requirements)는 프로세스 환경을
    // 직접 읽지 않는다: 바깥 게이트의 WIZARD_CI_BASE 가 임시 저장소를 보는 자체 시험에 새지 않게 하려는 것이다(FIX-4).
    // 비교 기준의 우선순위: --base, 없으면 WIZARD_CI_BASE(빈 값은 없는 것), 없으면 main 과의 갈림점(commit-msgs 는 main).
    // 종료 코드: 0 통과, 1 검사 실패, 2 사용법·환경 오류, 3 unity-scope 의 "건너뛰어도 된다", 124 시간 제한.
    public static class Program
    {
        public const int ExitSkip = 3;

        public static int Main(string[] args)
        {
            try
            {
                return Run(args, Console.Out, Console.Error, Environment.GetEnvironmentVariable);
            }
            catch (CiException e)
            {
                Console.Error.WriteLine("ci: " + e.Message);
                return e.ExitCode;
            }
        }

        // env: 환경 변수를 읽는 함수. Main 만 프로세스 환경을 준다(시험은 사전을 준다)
        public static int Run(string[] args, TextWriter stdout, TextWriter stderr, Func<string, string?> env)
        {
            // 도구의 출력은 줄 끝이 LF 다(T-67 5번). 받은 쓰개가 OS 기본(Windows 의 CRLF)이어도 같은 바이트를 낸다
            stdout.NewLine = "\n";
            stderr.NewLine = "\n";
            if (args.Length == 0)
            {
                throw new CiException("usage: ci stage|gate|run|changed|affected-tests|test-affected|unity-scope|require|precommit|commit-msg|commit-msgs|exec … (Program.cs 머리말)");
            }
            string cmd = args[0];
            if (cmd == "exec")
            {
                return Exec(args, stdout);
            }
            var rest = new List<string>();
            string? rootOpt = null;
            string? baseRef = null;
            bool all = env("WIZARD_CI_FORCE_ALL") == "1";
            for (int i = 1; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--root": rootOpt = Value(args, ref i); break;
                    case "--base": baseRef = Value(args, ref i); break;
                    case "--all": all = true; break;
                    default: rest.Add(args[i]); break;
                }
            }
            baseRef ??= NullIfEmpty(env("WIZARD_CI_BASE"));
            string root = RepoPaths.Resolve(rootOpt);
            switch (cmd)
            {
                case "stage":
                {
                    string stage = One(rest, "ci stage fast|full|integration");
                    using CiLock l = CiLock.Acquire(root, LockWait(env), stdout);
                    var runner = new StageRunner(root, stdout, all, baseRef, StepTimeout(env), BashLocator.FromEnv(env));
                    List<StageResult> results = runner.RunUpTo(stage);
                    return Summarize(stage, results, stdout);
                }
                case "gate":
                {
                    string ms = One(rest, "ci gate M<N>");
                    if (!System.Text.RegularExpressions.Regex.IsMatch(ms, "^M[0-9]+$"))
                    {
                        throw new CiException("마일스톤은 M<N> 꼴이다: " + ms);
                    }
                    // 먼저 마일스톤이 있는지 본다(오래 도는 검사 뒤에 이름이 틀렸다고 알리지 않게)
                    GateFile.HumanChecks(File.ReadAllText(Path.Combine(root, "docs", "dev", "tasks.json"), Encoding.UTF8), ms);
                    using CiLock l = CiLock.Acquire(root, LockWait(env), stdout);
                    var runner = new StageRunner(root, stdout, true, baseRef, StepTimeout(env), BashLocator.FromEnv(env));
                    List<StageResult> results = runner.RunUpTo("integration");
                    string? branch = Git.IsRepo(root) ? Git.CurrentBranch(root) : null;
                    string? commit = Git.IsRepo(root) ? Git.RevParse(root, "HEAD") : null;
                    GateFile.Write(root, ms, results, branch, commit);
                    stdout.WriteLine("ci gate " + ms + ": 게이트 파일 " + RepoPaths.Relative(root, GateFile.PathFor(root, ms)));
                    return Summarize("gate " + ms, results, stdout);
                }
                case "run":
                {
                    string name = One(rest, "ci run <스크립트>");
                    using CiLock l = CiLock.Acquire(root, LockWait(env), stdout);
                    var runner = new StageRunner(root, stdout, all, baseRef, StepTimeout(env), BashLocator.FromEnv(env));
                    StageResult r = runner.RunSingle(name);
                    return r.Passed ? 0 : 1;
                }
                case "changed":
                {
                    None(rest, "ci changed [--base <rev>]");
                    ChangeScope scope = ChangeScope.Detect(root, baseRef, all);
                    stdout.WriteLine("# " + (scope.All ? "전부: " : "") + scope.Reason);
                    foreach (string p in scope.Paths)
                    {
                        stdout.WriteLine(p);
                    }
                    return 0;
                }
                case "affected-tests":
                {
                    None(rest, "ci affected-tests [--base <rev>]");
                    AffectedTests a = AffectedTests.Select(ProjectGraph.Load(root), ChangeScope.Detect(root, baseRef, all));
                    stdout.WriteLine("# " + (a.All ? "전부: " : "") + a.Reason);
                    foreach (string p in a.Projects)
                    {
                        stdout.WriteLine(p);
                    }
                    return 0;
                }
                case "test-affected":
                {
                    None(rest, "ci test-affected [--base <rev>]");
                    AffectedTests a = AffectedTests.Select(ProjectGraph.Load(root), ChangeScope.Detect(root, baseRef, all));
                    stdout.WriteLine("test-affected: " + (a.All ? "전부 — " : "") + a.Reason);
                    string? note = env("WIZARD_CI_NOTE");
                    if (a.Projects.Count == 0)
                    {
                        string why = "바뀐 프로젝트에 딸린 테스트 프로젝트가 없다(" + a.Reason + ")";
                        stdout.WriteLine("test-affected: " + why);
                        if (!string.IsNullOrEmpty(note))
                        {
                            File.AppendAllText(note, "skipped: " + why + "\n");
                        }
                        return 0;
                    }
                    if (!string.IsNullOrEmpty(note))
                    {
                        File.AppendAllText(note, (a.All ? "전부: " : "") + a.Reason + " → 테스트 프로젝트 " + a.Projects.Count.ToString(CultureInfo.InvariantCulture) + "개\n");
                    }
                    string trx = env("WIZARD_CI_TRX_DIR") is string t && t.Length > 0
                        ? t
                        : Path.Combine(root, "artifacts", "ci", "tests");
                    return AffectedTests.Run(root, a.Projects, trx, stdout, TimeSpan.FromMinutes(20));
                }
                case "trx-check":
                {
                    const string usage = "ci trx-check <TRX 폴더> [--expect <수>|--expect solution]";
                    int? expected = null;
                    if (rest.Count == 3 && rest[1] == "--expect")
                    {
                        if (rest[2] == "solution")
                        {
                            expected = AffectedTests.Select(ProjectGraph.Load(root), ChangeScope.Everything("trx-check")).Projects.Count;
                        }
                        else if (int.TryParse(rest[2], NumberStyles.None, CultureInfo.InvariantCulture, out int n) && n > 0)
                        {
                            expected = n;
                        }
                        else
                        {
                            throw new CiException("usage: " + usage);
                        }
                    }
                    else if (rest.Count != 1)
                    {
                        throw new CiException("usage: " + usage);
                    }
                    return TrxCheck.Run(rest[0], expected, stdout, stderr);
                }
                case "unity-scope":
                {
                    None(rest, "ci unity-scope [--base <rev>]");
                    bool needed = UnityScope.Needed(ChangeScope.Detect(root, baseRef, all), out string reason);
                    stdout.WriteLine((needed ? "needed: " : "skip: ") + reason);
                    return needed ? 0 : ExitSkip;
                }
                case "require":
                {
                    if (rest.Count == 0)
                    {
                        throw new CiException("usage: ci require " + string.Join("|", Requirements.Known) + " …");
                    }
                    int missing = 0;
                    foreach (string what in rest)
                    {
                        string? msg = Requirements.Check(what, root, env);
                        if (msg == null)
                        {
                            stdout.WriteLine("ok    " + what);
                        }
                        else
                        {
                            stderr.WriteLine("FAIL  " + msg);
                            missing++;
                        }
                    }
                    return missing == 0 ? 0 : 1;
                }
                case "precommit":
                    None(rest, "ci precommit");
                    return PreCommit.Run(root, stdout, BashLocator.FromEnv(env));
                case "commit-msg":
                {
                    string file = One(rest, "ci commit-msg <메시지 파일>");
                    var ids = TaskIds(root);
                    List<string> problems = CommitMessage.Check(File.ReadAllText(file, Encoding.UTF8), ids);
                    foreach (string p in problems)
                    {
                        stderr.WriteLine("FAIL  " + p);
                    }
                    if (problems.Count > 0)
                    {
                        stderr.WriteLine("commit-msg: 커밋을 거절한다. 형식은 CLAUDE.md 10절:\n  <type>(<작업 id>): <요약>\n\n  <본문>\n\n  " + CommitMessage.CoAuthors[0]);
                        return 1;
                    }
                    stdout.WriteLine("commit-msg: ok");
                    return 0;
                }
                case "commit-msgs":
                {
                    string? taskId = null;
                    if (rest.Count == 2 && rest[0] == "--task")
                    {
                        taskId = rest[1];
                    }
                    else
                    {
                        None(rest, "ci commit-msgs [--task <id>] [--base <rev>]");
                    }
                    List<BranchCommits.Problem> problems = BranchCommits.Check(root, baseRef, taskId, TaskIds(root), out int count, out string range);
                    foreach (BranchCommits.Problem p in problems)
                    {
                        stderr.WriteLine("FAIL  " + p);
                    }
                    string summary = "commit-msgs: " + range + " 커밋 " + count.ToString(CultureInfo.InvariantCulture) + "개(병합 커밋 제외), 문제 " +
                                     problems.Count.ToString(CultureInfo.InvariantCulture) + "건";
                    if (problems.Count > 0)
                    {
                        stderr.WriteLine(summary + ". 형식은 CLAUDE.md 10절. 고치려면 그 커밋의 메시지를 다시 쓴다(git rebase 로 reword, 또는 git commit --amend)");
                        return 1;
                    }
                    stdout.WriteLine(summary);
                    return 0;
                }
                default:
                    throw new CiException("알 수 없는 명령 '" + cmd + "' (Program.cs 머리말)");
            }
        }

        public static HashSet<string> TaskIds(string root)
        {
            string path = Path.Combine(root, "docs", "dev", "tasks.json");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            System.Text.Json.Nodes.JsonNode? doc;
            try
            {
                doc = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path, Encoding.UTF8));
            }
            catch (Exception e) when (e is IOException || e is System.Text.Json.JsonException)
            {
                throw new CiException("작업 id 를 읽지 못했다(" + path + "): " + e.Message);
            }
            if (doc?["tasks"] is System.Text.Json.Nodes.JsonArray tasks)
            {
                foreach (System.Text.Json.Nodes.JsonNode? t in tasks)
                {
                    if ((string?)t?["id"] is string id)
                    {
                        ids.Add(id);
                    }
                }
            }
            return ids;
        }

        private static int Summarize(string what, List<StageResult> results, TextWriter stdout)
        {
            int passed = 0, failed = 0, skipped = 0;
            foreach (StageResult r in results)
            {
                passed += r.PassedCount;
                failed += r.FailedCount;
                skipped += r.SkippedCount;
            }
            stdout.WriteLine("ci " + what + " 합계: " + passed.ToString(CultureInfo.InvariantCulture) + " passed, " + failed.ToString(CultureInfo.InvariantCulture) +
                             " failed, " + skipped.ToString(CultureInfo.InvariantCulture) + " skipped");
            foreach (StageResult r in results)
            {
                foreach (StepResult s in r.Steps)
                {
                    if (!s.Passed)
                    {
                        stdout.WriteLine("  FAIL  [" + r.Stage + "] " + s.Script.Name + " (exit " + s.ExitCode.ToString(CultureInfo.InvariantCulture) + (s.TimedOut ? ", 시간 제한" : "") + ")");
                    }
                    else if (s.Skipped)
                    {
                        stdout.WriteLine("  SKIP  [" + r.Stage + "] " + s.Script.Name + ": " + s.SkipReason);
                    }
                }
            }
            return failed == 0 ? 0 : 1;
        }

        private static TimeSpan LockWait(Func<string, string?> env)
        {
            string? v = env("WIZARD_CI_LOCK_WAIT");
            return TimeSpan.FromSeconds(v != null && int.TryParse(v, NumberStyles.None, CultureInfo.InvariantCulture, out int s) ? s : 3600);
        }

        // 검사 하나의 기본 시간 제한(초). CI_STEP_TIMEOUT 이 양의 정수면 그것(스크립트의 "# ci-timeout:" 이 먼저다)
        private static int StepTimeout(Func<string, string?> env)
        {
            string? t = env("CI_STEP_TIMEOUT");
            return t != null && int.TryParse(t, NumberStyles.None, CultureInfo.InvariantCulture, out int s) && s > 0 ? s : StageRunner.DefaultStepTimeoutSeconds;
        }

        private static string? NullIfEmpty(string? s) => string.IsNullOrEmpty(s) ? null : s;

        private static string One(List<string> rest, string usage)
        {
            if (rest.Count != 1)
            {
                throw new CiException("usage: " + usage);
            }
            return rest[0];
        }

        private static void None(List<string> rest, string usage)
        {
            if (rest.Count != 0)
            {
                throw new CiException("usage: " + usage + " (남은 인자: " + string.Join(" ", rest) + ")");
            }
        }

        private static string Value(string[] args, ref int i)
        {
            if (i + 1 >= args.Length)
            {
                throw new CiException(args[i] + " 뒤에 값이 없다");
            }
            i++;
            return args[i];
        }

        // ci exec --name <n> --timeout <초> --out <json> -- <명령> [인자…]: 명령 하나를 시간 제한 안에서 돌리고 결과를 json 으로 남긴다.
        private static int Exec(string[] args, TextWriter stdout)
        {
            string name = "step";
            int timeoutSec = StageRunner.DefaultStepTimeoutSeconds;
            string? outPath = null;
            int i = 1;
            for (; i < args.Length; i++)
            {
                string a = args[i];
                if (a == "--")
                {
                    i++;
                    break;
                }
                switch (a)
                {
                    case "--name": name = Value(args, ref i); break;
                    case "--timeout":
                        if (!int.TryParse(Value(args, ref i), NumberStyles.None, CultureInfo.InvariantCulture, out timeoutSec) || timeoutSec <= 0)
                        {
                            throw new CiException("--timeout 은 양의 정수(초)다");
                        }
                        break;
                    case "--out": outPath = Value(args, ref i); break;
                    default: throw new CiException("ci exec: 알 수 없는 인자 " + a);
                }
            }
            if (i >= args.Length)
            {
                throw new CiException("ci exec: -- 뒤에 명령이 없다");
            }
            var req = new ProcRequest(args[i], new ArraySegment<string>(args, i + 1, args.Length - i - 1))
            {
                Name = name,
                Timeout = TimeSpan.FromSeconds(timeoutSec),
                Echo = stdout,
                EchoError = stdout,
            };
            ProcResult r = ProcessRunner.Run(req);
            if (outPath != null)
            {
                JsonFile.Write(outPath, w =>
                {
                    w.WriteStartObject();
                    w.WriteString("name", name);
                    w.WriteNumber("exit_code", r.ExitCode);
                    w.WriteBoolean("passed", r.ExitCode == 0);
                    w.WriteBoolean("timed_out", r.TimedOut);
                    w.WriteNumber("seconds", Math.Round(r.Seconds, 1));
                    w.WriteEndObject();
                });
            }
            return r.ExitCode;
        }
    }
}

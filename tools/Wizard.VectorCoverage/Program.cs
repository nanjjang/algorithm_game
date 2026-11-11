using System;
using System.Collections.Generic;
using System.IO;
using Wizard.TestKit.Shared;
using Wizard.TestKit.Shared.Vectors;

namespace Wizard.VectorCoverage
{
    // 벡터 대응 검사기(PIPELINE 4.6·7.1 G1, CLAUDE.md 8절).
    //
    //   vector-coverage                         전역 검사: 명세 벡터마다 정확히 한 작업에 배정, 끝난 작업의 벡터마다 테스트,
    //                                           벡터 테스트 이름이 명세의 벡터를 가리킴. 명세별로 빠진 벡터 수를 보고한다
    //   vector-coverage --task <id>             그 작업의 vectors 가 가리킨 벡터마다 테스트가 있는가
    //            [--trx <파일|폴더>]…           dotnet test --logger trx 의 결과로 그 테스트가 통과했는지도 본다
    //   공통: --root <저장소>  --tasks <tasks.json>  --state <taskstate.json>
    //
    // 저장소 뿌리는 현재 폴더가 아니라 이 도구가 놓인 곳에서 위로 올라가 찾는다(Wizard.sln + docs/spec). 작업 트리의 도구는
    // 그 작업 트리를 본다. 벡터 id 를 모으는 규칙은 tests/vectors.config.json 이다.
    // 종료 코드: 0 통과, 1 검사 실패(무엇이 어디서 틀렸는지 FAIL 줄로 알린다), 2 사용법·환경 오류.
    public static class Program
    {
        public static int Main(string[] args)
        {
            try
            {
                return Run(args, Console.Out, Console.Error);
            }
            catch (CoverageException e)
            {
                Console.Error.WriteLine("vector-coverage: " + e.Message);
                return e.ExitCode;
            }
        }

        public static int Run(string[] args, TextWriter stdout, TextWriter stderr)
        {
            string? root = null;
            string? tasksPath = null;
            string? statePath = null;
            string? task = null;
            var trx = new List<string>();
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                switch (a)
                {
                    case "--root": root = Value(args, ref i); break;
                    case "--tasks": tasksPath = Value(args, ref i); break;
                    case "--state": statePath = Value(args, ref i); break;
                    case "--task": task = Value(args, ref i); break;
                    case "--trx": trx.Add(Path.GetFullPath(Value(args, ref i))); break;
                    case "-h":
                    case "--help":
                        stdout.WriteLine("usage: vector-coverage [--task <id> [--trx <file|dir>]…] [--root <dir>] [--tasks <tasks.json>] [--state <taskstate.json>]");
                        return 0;
                    default:
                        throw new CoverageException("알 수 없는 인자: " + a + " (--help)", CoverageException.UsageExitCode);
                }
            }
            if (trx.Count > 0 && task == null)
            {
                throw new CoverageException("--trx 는 --task 와 같이 쓴다", CoverageException.UsageExitCode);
            }
            root = root != null ? Path.GetFullPath(root) : FindRoot();
            VectorConfig config;
            try
            {
                config = VectorConfig.LoadFromRepo(root);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is Wizard.TestKit.Shared.Golden.GoldenFormatException)
            {
                throw new CoverageException("tests/vectors.config.json 을 읽지 못했다: " + e.Message, CoverageException.UsageExitCode);
            }
            SpecVectorScan scan = SpecVectorScanner.ScanRepo(root, config);
            IReadOnlyList<TaskEntry> tasks = TaskEntry.Load(tasksPath ?? Path.Combine(root, "docs", "dev", "tasks.json"));
            TestIndex tests = TestIndex.ScanRepo(root, config.TestName);

            CoverageCheck check;
            if (task != null)
            {
                TrxResults? results = trx.Count > 0 ? TrxResults.Load(trx, config.TestName) : null;
                check = CoverageCheck.ForTask(task, scan, tasks, tests, results);
            }
            else
            {
                string state = statePath ?? TaskState.DefaultPath(root);
                check = CoverageCheck.Global(scan, tasks, TaskState.ReadDone(state), tests);
                stdout.WriteLine("저장소 " + root + ", 상태 파일 " + state);
            }
            foreach (string line in check.Lines)
            {
                stdout.WriteLine(line);
            }
            foreach (string w in check.Warnings)
            {
                stdout.WriteLine("warn  " + w);
            }
            foreach (string f in check.Failures)
            {
                stderr.WriteLine("FAIL  " + f);
            }
            stdout.WriteLine(check.Passed ? "vector-coverage: ok" : "vector-coverage: " + check.Failures.Count + " problem(s)");
            return check.Passed ? 0 : CoverageException.CheckFailedExitCode;
        }

        public static string FindRoot()
        {
            try
            {
                return RepoRoot.Find(AppContext.BaseDirectory);
            }
            catch (DirectoryNotFoundException e)
            {
                throw new CoverageException(e.Message + " (--root <저장소>로 알려 준다)", CoverageException.UsageExitCode);
            }
        }

        private static string Value(string[] args, ref int i)
        {
            if (i + 1 >= args.Length)
            {
                throw new CoverageException(args[i] + " 뒤에 값이 없다", CoverageException.UsageExitCode);
            }
            i++;
            return args[i];
        }
    }
}

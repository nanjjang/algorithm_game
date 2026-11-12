using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Wizard.Ci
{
    // ci.sh fast 의 "바뀐 프로젝트의 테스트"(PIPELINE 4.6). 바뀐 경로를 가진 프로젝트와 그것을 (전이로) 참조하는 프로젝트 가운데
    // 테스트 프로젝트를 고른다. 줄일 근거가 없으면 줄이지 않는다:
    //   - ChangeScope.All(게이트, main, 기준 없음)
    //   - 어느 프로젝트에도 속하지 않는 경로가 바뀜(Directory.Build.props, data/, docs/spec/, tests/vectors.config.json, tools/ci/ …):
    //     어느 테스트가 그것을 읽는지 알 수 없다
    // art/ 와 unity/ 는 dotnet 테스트가 읽지 않는다(Unity 쪽은 full 의 Unity 단계가 본다).
    public sealed class AffectedTests
    {
        public static readonly string[] NotDotnet = { "art/", "unity/" };

        private AffectedTests(bool all, string reason, IReadOnlyList<string> projects)
        {
            All = all;
            Reason = reason;
            Projects = projects;
        }

        public bool All { get; }
        public string Reason { get; }

        // 돌릴 테스트 프로젝트(저장소 기준 csproj 경로, 오름차순). All 이면 그래프의 모든 테스트 프로젝트다
        public IReadOnlyList<string> Projects { get; }

        public static AffectedTests Select(ProjectGraph graph, ChangeScope scope)
        {
            var allTests = new List<string>();
            foreach (ProjectNode n in graph.Nodes.Values)
            {
                if (n.IsTest)
                {
                    allTests.Add(n.Path);
                }
            }
            if (scope.All)
            {
                return new AffectedTests(true, scope.Reason, allTests);
            }
            var owners = new SortedSet<string>(StringComparer.Ordinal);
            foreach (string p in scope.Paths)
            {
                if (Array.Exists(NotDotnet, x => p.StartsWith(x, StringComparison.Ordinal)))
                {
                    continue;
                }
                ProjectNode? owner = graph.Owner(p);
                if (owner == null)
                {
                    return new AffectedTests(true, "프로젝트 밖의 경로 " + p + " 이 바뀌었다(어느 테스트가 읽는지 알 수 없어 전부 돈다)", allTests);
                }
                owners.Add(owner.Path);
            }
            var tests = new List<string>();
            foreach (string p in graph.WithDependents(owners))
            {
                if (graph.Nodes[p].IsTest)
                {
                    tests.Add(p);
                }
            }
            string reason = "바뀐 프로젝트 " + owners.Count.ToString(CultureInfo.InvariantCulture) + "개와 그것을 참조하는 프로젝트(" + scope.Reason + ")";
            return new AffectedTests(false, reason, tests);
        }

        // 고른 테스트 프로젝트를 하나씩 돌린다(Debug). 결과는 trxDir/<프로젝트 이름>/*.trx.
        // 실패, 건너뛴 테스트, 결과 파일이 없는 실행은 실패다(테스트를 건너뛰게 표시하지 않는다. CLAUDE.md 2절).
        public static int Run(string root, IReadOnlyList<string> projects, string trxDir, TextWriter output, TimeSpan perProject)
        {
            int failed = 0;
            int total = 0;
            foreach (string proj in projects)
            {
                string name = Path.GetFileNameWithoutExtension(proj);
                string dir = Path.Combine(trxDir, name);
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, true);
                }
                output.WriteLine("test-affected: dotnet test " + proj);
                output.Flush();
                var req = new ProcRequest("dotnet", new[]
                {
                    "test", RepoPaths.Combine(root, proj), "-c", "Debug", "-nologo", "--logger", "trx", "--results-directory", dir,
                })
                {
                    Name = name,
                    WorkingDirectory = root,
                    Timeout = perProject,
                    Echo = output,
                    EchoError = output,
                };
                // 바뀐 경로를 정하는 게이트의 변수는 이 체크아웃의 것이다. 테스트가 만드는 임시 저장소에는 그 커밋이 없으므로 넘기지 않는다(FIX-4)
                foreach (string v in ScopeVariables)
                {
                    req.Environment[v] = null;
                }
                ProcResult r = ProcessRunner.Run(req);
                TrxCounts c = TrxCounts.ReadDir(dir);
                total += c.Total;
                string summary = name + ": 전체 " + N(c.Total) + ", 통과 " + N(c.Passed) + ", 실패 " + N(c.Failed) + ", 건너뜀 " + N(c.NotExecuted);
                if (r.ExitCode != 0)
                {
                    output.WriteLine("FAIL  " + summary + " (dotnet test 종료 " + N(r.ExitCode) + (r.TimedOut ? ", 시간 제한" : "") + ")");
                    failed++;
                }
                else if (c.Files == 0)
                {
                    output.WriteLine("FAIL  " + name + ": dotnet test 가 0 으로 끝났지만 TRX 결과가 없다(" + dir + ")");
                    failed++;
                }
                else if (c.Failed > 0 || c.NotExecuted > 0)
                {
                    output.WriteLine("FAIL  " + summary + " (건너뛴 테스트도 실패다)");
                    failed++;
                }
                else
                {
                    output.WriteLine("ok    " + summary);
                }
            }
            output.WriteLine("test-affected: 프로젝트 " + N(projects.Count) + "개, 테스트 " + N(total) + "개, 실패한 프로젝트 " + N(failed) + "개");
            return failed == 0 ? 0 : 1;
        }

        // dotnet test 의 자식에게 넘기지 않는 변수
        public static readonly IReadOnlyList<string> ScopeVariables = new[] { "WIZARD_CI_BASE", "WIZARD_CI_FORCE_ALL" };

        private static string N(int n) => n.ToString(CultureInfo.InvariantCulture);
    }
}

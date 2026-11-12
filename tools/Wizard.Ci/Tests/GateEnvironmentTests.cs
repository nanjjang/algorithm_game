using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Wizard.Ci.Tests
{
    // 게이트의 환경 변수(WIZARD_CI_BASE, WIZARD_CI_FORCE_ALL …)는 명령 줄 층(Program.Run 의 env) 한 곳에서만 읽는다(FIX-4).
    // 판정 함수(ChangeScope, BranchCommits, StageRunner)가 프로세스 환경을 읽으면, 계약 개정 커밋을 기준으로 준 게이트
    // (WIZARD_CI_BASE=<그 커밋> tools/ci/ci.sh fast, DECISIONS-TECH T-48)에서 임시 저장소를 보는 자체 시험이 바깥 기준을 물려받아 실패한다.
    public sealed class GateEnvironmentTests
    {
        private static Func<string, string?> Env(params (string Key, string Value)[] kv)
        {
            var d = kv.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
            return k => d.TryGetValue(k, out string? v) ? v : null;
        }

        // 이 체크아웃(바깥 저장소)의 HEAD: 임시 저장소에는 없는 커밋이다. M1-TE-01 에서 본 누출과 같은 꼴의 기준
        private static readonly Lazy<string> OuterCommit = new Lazy<string>(() => TempGit.GitIn(TempGit.RepoRoot, "rev-parse", "HEAD").Trim());

        [Fact]
        public void OnlyTheCommandLineLayer_ReadsTheProcessEnvironment()
        {
            // 판정 함수는 비교 기준과 "전부" 여부를 인자로만 받는다. 프로세스 환경을 읽는 곳은 Program.Main 한 곳과
            // CiLock(이 프로세스의 .NET 런타임 설정 DOTNET_SYSTEM_IO_DISABLEFILELOCKING)뿐이다
            string dir = Path.Combine(TempGit.RepoRoot, "tools", "Wizard.Ci");
            var readers = new List<string>();
            foreach (string f in Directory.GetFiles(dir, "*.cs", SearchOption.TopDirectoryOnly).OrderBy(x => x, StringComparer.Ordinal))
            {
                string text = File.ReadAllText(f);
                int n = Count(text, "GetEnvironmentVariable");
                if (n > 0)
                {
                    readers.Add(Path.GetFileName(f) + ":" + n);
                }
            }
            Assert.Equal(new[] { "CiLock.cs:1", "Program.cs:1" }, readers.ToArray());
            string program = File.ReadAllText(Path.Combine(dir, "Program.cs"));
            Assert.Contains("Run(args, Console.Out, Console.Error, Environment.GetEnvironmentVariable)", program, StringComparison.Ordinal);
            string ciLock = File.ReadAllText(Path.Combine(dir, "CiLock.cs"));
            Assert.Contains("GetEnvironmentVariable(\"DOTNET_SYSTEM_IO_DISABLEFILELOCKING\")", ciLock, StringComparison.Ordinal);
        }

        private static int Count(string text, string what)
        {
            int n = 0;
            for (int i = text.IndexOf(what, StringComparison.Ordinal); i >= 0; i = text.IndexOf(what, i + what.Length, StringComparison.Ordinal))
            {
                n++;
            }
            return n;
        }

        // 임시 저장소: main(첫 커밋) ← task/X 에 a.txt(c1), b.txt(c2)
        private static (TempGit G, string Init, string C1) TwoCommitBranch()
        {
            TempGit g = TempGit.Create();
            string init = g.Git("rev-parse", "HEAD").Trim();
            g.Git("checkout", "-q", "-b", "task/X");
            g.Write("a.txt", "a\n");
            g.Commit("chore: a");
            string c1 = g.Git("rev-parse", "HEAD").Trim();
            g.Write("b.txt", "b\n");
            g.Commit("chore: b");
            return (g, init, c1);
        }

        private static (int Code, string Header, string[] Paths) Changed(string dir, Func<string, string?> env, params string[] extra)
        {
            var args = new List<string> { "changed", "--root", dir };
            args.AddRange(extra);
            var stdout = new StringWriter();
            int code = Program.Run(args.ToArray(), stdout, new StringWriter(), env);
            string[] lines = stdout.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
            return (code, lines[0], lines.Skip(1).ToArray());
        }

        [Fact]
        public void CommandLine_TakesTheBase_FromTheFlag_ThenWizardCiBase_ThenTheMergeBaseWithMain()
        {
            (TempGit g, string init, string c1) = TwoCommitBranch();
            using (g)
            {
                // 아무것도 없으면 main 과의 갈림점
                (int code, string header, string[] paths) = Changed(g.Dir, Env());
                Assert.Equal(0, code);
                Assert.Contains("merge-base HEAD main", header, StringComparison.Ordinal);
                Assert.Equal(new[] { "a.txt", "b.txt" }, paths);

                // 빈 WIZARD_CI_BASE 는 없는 것과 같다
                Assert.Equal(new[] { "a.txt", "b.txt" }, Changed(g.Dir, Env(("WIZARD_CI_BASE", ""))).Paths);

                // WIZARD_CI_BASE 가 있으면 그것(계약 개정 커밋을 기준으로 주는 길)
                (_, string h2, string[] p2) = Changed(g.Dir, Env(("WIZARD_CI_BASE", c1)));
                Assert.Contains(c1.Substring(0, 12), h2, StringComparison.Ordinal);
                Assert.Equal(new[] { "b.txt" }, p2);

                // --base 가 WIZARD_CI_BASE 보다 먼저다
                Assert.Equal(new[] { "a.txt", "b.txt" }, Changed(g.Dir, Env(("WIZARD_CI_BASE", c1)), "--base", init).Paths);

                // WIZARD_CI_FORCE_ALL=1 이면 줄이지 않는다
                (_, string h3, string[] p3) = Changed(g.Dir, Env(("WIZARD_CI_FORCE_ALL", "1"), ("WIZARD_CI_BASE", c1)));
                Assert.StartsWith("# 전부: ", h3, StringComparison.Ordinal);
                Assert.Empty(p3);

                // 명령 줄에서 준 기준을 git 이 모르면 사용법 오류다(조용히 전부로 넘어가지 않는다)
                CiException e = Assert.Throws<CiException>(() => Changed(g.Dir, Env(("WIZARD_CI_BASE", OuterCommit.Value))));
                Assert.Contains(OuterCommit.Value, e.Message, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void ScopeFunctions_IgnoreTheProcessEnvironment_WhenGivenNoBase()
        {
            // 판정 함수에 기준을 주지 않으면(null) 바깥 값과 관계없이 main 과의 갈림점(ChangeScope), main(BranchCommits)이다.
            // 이 시험이 돌 때 프로세스 환경에 바깥 게이트의 WIZARD_CI_BASE 가 있어도(WholeSuiteUnderGateEnvironmentTests 의 자식 실행) 결과가 같다
            (TempGit g, _, _) = TwoCommitBranch();
            using (g)
            {
                ChangeScope s = ChangeScope.Detect(g.Dir, null, false);
                Assert.False(s.All, s.Reason);
                Assert.Equal(new[] { "a.txt", "b.txt" }, s.Paths.ToArray());
                // 메시지 형식의 문제는 이 시험이 보는 것이 아니다(본 범위와 커밋 수만 본다)
                BranchCommits.Check(g.Dir, null, null, new HashSet<string>(StringComparer.Ordinal), out int count, out string range);
                Assert.Equal(2, count);
                Assert.Contains("(main 와의 갈림점)", range, StringComparison.Ordinal);
            }
        }
    }
}

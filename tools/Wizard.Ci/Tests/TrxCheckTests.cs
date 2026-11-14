using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace Wizard.Ci.Tests
{
    // dotnet test 의 판정은 TRX 로 한다(ci trx-check, TrxCounts). FIX-9 검토에서 본 구멍 둘을 막는다:
    //   1) test.sh·pg-test.sh 가 영어 요약 줄('Skipped: 1')을 grep 했는데 이 PC 의 dotnet 은 '건너뜀: 1'로 내서 건너뜀 검사가 조용히 꺼져 있었다.
    //   2) xunit 의 [Fact(Skip=…)] 은 TRX 의 Counters 에 notExecuted="0" 으로 남는다(결과 줄의 outcome 과 executed < total 로만 드러난다).
    // 아래 SkippedRun 은 이 PC(dotnet 10.0.201, xunit 2.9.3, xunit.runner.visualstudio 3.1.4)에서 [Fact] 하나와 [Fact(Skip="x")] 하나로
    // 실제로 낸 TRX 에서 시간·경로·id 를 줄인 것이다. 그때 콘솔 요약은 '통과!  - 실패: 0, 통과: 1, 건너뜀: 1, 전체: 2' 였고 종료 코드는 0 이었다.
    public sealed class TrxCheckTests
    {
        private const string Ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";

        private const string SkippedRun =
            "﻿<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
            "<TestRun id=\"1\" name=\"run\" xmlns=\"" + Ns + "\">\n" +
            "  <Results>\n" +
            "    <UnitTestResult testName=\"T.B\" outcome=\"NotExecuted\"><Output><ErrorInfo><Message>x</Message></ErrorInfo></Output></UnitTestResult>\n" +
            "    <UnitTestResult testName=\"T.A\" outcome=\"Passed\" />\n" +
            "  </Results>\n" +
            "  <ResultSummary outcome=\"Completed\">\n" +
            "    <Counters total=\"2\" executed=\"1\" passed=\"1\" failed=\"0\" error=\"0\" timeout=\"0\" aborted=\"0\" inconclusive=\"0\" passedButRunAborted=\"0\"" +
            " notRunnable=\"0\" notExecuted=\"0\" disconnected=\"0\" warning=\"0\" completed=\"0\" inProgress=\"0\" pending=\"0\" />\n" +
            "    <RunInfos><RunInfo outcome=\"Warning\"><Text>[xUnit.net 00:00:00.24]     T.B [SKIP]</Text></RunInfo></RunInfos>\n" +
            "  </ResultSummary>\n" +
            "</TestRun>\n";

        private static string Run(int passed, int failed)
        {
            var rows = new System.Text.StringBuilder();
            for (int i = 0; i < passed; i++)
            {
                rows.Append("<UnitTestResult testName=\"P").Append(i).Append("\" outcome=\"Passed\" />");
            }
            for (int i = 0; i < failed; i++)
            {
                rows.Append("<UnitTestResult testName=\"F").Append(i).Append("\" outcome=\"Failed\" />");
            }
            int total = passed + failed;
            return "<?xml version=\"1.0\" encoding=\"utf-8\"?><TestRun xmlns=\"" + Ns + "\"><Results>" + rows + "</Results><ResultSummary outcome=\"Completed\">" +
                   "<Counters total=\"" + total + "\" executed=\"" + total + "\" passed=\"" + passed + "\" failed=\"" + failed +
                   "\" error=\"0\" timeout=\"0\" aborted=\"0\" inconclusive=\"0\" notExecuted=\"0\" disconnected=\"0\" warning=\"0\" pending=\"0\" />" +
                   "</ResultSummary></TestRun>";
        }

        [Fact]
        public void XunitSkip_WithZeroNotExecutedCounter_IsCountedAsSkipped()
        {
            TrxCounts c = TrxCounts.Parse(SkippedRun);
            Assert.Equal(2, c.Total);
            Assert.Equal(1, c.Passed);
            Assert.Equal(0, c.Failed);
            Assert.Equal(1, c.NotExecuted);
            string? problem = TrxCheck.Problem(c, 1);
            Assert.NotNull(problem);
            Assert.Contains("건너뛴 테스트가 1개", problem!, StringComparison.Ordinal);
        }

        [Fact]
        public void SkipCountedFromCountersOrExecutedOrResultRows_EachAlone()
        {
            // Counters 의 notExecuted 만 있는 꼴(결과 줄 없음, executed 없음)
            TrxCounts a = TrxCounts.Parse("<TestRun xmlns=\"" + Ns + "\"><ResultSummary><Counters total=\"3\" passed=\"2\" notExecuted=\"1\" /></ResultSummary></TestRun>");
            Assert.Equal(1, a.NotExecuted);
            // executed < total 만 있는 꼴
            TrxCounts b = TrxCounts.Parse("<TestRun xmlns=\"" + Ns + "\"><ResultSummary><Counters total=\"3\" executed=\"1\" passed=\"1\" /></ResultSummary></TestRun>");
            Assert.Equal(2, b.NotExecuted);
            // 결과 줄의 outcome 만 있는 꼴(Counters 는 모두 0)
            TrxCounts d = TrxCounts.Parse("<TestRun xmlns=\"" + Ns + "\"><Results><UnitTestResult outcome=\"Inconclusive\" /><UnitTestResult outcome=\"Failed\" /></Results>" +
                                          "<ResultSummary><Counters total=\"2\" /></ResultSummary></TestRun>");
            Assert.Equal(1, d.NotExecuted);
            Assert.Equal(1, d.Failed);
        }

        [Fact]
        public void Problem_FailsOnMissingMismatchedEmptyFailedOrSkipped_AndPassesOnlyAClean_Run()
        {
            Assert.Contains("TRX 결과가 없다", TrxCheck.Problem(new TrxCounts(0, 0, 0, 0, 0), null)!, StringComparison.Ordinal);
            Assert.Contains("테스트 프로젝트 18개", TrxCheck.Problem(new TrxCounts(17, 10, 10, 0, 0), 18)!, StringComparison.Ordinal);
            Assert.Contains("전체 0", TrxCheck.Problem(new TrxCounts(1, 0, 0, 0, 0), 1)!, StringComparison.Ordinal);
            Assert.Contains("실패한 테스트가 2개", TrxCheck.Problem(new TrxCounts(1, 5, 3, 2, 0), 1)!, StringComparison.Ordinal);
            Assert.Contains("건너뛴 테스트가 1개", TrxCheck.Problem(new TrxCounts(1, 5, 4, 0, 1), 1)!, StringComparison.Ordinal);
            Assert.Null(TrxCheck.Problem(new TrxCounts(2, 5, 5, 0, 0), 2));
            Assert.Null(TrxCheck.Problem(new TrxCounts(2, 5, 5, 0, 0), null));
        }

        private static int Cli(string dir, out string stdout, out string stderr, params string[] extra)
        {
            var o = new StringWriter();
            var e = new StringWriter();
            var args = new List<string> { "trx-check", dir };
            args.AddRange(extra);
            args.Add("--root");
            args.Add(TempGit.RepoRoot);
            int code = Program.Run(args.ToArray(), o, e, _ => null);
            stdout = o.ToString();
            stderr = e.ToString();
            return code;
        }

        [Fact]
        public void Cli_FailsOnASkippedRun_AndPassesACleanOne()
        {
            using TempGit g = TempGit.Create();
            g.Write("skip/a.trx", SkippedRun);
            int code = Cli(g.PathOf("skip"), out string so, out string se);
            Assert.True(code == 1, so + se);
            Assert.Contains("건너뛴 테스트가 1개", se, StringComparison.Ordinal);
            Assert.Contains("건너뜀 1", se, StringComparison.Ordinal);

            g.Write("ok/a.trx", Run(3, 0));
            g.Write("ok/b.trx", Run(2, 0));
            code = Cli(g.PathOf("ok"), out so, out se, "--expect", "2");
            Assert.True(code == 0, so + se);
            Assert.Contains("tests: 5 passed", so, StringComparison.Ordinal);

            code = Cli(g.PathOf("ok"), out so, out se, "--expect", "3");
            Assert.True(code == 1, so + se);

            g.Write("bad/a.trx", Run(3, 1));
            Assert.Equal(1, Cli(g.PathOf("bad"), out _, out _));

            Assert.Equal(1, Cli(g.PathOf("nothing-here"), out _, out se));
            Assert.Contains("TRX 결과가 없다", se, StringComparison.Ordinal);
        }

        [Fact]
        public void Cli_ExpectSolution_IsTheNumberOfTestProjectsInTheGraph()
        {
            int projects = AffectedTests.Select(ProjectGraph.Load(TempGit.RepoRoot), ChangeScope.Everything("t")).Projects.Count;
            Assert.True(projects > 1);
            using TempGit g = TempGit.Create();
            for (int i = 0; i < projects; i++)
            {
                g.Write("all/r" + i + ".trx", Run(1, 0));
            }
            Assert.Equal(0, Cli(g.PathOf("all"), out _, out _, "--expect", "solution"));
            File.Delete(g.PathOf("all/r0.trx"));
            Assert.Equal(1, Cli(g.PathOf("all"), out _, out string se, "--expect", "solution"));
            Assert.Contains("테스트 프로젝트 " + projects + "개", se, StringComparison.Ordinal);
        }

        // 테스트를 돌리는 검사 스크립트는 TRX 로 판정하고 콘솔 요약 줄(언어에 따라 'Skipped:'·'건너뜀:')을 세지 않는다
        [Theory]
        [InlineData("tools/ci/test.sh", "--expect solution")]
        [InlineData("tools/ci/test-release.sh", "--expect solution")]
        [InlineData("tools/ci/pg-test.sh", "--expect 1")]
        public void TestScripts_JudgeByTrx_NotByTheConsoleSummary(string rel, string expect)
        {
            string text = File.ReadAllText(RepoPaths.Combine(TempGit.RepoRoot, rel));
            Assert.Contains("--logger trx --results-directory \"$TRX\"", text, StringComparison.Ordinal);
            Assert.Contains("ci_tool trx-check \"$TRX\" " + expect, text, StringComparison.Ordinal);
            Assert.Contains("/bin/rm -rf \"$TRX\"", text, StringComparison.Ordinal);
            foreach (string bad in new[] { "Skipped:", "Total:", "건너뜀:", "전체:" })
            {
                Assert.DoesNotContain(bad, text, StringComparison.Ordinal);
            }
        }
    }
}

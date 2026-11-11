using System.Linq;
using Xunit;

namespace Wizard.VectorCoverage.Tests
{
    // 전역 검사와 작업 검사(PIPELINE 7.1 G1)
    public sealed class CoverageCheckTests
    {
        private static readonly TaskEntry All01 = Fx.Task("A", "S01§14.1", "S01§14.2", "S01§15");

        [Fact]
        public void Global_EveryVectorAssignedAndNoDoneTask_Passes_AndReportsMissingTestsPerSpec()
        {
            CoverageCheck c = CoverageCheck.Global(Fx.Scan01(), Fx.Tasks(All01), Fx.Done(), Fx.Index(Fx.FactClass("S01_T_H01__X")));
            Assert.True(c.Passed, string.Join("\n", c.Failures));
            Assert.Contains("  S01: 벡터 6, 테스트 있음 1, 빠짐 5", c.Lines);
            Assert.Equal(6, c.Targets);
            Assert.Equal(1, c.Covered);
            Assert.Equal(5, c.Missing);
        }

        [Fact]
        public void Global_UnassignedVectorInTheVectorChapter_Fails_GroupedBySection()
        {
            CoverageCheck c = CoverageCheck.Global(Fx.Scan01(), Fx.Tasks(Fx.Task("A", "S01§14.1", "S01:T-M01", "S01§15")), Fx.Done(), Fx.Index());
            Assert.False(c.Passed);
            Assert.Equal("미배정 벡터 S01§14.2: T-M02", Assert.Single(c.Failures));
        }

        [Fact]
        public void Global_UnassignedVectorOutsideTheVectorChapter_IsAWarningNotAFailure()
        {
            CoverageCheck c = CoverageCheck.Global(Fx.Scan01(), Fx.Tasks(Fx.Task("A", "S01§14.1", "S01§14.2")), Fx.Done(), Fx.Index());
            Assert.True(c.Passed, string.Join("\n", c.Failures));
            Assert.Contains("S01§15: T-Z01", Assert.Single(c.Warnings));
        }

        [Fact]
        public void Global_DoubleAssignment_Fails()
        {
            CoverageCheck c = CoverageCheck.Global(Fx.Scan01(), Fx.Tasks(All01, Fx.Task("B", "S01:T-H03")), Fx.Done(), Fx.Index());
            Assert.False(c.Passed);
            Assert.Contains(c.Failures, f => f.Contains("두 작업에 배정", System.StringComparison.Ordinal) && f.Contains("S01:T-H03", System.StringComparison.Ordinal));
        }

        [Fact]
        public void Global_DoneTaskWithoutATestForItsVector_Fails_AndOtherTasksDoNot()
        {
            var tasks = Fx.Tasks(Fx.Task("A", "S01§14.1"), Fx.Task("B", "S01§14.2", "S01§15"));
            TestIndex idx = Fx.Index(Fx.FactClass("S01_T_H01__One", "S01_T_H03__Three"));
            CoverageCheck c = CoverageCheck.Global(Fx.Scan01(), tasks, Fx.Done("A"), idx);
            Assert.False(c.Passed);
            Assert.Equal("끝난 작업 A 의 벡터에 테스트가 없다 S01: T-H02", Assert.Single(c.Failures));

            CoverageCheck ok = CoverageCheck.Global(Fx.Scan01(), tasks, Fx.Done("A"),
                Fx.Index(Fx.FactClass("S01_T_H01__One", "S01_T_H02__Two", "S01_T_H03__Three")));
            Assert.True(ok.Passed, string.Join("\n", ok.Failures));
        }

        [Fact]
        public void Global_TestNamingAVectorThatIsNotInTheSpec_Fails()
        {
            CoverageCheck c = CoverageCheck.Global(Fx.Scan01(), Fx.Tasks(All01), Fx.Done(), Fx.Index(Fx.FactClass("S01_T_H99__Typo")));
            Assert.False(c.Passed);
            Assert.Contains("S01:T-H99", Assert.Single(c.Failures));
        }

        [Fact]
        public void Global_BadTestNameRule_Fails()
        {
            CoverageCheck c = CoverageCheck.Global(Fx.Scan01(), Fx.Tasks(All01), Fx.Done(), Fx.Index(Fx.FactClass("S01__Broken")));
            Assert.False(c.Passed);
            Assert.Contains("S01__Broken", Assert.Single(c.Failures));
        }

        [Fact]
        public void Task_EveryVectorHasATest_Passes()
        {
            var tasks = Fx.Tasks(Fx.Task("A", "S01:T-H01..T-H02"), Fx.Task("B", "S01:T-M01"));
            CoverageCheck c = CoverageCheck.ForTask("A", Fx.Scan01(), tasks, Fx.Index(Fx.FactClass("S01_T_H01__A", "S01_T_H02__B")), null);
            Assert.True(c.Passed, string.Join("\n", c.Failures));
            Assert.Equal(2, c.Targets);
            Assert.Equal(0, c.Missing);
            Assert.Contains(c.Lines, l => l.StartsWith("vector-coverage --task A: 대상 2개, 테스트 있음 2개", System.StringComparison.Ordinal));
        }

        [Fact]
        public void Task_MissingTests_AreListedPerSpec()
        {
            var tasks = Fx.Tasks(Fx.Task("A", "S01§14.1", "S01:T-M02"));
            CoverageCheck c = CoverageCheck.ForTask("A", Fx.Scan01(), tasks, Fx.Index(Fx.FactClass("S01_T_H02__B")), null);
            Assert.False(c.Passed);
            Assert.Equal("작업 A 의 벡터에 테스트가 없다 S01: T-H01, T-H03, T-M02", Assert.Single(c.Failures));
            Assert.Equal(3, c.Missing);
        }

        [Fact]
        public void Task_WithoutVectors_HasNothingToCover()
        {
            CoverageCheck c = CoverageCheck.ForTask("A", Fx.Scan01(), Fx.Tasks(Fx.Task("A"), All01.WithId("B")), Fx.Index(), null);
            Assert.True(c.Passed);
            Assert.Equal(0, c.Targets);
        }

        [Fact]
        public void Task_UnknownId_IsAUsageError()
        {
            CoverageException e = Assert.Throws<CoverageException>(() => CoverageCheck.ForTask("Z", Fx.Scan01(), Fx.Tasks(All01), Fx.Index(), null));
            Assert.Equal(CoverageException.UsageExitCode, e.ExitCode);
        }

        [Fact]
        public void Task_SelectorProblemsOfThatTaskOnly_AreFailures()
        {
            var tasks = Fx.Tasks(Fx.Task("A", "S01:T-H01"), Fx.Task("B", "S01:T-H99"));
            Assert.True(CoverageCheck.ForTask("A", Fx.Scan01(), tasks, Fx.Index(Fx.FactClass("S01_T_H01__A")), null).Passed);
            Assert.False(CoverageCheck.ForTask("B", Fx.Scan01(), tasks, Fx.Index(), null).Passed);
        }

        [Fact]
        public void Task_WithTrx_RequiresEveryVectorTestToHavePassed()
        {
            var tasks = Fx.Tasks(Fx.Task("A", "S01:T-H01..T-H03"));
            TestIndex idx = Fx.Index(Fx.FactClass("S01_T_H01__A", "S01_T_H02__B", "S01_T_H03__C"));
            string trx = "<TestRun xmlns=\"http://microsoft.com/schemas/VisualStudio/TeamTest/2010\"><Results>" +
                         "<UnitTestResult testName=\"Demo.Tests.DemoTests.S01_T_H01__A\" outcome=\"Passed\" />" +
                         "<UnitTestResult testName=\"Demo.Tests.DemoTests.S01_T_H02__B\" outcome=\"Failed\" />" +
                         "</Results></TestRun>";
            CoverageCheck c = CoverageCheck.ForTask("A", Fx.Scan01(), tasks, idx, TrxResults.FromXml(trx, Fx.TestNameRx));
            Assert.False(c.Passed);
            Assert.Equal(2, c.Failures.Count);
            Assert.Contains(c.Failures, f => f.Contains("T-H02: ", System.StringComparison.Ordinal) && f.Contains("Failed", System.StringComparison.Ordinal));
            Assert.Contains(c.Failures, f => f.Contains("T-H03: ", System.StringComparison.Ordinal) && f.Contains("실행이 없다", System.StringComparison.Ordinal));
            Assert.Contains(c.Lines, l => l.Contains("통과 1개", System.StringComparison.Ordinal));
        }

        [Fact]
        public void Output_IsTheSameForTheSameInput()
        {
            string Run() => string.Join("\n", CoverageCheck.Global(Fx.Scan01(), Fx.Tasks(Fx.Task("A", "S01§14.1")), Fx.Done("A"), Fx.Index()).Lines
                .Concat(CoverageCheck.Global(Fx.Scan01(), Fx.Tasks(Fx.Task("A", "S01§14.1")), Fx.Done("A"), Fx.Index()).Failures));
            Assert.Equal(Run(), Run());
        }
    }

    internal static class TaskEntryExtensions
    {
        public static TaskEntry WithId(this TaskEntry t, string id) => new TaskEntry(id, t.Milestone, t.Vectors);
    }
}

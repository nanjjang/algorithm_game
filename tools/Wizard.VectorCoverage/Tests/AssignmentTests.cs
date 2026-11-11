using System.Linq;
using Wizard.TestKit.Shared.Vectors;
using Xunit;

namespace Wizard.VectorCoverage.Tests
{
    // tasks.json 의 vectors 선택자 펼치기와 "정확히 한 작업" 규칙(PIPELINE 7.1 G1)
    public sealed class AssignmentTests
    {
        private static string[] Ids(Assignment a, string task) => a.VectorsOf(task).Select(v => v.Id).ToArray();

        [Fact]
        public void SectionSelector_TakesEveryVectorOfThatSection()
        {
            Assignment a = Assignment.Resolve(Fx.Scan01(), Fx.Tasks(Fx.Task("A", "S01§14.1")));
            Assert.Empty(a.Problems);
            Assert.Equal(new[] { "T-H01", "T-H02", "T-H03" }, Ids(a, "A"));
        }

        [Fact]
        public void SingleAndRangeSelectors_FollowTheSpecLineOrder()
        {
            Assignment a = Assignment.Resolve(Fx.Scan01(), Fx.Tasks(Fx.Task("A", "S01:T-H02..T-H03", "S01:T-M01")));
            Assert.Empty(a.Problems);
            Assert.Equal(new[] { "T-H02", "T-H03", "T-M01" }, Ids(a, "A"));
        }

        [Fact]
        public void ChapterNumberSelector_ReachesAVectorDefinedOutsideTheVectorChapter()
        {
            Assignment a = Assignment.Resolve(Fx.Scan01(), Fx.Tasks(Fx.Task("A", "S01§15")));
            Assert.Empty(a.Problems);
            Assert.Equal(new[] { "T-Z01" }, Ids(a, "A"));
        }

        [Theory]
        [InlineData("S1:T-H01", "꼴이 틀렸다")]
        [InlineData("S01-T-H01", "꼴이 틀렸다")]
        [InlineData("S01§14.9", "절이 없다")]
        [InlineData("S01:T-H09", "벡터 T-H09 가 없다")]
        [InlineData("S01:T-H03..T-H01", "거꾸로")]
        [InlineData("S01:T-H03..T-M01", "절을 넘는다")]
        public void BadSelectors_AreReportedAgainstTheTask(string selector, string expected)
        {
            Assignment a = Assignment.Resolve(Fx.Scan01(), Fx.Tasks(Fx.Task("A", selector)));
            string p = Assert.Single(a.Problems);
            Assert.Contains(expected, p);
            Assert.StartsWith("A:", p);
            Assert.Equal(p, Assert.Single(a.ProblemsOf("A")));
        }

        [Fact]
        public void TheSameVectorInTwoTasks_IsADoubleAssignmentForBoth()
        {
            Assignment a = Assignment.Resolve(Fx.Scan01(), Fx.Tasks(Fx.Task("A", "S01§14.1"), Fx.Task("B", "S01:T-H02")));
            string p = Assert.Single(a.Problems);
            Assert.Contains("S01:T-H02", p);
            Assert.Contains("A, B", p);
            Assert.Single(a.ProblemsOf("A"));
            Assert.Single(a.ProblemsOf("B"));
            Assert.Equal("A", a.OwnerOf(Fx.Scan01().Find("01", "T-H02")!));
        }

        [Fact]
        public void TheSameVectorTwiceInOneTask_IsReported()
        {
            Assignment a = Assignment.Resolve(Fx.Scan01(), Fx.Tasks(Fx.Task("A", "S01§14.2", "S01:T-M02")));
            Assert.Contains("두 번 가리킨다", Assert.Single(a.Problems));
        }

        [Fact]
        public void UnclaimedVectors_HaveNoOwner()
        {
            SpecVectorScan scan = Fx.Scan01();
            Assignment a = Assignment.Resolve(scan, Fx.Tasks(Fx.Task("A", "S01§14.1")));
            Assert.Null(a.OwnerOf(scan.Find("01", "T-M01")!));
            Assert.Empty(a.VectorsOf("nobody"));
        }
    }
}

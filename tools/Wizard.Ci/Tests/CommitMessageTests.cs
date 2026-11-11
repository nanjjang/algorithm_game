using System;
using System.Collections.Generic;
using Xunit;

namespace Wizard.Ci.Tests
{
    // 커밋 메시지 형식(CLAUDE.md 10절, PIPELINE 4.2)
    public sealed class CommitMessageTests
    {
        private static readonly HashSet<string> Ids = new HashSet<string>(StringComparer.Ordinal) { "M0-BT-05", "M1-SIM-03" };
        private const string Opus = "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>";

        private static List<string> Check(params string[] lines) => CommitMessage.Check(string.Join("\n", lines) + "\n", Ids);

        [Fact]
        public void WellFormedMessage_Passes_ForEachModelLine()
        {
            foreach (string line in CommitMessage.CoAuthors)
            {
                Assert.Empty(Check("build(M0-BT-05): CI 스크립트를 넣는다", "", "왜, 무엇이 달라지는가.", "수락 테스트 통과.", "", line));
            }
        }

        [Fact]
        public void GitCommentsAndTheScissorsPart_AreIgnored()
        {
            Assert.Empty(Check("# 주석", "feat(M1-SIM-03): 입력", "", "본문", "", Opus, "# Please enter the commit message",
                "# ------------------------ >8 ------------------------", "diff --git a/x b/x"));
        }

        [Theory]
        [InlineData("CI 스크립트를 넣는다", "꼴이 아니다")]
        [InlineData("build: 범위가 없다", "꼴이 아니다")]
        [InlineData("build(M0-BT-05):붙었다", "꼴이 아니다")]
        [InlineData("feature(M0-BT-05): type 이 틀렸다", "type 'feature'")]
        [InlineData("build(M9-XX-99): 없는 작업", "'M9-XX-99'")]
        public void BadSubject_IsRejected(string subject, string expected)
        {
            List<string> p = Check(subject, "", "본문", "", Opus);
            Assert.Contains(p, x => x.Contains(expected, StringComparison.Ordinal));
        }

        [Fact]
        public void SecondLineMustBeBlank()
        {
            Assert.Contains(Check("build(M0-BT-05): 요약", "바로 본문", "", Opus), x => x.Contains("둘째 줄", StringComparison.Ordinal));
        }

        [Fact]
        public void BodyIsRequired()
        {
            Assert.Contains(Check("build(M0-BT-05): 요약", "", Opus), x => x.Contains("본문이 없다", StringComparison.Ordinal));
        }

        [Fact]
        public void CoAuthoredByIsRequired_AndMustBeAKnownModelLine_AtTheEnd()
        {
            Assert.Contains(Check("build(M0-BT-05): 요약", "", "본문"), x => x.Contains("Co-Authored-By 줄이 없다", StringComparison.Ordinal));
            Assert.Contains(Check("build(M0-BT-05): 요약", "", "본문", "", "Co-Authored-By: Someone <a@b.c>"),
                x => x.Contains("정해진 모델 줄이 아니다", StringComparison.Ordinal));
            Assert.Contains(Check("build(M0-BT-05): 요약", "", "본문", "", "co-authored-by: claude opus 5.5 <noreply@anthropic.com>"),
                x => x.Contains("정해진 모델 줄이 아니다", StringComparison.Ordinal));
            Assert.Contains(Check("build(M0-BT-05): 요약", "", Opus, "", "본문이 뒤에 온다"),
                x => x.Contains("끝에 둔다", StringComparison.Ordinal));
        }

        [Fact]
        public void EmptyMessage_IsRejected()
        {
            Assert.Contains(CommitMessage.Check("# 주석만\n\n", Ids), x => x.Contains("비었다", StringComparison.Ordinal));
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Wizard.TaskGraph.Tests
{
    // FIX-7, DECISIONS-TECH T-65 9번: DECISIONS-TECH.md 와 OPEN-QUESTIONS.md 의 줄 단위 3방향 병합.
    // 바탕이 빈 충돌 덩어리만 "main 쪽 묶음 전체 다음에 옮기는 쪽 묶음 전체"로 푼다.
    public sealed class TextMergeTests
    {
        private const string Tail = "## 아직 정하지 않은 기술 항목\n\n- 없음\n";

        private static string Decisions(string rows, string sections)
        {
            return "# 기술 결정\n\n## 요약\n\n| # | 결정 | 표시 |\n|---|---|---|\n| T-01 | 첫 결정 | [신규] |\n" + rows
                + "\n## T-01 첫 결정\n\n- **결정**: 하나\n- **이유**: 둘\n\n" + sections + Tail;
        }

        private static string Section(int n, string what)
        {
            return $"## T-{n:00} {what}\n\n- **결정**: {what}의 결정\n- **이유**:\n  - {what}의 이유\n\n";
        }

        private static string Row(int n, string what) => $"| T-{n:00} | {what} | [신규] |\n";

        private static TasksMerge.Outcome Merge(string b, string m, string t)
        {
            using TempRepo work = TempRepo.Create(git: false);
            return TextMerge.Merge(Ledger.DecisionsPath, b, m, t, work.Dir);
        }

        [Fact]
        public void A_BothAddASectionAtTheSamePlace_BothWhole_MainFirst_NotInterleaved()
        {
            string b = Decisions("", "");
            string m = Decisions(Row(2, "main"), Section(2, "main"));
            string t = Decisions(Row(3, "moving"), Section(3, "moving"));
            TasksMerge.Outcome o = Merge(b, m, t);
            Assert.True(o.Ok, string.Join("\n", o.Conflicts));
            Assert.Equal(Decisions(Row(2, "main") + Row(3, "moving"), Section(2, "main") + Section(3, "moving")), o.Text);
        }

        [Fact]
        public void B_BothAddRowsAtTheEndOfSummaryAndQuestionTables()
        {
            string q0 = "# 물을 것\n\n| # | 물음 |\n|---|---|\n| Q-01 | 하나 |\n";
            using TempRepo work = TempRepo.Create(git: false);
            TasksMerge.Outcome q = TextMerge.Merge(Ledger.QuestionsPath, q0, q0 + "| Q-02 | main |\n", q0 + "| Q-03 | moving |\n", work.Dir);
            Assert.True(q.Ok, string.Join("\n", q.Conflicts));
            Assert.Equal(q0 + "| Q-02 | main |\n| Q-03 | moving |\n", q.Text);

            TasksMerge.Outcome d = Merge(Decisions("", ""), Decisions(Row(2, "main"), ""), Decisions(Row(3, "moving"), ""));
            Assert.True(d.Ok, string.Join("\n", d.Conflicts));
            Assert.Equal(Decisions(Row(2, "main") + Row(3, "moving"), ""), d.Text);
        }

        [Fact]
        public void C_OneSideEditsAnOldLine_OtherAddsRightAfterIt_IsNotResolved()
        {
            string b = Decisions("", "");
            string m = b.Replace("- **이유**: 둘\n", "- **이유**: 고쳐 적은 둘\n", StringComparison.Ordinal);
            string t = b.Replace("- **이유**: 둘\n", "- **이유**: 둘\n- **따라오는 일**: 더한 줄\n", StringComparison.Ordinal);
            TasksMerge.Outcome o = Merge(b, m, t);
            Assert.False(o.Ok);
            Assert.Null(o.Text);
            Assert.Single(o.Conflicts);
            Assert.Contains("있던 줄을 고친 덩어리가 충돌했다", o.Conflicts[0]);
            Assert.StartsWith(Ledger.DecisionsPath + ":", o.Conflicts[0]);
        }

        [Fact]
        public void D_BothUseTheSameNumber_LedgerCheckFails_NothingResolved()
        {
            string b = Decisions("", "");
            string m = Decisions(Row(2, "main"), Section(2, "main"));
            string t = Decisions(Row(2, "moving"), Section(2, "moving"));
            Assert.True(Merge(b, m, t).Ok);   // 줄 병합만으로는 풀린다

            using TempRepo work = TempRepo.Create(git: false);
            var conflicted = new Dictionary<string, LedgerResolver.Stages> { [Ledger.DecisionsPath] = new LedgerResolver.Stages(b, m, t) };
            LedgerResolver.Result r = LedgerResolver.Resolve(conflicted, _ => "", null, null, _ => MergeHistory.NotRead(), work.Dir);
            Assert.False(r.Ok);
            Assert.Empty(r.Resolved);
            Assert.Contains(r.Conflicts, c => c.StartsWith("합친 결과가 ledger 검사에 실패한다 — docs/dev/DECISIONS-TECH.md:", StringComparison.Ordinal) && c.Contains("T-02 절 제목이 두 번 나온다", StringComparison.Ordinal));
            Assert.Contains(r.Conflicts, c => c.Contains("요약 표에 T-02 줄이 두 번 나온다", StringComparison.Ordinal));

            // Q 번호도 같다
            string q0 = "| Q-01 | 하나 |\n";
            var qc = new Dictionary<string, LedgerResolver.Stages> { [Ledger.QuestionsPath] = new LedgerResolver.Stages(q0, q0 + "| Q-02 | a |\n", q0 + "| Q-02 | b |\n") };
            LedgerResolver.Result qr = LedgerResolver.Resolve(qc, _ => "", null, null, _ => MergeHistory.NotRead(), work.Dir);
            Assert.False(qr.Ok);
            Assert.Contains(qr.Conflicts, c => c.Contains("Q-02 줄이 두 번 나온다", StringComparison.Ordinal));
        }

        [Fact]
        public void Resolve_ParsesDiff3Output_EmptyBaseOnly()
        {
            string diff3 = "앞\n<<<<<<< main\nA1\nA2\n||||||| base\n=======\nB1\n>>>>>>> moving\n뒤\n";
            TasksMerge.Outcome o = TextMerge.Resolve("x.md", diff3);
            Assert.True(o.Ok);
            Assert.Equal("앞\nA1\nA2\nB1\n뒤\n", o.Text);
            TasksMerge.Outcome bad = TextMerge.Resolve("x.md", "앞\n<<<<<<< main\nA\n||||||| base\nO\n=======\nB\n>>>>>>> moving\n");
            Assert.False(bad.Ok);
            Assert.Equal(new[] { "x.md:2: 있던 줄을 고친 덩어리가 충돌했다(바탕 쪽 1줄, 첫 줄 \"O\")" }, bad.Conflicts.ToArray());
        }
    }
}

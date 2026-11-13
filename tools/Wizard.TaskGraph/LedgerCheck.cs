using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Wizard.TaskGraph
{
    // taskgraph ledger (DECISIONS-TECH T-65 7번). 개정 장부의 번호와 충돌 표식을 본다. 글자만 받는다(파일·git 을 읽지 않는다).
    //   1. DECISIONS-TECH.md 의 '## T-<번호>' 절 제목의 번호가 겹치지 않는다
    //   2. 요약 표의 '| T-<번호> |' 줄의 번호가 겹치지 않고 그 번호의 절이 있다(절은 있는데 요약 줄이 없는 것은 실패가 아니다)
    //   3. OPEN-QUESTIONS.md 의 '| Q-<번호> |' 줄의 번호가 겹치지 않는다
    //   4. 세 장부에 충돌 표식 줄('<<<<<<< ', '||||||| ', '>>>>>>> '로 시작)이 없다
    // 실패는 "파일:줄: …" 꼴이다. 번호는 정수로 비교한다(T-05 와 T-5 는 같다).
    public static class LedgerCheck
    {
        private static readonly Regex Section = new Regex("^## T-([0-9]+)(?![0-9])", RegexOptions.CultureInvariant);
        private static readonly Regex SummaryRow = new Regex("^\\|\\s*T-([0-9]+)\\s*\\|", RegexOptions.CultureInvariant);
        private static readonly Regex QuestionRow = new Regex("^\\|\\s*Q-([0-9]+)\\s*\\|", RegexOptions.CultureInvariant);
        private static readonly string[] Markers = { "<<<<<<< ", "||||||| ", ">>>>>>> " };

        public sealed class Result
        {
            public Result(IReadOnlyList<string> errors, int sections, int summaryRows, int questions)
            {
                Errors = errors;
                Sections = sections;
                SummaryRows = summaryRows;
                Questions = questions;
            }

            public IReadOnlyList<string> Errors { get; }
            public int Sections { get; }
            public int SummaryRows { get; }
            public int Questions { get; }
            public bool Ok => Errors.Count == 0;

            public string SummaryLine => $"ledger: T 절 {Sections}개, 요약 줄 {SummaryRows}개, Q 줄 {Questions}개";
        }

        // 없는 파일은 null 로 준다(그렇게 알린다)
        public static Result Check(string? decisions, string? questions, string? tasks)
        {
            var errors = new List<string>();
            int sections = 0, rows = 0, qrows = 0;

            if (decisions == null)
            {
                errors.Add($"{Ledger.DecisionsPath}: 파일이 없다");
            }
            else
            {
                string[] lines = Lines(decisions);
                var sectionAt = new Dictionary<int, int>();
                var rowAt = new Dictionary<int, int>();
                var rowList = new List<(int Number, int Line)>();
                for (int i = 0; i < lines.Length; i++)
                {
                    Match m = Section.Match(lines[i]);
                    if (m.Success && TryNumber(m.Groups[1].Value, out int n))
                    {
                        sections++;
                        if (sectionAt.TryGetValue(n, out int prev))
                        {
                            errors.Add($"{Ledger.DecisionsPath}:{i + 1}: T-{Fmt(n)} 절 제목이 두 번 나온다(앞: {prev}줄)");
                        }
                        else
                        {
                            sectionAt[n] = i + 1;
                        }
                        continue;
                    }
                    m = SummaryRow.Match(lines[i]);
                    if (m.Success && TryNumber(m.Groups[1].Value, out n))
                    {
                        rows++;
                        if (rowAt.TryGetValue(n, out int prev))
                        {
                            errors.Add($"{Ledger.DecisionsPath}:{i + 1}: 요약 표에 T-{Fmt(n)} 줄이 두 번 나온다(앞: {prev}줄)");
                        }
                        else
                        {
                            rowAt[n] = i + 1;
                            rowList.Add((n, i + 1));
                        }
                    }
                }
                foreach ((int n, int line) in rowList)
                {
                    if (!sectionAt.ContainsKey(n))
                    {
                        errors.Add($"{Ledger.DecisionsPath}:{line}: 요약 표의 T-{Fmt(n)} 줄에 맞는 '## T-{Fmt(n)}' 절이 없다");
                    }
                }
                Markers_(Ledger.DecisionsPath, lines, errors);
            }

            if (questions == null)
            {
                errors.Add($"{Ledger.QuestionsPath}: 파일이 없다");
            }
            else
            {
                string[] lines = Lines(questions);
                var at = new Dictionary<int, int>();
                for (int i = 0; i < lines.Length; i++)
                {
                    Match m = QuestionRow.Match(lines[i]);
                    if (m.Success && TryNumber(m.Groups[1].Value, out int n))
                    {
                        qrows++;
                        if (at.TryGetValue(n, out int prev))
                        {
                            errors.Add($"{Ledger.QuestionsPath}:{i + 1}: Q-{Fmt(n)} 줄이 두 번 나온다(앞: {prev}줄)");
                        }
                        else
                        {
                            at[n] = i + 1;
                        }
                    }
                }
                Markers_(Ledger.QuestionsPath, lines, errors);
            }

            if (tasks == null)
            {
                errors.Add($"{Ledger.TasksPath}: 파일이 없다");
            }
            else
            {
                Markers_(Ledger.TasksPath, Lines(tasks), errors);
            }
            return new Result(errors, sections, rows, qrows);
        }

        private static void Markers_(string path, string[] lines, List<string> errors)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                foreach (string mk in Markers)
                {
                    if (lines[i].StartsWith(mk, StringComparison.Ordinal))
                    {
                        errors.Add($"{path}:{i + 1}: 충돌 표식이 남아 있다('{mk.TrimEnd()}')");
                        break;
                    }
                }
            }
        }

        private static string[] Lines(string text) => text.Replace("\r\n", "\n").Split('\n');

        private static bool TryNumber(string s, out int n) => int.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out n);

        private static string Fmt(int n) => n.ToString("00", CultureInfo.InvariantCulture);
    }
}

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Wizard.Ci
{
    // 커밋 메시지 형식(CLAUDE.md 10절, PIPELINE 4.2). commit-msg 훅이 부른다.
    //
    //   <type>(<작업 id>): <한 줄 요약>
    //   (빈 줄)
    //   <왜, 무엇이 달라지는가. 수락 테스트 결과 요약>
    //   (빈 줄)
    //   Co-Authored-By: <그 커밋을 만든 모델의 줄>
    //
    // type 은 아래 목록, 작업 id 는 docs/dev/tasks.json 에 있어야 한다. 본문이 있어야 하고, Co-Authored-By 줄은 하나 이상이고
    // 전부 아래 모델 줄 가운데 하나이며, 메시지의 마지막 줄이다. '#' 로 시작하는 줄과 가위 줄(">8") 아래는 git 이 지우는 것이라 보지 않는다.
    public static class CommitMessage
    {
        public static readonly string[] Types = { "feat", "fix", "test", "data", "docs", "build", "refactor", "perf", "art", "chore" };

        public static readonly string[] CoAuthors =
        {
            "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>",
            "Co-Authored-By: Claude Fable 5.2 <noreply@anthropic.com>",
            "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>",
        };

        internal static readonly Regex Subject = new Regex("^(?<type>[a-z]+)\\((?<id>[^()\\s]+)\\): (?<summary>\\S.*)$", RegexOptions.CultureInvariant);
        private static readonly Regex Scissors = new Regex("^# -+ >8 -+$", RegexOptions.CultureInvariant);

        // 문제 목록(비면 통과)
        public static List<string> Check(string message, ISet<string> taskIds)
        {
            var problems = new List<string>();
            var lines = new List<string>();
            foreach (string raw in message.Replace("\r\n", "\n").Split('\n'))
            {
                if (Scissors.IsMatch(raw))
                {
                    break;
                }
                if (raw.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }
                lines.Add(raw.TrimEnd());
            }
            while (lines.Count > 0 && lines[lines.Count - 1].Length == 0)
            {
                lines.RemoveAt(lines.Count - 1);
            }
            while (lines.Count > 0 && lines[0].Length == 0)
            {
                lines.RemoveAt(0);
            }
            if (lines.Count == 0)
            {
                problems.Add("커밋 메시지가 비었다");
                return problems;
            }

            Match m = Subject.Match(lines[0]);
            if (!m.Success)
            {
                problems.Add("첫 줄이 '<type>(<작업 id>): <요약>' 꼴이 아니다: \"" + lines[0] + "\"");
            }
            else
            {
                if (Array.IndexOf(Types, m.Groups["type"].Value) < 0)
                {
                    problems.Add("type '" + m.Groups["type"].Value + "' 은 " + string.Join(", ", Types) + " 가운데 하나여야 한다");
                }
                if (!taskIds.Contains(m.Groups["id"].Value))
                {
                    problems.Add("작업 id '" + m.Groups["id"].Value + "' 가 docs/dev/tasks.json 에 없다");
                }
            }
            if (lines.Count > 1 && lines[1].Length != 0)
            {
                problems.Add("둘째 줄은 비어 있어야 한다(첫 줄 = 요약, 그 뒤 빈 줄, 본문)");
            }

            // 끝에 붙은 Co-Authored-By 줄 묶음의 첫 줄
            int firstTrailer = lines.Count;
            while (firstTrailer - 1 >= 1 && lines[firstTrailer - 1].StartsWith("Co-Authored-By:", StringComparison.OrdinalIgnoreCase))
            {
                firstTrailer--;
            }
            bool body = false;
            for (int i = 2; i < firstTrailer; i++)
            {
                if (lines[i].Length > 0)
                {
                    body = true;
                    break;
                }
            }
            if (!body)
            {
                problems.Add("본문이 없다(왜, 무엇이 달라지는가, 수락 테스트 결과 요약)");
            }

            int coAuthors = 0;
            for (int i = 1; i < lines.Count; i++)
            {
                if (!lines[i].StartsWith("Co-Authored-By:", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                if (Array.IndexOf(CoAuthors, lines[i]) < 0)
                {
                    problems.Add("Co-Authored-By 줄이 정해진 모델 줄이 아니다: \"" + lines[i] + "\" (" + string.Join(" | ", CoAuthors) + ")");
                }
                else if (i < firstTrailer)
                {
                    problems.Add("Co-Authored-By 줄은 메시지 끝에 둔다: \"" + lines[i] + "\"");
                }
                coAuthors++;
            }
            if (coAuthors == 0)
            {
                problems.Add("Co-Authored-By 줄이 없다(그 커밋을 만든 모델의 줄: " + string.Join(" | ", CoAuthors) + ")");
            }
            return problems;
        }
    }
}

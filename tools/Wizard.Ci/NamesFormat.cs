using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Wizard.Ci
{
    // 이름 표(data/names/names.ko.json)의 형식(DECISIONS-TECH T-35, PIPELINE 4.4): 커밋 훅의 형식 검사.
    //   { 로 시작해 } 로 끝나고, 그 사이는 한 줄에 열쇠 하나("열쇠": "값"), 열쇠는 서수 비교로 오름차순(중복 없음),
    //   마지막 항목은 표지 열쇠 "~end": "" 이고 그 앞의 항목은 모두 쉼표로 끝난다. 전체가 올바른 JSON 이다.
    // 정본 명령은 databuild names --check(M1-CONT-02)다. 그것이 생기기 전에도 훅이 형식을 막도록 여기 둔다.
    public static class NamesFormat
    {
        public const string Path = "data/names/names.ko.json";
        public const string EndKey = "~end";

        private static readonly Regex Entry = new Regex(
            "^\\s*\"(?<k>(?:[^\"\\\\]|\\\\.)*)\"\\s*:\\s*\"(?<v>(?:[^\"\\\\]|\\\\.)*)\"\\s*(?<comma>,?)\\s*$", RegexOptions.CultureInvariant);

        public static List<string> Check(string text)
        {
            var problems = new List<string>();
            try
            {
                using JsonDocument doc = JsonDocument.Parse(text);
                if (doc.RootElement.ValueKind != JsonValueKind.Object)
                {
                    problems.Add(Path + ": JSON 객체가 아니다");
                    return problems;
                }
            }
            catch (JsonException e)
            {
                problems.Add(Path + ": JSON 이 아니다 — " + e.Message);
                return problems;
            }
            string[] lines = text.Replace("\r\n", "\n").TrimEnd('\n').Split('\n');
            if (lines.Length < 3 || lines[0].Trim() != "{" || lines[lines.Length - 1].Trim() != "}")
            {
                problems.Add(Path + ": 첫 줄은 '{', 마지막 줄은 '}' 이고 그 사이에 한 줄에 열쇠 하나다");
                return problems;
            }
            string? prev = null;
            for (int i = 1; i < lines.Length - 1; i++)
            {
                string where = Path + ":" + (i + 1).ToString(CultureInfo.InvariantCulture);
                Match m = Entry.Match(lines[i]);
                if (!m.Success)
                {
                    problems.Add(where + ": 한 줄에 \"열쇠\": \"값\" 하나여야 한다");
                    continue;
                }
                string key = JsonSerializer.Deserialize<string>("\"" + m.Groups["k"].Value + "\"") ?? "";
                bool last = i == lines.Length - 2;
                bool comma = m.Groups["comma"].Value.Length > 0;
                if (last)
                {
                    if (key != EndKey || m.Groups["v"].Value.Length != 0 || comma)
                    {
                        problems.Add(where + ": 마지막 항목은 \"~end\": \"\" 이고 쉼표가 없다");
                    }
                    continue;
                }
                if (key == EndKey)
                {
                    problems.Add(where + ": 표지 열쇠 \"~end\" 는 마지막 줄에만 둔다");
                    continue;
                }
                if (!comma)
                {
                    problems.Add(where + ": 마지막이 아닌 항목은 쉼표로 끝난다");
                }
                if (prev != null)
                {
                    int c = string.CompareOrdinal(prev, key);
                    if (c == 0)
                    {
                        problems.Add(where + ": 열쇠 '" + key + "' 가 두 번 나온다");
                    }
                    else if (c > 0)
                    {
                        problems.Add(where + ": 열쇠 '" + key + "' 가 '" + prev + "' 뒤에 있다(서수 오름차순이어야 한다)");
                    }
                }
                prev = key;
            }
            return problems;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Wizard.TaskGraph
{
    // taskgraph next-ids (DECISIONS-TECH T-65 6번). 여러 곳(main, task/* 브랜치의 끝, 작업 트리의 작업 파일)의 장부에서 쓰인
    // T·Q·FIX·CCR 번호의 최댓값, 다음 번호, 그 최댓값이 쓰인 곳을 낸다. 이 클래스는 글자만 받는다(git 은 NextIdsReader 가 부른다).
    //   T   : DECISIONS-TECH.md 의 '## T-<번호>' 절 제목과 요약 표의 '| T-<번호> |' 줄
    //   Q   : OPEN-QUESTIONS.md 에 적힌 Q-<번호>
    //   FIX·CCR : tasks.json 의 카드 id 와 docs/dev/*.md 에 적힌 번호
    public static class NextIds
    {
        public static readonly string[] Kinds = { "T", "Q", "FIX", "CCR" };

        private static readonly Regex TSection = new Regex("^## T-([0-9]+)(?![0-9])", RegexOptions.CultureInvariant | RegexOptions.Multiline);
        private static readonly Regex TRow = new Regex("^\\|\\s*T-([0-9]+)\\s*\\|", RegexOptions.CultureInvariant | RegexOptions.Multiline);
        private static readonly Regex QAny = new Regex("(?<![A-Za-z0-9_])Q-([0-9]+)(?![0-9])", RegexOptions.CultureInvariant);
        private static readonly Regex TaskId = new Regex("\"id\"\\s*:\\s*\"(FIX|CCR)-([0-9]+)\"", RegexOptions.CultureInvariant);
        private static readonly Regex DocId = new Regex("(?<![A-Za-z0-9_])(FIX|CCR)-([0-9]+)(?![0-9])", RegexOptions.CultureInvariant);

        // 한 곳(브랜치나 작업 트리)의 파일들. 열쇠는 저장소 뿌리에서의 경로(docs/dev/…)
        public sealed class Source
        {
            public Source(string name, IReadOnlyDictionary<string, string> files)
            {
                Name = name;
                Files = files;
            }

            public string Name { get; }
            public IReadOnlyDictionary<string, string> Files { get; }
        }

        public sealed class Entry
        {
            public Entry(string kind, int max, IReadOnlyList<string> where)
            {
                Kind = kind;
                Max = max;
                Where = where;
            }

            public string Kind { get; }
            public int Max { get; }
            public IReadOnlyList<string> Where { get; }
            public string Next => Kind + "-" + Format(Kind, Max + 1);
        }

        public static IReadOnlyList<Entry> Scan(IEnumerable<Source> sources)
        {
            var max = Kinds.ToDictionary(k => k, _ => 0, StringComparer.Ordinal);
            var where = Kinds.ToDictionary(k => k, _ => new List<string>(), StringComparer.Ordinal);
            void Note(string kind, string digits, string src)
            {
                if (!int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out int n))
                {
                    return;
                }
                if (n > max[kind])
                {
                    max[kind] = n;
                    where[kind].Clear();
                    where[kind].Add(src);
                }
                else if (n == max[kind] && n > 0 && !where[kind].Contains(src))
                {
                    where[kind].Add(src);
                }
            }
            foreach (Source s in sources)
            {
                foreach (KeyValuePair<string, string> f in s.Files.OrderBy(kv => kv.Key, StringComparer.Ordinal))
                {
                    if (f.Key == Ledger.DecisionsPath)
                    {
                        foreach (Match m in TSection.Matches(f.Value))
                        {
                            Note("T", m.Groups[1].Value, s.Name);
                        }
                        foreach (Match m in TRow.Matches(f.Value))
                        {
                            Note("T", m.Groups[1].Value, s.Name);
                        }
                    }
                    if (f.Key == Ledger.QuestionsPath)
                    {
                        foreach (Match m in QAny.Matches(f.Value))
                        {
                            Note("Q", m.Groups[1].Value, s.Name);
                        }
                    }
                    if (f.Key == Ledger.TasksPath)
                    {
                        foreach (Match m in TaskId.Matches(f.Value))
                        {
                            Note(m.Groups[1].Value, m.Groups[2].Value, s.Name);
                        }
                    }
                    else if (IsDevMarkdown(f.Key))
                    {
                        foreach (Match m in DocId.Matches(f.Value))
                        {
                            Note(m.Groups[1].Value, m.Groups[2].Value, s.Name);
                        }
                    }
                }
            }
            return Kinds.Select(k => new Entry(k, max[k], where[k])).ToList();
        }

        // docs/dev/ 바로 아래의 .md
        public static bool IsDevMarkdown(string rel)
        {
            return rel.StartsWith("docs/dev/", StringComparison.Ordinal) && rel.EndsWith(".md", StringComparison.Ordinal)
                && rel.IndexOf('/', "docs/dev/".Length) < 0;
        }

        // taskgraph add 에 넘기는 꼴(FIX, CCR 의 최댓값)
        public static IReadOnlyDictionary<string, int> MaxByKind(IReadOnlyList<Entry> entries)
        {
            return entries.ToDictionary(e => e.Kind, e => e.Max, StringComparer.Ordinal);
        }

        public static string Text(IReadOnlyList<Entry> entries)
        {
            var sb = new System.Text.StringBuilder();
            foreach (Entry e in entries)
            {
                string max = e.Max == 0 ? "없음" : e.Kind + "-" + Format(e.Kind, e.Max);
                string where = e.Where.Count == 0 ? "" : "  (쓰인 곳: " + string.Join(", ", e.Where) + ")";
                sb.Append($"{e.Kind,-4}최댓값 {max}  다음 {e.Next}{where}\n");
            }
            return sb.ToString();
        }

        public static string Json(IReadOnlyList<Entry> entries)
        {
            var o = new JsonObject();
            foreach (Entry e in entries)
            {
                var where = new JsonArray();
                foreach (string w in e.Where)
                {
                    where.Add(w);
                }
                o[e.Kind] = new JsonObject { ["max"] = e.Max, ["next"] = e.Next, ["where"] = where };
            }
            return JsStyleJson.Serialize(o, 1);
        }

        // T·Q 는 두 자리(T-05), FIX·CCR 은 그대로(FIX-7). 장부에 적힌 꼴을 따른다
        private static string Format(string kind, int n)
        {
            return kind == "T" || kind == "Q" ? n.ToString("00", CultureInfo.InvariantCulture) : n.ToString(CultureInfo.InvariantCulture);
        }
    }
}

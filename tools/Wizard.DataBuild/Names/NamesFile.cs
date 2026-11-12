using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Wizard.DataBuild.Core;

namespace Wizard.DataBuild.Names
{
    // data/names/names.ko.json(DECISIONS-TECH T-35, CONTRACTS 5절, ARCHITECTURE 8.3).
    //
    // 정규 꼴(names --check 가 요구하고 names --fix 가 만드는 꼴. 둘은 같다: --check 를 통과하면 --fix 는 아무것도 바꾸지 않는다):
    //   {
    //     "<열쇠>": "<값>",          ← 두 칸 들여쓰기, 한 줄에 열쇠 하나, 열쇠는 서수 오름차순, 중복 없음, 줄마다 쉼표
    //     "~end": ""                 ← 마지막 항목은 표지 열쇠. 쉼표가 없다. 이름이 아니다(이진 절에 싣지 않는다)
    //   }
    //   줄 끝은 LF, 파일은 줄바꿈 하나로 끝난다. 전체가 올바른 JSON 객체다.
    // 열쇠는 spec 14 1.1절의 id 문법(ASCII 소문자, 마디는 '.')이다: ^[a-z][a-z0-9_]*(\.[a-z0-9][a-z0-9_]*)+$
    // 그래서 '~' 로 시작하는 표지는 서수 비교에서 늘 끝에 온다.
    //
    // names --fix 는 union 병합(.gitattributes) 뒤의 흐트러짐을 고친다: 순서, 같은 줄의 중복, 빠지거나 여러 개이거나 중간에 낀 표지,
    // 쉼표, 띄어쓰기, CRLF, 끝 줄바꿈. 고치지 못하는 것(항목 꼴이 아닌 줄, 값이 다른 같은 열쇠, 열쇠 문법 위반, 틀린 이스케이프)은
    // 오류로 알리고 파일을 쓰지 않는다. 같은 열쇠에 값이 둘이면 어느 쪽이 맞는지는 사람이 고른다.
    public sealed class NamesFile
    {
        public const string RepoPath = "data/names/names.ko.json";
        public const string Locale = "ko";
        public const string EndKey = "~end";
        public const string Indent = "  ";

        private static readonly Regex EntryLine = new Regex(
            "^\\s*\"(?<k>(?:[^\"\\\\]|\\\\.)*)\"\\s*:\\s*\"(?<v>(?:[^\"\\\\]|\\\\.)*)\"\\s*(?<comma>,?)\\s*$", RegexOptions.CultureInvariant);

        public static readonly Regex KeyGrammar = new Regex("^[a-z][a-z0-9_]*(\\.[a-z0-9][a-z0-9_]*)+$", RegexOptions.CultureInvariant);

        private readonly Dictionary<string, NameEntry> _byKey;

        private NamesFile(List<NameEntry> entries)
        {
            Entries = entries;
            _byKey = new Dictionary<string, NameEntry>(StringComparer.Ordinal);
            foreach (NameEntry e in entries)
            {
                _byKey[e.Key] = e;
            }
        }

        // 표지를 뺀 항목. 열쇠 서수 오름차순
        public IReadOnlyList<NameEntry> Entries { get; }

        public bool Contains(string key) => _byKey.ContainsKey(key);

        public bool TryGet(string key, out string value)
        {
            if (_byKey.TryGetValue(key, out NameEntry? e))
            {
                value = e.Value;
                return true;
            }
            value = "";
            return false;
        }

        // 저장소의 이름 표를 검사해 읽는다. 문제가 있으면 진단에 적고 null
        public static NamesFile? LoadFromRepo(string root, Diagnostics diagnostics)
        {
            string path = Path.Combine(root, RepoPath);
            if (!File.Exists(path))
            {
                diagnostics.Error(RepoPath, "이름 표가 없다");
                return null;
            }
            string text = ReadUtf8(path);
            List<string> problems = Check(text);
            if (problems.Count > 0)
            {
                foreach (string p in problems)
                {
                    diagnostics.Error(RepoPath, p);
                }
                return null;
            }
            return new NamesFile(ParseLoose(text, new List<string>()).Entries);
        }

        public static string ReadUtf8(string path) => new UTF8Encoding(false, true).GetString(File.ReadAllBytes(path));

        // 정규 꼴에서 어긋난 곳. 한 줄에 하나("<줄>: <이유>"). 비었으면 통과
        public static List<string> Check(string text)
        {
            var problems = new List<string>();
            if (text.Length > 0 && text[0] == '﻿')
            {
                problems.Add("1: 파일 앞에 BOM 이 있다(UTF-8, BOM 없음)");
                return problems;
            }
            // JSON 이 아니어도 줄 단위 검사는 계속한다(빠진 쉼표처럼 어느 줄이 틀렸는지를 같이 알린다)
            if (!IsJsonObject(text, out string jsonError))
            {
                problems.Add("JSON 객체가 아니다 — " + jsonError);
            }
            if (text.IndexOf('\r') >= 0)
            {
                problems.Add("줄 끝은 LF 다(CRLF 가 있다)");
            }
            if (!text.EndsWith("}\n", StringComparison.Ordinal) || text.EndsWith("\n\n", StringComparison.Ordinal))
            {
                problems.Add("파일은 '}' 줄과 줄바꿈 하나로 끝난다");
            }
            string[] lines = SplitLines(text);
            if (lines.Length < 3 || lines[0] != "{" || lines[lines.Length - 1] != "}")
            {
                problems.Add("첫 줄은 '{', 마지막 줄은 '}' 이고 그 사이에 한 줄에 열쇠 하나다");
                return problems;
            }
            string? prev = null;
            for (int i = 1; i < lines.Length - 1; i++)
            {
                string where = (i + 1).ToString(CultureInfo.InvariantCulture) + ": ";
                if (!TryParseLine(lines[i], out string key, out string rawValue, out bool comma, out string lineError))
                {
                    problems.Add(where + lineError);
                    continue;
                }
                bool last = i == lines.Length - 2;
                if (last)
                {
                    if (key != EndKey || rawValue.Length != 0 || comma)
                    {
                        problems.Add(where + "마지막 항목은 \"~end\": \"\" 이고 쉼표가 없다");
                    }
                    else if (lines[i] != Indent + "\"" + EndKey + "\": \"\"")
                    {
                        problems.Add(where + "띄어쓰기가 정규 꼴(두 칸 들여쓰기, \"열쇠\": \"값\")이 아니다(names --fix)");
                    }
                    continue;
                }
                if (key == EndKey)
                {
                    problems.Add(where + "표지 열쇠 \"~end\" 는 마지막 항목에만 둔다");
                    continue;
                }
                if (!KeyGrammar.IsMatch(key))
                {
                    problems.Add(where + "열쇠 '" + key + "' 가 id 문법(spec 14 1.1: ^[a-z][a-z0-9_]*(\\.[a-z0-9][a-z0-9_]*)+$)에 맞지 않는다");
                }
                if (!comma)
                {
                    problems.Add(where + "마지막이 아닌 항목은 쉼표로 끝난다");
                }
                else if (lines[i] != CanonicalLine(key, rawValue, true))
                {
                    problems.Add(where + "띄어쓰기가 정규 꼴(두 칸 들여쓰기, \"열쇠\": \"값\",)이 아니다(names --fix)");
                }
                if (prev != null)
                {
                    int c = string.CompareOrdinal(prev, key);
                    if (c == 0)
                    {
                        problems.Add(where + "열쇠 '" + key + "' 가 두 번 나온다");
                    }
                    else if (c > 0)
                    {
                        problems.Add(where + "열쇠 '" + key + "' 가 '" + prev + "' 뒤에 있다(서수 오름차순이어야 한다)");
                    }
                }
                prev = key;
            }
            return problems;
        }

        // 정규 꼴로 고친 글. 고칠 수 없는 문제가 있으면 problems 에 적고 null
        public static string? Fix(string text, List<string> problems)
        {
            if (text.Length > 0 && text[0] == '﻿')
            {
                text = text.Substring(1);
            }
            Parsed parsed = ParseLoose(text, problems);
            if (problems.Count > 0)
            {
                return null;
            }
            return Format(parsed.Entries);
        }

        public static string Format(IReadOnlyList<NameEntry> entries)
        {
            var sb = new StringBuilder();
            sb.Append("{\n");
            foreach (NameEntry e in entries)
            {
                sb.Append(CanonicalLine(e.Key, e.RawValue, true)).Append('\n');
            }
            sb.Append(Indent).Append("\"").Append(EndKey).Append("\": \"\"\n");
            sb.Append("}\n");
            return sb.ToString();
        }

        // 메모리에서 만든 표(테스트, 도구). 열쇠 문법·중복은 Fix 와 같이 본다
        public static NamesFile FromText(string text)
        {
            var problems = new List<string>();
            Parsed p = ParseLoose(text, problems);
            if (problems.Count > 0)
            {
                throw new DataBuildException(RepoPath + ": " + problems[0]);
            }
            return new NamesFile(p.Entries);
        }

        private sealed class Parsed
        {
            public Parsed(List<NameEntry> entries)
            {
                Entries = entries;
            }

            public List<NameEntry> Entries { get; }
        }

        // 순서·중복·표지·쉼표·띄어쓰기를 따지지 않고 읽는다. 고칠 수 없는 문제만 problems 에 적는다
        private static Parsed ParseLoose(string text, List<string> problems)
        {
            string[] lines = SplitLines(text.Replace("\r\n", "\n"));
            int first = 0;
            while (first < lines.Length && lines[first].Trim().Length == 0)
            {
                first++;
            }
            int last = lines.Length - 1;
            while (last >= 0 && lines[last].Trim().Length == 0)
            {
                last--;
            }
            var entries = new Dictionary<string, NameEntry>(StringComparer.Ordinal);
            if (first > last || lines[first].Trim() != "{" || lines[last].Trim() != "}")
            {
                problems.Add("첫 줄은 '{', 마지막 줄은 '}' 이어야 한다");
                return new Parsed(new List<NameEntry>());
            }
            for (int i = first + 1; i < last; i++)
            {
                string where = (i + 1).ToString(CultureInfo.InvariantCulture) + ": ";
                if (lines[i].Trim().Length == 0)
                {
                    continue;
                }
                if (!TryParseLine(lines[i], out string key, out string rawValue, out _, out string lineError))
                {
                    problems.Add(where + lineError);
                    continue;
                }
                if (key == EndKey)
                {
                    continue;
                }
                if (!KeyGrammar.IsMatch(key))
                {
                    problems.Add(where + "열쇠 '" + key + "' 가 id 문법(spec 14 1.1)에 맞지 않는다. 고쳐 쓸 열쇠는 사람이 정한다");
                    continue;
                }
                if (!TryDecode(rawValue, out string value))
                {
                    problems.Add(where + "값의 이스케이프가 틀렸다");
                    continue;
                }
                if (entries.TryGetValue(key, out NameEntry? had))
                {
                    if (had.RawValue != rawValue)
                    {
                        problems.Add(where + "열쇠 '" + key + "' 에 값이 둘이다(\"" + had.RawValue + "\" / \"" + rawValue + "\"). 어느 쪽인지 사람이 고른다");
                    }
                    continue;
                }
                entries[key] = new NameEntry(key, value, rawValue);
            }
            var list = new List<NameEntry>(entries.Values);
            list.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            return new Parsed(list);
        }

        private static bool TryParseLine(string line, out string key, out string rawValue, out bool comma, out string error)
        {
            Match m = EntryLine.Match(line);
            if (!m.Success)
            {
                key = rawValue = error = "";
                comma = false;
                error = "한 줄에 \"열쇠\": \"값\" 하나여야 한다";
                return false;
            }
            string rawKey = m.Groups["k"].Value;
            rawValue = m.Groups["v"].Value;
            comma = m.Groups["comma"].Value.Length > 0;
            error = "";
            if (rawKey.IndexOf('\\') >= 0)
            {
                key = rawKey;
                error = "열쇠에 이스케이프를 쓰지 않는다(열쇠는 ASCII id 다)";
                return false;
            }
            key = rawKey;
            if (!TryDecode(rawValue, out _))
            {
                error = "값의 이스케이프가 틀렸다";
                return false;
            }
            return true;
        }

        private static string CanonicalLine(string key, string rawValue, bool comma) =>
            Indent + "\"" + key + "\": \"" + rawValue + "\"" + (comma ? "," : "");

        private static bool TryDecode(string raw, out string value)
        {
            try
            {
                value = JsonSerializer.Deserialize<string>("\"" + raw + "\"") ?? "";
                return true;
            }
            catch (JsonException)
            {
                value = "";
                return false;
            }
        }

        private static bool IsJsonObject(string text, out string error)
        {
            try
            {
                using (JsonDocument doc = JsonDocument.Parse(text))
                {
                    if (doc.RootElement.ValueKind != JsonValueKind.Object)
                    {
                        error = "최상위가 객체가 아니다";
                        return false;
                    }
                    foreach (JsonProperty p in doc.RootElement.EnumerateObject())
                    {
                        if (p.Value.ValueKind != JsonValueKind.String)
                        {
                            error = "값 '" + p.Name + "' 이 문자열이 아니다";
                            return false;
                        }
                    }
                }
                error = "";
                return true;
            }
            catch (JsonException e)
            {
                error = e.Message;
                return false;
            }
        }

        // 마지막 줄바꿈 뒤의 빈 조각은 줄이 아니다
        private static string[] SplitLines(string text)
        {
            string t = text.EndsWith("\n", StringComparison.Ordinal) ? text.Substring(0, text.Length - 1) : text;
            return t.Split('\n');
        }
    }
}
